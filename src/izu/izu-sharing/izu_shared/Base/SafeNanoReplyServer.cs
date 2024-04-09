using NNanomsg;
using NNanomsg.Protocols;
using System.Net;
using System.Runtime.CompilerServices;
using Wonder.Infrastructure;

namespace IZU.Base
{
    /*
        SafeNanoReplyServer server = new SafeNanoReplyServer();
        server.Create(new IPEndPoint(IPAddress.Any, 9009));
        server.Run((buffer) => { Console.WriteLine(DateTime.Now); });
        server.Shutdown();
     */
    /// <summary>
    /// 基于tcp协议的NanoMsg ReplySocket 服务器
    /// 可以随意创建运行或者关闭
    /// </summary>
    internal class SafeNanoReplyServer
    {
        private int receiveTimeoutSeconds = 2;
        private int sendTimeoutSeconds = 2;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private ReplySocket? _replySocket { get; set; }
        private NanomsgEndpoint _nano_endpoint;
        /// <summary>
        /// 0=shutdown/null  1=created  2=receiving  3=shutting down
        /// </summary>
        private int _serverState = 0;
        private readonly ILogger _logger;
        internal bool ServerIsRunning { get { return _serverState == 1; } }
        internal ReplySocket Socket { get { return _replySocket; } }
        public SafeNanoReplyServer(int receiveTimeoutSeconds = 2, int sendTimeoutSeconds = 2)
        {
            _logger = LogManager.Factory.CreateLogger<SafeNanoReplyServer>();
            this.receiveTimeoutSeconds = receiveTimeoutSeconds;
            this.sendTimeoutSeconds = sendTimeoutSeconds;
        }
        internal void Create(IPEndPoint ipEndPoint)
        {
            if (ipEndPoint == null)
                _logger.LogWarning($"ip endpoint should not be null");

            if (_serverState > 0)
            {
                _logger.LogWarning($"nano server {_replySocket?.SocketID} is running");
                return;
            }
            Interlocked.Exchange(ref _serverState, 1);
            _replySocket = new ReplySocket();
            _replySocket.Options.ReceiveTimeout = TimeSpan.FromSeconds(receiveTimeoutSeconds);
            _replySocket.Options.SendTimeout = TimeSpan.FromSeconds(sendTimeoutSeconds);
            _nano_endpoint = _replySocket.Bind($"tcp://{ipEndPoint}");
            _logger.LogInformation($"nano server {_replySocket.SocketID} created @{ipEndPoint}");
        }

        internal void Run(Action<byte[]> action)
        {
            if (_replySocket == null || _serverState == 0)
            {
                Console.WriteLine($"nano server is not created");
                return;
            }
            if (_serverState == 2)
            {
                Console.WriteLine($"nano server is running");
                return;
            }
            if (_serverState == 3)
            {
                Console.WriteLine($"nano server has shut down");
                return;
            }
            _cts = new CancellationTokenSource();
            Task.Factory.StartNew(() =>
            {
                Interlocked.Exchange(ref _serverState, 1);
                while (!_cts.IsCancellationRequested)
                {
                    byte[] buffer = _replySocket!.Receive();
                    if (buffer == null) { continue; }
                    try
                    {
                        action.Invoke(buffer);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"action invoke error: {ex.StackTrace}");
                    }
                }
            })
            .ContinueWith(t =>
            {
                Interlocked.Exchange(ref _serverState, 0);
                _logger.LogWarning($"nano server {_replySocket.SocketID} has shut down!");
            });
        }

        internal void Shutdown()
        {
            try
            {
                if (_replySocket == null || _serverState == 0)
                {
                    Console.WriteLine($"nano server is not created");
                    return;
                }
                if (_serverState == 1 || _serverState == 2)
                {
                    _cts.Cancel();
                    _replySocket!.Shutdown(_nano_endpoint);
                    if (_serverState == 1)
                        Interlocked.Exchange(ref _serverState, 0);
                    else if (_serverState == 2)
                        Interlocked.Exchange(ref _serverState, 3);
                }
                _logger.LogInformation($"nano server {_replySocket.SocketID} is shutting down!");
            }
            catch (RuntimeWrappedException ex)
            {
                _logger.LogError($"nano server's shutdown failed! error: {ex.StackTrace}");
            }
        }

    }
}
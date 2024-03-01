/*
 * 
 *   Telnet Server
 *   通过 Telnet Client 远程管理程序
 *   使用方法：
 *   1. win+R 打开控制台（CMD)
 *   2. 输入 telnet IPAddress Port 然后回车 （ip为安装服务的主机地址，port默认为666
 *   3. 控制台会跳转到登录，输入
 *       UserName： admin 回车
 *       Password：wonder 回车
 *       登入成功。
 * 
 */
namespace Wonder.Service
{
    using System.Net;

    public class WonderTelnetService : ITelnetService
    {
        protected readonly ITelnetCommandService _telnetCommandService;
        private readonly ILogger<WonderTelnetService> _logger;
        List<TelnetClient> log_clients = new List<TelnetClient>();
        private readonly TelnetServer? _telnetServer;
        public TelnetServer Server { get { return _telnetServer!; } }
        public static ITelnetService? TelnetService { get; set; }
       
        public WonderTelnetService(ILogger<WonderTelnetService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _telnetCommandService = new TelnetCommandService(serviceProvider);
            _telnetServer = new TelnetServer(IPAddress.Any, 666);
        }
        public void Start()
        {
            _telnetServer!.ClientConnected += OnClientConnected;
            _telnetServer.ClientDisconnected += OnClientDisconnected;
            _telnetServer.ConnectionBlocked += OnConnectionBlocked;
            _telnetServer.MessageReceived += OnMessageReceivedAsync;
            _telnetServer.Start();
            _telnetCommandService.CollectCommands();
        }
        public void Stop()
        {
            _telnetServer.ClientConnected -= OnClientConnected;
            _telnetServer.ClientDisconnected -= OnClientDisconnected;
            _telnetServer.ConnectionBlocked -= OnConnectionBlocked;
            _telnetServer.MessageReceived -= OnMessageReceivedAsync;
            _telnetServer.Stop();
        }
        private void Login(TelnetClient c, string message)
        {
            switch (c.Status)
            {
                case ClientTypes.Guest:
                    {
                        if (message.ToLower().Trim() == "admin")
                        {
                            Reply(c, "Password: ", false);
                            c.SetStatus(ClientTypes.Authenticating);
                        }
                        else
                            _telnetServer!.ClientForceOffline(c);
                    }
                    break;
                case ClientTypes.Authenticating:
                    {
                        if (message == "wonder")
                        {
                            Reply(c, "User Successfully authenticated.", true, true);
                            ResetInput(c, true, true);
                            c.SetStatus(ClientTypes.LoggedIn);
                        }

                        else
                            _telnetServer!.ClientForceOffline(c);
                    }
                    break;
                case ClientTypes.LoggedIn:
                    break;

            }
        }
        private void OnClientConnected(TelnetClient c)
        {
            _telnetServer!.SendMessage(c, $"" +
                $"{TelnetServer.Logo}Welcome to the IZU remote management system, please login!" + TelnetServer.END_LINE + TelnetServer.REPLY + "Username: ");
        }

        private void OnClientDisconnected(TelnetClient c)
        {
            //_logger.LogInformation("client disconnected.  {0}", c);
        }

        private void OnConnectionBlocked(IPEndPoint ep)
        {
            Console.WriteLine(string.Format("BLOCKED: {0}:{1} at {2}", ep.Address, ep.Port, System.DateTime.Now));
        }

        private void OnMessageReceivedAsync(TelnetClient c, string message)
        {
            if (c.Status != ClientTypes.LoggedIn)
            {
                Login(c, message);
                return;
            }
            switch (message)
            {
                default:
                    string result = _telnetCommandService.RunCommand(message.ToLower().Split(" "));
                    Reply(c, result, false, true);
                    ResetInput(c, true, true);
                    break;
                case "postlog":
                    log_clients.Add(c);
                    ResetInput(c, true, true);
                    break;
                case "postlog-off":
                    log_clients.Remove(c);
                    ResetInput(c, true, true);
                    break;
                case "kickmyass":
                case "logout":
                case "exit":
                    _telnetServer!.ClientForceOffline(c);
                    ResetInput(c, true, true);
                    break;
                case "clear":
                case "cls":
                    ClearScreen(c);
                    break;
            }

        }
        void ResetInput(TelnetClient client, bool end = false, bool tip = false)
        {
            _telnetServer!.SendMessage(client, $"{(end ? TelnetServer.END_LINE : string.Empty)}{(tip ? TelnetServer.CURSOR : string.Empty)}");
        }
        void Reply(TelnetClient client, string message, bool end = false, bool tip = false)
        {
            _telnetServer!.SendMessage(client, $"{TelnetServer.END_LINE}{TelnetServer.REPLY}{message}{(end ? TelnetServer.END_LINE : string.Empty)}");
        }
        void ClearScreen(TelnetClient client)
        {
            _telnetServer!.ClearClientScreen(client);
        }
        public void PostLog(string log)
        {
            if (log_clients.Count == 0)
                return;

            foreach (var client in log_clients)
            {
                Reply(client, log, true, true);
                //Server.SendMessage(client, log);
            }
        }
    }
}

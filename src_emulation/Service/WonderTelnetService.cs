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
namespace IZU.Service
{
    using IZU.Base;
    using IZU.Commands;
    using IZU.Interfaces;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging.Configuration;
    using Microsoft.Extensions.Options;
    using System.Collections.Concurrent;
    using System.CommandLine;
    using System.CommandLine.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.Versioning;
    using System.Text;

    /// <summary>
    /// 扩展方法使用顺序（注：顺序不能颠倒，因为日志在一开始就需要初始化）
    /// 1. 在CreateBuilder后添加AddTelnetLogger（该方法会初始化TelnetLogger）
    /// 2. 然后在添加服务 AddTelnetService 
    /// 3. 在var app = builder.Build() 后添加 UseTelnet
    /// </summary>
    public static class TelnetExtensions
    {
        /// <summary>
        /// 添加自定义日志扩展方法
        /// 用于远程访问服务时，将日志输出到 Telnet 客户端
        /// </summary>
        /// <param name="builder"><see cref="ILoggingBuilder"/></param>
        /// <param name="configure"><see cref="TelnetLoggerConfiguration"/></param>
        /// <returns></returns>
        public static ILoggingBuilder AddTelnetLogger(this ILoggingBuilder builder, Action<TelnetLoggerConfiguration> configure)
        {
            builder.AddConfiguration();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, TelnetLoggerProvider>());
            LoggerProviderOptions.RegisterProviderOptions<TelnetLoggerConfiguration, TelnetLoggerProvider>(builder.Services);
            builder.Services.Configure(configure);
            return builder;
        }
        /// <summary>
        /// 添加 Telnet 服务器
        /// </summary>
        /// <param name="service"><see cref="IServiceCollection"/></param>
        /// <returns></returns>
        public static IServiceCollection AddTelnetService(this IServiceCollection service)
        {
            service.AddSingleton<ITelnetService, WonderTelnetService>();
            return service;
        }
        /// <summary>
        /// 启动 Telnet 服务器
        /// </summary>
        /// <param name="app"></param>
        /// <exception cref="Exception"></exception>
        public static void UseTelnet(this WebApplication app)
        {
            ITelnetService? telnetService = app.Services.GetService<ITelnetService>();
            if (telnetService == null)
                throw new Exception("should add TelnetService first");
            WonderTelnetService.TelnetService = telnetService;
            telnetService.Start();
        }
    }


    public interface ITelnetCommandService
    {
        void CollectCommands();
        string RunCommand(params string[] args);
        void WriteLine(string message);
    }

    public class TelnetCommandService : ITelnetCommandService
    {
        protected readonly IServiceProvider _serviceProvider;
        protected IIZUService? _izuService;
        protected IS7NetService? _s7netService;
        private readonly RootCommand _commandRoot;
        private TestConsole? _telnetConsole;
        public TelnetCommandService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _commandRoot = new RootCommand("izu command line") { Name="izu"};
        }

        public void CollectCommands()
        {
            _izuService = _serviceProvider.GetService<IIZUService>()!;
            _s7netService = _serviceProvider.GetService<IS7NetService>()!;

            var commandTypes = GetAllTypesThatImplementInterface<Command>();
            foreach (var type in commandTypes)
            {
                var command = Activator.CreateInstance(type,this, _izuService, _s7netService) as Command;
                if (command == null)
                    continue;

                _commandRoot.Add(command);
            }
        }
        public void WriteLine(string message)
        {
            _telnetConsole!.WriteLine(message);
        }
        public string RunCommand(params string[] args)
        {
            string name = args[0];
            if (string.IsNullOrEmpty(name)) 
                return string.Empty;
            
            _telnetConsole = new();
            _commandRoot.Invoke(args, _telnetConsole);
            string result = _telnetConsole.Out.ToString()!;
            //var command = _commandRoot.FirstOrDefault(t => t.Name == name);
            //if (command == null)
            //    result = $"command [{name}] not exist!";
            //else
            //{
            //    _telnetConsole = new();
            //    _commandRoot.Invoke(args, _telnetConsole);
            //    result = _telnetConsole.Out.ToString()!;
            //    //result = command.Execute(args.Length > 1 ? args.Skip(1).ToArray() : Array.Empty<string>());
            //    //result = command.Execute(args);
            //}
            return result;
        }
        private IEnumerable<Type> GetAllTypesThatImplementInterface<T>()
        {
            return System.Reflection.Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(type => typeof(T).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
        }
    }

    public interface ITelnetService
    {
        TelnetServer Server { get; }
        void PostLog(string log);
        void Start();
        void Stop();
    }
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









    public delegate void ConnectionEventHandler(TelnetClient c);
    public delegate void ConnectionBlockedEventHandler(IPEndPoint endPoint);
    public delegate void MessageReceivedEventHandler(TelnetClient c, string message);
    public class TelnetServer : SocketBase
    {
        public const string Logo = @"
____________________________________________________________________________________________________________
  ____    __    ____  ______   .__   __.  _______   _______ .______                 __  .__   __.   ______    
  \   \  /  \  /   / /  __  \  |  \ |  | |       \ |   ____||   _  \               |  | |  \ |  |  /      |   
   \   \/    \/   / |  |  |  | |   \|  | |  .--.  ||  |__   |  |_)  |              |  | |   \|  | |  ,----'   
    \            /  |  |  |  | |  . `  | |  |  |  ||   __|  |      /               |  | |  . `  | |  |        
     \    /\    /   |  `--'  | |  |\   | |  '--'  ||  |____ |  |\  \----.    __    |  | |  |\   | |  `----.   
      \__/  \__/     \______/  |__| \__| |_______/ |_______|| _| `._____|   (__)   |__| |__| \__|  \______|
____________________________________________________________________________________________________________

";
        public event ConnectionEventHandler? ClientConnected = null;
        public event ConnectionEventHandler? ClientDisconnected = null;
        public event ConnectionBlockedEventHandler? ConnectionBlocked = null;
        public event MessageReceivedEventHandler? MessageReceived = null;
        public TelnetServer(IPAddress ip, int port, int dataSize = 1024)
             : base(ip, port, dataSize)
        {
        }

        public void ClearClientScreen(TelnetClient c)
        {
            SendMessage(c, $"\u001B[1J\u001B[H{Logo}{TelnetServer.CURSOR}");
        }

        public void ClientForceOffline(TelnetClient client)
        {
            CloseSocket(GetSocketByClient(client));
            ClientDisconnected!(client);
        }

        public void SendMessage(TelnetClient c, string message)
        {
            Socket clientSocket = GetSocketByClient(c);
            Send(clientSocket, message);
        }

        protected override void IncomingConnectionAccepted(IAsyncResult result)
        {
            try
            {
                Socket oldSocket = (Socket)result.AsyncState!;

                if (AcceptIncomingConnections)
                {
                    Socket newSocket = oldSocket.EndAccept(result);

                    uint clientID = (uint)SocketClients.Count + 1;
                    TelnetClient client = new TelnetClient(clientID, (IPEndPoint)newSocket.RemoteEndPoint!);
                    SocketClients.Add(newSocket, client);

                    SendBytes(
                        newSocket,
                        new byte[] {
                            0xff, 0xfd, 0x01,   // Do Echo
                            0xff, 0xfd, 0x21,   // Do Remote Flow Control
                            0xff, 0xfb, 0x01,   // Will Echo
                            0xff, 0xfb, 0x03    // Will Supress Go Ahead
                        }
                    );

                    client.ResetReceivedData();

                    ClientConnected!(client);

                    SocketServer.BeginAccept(new AsyncCallback(IncomingConnectionAccepted), SocketServer);
                }

                else
                {
                    ConnectionBlocked!((IPEndPoint)oldSocket.RemoteEndPoint!);
                }
            }

            catch { }
        }

        protected override void ReceiveData(IAsyncResult result)
        {
            try
            {
                Socket clientSocket = (Socket)result.AsyncState!;
                TelnetClient client = GetClientBySocket(clientSocket);
                int bytesReceived = clientSocket.EndReceive(result);
                if (bytesReceived == 0)
                {
                    CloseSocket(clientSocket);
                    SocketServer.BeginAccept(new AsyncCallback(IncomingConnectionAccepted), SocketServer);
                }

                else if (Data[0] < 0xF0)
                {
                    string receivedData = client.GetReceivedData();

                    //Console.WriteLine("received from client {0}({1}): {2}", client.GetClientID(), bytesReceived, receivedData);
                    // 0x2E = '.', 0x0D = carriage return, 0x0A = new line
                    if (Data[0] == 0x2E && Data[1] == 0x0D && receivedData.Length == 0 ||
                        Data[0] == 0x0D && Data[1] == 0x0A)
                    {
                        //sendMessageToSocket(clientSocket, "\u001B[1J\u001B[H");
                        MessageReceived!(client, client.GetReceivedData());
                        client.ResetReceivedData();
                    }

                    else
                    {
                        // 0x08 => backspace character
                        if (Data[0] == 0x08)
                        {
                            if (receivedData.Length > 0)
                            {
                                client.RemoveLastCharacterReceived();
                                SendBytes(clientSocket, new byte[] { 0x08, 0x20, 0x08 });
                            }

                            else
                                clientSocket.BeginReceive(Data, 0, DataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                        }

                        // 0x7F => delete character
                        else if (Data[0] == 0x7F)
                            clientSocket.BeginReceive(Data, 0, DataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);

                        else
                        {
                            client.AppendReceivedData(SocketEncoding.GetString(Data, 0, bytesReceived));

                            // Echo back the received character
                            // if client is not writing any password
                            if (client.Status != ClientTypes.Authenticating)
                                SendBytes(clientSocket, new byte[] { Data[0] });

                            // Echo back asterisks if client is
                            // writing a password
                            else
                                Send(clientSocket, "*");

                            clientSocket.BeginReceive(Data, 0, DataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                        }
                    }
                }

                else
                    clientSocket.BeginReceive(Data, 0, DataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
            }
            catch { }
        }

    }


    public class TelnetClient
    {
        private uint id;
        private ClientTypes status;
        private string receivedData;

        public readonly IPEndPoint RemoteAddress;
        public readonly System.DateTime ConnectedTime;
        public ClientTypes Status { get { return status; } }
        public uint ID { get { return id; } }

        public TelnetClient(uint clientId, IPEndPoint remoteAddress)
        {
            id = clientId;
            RemoteAddress = remoteAddress;
            ConnectedTime = DateTime.Now;
            status = ClientTypes.Guest;
            receivedData = string.Empty;
        }

        public string GetReceivedData()
        {
            return receivedData;
        }

        public void SetStatus(ClientTypes newStatus)
        {
            status = newStatus;
        }

        public void SetReceivedData(string newReceivedData)
        {
            receivedData = newReceivedData;
        }

        public void AppendReceivedData(string dataToAppend)
        {
            receivedData += dataToAppend;
        }

        public void RemoveLastCharacterReceived()
        {
            receivedData = receivedData.Substring(0, receivedData.Length - 1);
        }

        public void ResetReceivedData()
        {
            receivedData = string.Empty;
        }

        public override string ToString()
        {
            string ip = string.Format("{0}:{1}", RemoteAddress.Address.ToString(), RemoteAddress.Port);

            string res = string.Format("client {0} (From: {1}, Status: {2}, Connection time: {3})", id, ip, status, ConnectedTime);

            return res;
        }
    }


    public abstract class SocketBase : ClientBase
    {
        public const string END_LINE = "\r\n";
        public const string REPLY = "";
        public const string CURSOR = " >> ";
        protected readonly Encoding SocketEncoding = Encoding.GetEncoding("GB2312");
        protected IPAddress ServerIPAddress { get; }
        protected int Port { get; }
        protected bool AcceptIncomingConnections { get; set; }

        protected readonly Socket SocketServer;
        protected readonly int DataSize;
        protected byte[] Data;


        public SocketBase(IPAddress ip, int port, int datasize, bool acceptIncomingConnections = true)
        {
            ServerIPAddress = ip;
            Port = port;
            DataSize = datasize;
            Data = new byte[datasize];
            AcceptIncomingConnections = acceptIncomingConnections;
            SocketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start()
        {
            SocketServer.Bind(new IPEndPoint(ServerIPAddress, Port));
            SocketServer.Listen(0);
            SocketServer.BeginAccept(IncomingConnectionAccepted, SocketServer);
        }

        public void Stop()
        {
            SocketServer.Close();
        }
        protected void CloseSocket(Socket clientSocket)
        {
            clientSocket.Close();
            SocketClients.Remove(clientSocket);
        }
        protected virtual void SendData(IAsyncResult result)
        {
            try
            {
                Socket clientSocket = (Socket)result.AsyncState!;

                clientSocket.EndSend(result);

                clientSocket.BeginReceive(Data, 0, DataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
            }

            catch { }
        }
        protected virtual void ReceiveData(IAsyncResult result)
        {
        }
        protected virtual void IncomingConnectionAccepted(IAsyncResult result)
        {

        }

        protected void Send(Socket sock, string message)
        {
            byte[] data = SocketEncoding.GetBytes(message);
            SendBytes(sock, data);
        }
        protected void SendBytes(Socket sock, byte[] data)
        {
            if (sock == null) return;
            sock.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendData), sock);
        }

        public void Broadcast(string message)
        {
            foreach (Socket s in SocketClients.Keys)
            {
                try
                {
                    TelnetClient c = SocketClients[s];

                    if (c.Status == ClientTypes.LoggedIn)
                    {
                        Send(s, END_LINE + message + END_LINE + CURSOR);
                        c.ResetReceivedData();
                    }
                }

                catch
                {
                    SocketClients.Remove(s);
                }
            }
        }

    }

    public abstract class ClientBase
    {
        protected readonly Dictionary<Socket, TelnetClient> SocketClients;
        public ClientBase()
        {
            SocketClients = new Dictionary<Socket, TelnetClient>();
        }
        protected virtual TelnetClient GetClientBySocket(Socket clientSocket)
        {
            SocketClients.TryGetValue(clientSocket, out TelnetClient? client);
            return client!;
        }
        protected virtual Socket GetSocketByClient(TelnetClient client)
        {
            return SocketClients.FirstOrDefault(x => x.Value.ID == client.ID).Key;
        }
    }

    public enum ClientTypes
    {
        Guest = 0,
        Authenticating = 1,
        LoggedIn = 2
    }





    public sealed class TelnetLoggerConfiguration
    {
        public int EventId { get; set; }

        public Dictionary<LogLevel, ConsoleColor> LogLevelToColorMap { get; set; } = new()
        {
            [LogLevel.Information] = ConsoleColor.Green,
            [LogLevel.Warning] = ConsoleColor.Yellow
        };
    }

    public sealed class TelnetLogger : ILogger
    {
        string name;
        Func<TelnetLoggerConfiguration> getCurrentConfig;
        public TelnetLogger(string name, Func<TelnetLoggerConfiguration> getCurrentConfig)
        {
            this.getCurrentConfig = getCurrentConfig;
            this.name = name;
        }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;

        public bool IsEnabled(LogLevel logLevel) => getCurrentConfig().LogLevelToColorMap.ContainsKey(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            WonderTelnetService.TelnetService!.PostLog($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {logLevel.ToString(),-12}: {name} - {formatter(state, exception)}");
        }
        public void Logbackup<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            TelnetLoggerConfiguration config = getCurrentConfig();
            if (config.EventId == 0 || config.EventId == eventId.Id)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                //Console.WriteLine($"[{eventId.Id,2}: {logLevel,-12}]");
                Console.ForegroundColor = originalColor;
                Console.Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} ");
                Console.ForegroundColor = config.LogLevelToColorMap[logLevel];
                Console.Write($"{logLevel.ToString()[..4].ToUpper()}：{name} - {formatter(state, exception)}");
                Console.ForegroundColor = originalColor;
                Console.WriteLine();
            }
        }
    }

    [UnsupportedOSPlatform("browser")]
    [ProviderAlias("ColorConsole")]
    public sealed class TelnetLoggerProvider : ILoggerProvider
    {
        private readonly IDisposable? _onChangeToken;
        private TelnetLoggerConfiguration _currentConfig;
        private readonly ConcurrentDictionary<string, TelnetLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

        public TelnetLoggerProvider(IOptionsMonitor<TelnetLoggerConfiguration> config)
        {
            _currentConfig = config.CurrentValue;
            _onChangeToken = config.OnChange(updatedConfig => _currentConfig = updatedConfig);
        }

        public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new TelnetLogger(name, GetCurrentConfig));

        private TelnetLoggerConfiguration GetCurrentConfig() => _currentConfig;

        public void Dispose()
        {
            _loggers.Clear();
            _onChangeToken?.Dispose();
        }
    }


    public static class TelnetLoggerExtensions
    {
    }

}

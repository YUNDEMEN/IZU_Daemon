using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using System.Windows.Input;
namespace IZU.Service
{
    public static class TelnetExtensions
    {
        public static IServiceCollection AddTelnetService(this IServiceCollection service)
        {
            service.AddSingleton<ITelnetService, WonderTelnetService>();
            return service;
        }
        public static void UseTelnet(this WebApplication app)
        {
            ITelnetService? telnetService = app.Services.GetService<ITelnetService>();
            if (telnetService == null)
                throw new Exception("should add TelnetService first");
            WonderTelnetService.TelnetService = telnetService;
            telnetService.Start();
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
        private readonly ILogger<WonderTelnetService> _logger;
        List<TelnetClient> log_clients = new List<TelnetClient>();
        private readonly TelnetServer? _telnetServer;
        public TelnetServer Server { get { return _telnetServer!; } }
        public static ITelnetService? TelnetService { get; set; }
        public WonderTelnetService(ILogger<WonderTelnetService> logger)
        {
            _logger = logger;
            _telnetServer = new TelnetServer(IPAddress.Any, 6666);
        }
        public void Start()
        {
            _telnetServer!.ClientConnected += OnClientConnected;
            _telnetServer.ClientDisconnected += OnClientDisconnected;
            _telnetServer.ConnectionBlocked += OnConnectionBlocked;
            _telnetServer.MessageReceived += OnMessageReceived;
            _telnetServer.Start();
        }
        public void Stop()
        {
            _telnetServer.ClientConnected -= OnClientConnected;
            _telnetServer.ClientDisconnected -= OnClientDisconnected;
            _telnetServer.ConnectionBlocked -= OnConnectionBlocked;
            _telnetServer.MessageReceived -= OnMessageReceived;
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
            //_logger.LogInformation("client connected. {0}", c);
            _telnetServer.SendMessage(c, "Welcom to Wonder.inc command server, please login first!" + TelnetServer.END_LINE + "Username: ");
        }

        private void OnClientDisconnected(TelnetClient c)
        {
            //_logger.LogInformation("client disconnected.  {0}", c);
        }

        private void OnConnectionBlocked(IPEndPoint ep)
        {
            Console.WriteLine(string.Format("BLOCKED: {0}:{1} at {2}", ep.Address, ep.Port, DateTime.Now));
        }

        private void OnMessageReceived(TelnetClient c, string message)
        {
            if (c.Status != ClientTypes.LoggedIn)
            {
                Login(c, message);
                return;
            }

            switch (message)
            {
                case "h":
                    Reply(c, "aa",true, true);
                    break;
                case "log":
                    log_clients.Add(c);
                    ResetInput(c, true, true);
                    break;
                case "log-off":
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
                default:
                    ResetInput(c, true, true);
                    break;
            }

        }
        void ResetInput(TelnetClient client, bool end = false, bool tip = false)
        {
            _telnetServer!.SendMessage(client, $"{(end ? TelnetServer.END_LINE : string.Empty)}{(tip ? TelnetServer.CURSOR : string.Empty)}");
        }
        void Reply(TelnetClient client, string message, bool end = false, bool tip = false)
        {
            _telnetServer!.SendMessage(client, $"{TelnetServer.END_LINE}{message}{(end ? TelnetServer.END_LINE : string.Empty)}{(tip ? TelnetServer.CURSOR : string.Empty)}");
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
                Server.SendMessage(client, log);
            }
        }
    }

    public class CommandService
    {
        private readonly IDictionary<string, Action> _commands;
        public CommandService()
        {
            _commands = new Dictionary<string, Action>();
        }

        public void CollectCommands()
        {
            _commands[""]=()=> { };
        }
    }


    public delegate void ConnectionEventHandler(TelnetClient c);
    public delegate void ConnectionBlockedEventHandler(IPEndPoint endPoint);
    public delegate void MessageReceivedEventHandler(TelnetClient c, string message);
    public class TelnetServer : SocketBase
    {
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
            SendMessage(c, $"\u001B[1J\u001B[H{TelnetServer.CURSOR}");
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
        public const string CURSOR = " >> ";
        protected readonly Encoding SocketEncoding = Encoding.UTF8;
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





    public sealed class ColorConsoleLoggerConfiguration
    {
        public int EventId { get; set; }

        public Dictionary<LogLevel, ConsoleColor> LogLevelToColorMap { get; set; } = new()
        {
            [LogLevel.Information] = ConsoleColor.Green,
            [LogLevel.Warning] = ConsoleColor.Yellow
        };
    }

    public sealed class ColorConsoleLogger : ILogger
    {
        string name;
        Func<ColorConsoleLoggerConfiguration> getCurrentConfig;
        public ColorConsoleLogger(string name, Func<ColorConsoleLoggerConfiguration> getCurrentConfig)
        {
            this.getCurrentConfig = getCurrentConfig;
            this.name = name;
        }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;

        public bool IsEnabled(LogLevel logLevel) => getCurrentConfig().LogLevelToColorMap.ContainsKey(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            WonderTelnetService.TelnetService!.PostLog($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {logLevel.ToString(),-12}: {name} - {formatter(state, exception)}{TelnetServer.END_LINE}{TelnetServer.CURSOR}");
        }
        public void Logbackup<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ColorConsoleLoggerConfiguration config = getCurrentConfig();
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
    public sealed class ColorConsoleLoggerProvider : ILoggerProvider
    {
        private readonly IDisposable? _onChangeToken;
        private ColorConsoleLoggerConfiguration _currentConfig;
        private readonly ConcurrentDictionary<string, ColorConsoleLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
        
        public ColorConsoleLoggerProvider(IOptionsMonitor<ColorConsoleLoggerConfiguration> config)
        {
            _currentConfig = config.CurrentValue;
            _onChangeToken = config.OnChange(updatedConfig => _currentConfig = updatedConfig);
        }

        public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new ColorConsoleLogger(name, GetCurrentConfig)); 

        private ColorConsoleLoggerConfiguration GetCurrentConfig() => _currentConfig;

        public void Dispose()
        {
            _loggers.Clear();
            _onChangeToken?.Dispose();
        }
    }


    public static class ColorConsoleLoggerExtensions
    {
        public static ILoggingBuilder AddColorConsoleLogger(this ILoggingBuilder builder)
        {
            builder.AddConfiguration();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ColorConsoleLoggerProvider>());
            LoggerProviderOptions.RegisterProviderOptions<ColorConsoleLoggerConfiguration, ColorConsoleLoggerProvider>(builder.Services);
            return builder;
        }

        public static ILoggingBuilder AddColorConsoleLogger(this ILoggingBuilder builder, Action<ColorConsoleLoggerConfiguration> configure)
        {
            builder.AddColorConsoleLogger();
            builder.Services.Configure(configure);
            return builder;
        }
    }

}

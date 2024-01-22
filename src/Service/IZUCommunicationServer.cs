//#define RELEASE

using AutoMapper;
using IZU.Base;
using IZU.DeviceFactories;
using IZU.Entities;
using IZU.Interfaces;
using Microsoft.AspNetCore.Hosting.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NNanomsg.Protocols;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace IZU.Service
{
    public class IZUCommunicationServer : ICommunication
    {
        private Guid oso = Guid.Parse("6F998BD2-B59F-4510-8E42-B50D18D22432");
        private Guid cfg = Guid.Parse("3cfeec89-feea-4be9-9e8f-f59d4feb8347");
        private readonly ILogger<IZUCommunicationServer> _logger;
        private const int BufferSize = 4096;
        private int task_ws_delay = 1000;
        private IS7NetService _s7NetService { get; }
        private UDPSocket _udpClient;
        private ReplySocket replySocket;
        private PairSocket nanoPairSocketServer;
        private Task task_nano_pair_server;
        private Task task_socket_udp;
        private CancellationTokenSource _cancelSourceUDP;
        private Task task_nano_server;
        readonly ConcurrentDictionary<Guid, InnerServerClient> _clients = new();
        public IZUCommunicationServer(IServer server, ILogger<IZUCommunicationServer> logger, IS7NetService s7netService)
        {
            _logger = logger;
            _s7NetService = s7netService;
            task_ws_delay = IZUConfig.PublishMillionSeconds;
        }
        public void Start()
        {
            InitialNanoReplyServer();
            InitialNanoPairServer();
            InitialUdpSocket();
            InitialWebsocket();
        }
        /// <summary>
        /// WEBSOCKET SERVER
        /// PORT 8031
        /// REMOTE SERVER
        /// </summary>
        void InitialWebsocket()
        {
            Task.Factory.StartNew(async () =>
            {
                _logger.LogDebug($"websocket publish task started, sending on port {IZUConfig.ServerPort}");
                while (true)
                {
#if RELEASE
                    if (_clients.Count != 0)
#endif
                    await WsPublishDevicesAsync();
                    await Task.Delay(task_ws_delay);
                }
            });
        }
        /// <summary>
        /// SOCKET UDP CLIENT
        /// PORT 8131
        /// REMOTE CLIENT
        /// </summary>
        void InitialUdpSocket()
        {
            _cancelSourceUDP = new CancellationTokenSource();
            _udpClient = new();
            _udpClient.Connect(IZUConfig.OSO_Server_ip, 8131);

            task_socket_udp = Task.Factory.StartNew(async () =>
            {
                _logger.LogDebug("socket UDP client task started, sending on port 8131");
                while (!_cancelSourceUDP.IsCancellationRequested)
                {
                    var msg = _s7NetService.GetAllDevices();
                    // 提取数据
                    List<string> data = new();
                    foreach (var it in msg)
                    {
                        if (it.DeviceType.Equals(DeviceTypes.AUTODOOR))
                        {
                            // 名称
                            string? name = it.Name;
                            object? status = CheckAuodoorStatus(it);

                            //if (DateTime.Now.Second < 20)
                            //    status = 0;
                            //else if (DateTime.Now.Second >= 20 && DateTime.Now.Second < 23)
                            //    status = 2;
                            //else if (DateTime.Now.Second >= 23 && DateTime.Now.Second <= 33)
                            //    status = 3;
                            //else if (DateTime.Now.Second > 33 && DateTime.Now.Second <= 36)
                            //    status = 1;
                            //else if (DateTime.Now.Second > 36)
                            //    status = 0;
                            //data.Add($"{name}:{status ?? 0}");
                        }

                    }
                    // 消息格式： izu::[3({name1}:0;{name2}:0),4({name1}:0;{name2}:0)]
                    string data_format = $"izu::[{(int)DeviceTypes.AUTODOOR}({string.Join(";", data)})]";

                    _udpClient.Send(data_format);
                    await Task.Delay(50);
                }
            }, _cancelSourceUDP.Token);
        }
        /// <summary>
        /// NANO REPLY SERVER
        /// PORT 8231
        /// REMOTE SERVER
        /// </summary>
        void InitialNanoReplyServer()
        {
            replySocket = new ReplySocket();
            replySocket.Bind($"tcp://{IZUConfig.ServerIP}:8231");
            task_nano_server = Task.Factory.StartNew(async () =>
            {
                _logger.LogDebug("nano repley server task started, listening on port 8231");
                while (true)
                {
                    byte[] buffer = replySocket.Receive();
                    string operation = System.Text.Encoding.UTF8.GetString(buffer);
                    operation = await OperationFromOso(operation);
                    replySocket.Send(System.Text.Encoding.UTF8.GetBytes(operation));
                }
            });
        }

        /// <summary>
        /// NANO PAIR SERVER
        /// PORT 18031
        /// LOCAL SYSTEM
        /// </summary>
        void InitialNanoPairServer()
        {
            List<DeviceEntity> cur = new List<DeviceEntity>();
            nanoPairSocketServer = new PairSocket();
            nanoPairSocketServer.Bind($"tcp://{IZUConfig.ServerIP}:18031");
            task_nano_pair_server = Task.Factory.StartNew(async () =>
            {
                _logger.LogDebug("nano pair server task started, listening on port 18031");
                while (true)
                {
                    nanoPairSocketServer.Receive();
                    cur = _s7NetService.GetAllDevices();
                    nanoPairSocketServer.Send(Encoding.GetEncoding("GB2312").GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(cur)));
                    await Task.Delay(10);
                }
            });
        }


        /// <summary>
        /// 刷新websocket发布数据频率
        /// </summary>
        public void Refresh()
        {
            task_ws_delay = IZUConfig.PublishMillionSeconds;
        }

        class InnerServerClient
        {
            public WebSocket Socket { get; }
            public Guid SessionId { get; }
            public int Status { get; set; } = 0;
            public string target { get; set; } = string.Empty;

            public InnerServerClient(WebSocket socket, Guid sessionId, string t = "")
            {
                Socket = socket;
                SessionId = sessionId;
                target = t;
            }
        }
        public async Task Acceptor(HttpContext context, Func<Task> next)
        {
            if (!context.WebSockets.IsWebSocketRequest) return;

            string? token = context.Request.Query["token"];
            if (string.IsNullOrEmpty(token)) return;

            //token 验证
            var token_value = token;
            if (string.IsNullOrEmpty(token_value) || !Guid.TryParse(token_value, out Guid sessionid))
                throw new Exception("token should be Guid");

            var socket = await context.WebSockets.AcceptWebSocketAsync();

            _clients.GetOrAdd(sessionid, cliid => new InnerServerClient(socket, sessionid));
            _logger.LogDebug($"websocket client connected, session id {sessionid}");

            var buffer = new byte[BufferSize];
            var seg = new ArraySegment<byte>(buffer);
            try
            {
                while (socket.State == WebSocketState.Open && _clients.ContainsKey(sessionid))
                {
                    var incoming = await socket.ReceiveAsync(seg, CancellationToken.None);
                    var outgoing = new ArraySegment<byte>(buffer, 0, incoming.Count);
                }
                socket.Abort();
            }
            catch
            {
            }
            _clients.TryRemove(sessionid, out var removedClient);
            _logger.LogDebug($"websocket client disconnected, session id {sessionid}");
        }

        object? CheckAuodoorStatus(DeviceEntity deviceEntity)
        {
            var opening = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R05")?.Value;
            var opened = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R07")?.Value;
            var openState = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value;
            var closing = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R06")?.Value;
            var closed = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R08")?.Value;
            var closeState = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R03")?.Value;
            if (opening == null || opened == null || openState == null || closing == null || closed == null || closeState == null)
                return  null;
            else
            {
                if ((bool)closing == false && (bool)closed && (bool)closeState && (bool)opening == false && (bool)opened == false && (bool)openState == false)
                    return 0;
                else if ((bool)closing && (bool)closed == false && (bool)closeState == false && (bool)opening == false && (bool)opened == false && (bool)openState == false)
                    return 1;
                else if ((bool)closing == false && (bool)closed == false && (bool)closeState == false && (bool)opening && (bool)opened == false && (bool)openState == false)
                    return 2;
                else if ((bool)closing == false && (bool)closed == false && (bool)closeState == false && (bool)opening == false && (bool)opened && (bool)openState)
                    return 3;
                else
                    return null;
            }
        }

        public async Task WsPublishDevicesAsync()
        {
            try
            {
                //if (msgtxt.StartsWith("__IZU__(ForceOffline)"))
                //{
                //	if (Guid.TryParse(msgtxt.AsSpan(24), out var clientId) && _clients.TryRemove(clientId, out var oldclients))
                //		foreach (var oldcli in oldclients)
                //		{
                //			try { oldcli.Value.socket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "disconnect", CancellationToken.None).GetAwaiter().GetResult(); } catch { }
                //			try { oldcli.Value.socket.Abort(); } catch { }
                //			try { oldcli.Value.socket.Dispose(); } catch { }
                //		}
                //	return;
                //}
                var msg = _s7NetService.GetAllDevices();
                JObject root = new();
                JArray currentArray = new();
                JObject currentObject = new();
                foreach (var it in msg)
                {
                    if (root.ContainsKey(it.DeviceType.ToString().ToLower()))
                    {
                        currentArray = (JArray)root[it.DeviceType.ToString().ToLower()]!;
                    }
                    else
                    {
                        currentArray = new JArray();
                        root[it.DeviceType.ToString().ToLower()] = currentArray;
                    }
                    currentObject = new();
                    currentObject["name"] = it.Name;
                    var list = from x in it.Variables where x.ActionType.StartsWith('R') select new { k = x.ActionType.ToLower(), v = x.Value };
                    foreach (var item in list)
                    {
                        currentObject[item.k] = new JValue(item.v);
                    }
                    currentArray.Add(currentObject);
                }

                var outgoing = new ArraySegment<byte>(Encoding.GetEncoding("GB2312").GetBytes(root.ToString(Formatting.None)));
                foreach (var client in _clients.Values)
                {
                    if (client.Status == 1 || !string.IsNullOrEmpty(client.target)) continue;

                    await client.Socket.SendAsync(outgoing, WebSocketMessageType.Text, true, CancellationToken.None)
                        .ContinueWith(async (t, state) =>
                        {
                            if (t.Exception != null && state is InnerServerClient client)
                            {
                                try { await client.Socket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "disconnect", CancellationToken.None); } catch { }
                                try { client.Socket.Abort(); } catch { }
                                try { client.Socket.Dispose(); } catch { }
                                client.Status = 1;//marked as wasted
                            }
                        }, client).ConfigureAwait(false);
                }
                foreach (var client in _clients.Values)
                {
                    if (client.Status == 0 || !string.IsNullOrEmpty(client.target))
                        continue;
                    _clients.TryRemove(client.SessionId, out var removedClient);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"websocket server publish error: {ex.Message}");
            }
        }


        enum DeviceOperations
        {
            None,
            Open,
            Close
        }
        /// <summary>
        /// 开门指令
        /// </summary>
        /// <param name="data">接受指令( 格式：{deviceType}:{deviceName}:{commandName}:{commandArg} )</param>
        /// <returns></returns>
        public async Task<string> OperationFromOso(string data)
        {
            string[] cmdArray = data.Split(':');
            if (cmdArray.Length > 4)
                return "command not available";

            int type = cmdArray[0].ToInt32();
            if (type == 0)
                return "device type not available";

            DeviceTypes deviceType = (DeviceTypes)type;
            string deviceName = cmdArray[1];
            var device = _s7NetService.GetDevice(deviceName);
            if (device == null)
                return $"device {deviceName} is not existed";

            int oper = cmdArray[2].ToInt32();
            if (oper == 0)
                return "device operation not available";
            DeviceOperations deviceOperation = (DeviceOperations)oper;

            IOperatable? deviceObject = null;
            switch (deviceType)
            {
                case DeviceTypes.NONE:
                    break;
                case DeviceTypes.IZU:
                    break;
                case DeviceTypes.HID:
                    break;
                case DeviceTypes.AUTODOOR:
                    deviceObject = new AutoDoor(device);
                    break;
                case DeviceTypes.FIREDOOR:
                    break;
            }
            if (deviceObject == null)
                return $"unknown device {deviceName}";
            else
            {
                switch (deviceOperation)
                {
                    default:
                    case DeviceOperations.None:
                        return "unkown device operation";
                    case DeviceOperations.Open:
                        return await deviceObject!.OpenAsync();
                    case DeviceOperations.Close:
                        return await deviceObject!.CloseAsync();

                }
            }

        }
    }
}

/// <summary>
/// 向指定的多个客户端id发送消息
/// </summary>
/// <param name="senderClientId">发送者的客户端id</param>
/// <param name="receiveClientId">接收者的客户端id</param>
/// <param name="message">消息</param>
/// <param name="receipt">是否回执</param>
//internal void SendMessage(Guid senderClientId, IEnumerable<Guid> receiveClientId, object message, bool receipt = false)
//{
//	receiveClientId = receiveClientId.Distinct().ToArray();
//	Dictionary<string, DalvsiSendEventArgs> redata = new Dictionary<string, DalvsiSendEventArgs>();

//	foreach (var uid in receiveClientId)
//	{
//		string server = SelectServer(uid);
//		if (!redata.ContainsKey(server)) 
//			redata.Add(server, new DalvsiSendEventArgs(server, senderClientId, message, receipt));
//		redata[server].ReceiveClientId.Add(uid);
//	}
//	var messageJson = JsonConvert.SerializeObject(message);
//	foreach (var sendArgs in redata.Values)
//	{
//		//OnSend?.Invoke(this, sendArgs);
//		_redis.Publish($"{_redisPrefix}Server{sendArgs.Server}", JsonConvert.SerializeObject((senderClientId, sendArgs.ReceiveClientId, messageJson, sendArgs.Receipt)));
//	}
//}
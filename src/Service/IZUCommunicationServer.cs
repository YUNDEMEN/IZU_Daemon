using AutoMapper;
using IZU.Base;
using IZU.DeviceFactories;
using IZU.Entities;
using IZU.Interfaces;
using Microsoft.AspNetCore.Hosting.Server;
using Newtonsoft.Json;
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
                    //if (_clients.Count != 0)
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

                            // 门状态（0关到位；1正在关；2正在开；3开到位；-1读null或全部是false）
                            var statusOpeningEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R05");
                            var statusOpening = statusOpeningEnt?.Value;
                            var statusOpenedEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R07");
                            var statusOpened = statusOpenedEnt?.Value;
                            var statusCloseingEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R06");
                            var statusClosing = statusCloseingEnt?.Value;
                            var statusClosedEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R08");
                            var statusClosed = statusClosedEnt?.Value;
                            object? status = null;
                            if (statusOpening == null || statusOpened == null || statusClosing == null || statusClosed == null)
                            {
                                status = null;
                            }
                            else
                            {
                                if (statusClosed.ToString().ToLower().Equals(true.ToString())) status = 0;
                                else if (statusClosing.ToString().ToLower().Equals(true.ToString())) status = 1;
                                else if (statusOpening.ToString().ToLower().Equals(true.ToString())) status = 2;
                                else if (statusOpened.ToString().ToLower().Equals(true.ToString())) status = 3;
                                else status = null;
                            }
                            if (DateTime.Now.Second < 20)
                                status = 0;
                            else if (DateTime.Now.Second >= 20 && DateTime.Now.Second < 23)
                                status = 2;
                            else if (DateTime.Now.Second >= 23 && DateTime.Now.Second <= 33)
                                status = 3;
                            else if (DateTime.Now.Second > 33 && DateTime.Now.Second <= 36)
                                status = 1;
                            else if (DateTime.Now.Second > 36)
                                status = 0;
                            data.Add($"{name}:{status ?? 0}");
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

                // 提取数据
                BroadcastData data = new();
                BroadcastData2 data2 = new();
                foreach (var it in msg)
                {
                    if (it.DeviceType.Equals(DeviceTypes.IZU))
                    {
                        #region 第2种
                        var old = it.Variables.Where(p => p.ActionType.StartsWith("R"));
                        var temp = new IzuStatus { name = it.Name };
                        foreach (var item in old)
                        {
                            if (item.ActionType == "R01") temp.r01 = item.Value;
                            if (item.ActionType == "R02") temp.r02 = item.Value;
                            if (item.ActionType == "R03") temp.r03 = item.Value;
                            if (item.ActionType == "R04") temp.r04 = item.Value;
                            if (item.ActionType == "R05") temp.r05 = item.Value;
                            if (item.ActionType == "R06") temp.r06 = item.Value;
                            if (item.ActionType == "R07") temp.r07 = item.Value;
                            if (item.ActionType == "R08") temp.r08 = item.Value;
                            if (item.ActionType == "R09") temp.r09 = item.Value;
                            if (item.ActionType == "R10") temp.r10 = item.Value;
                            if (item.ActionType == "R11") temp.r11 = item.Value;
                            if (item.ActionType == "R12") temp.r12 = item.Value;
                            if (item.ActionType == "R13") temp.r13 = item.Value;
                            if (item.ActionType == "R14") temp.r14 = item.Value;
                            if (item.ActionType == "R15") temp.r15 = item.Value;
                            if (item.ActionType == "R16") temp.r16 = item.Value;
                            if (item.ActionType == "R17") temp.r17 = item.Value;
                            if (item.ActionType == "R18") temp.r18 = item.Value;
                            if (item.ActionType == "R19") temp.r19 = item.Value;
                            if (item.ActionType == "R20") temp.r20 = item.Value;
                        }
                        data2.izu.Add(temp);
                        #endregion

                        // 名称
                        string? name = it.Name;

                        // 系统开机状态（1开机；0关机；-1读null）
                        var online = it.Variables.FirstOrDefault(p => p.ActionType == "R01")?.Value?.ToString()?.ToLower() == true.ToString() ? 1 : 0;

                        // 启动状态（true触发启动；false不触发启动？）
                        var runningStatus = it.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value;

                        // 故障状态
                        var fault = it.Variables.FirstOrDefault(p => p.ActionType == "R05")?.Value;

                        data.izu.Add(new BroadcastIzuInfo(name, online, runningStatus, fault));
                    }
                    else if (it.DeviceType.Equals(DeviceTypes.HID))
                    {
                        #region 第2种
                        var old = it.Variables.Where(p => p.ActionType.StartsWith("R"));
                        var temp = new HidStatus { name = it.Name };
                        foreach (var item in old)
                        {
                            if (item.ActionType == "R00") temp.r00 = item.Value;
                            if (item.ActionType == "R01") temp.r01 = item.Value;
                            if (item.ActionType == "R02") temp.r02 = item.Value;
                            if (item.ActionType == "R03") temp.r03 = item.Value;
                            if (item.ActionType == "R04") temp.r04 = item.Value;
                            if (item.ActionType == "R05") temp.r05 = item.Value;
                            if (item.ActionType == "R06") temp.r06 = item.Value;
                            if (item.ActionType == "R07") temp.r07 = item.Value;
                            if (item.ActionType == "R08") temp.r08 = item.Value;
                            if (item.ActionType == "R09") temp.r09 = item.Value;
                            if (item.ActionType == "R10") temp.r10 = item.Value;
                        }
                        data2.hid.Add(temp);
                        #endregion


                        string? name = it.Name;
                        var powerOn = it.Variables.FirstOrDefault(p => p.ActionType == "R00")?.Value;

                        // 故障状态
                        var fault = it.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value;

                        //data.hid.Add(new BroadcastHidInfo(name, online, fault));

                        // AllData
                        var R00 = it.Variables.FirstOrDefault(p => p.ActionType == "R00")?.Value;
                        var R01 = it.Variables.FirstOrDefault(p => p.ActionType == "R01")?.Value;
                        var R02 = it.Variables.FirstOrDefault(p => p.ActionType == "R02")?.Value;
                        var R03 = it.Variables.FirstOrDefault(p => p.ActionType == "R03")?.Value;
                        var R04 = it.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value;
                        var R05 = it.Variables.FirstOrDefault(p => p.ActionType == "R05")?.Value;
                        var R06 = it.Variables.FirstOrDefault(p => p.ActionType == "R06")?.Value;
                        var R07 = it.Variables.FirstOrDefault(p => p.ActionType == "R07")?.Value;
                        var R08 = it.Variables.FirstOrDefault(p => p.ActionType == "R08")?.Value;
                        var R09 = it.Variables.FirstOrDefault(p => p.ActionType == "R09")?.Value;

                        R00 = R00 == null ? null : ((bool)R00).ToString() == "True" ? "__" : "F";
                        R01 = R01 == null ? null : ((bool)R01).ToString() == "True" ? "__" : "F";
                        R02 = R02 == null ? null : ((bool)R02).ToString() == "True" ? "__" : "F";
                        R03 = R03 == null ? null : ((bool)R03).ToString() == "True" ? "__" : "F";
                        R04 = R04 == null ? null : ((bool)R04).ToString() == "True" ? "__" : "F";
                        R05 = R05 == null ? null : ((bool)R05).ToString() == "True" ? "__" : "F";
                        R06 = R06 == null ? null : ((bool)R06).ToString() == "True" ? "__" : "F";
                        R07 = R07 == null ? null : ((bool)R07).ToString() == "True" ? "__" : "F";
                        R08 = R08 == null ? null : ((bool)R08).ToString() == "True" ? "__" : "F";

                        //Console.WriteLine("【  " +
                        //  "R00:" + R00 + " " +
                        //  "R01:" + R01 + " " +
                        //  "R02:" + R02 + " " +
                        //  "R03:" + R03 + " " +
                        //  "R04:" + R04 + " " +
                        //  "R05:" + R05 + " " +
                        //  "R06:" + R06 + " " +
                        //  "R07:" + R07 + " " +
                        //  "R08:" + R08 + " " +
                        //  "R09:" + R09 + " " +
                        //  " 】");
                    }
                    else if (it.DeviceType.Equals(DeviceTypes.AUTODOOR))
                    {
                        #region 第2种
                        var old = it.Variables.Where(p => p.ActionType.StartsWith("R"));
                        var temp = new AutodoorStatus { name = it.Name };
                        foreach (var item in old)
                        {
                            if (item.ActionType == "R00") temp.r00 = item.Value;
                            if (item.ActionType == "R01") temp.r01 = item.Value;
                            if (item.ActionType == "R02") temp.r02 = item.Value;
                            if (item.ActionType == "R03") temp.r03 = item.Value;
                            if (item.ActionType == "R04") temp.r04 = item.Value;
                            if (item.ActionType == "R05") temp.r05 = item.Value;
                            if (item.ActionType == "R06") temp.r06 = item.Value;
                            if (item.ActionType == "R07") temp.r07 = item.Value;
                            if (item.ActionType == "R08") temp.r08 = item.Value;
                            if (item.ActionType == "R09") temp.r09 = item.Value;
                            if (item.ActionType == "R10") temp.r10 = item.Value;
                            if (item.ActionType == "R11") temp.r11 = item.Value;
                            if (item.ActionType == "R12") temp.r12 = item.Value;
                            if (item.ActionType == "R13") temp.r13 = item.Value;
                        }
                        data2.autodoor.Add(temp);
                        #endregion

                        // 名称
                        string? name = it.Name;
                        // 上电状态
                        var powerOn = it.Variables.FirstOrDefault(p => p.ActionType == "R00")?.Value;
                        // 初始化完成状态
                        var initialized = it.Variables.FirstOrDefault(p => p.ActionType == "R11")?.Value;
                        // 自动运行状态
                        var runningStatus = it.Variables.FirstOrDefault(p => p.ActionType == "R02")?.Value;
                        // 故障状态
                        var fault = it.Variables.FirstOrDefault(p => p.ActionType == "R09")?.Value;
                        // 紧急停止返回信号
                        var emergStatus = it.Variables.FirstOrDefault(p => p.ActionType == "R13")?.Value;

                        // 门状态（0关到位；1正在关；2正在开；3开到位；-1读null或全部是false）
                        var opening = it.Variables.FirstOrDefault(p => p.ActionType == "R05")?.Value;
                        var opened = it.Variables.FirstOrDefault(p => p.ActionType == "R07")?.Value;
                        var openStatus = it.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value;
                        var closing = it.Variables.FirstOrDefault(p => p.ActionType == "R06")?.Value;
                        var closed = it.Variables.FirstOrDefault(p => p.ActionType == "R08")?.Value;
                        var closeStatus = it.Variables.FirstOrDefault(p => p.ActionType == "R03")?.Value;
                        object? doorStatus = null;
                        if (opening == null || opened == null || openStatus == null || closing == null || closed == null || closeStatus == null)
                        {
                            doorStatus = null;
                        }
                        else
                        {
                            if ((bool)closed & (bool)closeStatus) doorStatus = 0;
                            else if ((bool)closing) doorStatus = 1;
                            else if ((bool)opening) doorStatus = 2;
                            else if ((bool)opened & (bool)openStatus) doorStatus = 3;
                        }

                        // AllData
                        var R00 = it.Variables.FirstOrDefault(p => p.ActionType == "R00")?.Value;
                        var R01 = it.Variables.FirstOrDefault(p => p.ActionType == "R01")?.Value;
                        var R02 = it.Variables.FirstOrDefault(p => p.ActionType == "R02")?.Value;
                        var R03 = it.Variables.FirstOrDefault(p => p.ActionType == "R03")?.Value;
                        var R04 = it.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value;
                        var R05 = it.Variables.FirstOrDefault(p => p.ActionType == "R05")?.Value;
                        var R06 = it.Variables.FirstOrDefault(p => p.ActionType == "R06")?.Value;
                        var R07 = it.Variables.FirstOrDefault(p => p.ActionType == "R07")?.Value;
                        var R08 = it.Variables.FirstOrDefault(p => p.ActionType == "R08")?.Value;
                        var R09 = it.Variables.FirstOrDefault(p => p.ActionType == "R09")?.Value;
                        var R10 = it.Variables.FirstOrDefault(p => p.ActionType == "R10")?.Value;
                        var R11 = it.Variables.FirstOrDefault(p => p.ActionType == "R11")?.Value;
                        var R12 = it.Variables.FirstOrDefault(p => p.ActionType == "R12")?.Value;
                        var R13 = it.Variables.FirstOrDefault(p => p.ActionType == "R13")?.Value;

                        //R00（全部）不为空
                        if (R00 != null)
                        {
                            string rr = string.Empty;
                            if (doorStatus != null)
                            {
                                switch ((int)doorStatus)
                                {
                                    case 0:
                                        rr = "关到位";
                                        break;
                                    case 1:
                                        rr = "正在关";
                                        break;
                                    case 2:
                                        rr = "正在开";
                                        break;
                                    case 3:
                                        rr = "开到位";
                                        break;
                                    default:
                                        break;
                                }
                            }
                            //Console.WriteLine("【  " +
                            //  "上电:" + R00 + " " +
                            //  "初始化状态:" + R11 + " " +
                            //  "系统自动运行:" + R02 + " " +
                            //  "故障:" + R09 + " " +
                            //  "门状态:" + rr + " " +
                            //  "复位状态:" + R12 + " " +
                            //  "急停状态:" + R13 + " " +
                            //  " 】");
                        }

                        R00 = R00 == null ? null : ((bool)R00).ToString() == "True" ? "__" : "F";
                        R01 = R01 == null ? null : ((bool)R01).ToString() == "True" ? "__" : "F";
                        R02 = R02 == null ? null : ((bool)R02).ToString() == "True" ? "__" : "F";
                        R03 = R03 == null ? null : ((bool)R03).ToString() == "True" ? "__" : "F";
                        R04 = R04 == null ? null : ((bool)R04).ToString() == "True" ? "__" : "F";
                        R05 = R05 == null ? null : ((bool)R05).ToString() == "True" ? "__" : "F";
                        R06 = R06 == null ? null : ((bool)R06).ToString() == "True" ? "__" : "F";
                        R07 = R07 == null ? null : ((bool)R07).ToString() == "True" ? "__" : "F";
                        R08 = R08 == null ? null : ((bool)R08).ToString() == "True" ? "__" : "F";
                        R09 = R09 == null ? null : ((bool)R09).ToString() == "True" ? "__" : "F";
                        R10 = R10 == null ? null : ((bool)R10).ToString() == "True" ? "__" : "F";
                        R11 = R11 == null ? null : ((bool)R11).ToString() == "True" ? "__" : "F";
                        R12 = R12 == null ? null : ((bool)R12).ToString() == "True" ? "__" : "F";
                        R13 = R13 == null ? null : ((bool)R13).ToString() == "True" ? "__" : "F";

                        //Console.WriteLine("【  " +
                        //  "R00:" + R00 + " " +
                        //  "R01:" + R01 + " " +
                        //  "R02:" + R02 + " " +
                        //  "R03:" + R03 + " " +
                        //  "R04:" + R04 + " " +
                        //  "R05:" + R05 + " " +
                        //  "R06:" + R06 + " " +
                        //  "R07:" + R07 + " " +
                        //  "R08:" + R08 + " " +
                        //  "R09:" + R09 + " " +
                        //  "R10:" + R10 + " " +
                        //  "R11:" + R11 + " " +
                        //  "R12:" + R12 + " " +
                        //  "R13:" + R13 + " 】");


                        //Console.WriteLine("\n------------------------------------------------------");
                        //Console.WriteLine("【  " + opening + " " + opened + " " + openStatus + " " + closing + " " + closed + " " + closeStatus + " 】");
                        //Console.WriteLine("------------------------------------------------------\n");

                        //Console.WriteLine("\n------------------------------------------------------");
                        //Console.WriteLine("【  " + start + " " + initial + " " + fault + " 】");
                        //Console.WriteLine("------------------------------------------------------\n");

                        data.autodoor.Add(new BroadcastAutodoorInfo(name, powerOn, initialized, runningStatus, fault, emergStatus, doorStatus));
                    }
                    else if (it.DeviceType.Equals(DeviceTypes.FIREDOOR))
                    {
                        #region 第2种
                        var old = it.Variables.Where(p => p.ActionType.StartsWith("R"));
                        var temp = new FiredoorStatus { name = it.Name };
                        foreach (var item in old)
                        {
                            if (item.ActionType == "R00") temp.r00 = item.Value;
                            if (item.ActionType == "R01") temp.r01 = item.Value;
                            if (item.ActionType == "R02") temp.r02 = item.Value;
                            if (item.ActionType == "R03") temp.r03 = item.Value;
                            if (item.ActionType == "R04") temp.r04 = item.Value;
                            if (item.ActionType == "R05") temp.r05 = item.Value;
                            if (item.ActionType == "R06") temp.r06 = item.Value;
                            if (item.ActionType == "R07") temp.r07 = item.Value;
                            if (item.ActionType == "R08") temp.r08 = item.Value;
                            if (item.ActionType == "R09") temp.r09 = item.Value;
                            if (item.ActionType == "R10") temp.r10 = item.Value;
                            if (item.ActionType == "R11") temp.r11 = item.Value;
                            if (item.ActionType == "R12") temp.r12 = item.Value;
                            if (item.ActionType == "R13") temp.r13 = item.Value;
                        }
                        data2.firedoor.Add(temp);
                        #endregion

                        // 名称
                        string? name = it.Name;

                        // 系统开机状态（1开机；0关机；-1读null）
                        var online = it.Variables.FirstOrDefault(p => p.ActionType == "R01")?.Value?.ToString()?.ToLower() == true.ToString() ? 1 : 0;

                        data.firedoor.Add(new BroadcastFiredoorInfo(name, online));
                    }
                }
                var test = JsonConvert.SerializeObject(data);
                var test2 = JsonConvert.SerializeObject(data2);

                var outgoing = new ArraySegment<byte>(Encoding.GetEncoding("GB2312").GetBytes(JsonConvert.SerializeObject(data)));
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
                _logger.LogWarning($"broadcast server error: {ex.Message}");
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
                switch(deviceOperation)
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
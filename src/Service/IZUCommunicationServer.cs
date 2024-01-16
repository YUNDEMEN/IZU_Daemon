using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NNanomsg.Protocols;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection.Metadata.Ecma335;
using System.ServiceProcess;
using System.Text;

namespace IZU.Service
{
    public class IZUCommunicationServer : ICommunication
    {
        Guid oso = Guid.Parse("6F998BD2-B59F-4510-8E42-B50D18D22432");
        Guid cfg = Guid.Parse("3cfeec89-feea-4be9-9e8f-f59d4feb8347");
        readonly ILogger<IZUCommunicationServer> _logger;
        const int BufferSize = 4096;
        int taskDelay = 1000;
        IS7NetService _s7NetService { get; }
        UDPSocket _udpClient;
        Task loopTask;
        readonly ConcurrentDictionary<Guid, InnerServerClient> _clients = new();
        public IZUCommunicationServer(IServer server, ILogger<IZUCommunicationServer> logger, IS7NetService s7netService)
        {
            _logger = logger;
            _s7NetService = s7netService;
            taskDelay = IZUConfig.PublishMillionSeconds;
            InitialUdpSocket();
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await BroadcastDevicesAsync();
                    await Task.Delay(taskDelay);
                }

            });
            //Task.Factory.StartNew(async () =>
            //{
            //    while (true)
            //    {
            //        await BroadcastDevicesToConfigClientAsync();
            //        await Task.Delay(20);
            //    }

            //});

            CreateNanoServer();
        }

        CancellationTokenSource cts = new CancellationTokenSource();
        void InitialUdpSocket()
        {
            _udpClient = new();
            _udpClient.Connect("127.0.0.1", 18031);

            loopTask = Task.Factory.StartNew(async () => {
                while (!cts.IsCancellationRequested)
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
                    // 消息格式： {izu name}::[3({name1}:0;{name2}:0),4({name1}:0;{name2}:0)]
                    string data_format = $"izu::[{(int)DeviceTypes.AUTODOOR}({string.Join(";", data)})]";

                    _udpClient.Send(data_format);
                    await Task.Delay(50);
                }
            }, cts.Token);
        }



        /// <summary>
        /// 刷新websocket发布数据频率
        /// </summary>
        public void Refresh()
        {
            taskDelay = IZUConfig.PublishMillionSeconds;
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
            
            if (sessionid == oso)
                _clients.GetOrAdd(sessionid, cliid => new InnerServerClient(socket, sessionid, "oso"));
            else if (sessionid == cfg)
                _clients.GetOrAdd(sessionid, cliid => new InnerServerClient(socket, sessionid, "cfg"));
            else
                _clients.GetOrAdd(sessionid, cliid => new InnerServerClient(socket, sessionid));

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

        public async Task BroadcastDevicesAsync()
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
                foreach (var it in msg)
                {
                    if (it.DeviceType.Equals(DeviceTypes.IZU))
                    {
                        // 名称
                        string? name = it.Name;

                        // 系统开机状态（1开机；0关机；-1读null）
                        var onlineEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R01");
                        object? online = null;
                        if (onlineEnt != null && onlineEnt.Value != null)
                            online = onlineEnt.Value.ToString().ToLower() == true.ToString() ? 1 : 0;

                        // 启动状态（true触发启动；false不触发启动？）
                        var runningStatusEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R04");
                        object? runningStatus = null;
                        if (runningStatusEnt != null && runningStatusEnt.Value != null)
                            runningStatus = runningStatusEnt.Value.ToString().ToLower() == true.ToString() ? true : false;

                        // 故障状态
                        var faultEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R05");
                        object? fault = null;
                        if (faultEnt != null && faultEnt.Value != null)
                            fault = faultEnt.Value.ToString().ToLower() == true.ToString() ? true : false;

                        data.izu.Add(new BroadcastIzuInfo(name, online, runningStatus, fault));
                    }
                    else if (it.DeviceType.Equals(DeviceTypes.HID))
                    {
                        // 名称
                        string? name = it.Name;

                        // 系统开机状态（1开机；0关机；-1读null）
                        var onlineEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R01");
                        object? online = null;
                        if (onlineEnt != null && onlineEnt.Value != null)
                            online = onlineEnt.Value.ToString().ToLower() == true.ToString() ? 1 : 0;

                        // 故障状态
                        var faultEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R04");
                        object? fault = null;
                        if (faultEnt != null && faultEnt.Value != null)
                            fault = faultEnt.Value.ToString().ToLower() == true.ToString() ? true : false;

                        data.hid.Add(new BroadcastHidInfo(name, online, fault));
                    }
                    else if (it.DeviceType.Equals(DeviceTypes.AUTODOOR))
                    {
                        // 名称
                        string? name = it.Name;
                        var readableList = from x in it.Variables where x.ActionType.StartsWith("R") select (KeyValueObject)x;
                        // 上电状态
                        var powerOnEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R00");
                        var powerOn = powerOnEnt?.Value;

                        // 系统待机状态，系统开机状态（1开机；0关机；-1读null）
                        var onlineEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R01");
                        object? online = onlineEnt?.Value?.ToString()?.ToLower() == true.ToString() ? 1 : 0;

                        // 门状态（0关到位；1正在关；2正在开；3开到位；-1读null或全部是false）
                        var opening = it.Variables.FirstOrDefault(p => p.ActionType == "R05")?.Value;
                        var opened = it.Variables.FirstOrDefault(p => p.ActionType == "R07")?.Value;
                        var openStatus = it.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value;
                        var closing = it.Variables.FirstOrDefault(p => p.ActionType == "R06")?.Value;
                        var closed = it.Variables.FirstOrDefault(p => p.ActionType == "R08")?.Value;
                        var closeStatus = it.Variables.FirstOrDefault(p => p.ActionType == "R03")?.Value;

                        //Console.WriteLine("\n------------------------------------------------------");
                        //Console.WriteLine("【  " + opening + " " + opened + " " + openStatus + " " + closing + " " + closed + " " + closeStatus + " 】");
                        //Console.WriteLine("------------------------------------------------------\n");
                        object? status = null;
                        if (opening == null || opened == null || openStatus == null || closing == null || closed == null || closeStatus == null)
                        {
                            status = null;
                        }
                        else
                        {
                            if ((bool)closed & (bool)closeStatus) status = 0;
                            else if ((bool)closing) status = 1;
                            else if ((bool)opening) status = 2;
                            else if ((bool)opened & (bool)openStatus) status = 3;
                        }
                        //Console.WriteLine("\n------------------------------------------------------");
                        //Console.WriteLine("【  " + status + " 】");
                        //Console.WriteLine("------------------------------------------------------\n");

                        ///////////////////////////////和模式重复---------------------------------------
                        // 系统自动运行状态（true触发启动；false不触发启动？）
                        var start_sigEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R02");
                        object? start = start_sigEnt?.Value;

                        // 初始化状态（true触发初始化；false不触发初始化？）
                        var initial_sigEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R11");
                        object? initial = initial_sigEnt?.Value;

                        // 故障状态
                        var faultEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R09");
                        object? fault = faultEnt?.Value;

                        // 横式 (手动True 自动False) 默认手动
                        var modeEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R02");
                        object? mode = modeEnt?.Value;
                        if (mode != null)
                            mode = !(bool)mode;

                        //Console.WriteLine("\n------------------------------------------------------");
                        //Console.WriteLine("【  " + start + " " + initial + " " + fault + " 】");
                        //Console.WriteLine("------------------------------------------------------\n");

                        data.autodoor.Add(new BroadcastAutodoorInfo(name, powerOn, online, status, start, initial, fault, mode));
                    }
                    else if (it.DeviceType.Equals(DeviceTypes.FIREDOOR))
                    {
                        // 名称
                        string? name = it.Name;

                        // 系统开机状态（1开机；0关机；-1读null）
                        var onlineEnt = it.Variables.FirstOrDefault(p => p.ActionType == "R01");
                        object? online = null;
                        if (onlineEnt != null && onlineEnt.Value != null)
                            online = onlineEnt.Value.ToString().ToLower() == true.ToString() ? 1 : 0;

                        data.firedoor.Add(new BroadcastFiredoorInfo(name, online));
                    }
                }
                var test = JsonConvert.SerializeObject(data);

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


        public async Task BroadcastDevicesToOSOAsync()
        {
            try
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
                // 消息格式： {izu name}::[3({name1}:0;{name2}:0),4({name1}:0;{name2}:0)]
                string data_format = $"izu::[{(int)DeviceTypes.AUTODOOR}({string.Join(";", data)})]";
                var outgoing = new ArraySegment<byte>(Encoding.UTF8.GetBytes(data_format));
                foreach (var client in _clients.Values)
                {
                    if (client.Status == 1 || client.target != "oso") continue;

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
                    if (client.Status == 0 || client.target == string.Empty)
                        continue;
                    _clients.TryRemove(client.SessionId, out var removedClient);
                }
            }
            catch (Exception ex)
            {
                //_logger.LogWarning($"broadcast server error: {ex.Message}");
            }
        }


        private Task t_server;
        PairSocket nanoServer = new PairSocket();

        void CreateNanoServer()
        {
            List<DeviceEntity> cur = new List<DeviceEntity>();
            nanoServer.Bind($"tcp://{IZUConfig.ServerIP}:18031");
            t_server = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    nanoServer.Receive();
                    cur = _s7NetService.GetAllDevices();
                    nanoServer.Send(Encoding.GetEncoding("GB2312").GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(cur)));
                    await Task.Delay(10);
                }
            });
        }

        public async Task BroadcastDevicesToConfigClientAsync()
        {
            try
            {
                var msg = _s7NetService.GetAllDevices();
                var outgoing = new ArraySegment<byte>(Encoding.GetEncoding("GB2312").GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(msg)));
                foreach (var client in _clients.Values)
                {
                    if (client.Status == 1 || client.target != "cfg") continue;

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
                    if (client.Status == 0 || client.target == string.Empty)
                        continue;
                    _clients.TryRemove(client.SessionId, out var removedClient);
                }
            }
            catch (Exception ex)
            {
                //_logger.LogWarning($"broadcast server error: {ex.Message}");
            }
        }
    }
}

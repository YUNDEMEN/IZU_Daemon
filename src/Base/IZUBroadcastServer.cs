using IZU.Entities;
using IZU.Interfaces;
using IZU.Service;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection.Metadata.Ecma335;
using System.ServiceProcess;
using System.Text;

namespace IZU.Base
{
    public class IZUBroadcastServer : NLogProvider
    {
        const int BufferSize = 4096;
        IIZUService _izuService { get; }
        ConcurrentDictionary<Guid, InnerServerClient> _clients = new();
        public IZUBroadcastServer(IIZUService izuService)
        {
            _izuService = izuService;

            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await BroadcastDevicesAsync();
                    await Task.Delay(1000);
                }

            });
        }

        class InnerServerClient
        {
            public WebSocket Socket { get; }
            public Guid SessionId { get; }
            public int Status { get; set; } = 0;

            public InnerServerClient(WebSocket socket, Guid sessionId)
            {
                this.Socket = socket;
                this.SessionId = sessionId;
            }
        }
        internal async Task Acceptor(HttpContext context, Func<Task> next)
        {
            if (!context.WebSockets.IsWebSocketRequest) return;

            string token = context.Request.Query["token"];
            if (string.IsNullOrEmpty(token)) return;

            //token 验证
            var token_value = token;// _redis.Get($"{_redisPrefix}Token{token}");
            if (string.IsNullOrEmpty(token_value) || !Guid.TryParse(token_value, out Guid sessionid))
                throw new Exception("token should be Guid");

            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var client = _clients.GetOrAdd(sessionid, cliid => new InnerServerClient(socket, sessionid));

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
                var msg = _izuService.S7netService.GetAllDevices();

                // 提取数据
                BroadcastData bData = new();
                foreach (var it in msg)
                {
                    if (it.DeviceType.Equals(DeviceTypes.AUTODOOR))
                    {
                        // 名称
                        string name = it.Name;

                        // 系统开机状态（1开机；0关机；-1读null）
                        var onlineEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R01");
                        int online = -1;
                        if (onlineEnt != null && onlineEnt.Value != null)
                            online = onlineEnt.Value.ToString().ToLower() == true.ToString() ? 1 : 0;

                        // 门状态（0关到位；1正在关；2正在开；3开到位；-1读null或全部是false）
                        var statusOpeningEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R05");
                        var statusOpening = statusOpeningEnt?.Value;
                        var statusOpenedEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R07");
                        var statusOpened = statusOpenedEnt?.Value;
                        var statusCloseingEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R06");
                        var statusClosing = statusCloseingEnt?.Value;
                        var statusClosedEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R08");
                        var statusClosed = statusClosedEnt?.Value;
                        int status = -1;
                        if (statusOpening == null || statusOpened == null || statusClosing == null || statusClosed == null)
                        {
                            status = -1;
                        }
                        else
                        {
                            if (statusClosed.ToString().ToLower().Equals(true.ToString())) status = 0;
                            else if (statusClosing.ToString().ToLower().Equals(true.ToString())) status = 1;
                            else if (statusOpening.ToString().ToLower().Equals(true.ToString())) status = 2;
                            else if (statusOpened.ToString().ToLower().Equals(true.ToString())) status = 3;
                            else status = -1;
                        }

                        // 启动状态（true触发启动；false不触发启动？）
                        var start_sigEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R02");
                        bool start = false;
                        if (start_sigEnt != null && start_sigEnt.Value != null)
                            start = start_sigEnt.Value.ToString().ToLower() == true.ToString() ? true : false;

                        // 初始化状态（true触发初始化；false不触发初始化？）
                        var initial_sigEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R11");
                        bool initial = false;
                        if (initial_sigEnt != null && initial_sigEnt.Value != null)
                            initial = initial_sigEnt.Value.ToString().ToLower() == true.ToString() ? true : false;

                        // 故障状态
                        var faultEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R04");
                        var fault = false;
                        if (faultEnt != null && faultEnt.Value != null)
                            fault = faultEnt.Value.ToString().ToLower() == true.ToString() ? true : false;

                        // 横式 (手动True 自动False) 默认手动
                        var modeEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R02");
                        int mode = 1;

                        bData.autodoor.Add(new BroadcastAutodoorInfo(name, online, status, start, initial, fault, mode));
                    }
                    else if (it.DeviceType.Equals(DeviceTypes.IZU))
                    {
                        // 名称
                        string name = it.Name;

                        // 系统开机状态（1开机；0关机；-1读null）
                        var onlineEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R01");
                        int online = -1;
                        if (onlineEnt != null && onlineEnt.Value != null)
                            online = onlineEnt.Value.ToString().ToLower() == true.ToString() ? 1 : 0;

                        // 启动状态（true触发启动；false不触发启动？）
                        var runningStatusEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R04");
                        bool runningStatus = false;
                        if (runningStatusEnt != null && runningStatusEnt.Value != null)
                            runningStatus = runningStatusEnt.Value.ToString().ToLower() == true.ToString() ? true : false;

                        // 故障状态
                        var faultEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R05");
                        var fault = false;
                        if (faultEnt != null && faultEnt.Value != null)
                            fault = faultEnt.Value.ToString().ToLower() == true.ToString() ? true : false;

                        bData.izu.Add(new BroadcastIzuInfo(name, online, runningStatus, fault));
                    }
                    else if (it.DeviceType.Equals(DeviceTypes.FIREDOOR))
                    {
                        // 名称
                        string name = it.Name;

                        // 系统开机状态（1开机；0关机；-1读null）
                        var onlineEnt = it.Variables.FirstOrDefault(p => p.ActionType2 == "R01");
                        int online = -1;
                        if (onlineEnt != null && onlineEnt.Value != null)
                            online = onlineEnt.Value.ToString().ToLower() == true.ToString() ? 1 : 0;

                        bData.firedoor.Add(new BroadcastFiredoorInfo(name, online));
                    }
                }
                var test = JsonConvert.SerializeObject(bData);

                var outgoing = new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(bData)));
                foreach (var client in _clients.Values)
                {
                    if (client.Status == 1) continue;

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
                    if (client.Status == 0)
                        continue;
                    _clients.TryRemove(client.SessionId, out var removedClient);
                }
            }
            catch (Exception ex)
            {
                LogWarn($"broadcast server error: {ex.Message}");
            }
        }

    }
}

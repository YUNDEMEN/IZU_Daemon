using IZU.Base;
using IZU.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Wonder.Service.Framework;

namespace IZU.Service
{
    [Regist(RegisterTypes.Singleton | RegisterTypes.LongRunningTask)]
    public class IZUWebSocketService : LongRunningTask, IIZUWebSocketService
    {
        private readonly ILogger<IZUWebSocketService> _logger;
        private const int BufferSize = 4096;
        private int task_ws_delay = 100;
        private IS7NetService _s7NetService { get; }
        public readonly ConcurrentDictionary<Guid, WebsocketServerClient> Clients;
        public IZUWebSocketService(ILogger<IZUWebSocketService> logger, IS7NetService s7netService)
            : base(logger)
        {
            _logger = logger;
            _s7NetService = s7netService;
            Clients = new();
        }
        public override void Start()
        {
            Refresh();
            //_logger.LogDebug($"websocket publish task started, sending on port {IZUConfig.ServerPort}");
            base.Start();
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lastExecuteTime = DateTime.Now.ToString();
            JObject root = new();

#if RELEASE
                    if (_clients.Count == 0)
                    {
                        await Task.Delay(task_ws_delay);
                        continue;
                    }
#endif
            root = WsPublishDevices();

            var outgoing = new ArraySegment<byte>(Encoding.GetEncoding("GB2312").GetBytes(root.ToString(Formatting.None)));
            foreach (var client in Clients.Values)
            {
                if (client.Status == 1 || !string.IsNullOrEmpty(client.target)) continue;

                await client.Socket.SendAsync(outgoing, WebSocketMessageType.Text, true, CancellationToken.None)
                    .ContinueWith(async (t, state) =>
                    {
                        if (t.Exception != null && state is WebsocketServerClient client)
                        {
                            try { await client.Socket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "disconnect", CancellationToken.None); } catch { }
                            try { client.Socket.Abort(); } catch { }
                            try { client.Socket.Dispose(); } catch { }
                            client.Status = 1;//marked as wasted
                        }
                    }, client).ConfigureAwait(false);
            }
            foreach (var client in Clients.Values)
            {
                if (client.Status == 0 || !string.IsNullOrEmpty(client.target))
                    continue;
                Clients.TryRemove(client.SessionId, out var removedClient);
            }
        }
        public override void Stop()
        {
            base.Stop();
            Clients.Clear();
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

            Clients.GetOrAdd(sessionid, cliid => new WebsocketServerClient(socket, sessionid));
            _logger.LogDebug($"websocket client connected, session id {sessionid}");

            var buffer = new byte[BufferSize];
            var seg = new ArraySegment<byte>(buffer);
            try
            {
                while (socket.State == WebSocketState.Open && Clients.ContainsKey(sessionid))
                {
                    var incoming = await socket.ReceiveAsync(seg, CancellationToken.None);
                    var outgoing = new ArraySegment<byte>(buffer, 0, incoming.Count);
                }
                socket.Abort();
            }
            catch
            {
            }
            Clients.TryRemove(sessionid, out var removedClient);
            _logger.LogDebug($"websocket client disconnected, session id {sessionid}");
        }


        public JObject WsPublishDevices()
        {
            JObject root = new();
            JArray currentArray = new();
            JObject currentObject = new();
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
                    currentObject = new() { ["name"] = it.Name };
                    if (it.DeviceType == DeviceTypes.AUTODOOR)
                        currentObject["doorState"] = DeviceFactory.CheckAuodoorStatus(it) == null ? null : DeviceFactory.CheckAuodoorStatus(it)!.ToString();
                    var list = from x in it.Variables where x.ActionType.StartsWith('R') select new { k = x.ActionType.ToLower(), v = x.Value };
                    foreach (var item in list)
                    {
                        currentObject[item.k] = new JValue(item.v);
                    }
                    currentArray.Add(currentObject);
#if DEBUG2
                    #region 设备调试
                    if (it.DeviceType.Equals(DeviceTypes.IZU))
                    {
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
                        var R14 = it.Variables.FirstOrDefault(p => p.ActionType == "R14")?.Value;
                        var R15 = it.Variables.FirstOrDefault(p => p.ActionType == "R15")?.Value;
                        var R16 = it.Variables.FirstOrDefault(p => p.ActionType == "R16")?.Value;
                        var R17 = it.Variables.FirstOrDefault(p => p.ActionType == "R17")?.Value;
                        var R18 = it.Variables.FirstOrDefault(p => p.ActionType == "R18")?.Value;
                        var R19 = it.Variables.FirstOrDefault(p => p.ActionType == "R19")?.Value;
                        var R20 = it.Variables.FirstOrDefault(p => p.ActionType == "R20")?.Value;
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
                        R14 = R14 == null ? null : ((bool)R14).ToString() == "True" ? "__" : "F";
                        R15 = R15 == null ? null : ((bool)R15).ToString() == "True" ? "__" : "F";
                        R16 = R16 == null ? null : ((bool)R16).ToString() == "True" ? "__" : "F";
                        R17 = R17 == null ? null : ((bool)R17).ToString() == "True" ? "__" : "F";
                        R18 = R18 == null ? null : ((bool)R18).ToString() == "True" ? "__" : "F";
                        R19 = R19 == null ? null : ((bool)R19).ToString() == "True" ? "__" : "F";
                        R20 = R20 == null ? null : ((bool)R20).ToString() == "True" ? "__" : "F";
                        //if(it.Name.Contains("1"))
                        //Console.WriteLine("【  " +
                        //"Name:" + it.Name + " " +
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
                        //  "R13:" + R13 + " " +
                        //  "R14:" + R14 + " " +
                        //  "R15:" + R15 + " " +
                        //  "R16:" + R16 + " " +
                        //  "R17:" + R17 + " " +
                        //  "R18:" + R18 + " " +
                        //  "R19:" + R19 + " " +
                        //  "R20:" + R20 + " " +
                        //  " 】");
                    }
                    else if (it.DeviceType.Equals(DeviceTypes.HID))
                    {
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
                        R00 = R00 == null ? null : ((bool)R00).ToString() == "True" ? "__" : "F";
                        R01 = R01 == null ? null : ((bool)R01).ToString() == "True" ? "__" : "F";
                        R02 = R02 == null ? null : ((bool)R02).ToString() == "True" ? "__" : "F";
                        R03 = R03 == null ? null : ((bool)R03).ToString() == "True" ? "__" : "F";
                        R04 = R04 == null ? null : ((bool)R04).ToString() == "True" ? "__" : "F";
                        R05 = R05 == null ? null : ((bool)R05).ToString() == "True" ? "__" : "F";
                        R06 = R06 == null ? null : ((bool)R06).ToString() == "True" ? "__" : "F";
                        R07 = R07 == null ? null : ((bool)R07).ToString() == "True" ? "__" : "F";
                        R08 = R08 == null ? null : ((bool)R08).ToString() == "True" ? "__" : "F";
                        R10 = R10 == null ? null : ((bool)R10).ToString() == "True" ? "__" : "F";
                        R11 = R11 == null ? null : ((bool)R11).ToString() == "True" ? "__" : "F";
                        //if(it.Name.Contains("1"))
                        //Console.WriteLine("【  " +
                        //"Name:" + it.Name + " " +
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
                        //  "R10:" + R11 + " " +
                        //  " 】");
                    }
                    else if (it.DeviceType.Equals(DeviceTypes.AUTODOOR))
                    {
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
                        var R14 = it.Variables.FirstOrDefault(p => p.ActionType == "R14")?.Value;
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
                        R14 = R14 == null ? null : ((bool)R14).ToString() == "True" ? "__" : "F";
                        //if (it.Name.Contains("1"))
                        //    Console.WriteLine("【  " +
                        //       "Name:" + it.Name + " " +
                        //      "R00:" + R00 + " " +
                        //      "R01:" + R01 + " " +
                        //      "R02:" + R02 + " " +
                        //      "[R03:" + R03 + " " +
                        //      "R04:" + R04 + " " +
                        //      "R05:" + R05 + " " +
                        //      "R06:" + R06 + " " +
                        //      "R07:" + R07 + " " +
                        //      "R08:" + R08 + " " +
                        //      "]R09:" + R09 + " " +
                        //      "R10:" + R10 + " " +
                        //      "R11:" + R11 + " " +
                        //      "R12:" + R12 + " " +
                        //      "R13:" + R13 + " " +
                        //      "R14:" + R14 + " " +
                        //      " 】");
                        if (it.Name.Contains("1"))
                            Console.WriteLine("【  " +
                            "R00:" + R00 + " " +
                            "待机:" + R01 + " " +
                            "自动运行:" + R02 + " " +
                            "[R03关:" + R03 + " " +
                            "R04开:" + R04 + " " +
                            "R05K:" + R05 + " " +
                            "R06G:" + R06 + " " +
                            "R07开:" + R07 + " " +
                            "R08关:" + R08 + " " +
                            "]故障:" + R09 + " " +
                            "原点中:" + R10 + " " +
                            "原点完成:" + R11 + " " +
                            "复位返回:" + R12 + " " +
                            "急停返回:" + R13 + " " +
                            " 】");
                    }
                    else if (it.DeviceType.Equals(DeviceTypes.FIREDOOR))
                    {
                    }
                    #endregion
#endif
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"websocket server publish error: {ex.Message}");
            }
            return root;
        }

        public void Refresh()
        {
            ExecutionDelay = IZUConfig.PublishMillionSeconds;
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
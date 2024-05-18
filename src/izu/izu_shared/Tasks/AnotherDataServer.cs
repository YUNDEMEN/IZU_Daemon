using IZU.Base;
using IZU.Interfaces;
using Newtonsoft.Json.Linq;
using NNanomsg.Protocols;
using System.Text;
using Wonder.Service.Framework;

namespace IZU.Tasks
{

    [Regist(RegisterTypes.LongRunningTask)]
    public class AnotherDataServer : LongRunningTask, IAnotherDataServer
    {
        private readonly IS7NetService _s7NetService;
        private PairSocket? nanoPairSocketServer;

        public AnotherDataServer(ILogger<AnotherDataServer> logger, IS7NetService s7NetService)
            : base(logger)
        {
            _s7NetService = s7NetService;
        }
        public override void Start()
        {
            ExecutionDelay = IZUConfig.IntervalNanoDataServer;
            nanoPairSocketServer = new PairSocket();
            nanoPairSocketServer.Bind($"tcp://{IZUConfig.ServerIP}:{IZUConfig.PortNanoDataServer}");
            base.Start();
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lastExecuteTime = DateTime.Now.ToString();
            nanoPairSocketServer.Receive();
            nanoPairSocketServer.Send(Encoding.GetEncoding("GB2312").GetBytes(WsPublishDevices().ToString()));
            await Task.CompletedTask;
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
                        currentObject["status"] = DeviceFactory.CheckAuodoorStatus(it) == null ? null : DeviceFactory.CheckAuodoorStatus(it)!.ToString();
                    var list = from x in it.Variables where x.ActionType.StartsWith('R') select new { k = x.ActionType.ToLower(), v = x.Value };
                    foreach (var item in list)
                    {
                        currentObject[item.k] = new JValue(item.v);
                    }
                    currentArray.Add(currentObject);

                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"websocket server publish error: {ex.Message}");
            }
            return root;
        }

    }

}

using IZU.Base;
using IZU.Interfaces;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Wonder.Service.Framework;

namespace IZU.Tasks
{

    [Regist(RegisterTypes.LongRunningTask | RegisterTypes.Singleton)]
    public class AnotherDataServer : LongRunningTask, IAnotherDataServer
    {
        private readonly IS7NetService _s7NetService;
        private DataServer? dataServer;

        public AnotherDataServer(ILogger<AnotherDataServer> logger, IS7NetService s7NetService)
            : base(logger)
        {
            _s7NetService = s7NetService;
        }
        public override void Start()
        {
            ExecutionDelay = IZUConfig.IntervalDataSend;
            if (!IPAddress.TryParse(IZUConfig.ServerIP, out IPAddress ipAddress))
            {
                _logger.LogWarning($"[data server] ip address is incorrect, {IZUConfig.ServerIP}");
                return;
            }
            dataServer = new DataServer(ipAddress, IZUConfig.PortDataSend);
            dataServer.OnSessionCreated += DataServer_OnSessionCreated;
            string error = string.Empty;
            try
            {
                dataServer.Start();
                _logger.LogInformation($"[data server] listening on {ipAddress}:{IZUConfig.PortDataSend}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

            base.Start();
        }
        List<DataSession> DataSessions = new List<DataSession>();
        private void DataServer_OnSessionCreated(object? sender, DataSession e)
        {
            DataSessions.Add(e);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lastExecuteTime = DateTime.Now.ToString();
            foreach (var item in DataSessions)
            {
                item.SendAsync(Encoding.GetEncoding("GB2312").GetBytes(WsPublishDevices().ToString()));
            }
            await Task.CompletedTask;
        }
        ConcurrentDictionary<string,string> doorLocks = new ConcurrentDictionary<string,string>();

        public void UpdateDoorLock(string name,string oht)
        {
            doorLocks[name] = oht;
        }
        public JObject WsPublishDevices()
        {
            JObject root = new();
            JArray currentArray = new();
            JObject currentObject = new();
            try
            {
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
                    currentObject["connection"] = it.Server.ConnectionStatus;
                    switch (it.DeviceType)
                    {
                        case DeviceTypes.NONE:
                            break;
                        case DeviceTypes.IZU:
                            currentObject["id"] = IZUConfig.izuId;
                            break;
                        case DeviceTypes.HID:
                            break;
                        case DeviceTypes.AUTODOOR:
                            doorLocks.TryGetValue(it.Name, out string oht);
                            currentObject["oht"] = oht;
                            currentObject["status"] = DeviceFactory.CheckAuodoorStatus(it) == null ? null : DeviceFactory.CheckAuodoorStatus(it)!.ToString();
                            break;
                        case DeviceTypes.FIREDOOR:
                            break;
                        default:
                            break;
                    }
                    var list = from x in it.Variables where x.ActionType.StartsWith('R')|| x.ActionType.StartsWith('T') select new { k = x.ActionType.ToLower(), v = x.Value };
                    foreach (var item in list)
                    {
                        currentObject[item.k] = new JValue(item.v);
                    }
                    currentArray.Add(currentObject);

                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"data pack error: {ex.Message}");
            }
            return root;
        }

        public string? GetDoorLock(string name)
        {
            doorLocks.TryGetValue(name, out string? oht);
            return oht;
        }
    }

}

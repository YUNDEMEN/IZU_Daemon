using IZU.Base;
using IZU.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wonder.Service.Framework;

namespace IZU.Tasks
{
    /// <summary>
    /// SOCKET MULTICAST CLIENT
    /// PORT 8331
    /// REMOTE CLIENT
    /// </summary>
    [Regist(RegisterTypes.LongRunningTask)]
    public class MulticastTask2 : LongRunningTask
    {
        int? f_oldState = 0;
        int? curr_state = 0;
        long open_time = 0;
        long opened_time = 0;
        string operation = string.Empty;
        JArray root;
        private CancellationTokenSource _cancelMulticastFullServer;
        private readonly IS7NetService _s7NetService;
        private WonderMulticast _multicastFullSender;

        public MulticastTask2(ILogger<MulticastTask2> logger, IS7NetService s7NetService)
            : base(logger)
        {
            _s7NetService = s7NetService;
        }
        public override void Start()
        {
            ExecutionDelay = IZUConfig.IntervalMulticastFullDataServer;
            root = new();
            _cancelMulticastFullServer = new CancellationTokenSource();
            _multicastFullSender = new WonderMulticast(IZUConfig.MulticastIP);
            _multicastFullSender.RunAsClient(IZUConfig.PortMulticastFullDataServer);
            base.Start();
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lastExecuteTime = DateTime.Now.ToString();
            root = WsPublishDevices2();
            await _multicastFullSender.SendToAsync("PUB_DEVICE_STATUS" + root.ToString(Formatting.None));
        }


        public JArray WsPublishDevices2()
        {
            JArray root = new();
            JArray currentArray = new();
            try
            {
                var msg = _s7NetService.GetAllDevices();
                if (msg.Count == 0) return root;
                string izuNo = msg.FirstOrDefault(p => p.DeviceType == DeviceTypes.IZU)!.Name;
                foreach (var it in msg)
                {
                    JObject currentObject = new();

                    currentObject = new() { ["name"] = it.Name };
                    currentObject["type"] = it.DeviceType.ToString();
                    currentObject["izuNo"] = izuNo;
                    if (it.DeviceType == DeviceTypes.AUTODOOR)
                        currentObject["doorState"] = DeviceFactory.CheckAuodoorStatus(it) == null ? null : DeviceFactory.CheckAuodoorStatus(it)!.ToString();
                    var list2 = from x in it.Variables where x.ActionType.StartsWith('R') select new { k = x.ActionType.ToLower(), v = x.Value };
                    foreach (var item in list2)
                    {
                        currentObject[item.k] = new JValue(item.v);
                    }

                    // root[it.Name.ToString().ToLower()] = currentObject;
                    root.Add(currentObject);
                }
            }
            catch (Exception ex)
            {
                //_logger.LogWarning($"websocket server publish error: {ex.Message}");
            }
            string ss = JsonConvert.SerializeObject(root);
            return root;
        }

    }
}

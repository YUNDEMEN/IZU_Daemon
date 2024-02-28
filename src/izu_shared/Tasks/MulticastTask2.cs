using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wonder;

namespace IZU.Tasks
{
    /// <summary>
    /// SOCKET MULTICAST CLIENT
    /// PORT 8331
    /// REMOTE CLIENT
    /// </summary>
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
                        currentObject["doorState"] = CheckAuodoorStatus(it) == null ? null : CheckAuodoorStatus(it)!.ToString();
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

        int? CheckAuodoorStatus(DeviceEntity deviceEntity)
        {
            var opening = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R05")?.Value;
            var opened = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R07")?.Value;
            var openState = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value;
            var closing = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R06")?.Value;
            var closed = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R08")?.Value;
            var closeState = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R03")?.Value;
            if (opening == null || opened == null || openState == null || closing == null || closed == null || closeState == null)
                return null;
            else
            {
                if (// 关到位
/* R06=false*/(bool)closing == false
/* R08=true*/&& (bool)closed
/* R03=true*/&& (bool)closeState
/* R05=false*/&& (bool)opening == false
/* R07=false*/&& (bool)opened == false
/* R04=false*/&& (bool)openState == false)
                    return 0;



                else if (// 正在关
/* R06=true*/ (bool)closing
/* R08=false*/&& (bool)closed == false
/* R03=false*/&& (bool)closeState == false
/* R05=false*/&& (bool)opening == false
/* R07=false*/&& (bool)opened == false
/* R04=false*/&& (bool)openState == false)
                    return 1;



                else if (// 正在开
/* R06=false*/(bool)closing == false
/* R08=false*/&& (bool)closed == false
/* R03=false*/&& (bool)closeState == false
/* R05=true*/&& (bool)opening
/* R07=false*/&& (bool)opened == false
/* R04=false*/&& (bool)openState == false)
                    return 2;



                else if (// 开到位
/* R06=false*/(bool)closing == false
/* R08=false*/&& (bool)closed == false
/* R03=false*/&& (bool)closeState == false
/* R05=false*/&& (bool)opening == false
/* R07=true*/&& (bool)opened
/* R04=true*/&& (bool)openState)
                    return 3;


                else
                    return null;
            }
        }
    }
}

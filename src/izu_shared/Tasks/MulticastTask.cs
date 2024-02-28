using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
using Wonder;

namespace IZU.Tasks
{
    /// <summary>
    /// SOCKET UDP CLIENT
    /// PORT 8131
    /// REMOTE CLIENT
    /// </summary>
    public class MulticastTask : LongRunningTask
    {
        int? f_oldState = 0;
        int? curr_state = 0;
        long open_time = 0;
        long opened_time = 0;
        string operation = string.Empty;
        CancellationTokenSource _cancelMulticastServer;
        WonderMulticast _multicastSender;
        private readonly IS7NetService _s7NetService;
        public MulticastTask(ILogger<MulticastTask> logger, IS7NetService s7NetService)
            : base(logger)
        {
            _s7NetService = s7NetService;
        }
        public override void Start()
        {
            ExecutionDelay = IZUConfig.IntervalMulticastServer;
            _cancelMulticastServer = new CancellationTokenSource();
            _multicastSender = new WonderMulticast(IZUConfig.MulticastIP);
            _multicastSender.RunAsClient(IZUConfig.PortMulticastServer);
            base.Start();
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var msg = _s7NetService.GetAllDevices();
            // 提取数据
            List<string> data = new();
            foreach (var it in msg)
            {
                if (it.DeviceType.Equals(DeviceTypes.AUTODOOR))
                {
                    f_oldState = curr_state;
                    curr_state = CheckAuodoorStatus(it);
                    // 状态流转： (0关到位 1正在关 2正在开 3开到位)
                    // 0->2 开门
                    // 2->3 开到位
                    // 3->1 关门
                    // 1->0 关到位
                    operation = $"{f_oldState}->{curr_state}";
                    switch (operation)
                    {
                        case "0->2":
                            open_time = TimestampService.Pinning("open");
                            Console.WriteLine("{0} 开门中({1})", it.Name, operation);
                            break;
                        case "2->3":
                            Console.WriteLine("{0} 开到位({1}), 耗时{2}ms", it.Name, operation, TimestampService.Difference("open"));
                            break;
                        case "3->1":
                            TimestampService.Pinning("close");
                            Console.WriteLine("{0} 关门中({1})", it.Name, operation);
                            break;
                        case "1->0":
                            Console.WriteLine("{0} 关到位({1}), 耗时{2}ms", it.Name, operation, TimestampService.Difference("close"));
                            break;
                    }

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
                    data.Add($"{it.Name}:{curr_state ?? 0}");
                }

            }
            // 消息格式： izu::[3({name1}:0;{name2}:0),4({name1}:0;{name2}:0)]
            string data_format = $"izu::[{(int)DeviceTypes.AUTODOOR}({string.Join(";", data)})]";

            await _multicastSender.SendToAsync(data_format);
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

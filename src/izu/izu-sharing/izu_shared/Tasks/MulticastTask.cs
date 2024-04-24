using IZU.Base;
using IZU.Interfaces;
using Wonder.Service.Framework;

namespace IZU.Tasks
{
    /// <summary>
    /// SOCKET UDP CLIENT
    /// PORT 8131
    /// REMOTE CLIENT
    /// </summary>    
    [Regist(RegisterTypes.LongRunningTask)]
    public class MulticastTask : LongRunningTask
    {
        int? f_oldState = 0;
        int? curr_state = 0;
        long open_time = 0;
        long opened_time = 0;
        string operation = string.Empty;
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
            _multicastSender = new WonderMulticast(IZUConfig.MulticastIP);
            _multicastSender.RunAsClient(IZUConfig.PortMulticastServer);
            base.Start();
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lastExecuteTime = DateTime.Now.ToString();
            var msg = _s7NetService.GetAllDevices();
            // 提取数据
            List<string> data = new();
            foreach (var it in msg)
            {
                if (it.DeviceType.Equals(DeviceTypes.AUTODOOR))
                {
                    f_oldState = curr_state;
                    curr_state = DeviceFactory.CheckAuodoorStatus(it);
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
    }
}

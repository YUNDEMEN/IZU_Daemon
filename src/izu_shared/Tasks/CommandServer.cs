using IZU.Base;
using IZU.DeviceFactories;
using IZU.Entities;
using IZU.Interfaces;
using NNanomsg.Protocols;
using Wonder;

namespace IZU.Tasks
{
    /// <summary>
    /// NANO REPLY SERVER
    /// PORT 8231
    /// REMOTE COMMAND SERVER
    /// </summary>
    public class CommandServer : LongRunningTask
    {
        private readonly IS7NetService _s7NetService;
        private CancellationTokenSource _cancelNanoCommandServer;
        private ReplySocket replySocket;
        private NNanomsg.NanomsgEndpoint NanomsgEndpoint_CommandServer;
        public CommandServer(ILogger<CommandServer> logger, IS7NetService s7NetService)
            : base(logger)
        {
            _s7NetService = s7NetService;
        }
        public override void Start()
        {
            _cancelNanoCommandServer = new CancellationTokenSource();
            replySocket = new ReplySocket();
            NanomsgEndpoint_CommandServer = replySocket.Bind($"tcp://{IZUConfig.ServerIP}:{IZUConfig.PortNanoCommandServer}");
            base.Start();
        }
        protected override bool NoDelay => true;
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = replySocket.Receive();
            string operation = System.Text.Encoding.UTF8.GetString(buffer);
            operation = await OperationFromOso(operation);
            replySocket.Send(System.Text.Encoding.UTF8.GetBytes(operation));
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
                switch (deviceOperation)
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

using IZU.Base;
using IZU.DeviceFactories;
using IZU.Interfaces;
using NNanomsg.Protocols;
using Wonder.Infrastructure;
using Wonder.Service.Framework;

namespace IZU.Tasks
{
    /// <summary>
    /// NANO REPLY SERVER
    /// PORT 8231
    /// REMOTE COMMAND SERVER
    /// </summary>
    [Regist(RegisterTypes.LongRunningTask)]
    public class CommandServer : LongRunningTask
    {
        private readonly IS7NetService _s7NetService;
        private readonly ISendDeviceToOhtTask _sendDeviceToOhtTask;
        private ReplySocket replySocket;
        public CommandServer(ILogger<CommandServer> logger, IS7NetService s7NetService, ISendDeviceToOhtTask sendDeviceToOhtTask)
            : base(logger)
        {          
            _s7NetService = s7NetService;
            _sendDeviceToOhtTask = sendDeviceToOhtTask;
        }
        public override void Start()
        {
            replySocket = new ReplySocket();
            replySocket.Bind($"tcp://{IZUConfig.ServerIP}:{IZUConfig.PortNanoCommandServer}");
            base.Start();
        }
        protected override bool NoDelay => true;
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lastExecuteTime = DateTime.Now.ToString();
            byte[] buffer = replySocket.Receive();
            string operation_feedback = System.Text.Encoding.UTF8.GetString(buffer);
            operation_feedback = await CommandFromOso(operation_feedback);
            replySocket.Send(System.Text.Encoding.UTF8.GetBytes(operation_feedback));
        }

        /// <summary>
        /// 开门指令
        /// </summary>
        /// <param name="data">接受指令( 格式：{deviceType}:{deviceName}:{commandName}:{commandArg} )</param>
        /// <returns></returns>
        public async Task<string> CommandFromOso(string data)
        {
            _logger.LogDebug($"command received :{data}");
            string[] oper_arr = data.Split('>');
            if (oper_arr.Length < 2)
                return "command not available";

            DeviceOperations deviceOperation = (DeviceOperations)oper_arr[0].ToInt32(0);
            string cmd_str = oper_arr[1];
            if (string.IsNullOrEmpty(cmd_str))
                return $"command [{deviceOperation}] not available";

            switch (deviceOperation)
            {
                default:
                case DeviceOperations.None:
                    return $"unkown command [{deviceOperation}]";
                case DeviceOperations.Open:
                case DeviceOperations.Close://1>3:ad01
                    {
                        string[] cmd_arr = cmd_str.Split(':');
                        if (cmd_arr.Length < 2)
                            return $"command [{deviceOperation}] not available";

                        DeviceTypes deviceType = (DeviceTypes)cmd_arr[0].ToInt32(0);
                        string deviceName = cmd_arr[1];
                        var device = _s7NetService.GetDevice(deviceName);
                        if (device == null)
                            return $"device {deviceName} is not existed(command [{deviceOperation}])";

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
                            return $"unknown device {deviceName}(command [{deviceOperation}])";

                        return deviceOperation switch
                        {
                            DeviceOperations.Open => await deviceObject!.OpenAsync(),
                            DeviceOperations.Close => await deviceObject!.CloseAsync(),
                            _ => $"no such command : {deviceOperation}",
                        };
                        //return await SwitchDeviceOperationAsync(deviceOperation, deviceObject);
                    }
                case DeviceOperations.StopTransmit:
                case DeviceOperations.Transmit://3>[{"oht":"ip:port","device":"ad02","point_stop":"","point_brake"},{"oht":"ip:port","device":"ad02","point_stop":"","point_brake"}...]
                    {
                        return await TransmitOperationAsync(deviceOperation, cmd_str);
                    }
            }
        }

        async Task<string> TransmitOperationAsync(DeviceOperations @operations, string data)
        {
            OhtInfo? oht;
            try
            {
                oht = Newtonsoft.Json.JsonConvert.DeserializeObject<OhtInfo>(data);
            }
            catch(Exception ex)
            {
                return $"fail to deserialize command. {ex.Message}";
            }
            if (oht == null)
                return await Task.FromResult($"empty command arguments  {@operations}");

            string err = string.Empty;
            switch (@operations)
            {
                case DeviceOperations.Transmit:
                    {
                        try
                        {
                            _sendDeviceToOhtTask.Add(oht);
                        }
                        catch (Exception ex)
                        {
                            err = $"command  {@operations} error: {ex.Message}";
                        }
                        return await Task.FromResult(err);
                    }
                case DeviceOperations.StopTransmit:
                    {
                        try
                        {
                            _sendDeviceToOhtTask.Delete(oht);
                        }
                        catch (Exception ex)
                        {
                            err = $"command  {@operations} error: {ex.Message}";
                        }
                        return await Task.FromResult(err);
                    }
                default:
                    return $"no such command : {@operations}";
            };
        }

    }
}

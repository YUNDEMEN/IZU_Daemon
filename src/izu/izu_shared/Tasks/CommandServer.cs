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
        //private readonly ISendDeviceToOhtTask _sendDeviceToOhtTask;
        private ReplySocket replySocket;
        public CommandServer(ILogger<CommandServer> logger, IS7NetService s7NetService)//, ISendDeviceToOhtTask sendDeviceToOhtTask)
            : base(logger)
        {          
            _s7NetService = s7NetService;
            HeartsBeating.New(5000, HeartbeatingAction!);
            //_sendDeviceToOhtTask = sendDeviceToOhtTask;
        }
        void HeartbeatingAction()
        {
            if (IsFaulted)
            {
                Start();
            }
        }
        public override void Start()
        {
            replySocket = new ReplySocket();
            replySocket.Options.SendTimeout = TimeSpan.FromSeconds(2);
            replySocket.Bind($"tcp://{IZUConfig.ServerIP}:{IZUConfig.PortNanoCommandServer}");
            base.Start();
        }
        protected override bool NoDelay => true;
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lastExecuteTime = DateTime.Now.ToString();
            byte[] buffer = replySocket.Receive();
            if(buffer != null)
            {
                string operation_feedback = System.Text.Encoding.UTF8.GetString(buffer);
                operation_feedback = await CommandHandler(operation_feedback);
                replySocket.Send(System.Text.Encoding.UTF8.GetBytes(operation_feedback));
            }
            else
            {
                _logger.LogWarning($"CommandServer Received null message");
                replySocket.Send(System.Text.Encoding.UTF8.GetBytes("NULL"));
            }
        }
        IDictionary<string, string> ToKeyed(string argstr)
        {
            IDictionary<string, string> kd = new Dictionary<string, string>();
            var args = argstr.Split(';');
            foreach (var item in args)
            {
                int idx = item.IndexOf(':');
                if (idx < 0) continue;
                string k = item[..idx];
                string v = item[(idx + 1)..];
                kd[k] = v;
            }
            return kd;
        }
        /// <summary>
        /// 开门指令
        /// </summary>
        /// <param name="data">接受指令( 格式：operation>>>arg1:value1;arg2:value2 )</param>
        /// <returns></returns>
        public async Task<string> CommandHandler(string data)
        {
            _logger.LogDebug($"command received :{data}");
            int arrowIndex = data.IndexOf(">>>");
            if (arrowIndex < 0)
            {
                _logger.LogWarning($"command format is incorrect! {data}");
                return "NULL";
            }
            string operCode = data[..arrowIndex];
            if (string.IsNullOrEmpty(operCode))
            {
                _logger.LogWarning($"command format is incorrect! {data}");
                return "NULL";
            }

            string argstr = data[(arrowIndex + 3)..];
            IDictionary<string, string> args = ToKeyed(argstr);
            switch (operCode)
            {
                case "Open"://Open>>>oht:0;type:3;door:ad01
                case "Close"://Close>>>oht:0;type:3;door:ad01
                    {
                        args.TryGetValue("oht", out string? oht);
                        args.TryGetValue("type", out string? type);
                        args.TryGetValue("door", out string? name);

                        if (string.IsNullOrEmpty(oht) || string.IsNullOrEmpty(name))
                        {
                            _logger.LogWarning($"oht={oht} or device={name} is incorrect");
                            return "NULL";
                        }
                        DeviceTypes deviceType = (DeviceTypes)type.ToInt32(3);
                        var device = _s7NetService.GetDevice(name);
                        if (device == null)
                        {
                            _logger.LogWarning($"device {name} is not existed (command [{data}])");
                            return "NULL";
                        }

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
                        {
                            _logger.LogWarning($"unknown device {name} (command [{data}])");
                            return "NULL";
                        }

                        string result= operCode switch
                        {
                            "Open" => await deviceObject!.OpenAsync(),
                            "Close" => await deviceObject!.CloseAsync(),
                            _ => $"no such command : {operCode}",
                        };
                        if (!string.IsNullOrEmpty(result))
                        {
                            _logger.LogWarning(result);
                        }
                        return $"{deviceObject.GetStatus() ?? 0}";
                    }
                default:
                    {
                        _logger.LogWarning($"unkown command [{operCode}]");
                        return "NULL";
                    }
            }
        }

        /*
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
                            //_sendDeviceToOhtTask.Add(oht);
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
                            //_sendDeviceToOhtTask.Delete(oht);
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
        */
    }
}

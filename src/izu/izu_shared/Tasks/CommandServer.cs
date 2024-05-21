using IZU.Base;
using IZU.DeviceFactories;
using IZU.Interfaces;
using NNanomsg.Protocols;
using System.Collections.Concurrent;
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
        private ReplySocket replySocket;
        public CommandServer(ILogger<CommandServer> logger, IS7NetService s7NetService)
            : base(logger)
        {
            _s7NetService = s7NetService;
            HeartsBeating.New(5000, HeartbeatingAction!);
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
            if (buffer != null)
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
        /// 开门/关门指令
        /// </summary>
        /// <param name="data">接受指令( 格式：operation>>>arg1:value1;arg2:value2 )</param>
        /// <returns></returns>
        public async Task<string> CommandHandler(string data)
        {
            _logger.LogInformation($"command received :{data}");
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
                case "Open"://Open>>>oht:0;door:ad01
                case "Close"://Close>>>oht:0;type:3;door:ad01
                    {
                        args.TryGetValue("oht", out string? oht);
                        args.TryGetValue("door", out string? name);

                        if (string.IsNullOrEmpty(oht) || string.IsNullOrEmpty(name))
                        {
                            _logger.LogWarning($"oht={oht} or device={name} is incorrect");
                            return "NULL";
                        }

                        var device = _s7NetService.GetDevice(name);
                        if (device == null)
                        {
                            _logger.LogWarning($"device {name} is not existed (command [{data}])");
                            return "NULL";
                        }

                        IOperatable? deviceObject = null;
                        switch (device.DeviceType)
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

                        string result = string.Empty;
                        switch (operCode)
                        {
                            case "Open":
                                {
                                    result = await deviceObject!.OpenAsync();
                                    if (string.IsNullOrEmpty(result))
                                        Tasks.DoorActions.Add(name, oht.ToInt32(0));
                                    break;
                                }
                            case "Close":
                                {
                                    result = await deviceObject!.CloseAsync();
                                    break;
                                }
                        }
                        if (!string.IsNullOrEmpty(result))
                        {
                            _logger.LogWarning(result);
                        }
                        int status = deviceObject.GetStatus() ?? -1;
                        //Console.WriteLine($"[{DateTime.Now:HH:mm:ss:fff}]  send {device.Name} status={status} to oht{oht} ");
                        return $"{ToName(status)}";
                    }
                case "State"://State>>>door:ad01
                    {
                        args.TryGetValue("door", out string? name);
                        if (string.IsNullOrEmpty(name))
                        {
                            _logger.LogWarning($"device={name} is incorrect");
                            return "NULL";
                        }
                        var device = _s7NetService.GetDevice(name);
                        if (device == null)
                        {
                            _logger.LogWarning($"device {name} is not existed (command [{data}])");
                            return "NULL";
                        }
                        int status = DeviceFactory.CheckAuodoorStatus(device) ?? -1;
                        return ToName(status);
                    }
                default:
                    {
                        _logger.LogWarning($"unkown command [{operCode}]");
                        return "NULL";
                    }
            }
        }
        string ToName(int status)
        {
            return status switch
            {
                3 => "Open",
                2 => "Opening",
                1 => "Closing",
                0 => "Close",
                _ => "NULL",
            };
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


    public static class DoorActions
    {
        static ConcurrentDictionary<string, List<int>> openedoors;
        static DoorActions()
        {
            openedoors = new ConcurrentDictionary<string, List<int>>();
        }
        public static void Add(string key, int @value)
        {
            if (!openedoors.ContainsKey(key))
                openedoors[key] = new List<int>(new int[] { @value });

            if (!openedoors[key].Contains(@value))
            {
                openedoors[key].Add(@value);
            }
        }
        public static void Remove(string key, int @value)
        {
            if (!openedoors.ContainsKey(key)) return;
            openedoors[key].Remove(@value);
        }

        public static bool CanClose(string name)
        {
            if (!openedoors.ContainsKey(name))
                return true;

            if (openedoors[name].Count == 0)
                return true;

            return false;
        }

        public static string Ohts(string name)
        {
            if (!openedoors.ContainsKey(name))
                return string.Empty;
            if (openedoors[name].Count == 0) 
                return string.Empty;

            return string.Join(", ", openedoors[name]);
        }
    }
}

using IZU.Base;
using IZU.DeviceFactories;
using IZU.Interfaces;
using NNanomsg;
using NNanomsg.Protocols;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Policy;
using System.Text;
using Wonder.Service.Framework;

namespace IZU.Tasks
{
    [Regist(RegisterTypes.LongRunningTask)]
    public class CommandServer : LongRunningTask
    {
        private readonly IAnotherDataServer _anotherDataServer;
        private readonly IS7NetService _s7NetService;
        //private ReplySocket? replySocket;
        private int serverSocket;
        private BlockingCollection<QueuedMsg> requestQueue;
        public CommandServer(ILogger<CommandServer> logger, IS7NetService s7NetService, IAnotherDataServer anotherDataServer)
            : base(logger)
        {
            _anotherDataServer = anotherDataServer;
            _s7NetService = s7NetService;
            HeartsBeating.New(5000, HeartbeatingAction!);
        }
        public override void Start()
        {
            var ds = _s7NetService.GetAllDeviceNames();
            DoorMan.Init(ds);
            serverSocket = NN.Socket(Domain.SP_RAW, Protocol.REP);
            NN.Bind(serverSocket, $"tcp://{IZUConfig.ServerIP}:{IZUConfig.PortNanoCommandServer}");
            _logger.LogInformation("CommandServer start ... ");
            //NN.SetSockOpt(serverSocket, SocketOption.SNDBUF, 1024 * 1024);  // 设置发送缓冲区为1MB
            requestQueue = new BlockingCollection<QueuedMsg>();
            base.Start();
        }
        protected override bool NoDelay => true;
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lastExecuteTime = DateTime.Now.ToString();
            _ = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var receive = requestQueue.Take();
                        if (receive != null && receive.Bytes.Count() > 0)
                        {
                            string operation_feedback = System.Text.Encoding.UTF8.GetString(receive.Bytes);
                            _logger.LogInformation($"OSO Receive: {operation_feedback}");
                            operation_feedback = await CommandHandler(operation_feedback);
                            _logger.LogInformation($"command executed :{operation_feedback}");
                            unsafe
                            {
                                NNPollSend(serverSocket, operation_feedback, receive.ControlPointer);
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"CommandServer Received null message");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("CommandServer handle message with exception {0}", ex.ToString());
                    }
                }
            }, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var sockets = new[] { serverSocket };
                    var pollResults = NN.Poll(sockets, TimeSpan.FromSeconds(-1));
                    if (pollResults[0] == 1)
                    {
                        byte[] receive = new byte[1024];
                        unsafe
                        {
                            void* control = null;
                            var result = NN.Recv_W(serverSocket, out receive, &control, SendRecvFlags.NONE);
                            if (result > 0)
                            {
                                requestQueue.Add(new QueuedMsg
                                {
                                    Bytes = receive.ToArray(),
                                    ControlPointer = control
                                });
                            }
                        }

                    }
                    else
                        Thread.Sleep(1);

                }
                catch (Exception ae)
                {
                    _logger.LogError("CommandServer receive message with exception {0}", ae.ToString());
                }
            }
        }
        private unsafe void NNPollSend(int serverSocket, string msg, void* control)
        {
            var ack = NN.Send_W(serverSocket, Encoding.UTF8.GetBytes(msg), &control, SendRecvFlags.DONTWAIT);
            if (ack < 0)
            {
                int errorCode = NN.Errno();
                string errorMsg = NN.StrError(errorCode);
                _logger.LogDebug($"Reply OHT failed, error code: {errorCode}, error message: {errorMsg}");
            }
            _logger.LogDebug($"Already reply OHT: {msg} for message: {msg}, ack: {ack}");
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
        /// 分发并执行接收到的指令
        /// </summary>
        /// <param name="data"> 
        /// <code>
        /// 接受指令( 格式：operation>>>arg1:value1;arg2:value2 ) 
        /// operation: 指令名称
        /// >>>: 分隔符
        /// arg1:value1;arg2:value2: 参数
        /// 参数体使用分号(;)间隔, 参数名和参数值使用冒号(:)间隔
        /// </code>
        /// </param>
        /// <returns></returns>
        async Task<string> CommandHandler(string data)
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
            return operCode switch
            {
                "Delay" => await Delay(),
                "Online" => Online(args),
                "Info" => Info(),
                "Init" => await Init(args),
                "State" => State(args),
                "Error" => Error(args),
                "Open" => await OpenAsync(args),
                "Close" => await CloseAsync(args),
                "Stop" => await StopAsync(args),
                "Reset" => await ResetAsync(args),
                "JogOpen" => JogOpen(args),
                "JogClose" => JogClose(args),
                "SetEnable" => await SetEnableAsync(args),
                "SetJogSpeed" => await SetJogSpeedAsync(args),
                "SetAutoSpeed" => await SetAutoSpeedAsync(args),
                "SetPositionOpen" => await SetOpenedPositionAsync(args),
                "SetPositionClose" => await SetClosedPositionAsync(args),
                "Release" => ReleaseDoor(args),
                _ => ToNull($"unkown command [{operCode}][{data}]")
            };
        }

        async Task<string> Delay()
        {
            _logger.LogInformation("Begin Delay Command");
            await Task.Delay(4000);
            _logger.LogInformation("Delayed for 4 seconds");
            return string.Empty;
        }

        #region Switch Functions
        string ReleaseDoor(IDictionary<string, string> args)
        {
            args.TryGetValue("door", out string? name);
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"[RELEASE] device {name} is missing");
                return "NULL";
            }
            DoorMan.Release(name);
            return $"{name} released. current states: {string.Join("   ", DoorMan.GetEnumerable())}";
        }
        string Online(IDictionary<string, string> args)
        {
            args.TryGetValue("ip", out string? ip);
            if (string.IsNullOrEmpty(ip))
            {
                _logger.LogWarning($"device name is missing");
                return "NULL";
            }
            if (!IPAddress.TryParse(ip, out IPAddress? address))
            {
                _logger.LogWarning($"address is incorrect");
                return "NULL";
            }

            if (args.TryGetValue("port", out string? _port) && int.TryParse(_port, out int port))
            {
            }

            _anotherDataServer.Start();
            return string.Empty;
        }
        string Info()
        {
            var devices = _s7NetService.GetAllDeviceNames();
            return string.Join(",", devices);
        }

        async Task<string> Init(IDictionary<string, string> args)
        {
            args.TryGetValue("door", out string? name);

            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"device name is missing");
                return "NULL";
            }
            IAutoDoor? door = FindAutoDoor(name);
            if (door == null)
            {
                _logger.LogWarning($"device {name} is not existed");
                return "NULL";
            }

            return await door.InitialAsync();
        }

        async Task<string> OpenAsync(IDictionary<string, string> args)
        {
            args.TryGetValue("oht", out string? oht);
            if (string.IsNullOrEmpty(oht))
                oht = string.Empty;
            args.TryGetValue("door", out string? name);

            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"[{oht}][OPENDOOR] device name is missing");
                return "NULL";
            }

            IAutoDoor? door = FindAutoDoor(name);
            if (door == null)
            {
                _logger.LogWarning($"[{oht}][OPENDOOR] device {name} is not existed");
                return "NULL";
            }

            int status = -1;
            string result = await door.OpenAsync();
            if (!string.IsNullOrEmpty(result))
            {
                _logger.LogWarning($"[{oht}][OPENDOOR] " + result);
            }

            //尝试将当前天车与门锁定
            //一旦锁定, 在门关闭前无法再次锁定
            //只有门关闭后, 才释放
            bool locked = DoorMan.TryLock(name, oht);
            if (locked)
            {//锁定后, 下次不能锁定
                _logger.LogInformation($"[{oht}][OPENDOOR] {name} lock [{DoorMan.GetLock(name)}]");
            }

            //检查门是否被当前天车占用
            if (DoorMan.CheckLock(name, oht))
            {//如果是, 则返回门状态
                status = door.GetStatus() ?? -1;
            }
            else//如果否, 则返回关闭状态
                status = 0;
            _anotherDataServer.UpdateDoorLock(name, DoorMan.GetLock(name));
            await Task.Delay(50);
            return $"{ToName(status)}";
        }
        async Task<string> CloseAsync(IDictionary<string, string> args)
        {
            args.TryGetValue("oht", out string? oht);
            args.TryGetValue("door", out string? name);

            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"[{oht}][CLOSEDOOR] device name is missing");
                return "NULL";
            }

            IAutoDoor? door = FindAutoDoor(name);
            if (door == null)
            {
                _logger.LogWarning($"[{oht}][CLOSEDOOR]:device {name} is not existed");
                return "NULL";
            }
            string result = string.Empty;
            result = await door.CloseAsync();
            if (!string.IsNullOrEmpty(result))
            {
                _logger.LogWarning($"[{oht}][CLOSEDOOR] " + result);
            }
            else
            {
                ReleaseDoor(name, oht);
            }
            int status = door.GetStatus() ?? -1;
            return $"{ToName(status)}";
        }

        void ReleaseDoor(string doorName, string oht)
        {
            Task ret = Task.Factory.StartNew(async () =>
            {
                if (string.IsNullOrEmpty(DoorMan.GetLock(doorName))) return;
                IAutoDoor? door = FindAutoDoor(doorName);
                if (door == null) return;
                while (true)
                {
                    int status = door.GetStatus(_logger) ?? -1;
                    if (status == 1 || status == 0)
                    {
                        //门正在关或者关到位后, 自动解锁
                        DoorMan.Release(doorName);
                        _logger.LogInformation($"[{oht}]:released {doorName} (status={status})");
                        break;
                    }
                    else
                    {
                        _logger.LogInformation($"[{oht}]:failed to release {doorName} (status={status})");
                    }
                    await Task.Delay(200);
                }
                _anotherDataServer.UpdateDoorLock(doorName, DoorMan.GetLock(doorName));
            });
        }

        string State(IDictionary<string, string> args)
        {
            return string.Join("   ", DoorMan.GetEnumerable());
        }
        async Task<string> ResetAsync(IDictionary<string, string> args)
        {
            args.TryGetValue("door", out string? name);
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"device name is missing");
                return "NULL";
            }
            IAutoDoor? door = FindAutoDoor(name);
            if (door == null)
            {
                _logger.LogWarning($"device {name} is not existed");
                return "NULL";
            }
            var ds = _s7NetService.GetAllDeviceNames();
            DoorMan.Init(ds);
            return await door.ResetAsync(true);
        }
        async Task<string> StopAsync(IDictionary<string, string> args)
        {
            args.TryGetValue("door", out string? name);
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"device name is missing");
                return "NULL";
            }
            var device = _s7NetService.GetDevice(name);
            if (device == null)
            {
                _logger.LogWarning($"device {name} is not existed");
                return "NULL";
            }

            IAutoDoor? autoDoor = null;
            switch (device.DeviceType)
            {
                case DeviceTypes.NONE:
                    break;
                case DeviceTypes.IZU:
                    break;
                case DeviceTypes.HID:
                    break;
                case DeviceTypes.AUTODOOR:
                    autoDoor = new AutoDoor(device);
                    break;
                case DeviceTypes.FIREDOOR:
                    break;
            }
            if (autoDoor == null)
            {
                _logger.LogWarning($"unknown device {name}");
                return "NULL";
            }

            return await autoDoor.StopAsync();
        }
        string Error(IDictionary<string, string> args)
        {
            args.TryGetValue("door", out string? name);
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"device name is missing");
                return "NULL";
            }
            var device = _s7NetService.GetDevice(name);
            if (device == null)
            {
                _logger.LogWarning($"device {name} is not existed");
                return "NULL";
            }
            var v = device.Variables.FirstOrDefault(t => t.ActionType == "R09");
            int.TryParse($"{v?.Value}", out int code);
            return Convert.ToString(code, 2);
        }
        string JogOpen(IDictionary<string, string> args)
        {
            args.TryGetValue("door", out string? name);
            args.TryGetValue("jog", out string? jog);
            if (!bool.TryParse(jog, out bool jogFlag))
            {
                _logger.LogWarning($"jog should be true/false");
                return "NULL";
            }

            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"device name is missing");
                return "NULL";
            }
            var device = _s7NetService.GetDevice(name);
            if (device == null)
            {
                _logger.LogWarning($"device {name} is not existed");
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
            deviceObject.OpenManualAsync(jogFlag);
            return "";
        }
        string JogClose(IDictionary<string, string> args)
        {
            args.TryGetValue("door", out string? name);
            args.TryGetValue("jog", out string? jog);
            if (!bool.TryParse(jog, out bool jogFlag))
            {
                _logger.LogWarning($"jog should be true/false");
                return "NULL";
            }

            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"device name is missing");
                return "NULL";
            }
            var device = _s7NetService.GetDevice(name);
            if (device == null)
            {
                _logger.LogWarning($"device {name} is not existed");
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
            deviceObject.CloseManualAsync(jogFlag);
            return "";
        }
        async Task<string> SetEnableAsync(IDictionary<string, string> args)
        {
            args.TryGetValue("door", out string? name);
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"device name is missing");
                return "NULL";
            }
            args.TryGetValue("value", out string? @value);
            if (string.IsNullOrEmpty(@value))
            {
                _logger.LogWarning($"value is missing");
                return "NULL";
            }
            if (!bool.TryParse(value, out bool enabled))
            {
                _logger.LogWarning($"value should be true/false: {value}");
                return "NULL";
            }

            IAutoDoor? autoDoor = FindAutoDoor(name);
            if (autoDoor == null)
            {
                _logger.LogWarning($"unknown device {name}");
                return "NULL";
            }
            return await autoDoor.Enable(enabled);
        }
        async Task<string> SetJogSpeedAsync(IDictionary<string, string> args)
        {
            args.TryGetValue("door", out string? name);
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"device name is missing");
                return "NULL";
            }
            args.TryGetValue("value", out string? @value);
            if (string.IsNullOrEmpty(@value))
            {
                _logger.LogWarning($"value is missing");
                return "NULL";
            }
            if (!short.TryParse(value, out short jogspeed))
            {
                _logger.LogWarning($"value should be Int16/short: {value}");
                return "NULL";
            }

            IAutoDoor? autoDoor = FindAutoDoor(name);
            if (autoDoor == null)
            {
                _logger.LogWarning($"unknown device {name}");
                return "NULL";
            }
            return await autoDoor.JogSpeed(jogspeed);
        }
        async Task<string> SetAutoSpeedAsync(IDictionary<string, string> args)
        {
            args.TryGetValue("door", out string? name);
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"device name is missing");
                return "NULL";
            }
            args.TryGetValue("value", out string? @value);
            if (string.IsNullOrEmpty(@value))
            {
                _logger.LogWarning($"value is missing");
                return "NULL";
            }
            if (!short.TryParse(value, out short jogspeed))
            {
                _logger.LogWarning($"value should be Int16/short: {value}");
                return "NULL";
            }

            IAutoDoor? autoDoor = FindAutoDoor(name);
            if (autoDoor == null)
            {
                _logger.LogWarning($"unknown device {name}");
                return "NULL";
            }
            return await autoDoor.AutoSpeed(jogspeed);
        }
        async Task<string> SetOpenedPositionAsync(IDictionary<string, string> args)
        {
            args.TryGetValue("door", out string? name);
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"device name is missing");
                return "NULL";
            }
            args.TryGetValue("value", out string? @value);
            if (string.IsNullOrEmpty(@value))
            {
                _logger.LogWarning($"value is missing");
                return "NULL";
            }
            if (!short.TryParse(value, out short position))
            {
                _logger.LogWarning($"value should be Int16/short: {value}");
                return "NULL";
            }

            IAutoDoor? autoDoor = FindAutoDoor(name);
            if (autoDoor == null)
            {
                _logger.LogWarning($"unknown device {name}");
                return "NULL";
            }
            return await autoDoor.OpenedPosition(position);
        }
        async Task<string> SetClosedPositionAsync(IDictionary<string, string> args)
        {
            args.TryGetValue("door", out string? name);
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($"device name is missing");
                return "NULL";
            }
            args.TryGetValue("value", out string? @value);
            if (string.IsNullOrEmpty(@value))
            {
                _logger.LogWarning($"value is missing");
                return "NULL";
            }
            if (!short.TryParse(value, out short position))
            {
                _logger.LogWarning($"value should be Int16/short: {value}");
                return "NULL";
            }

            IAutoDoor? autoDoor = FindAutoDoor(name);
            if (autoDoor == null)
            {
                _logger.LogWarning($"unknown device {name}");
                return "NULL";
            }
            return await autoDoor.ClosedPosition(position);
        }

        #endregion

        string ToName(int status)
        {
            return status switch
            {
                3 => "Open",
                2 => "Opening",
                1 => "Closing",
                0 => "Close",
                _ => ToNull($"status={status}"),
            };
        }
        string ToNull(string log)
        {
            _logger.LogWarning($"black way: {log}");
            return "NULL";
        }
        IAutoDoor? FindAutoDoor(string name)
        {
            var device = _s7NetService.GetDevice(name);
            if (device == null)
            {
                _logger.LogWarning($"device {name} is not existed");
                return null;
            }
            IAutoDoor? autoDoor = null;
            switch (device.DeviceType)
            {
                case DeviceTypes.NONE:
                    break;
                case DeviceTypes.IZU:
                    break;
                case DeviceTypes.HID:
                    break;
                case DeviceTypes.AUTODOOR:
                    autoDoor = new AutoDoor(device);
                    break;
                case DeviceTypes.FIREDOOR:
                    break;
            }
            if (autoDoor == null)
            {
                _logger.LogWarning($"unknown device {name}");
                return null;
            }
            return autoDoor;
        }

         public async Task<string> ReleaseAutoDoor(string doorName)
        {
            if (string.IsNullOrEmpty(doorName))
            {
                return $"device {doorName} is missing";
            }
            IAutoDoor? door = FindAutoDoor(doorName);
            if (door == null)
                return $"Auto door is not existed";
            if (string.IsNullOrEmpty(DoorMan.GetLock(doorName))) 
                return $"Auto door is not locked";

            int status = door.GetStatus(_logger) ?? -1;
            if ( status == 0)
            {
                //门关到位则进行释放
                DoorMan.Release(doorName);
                _anotherDataServer.UpdateDoorLock(doorName, DoorMan.GetLock(doorName));
                _logger.LogInformation($"{doorName}released (status={status})");
            }
            else
            {
                return($"{doorName}failed to release (status={status})");
            }
            return string.Empty;
        }

        void HeartbeatingAction()
        {
            if (IsFaulted)
            {
                Start();
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

    public static class DoorMan
    {
        static ConcurrentDictionary<string, string> _doorState = new ConcurrentDictionary<string, string>();
        public static void Init(List<string> doors)
        {
            foreach (var dn in doors)
            {
                _doorState[dn] = string.Empty;
            }
        }

        public static bool CheckLock(string doorName, string oht)
        {
            return GetLock(doorName) == oht;
        }

        public static bool TryLock(string doorName, string oht)
        {
            if (string.IsNullOrEmpty(oht)) return false;
            string lockedOht = GetLock(doorName);
            if (string.IsNullOrEmpty(lockedOht))
            {
                _doorState[doorName] = oht;
                return true;
            }
            else return false;
        }
        public static string GetLock(string doorName)
        {
            _doorState.TryGetValue(doorName, out string lockedOHT);
            return lockedOHT;
        }

        public static IEnumerable<string> GetEnumerable()
        {
            foreach (var item in _doorState)
            {
                yield return $"{item.Key}={item.Value}";
            }
        }
        public static void Release(string doorName)
        {
            _doorState[doorName] = string.Empty;
        }
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

    public unsafe class QueuedMsg
    {
        public byte[] Bytes { get; set; }

        public void* ControlPointer { get; set; }
    }
}

using IZU.Interfaces;
using Microsoft.AspNetCore.Hosting.Server;
using S7.Net;
using S7.Net.Types;
using System.Net;
using System.ServiceProcess;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace IZU.Base
{
    public abstract class PlcBase
    {
        private IDictionary<int, VariableEntity> _hashes = new Dictionary<int, VariableEntity>();
        protected IDictionary<string, string> Addresses { get; set; }
        protected Plc Server { get; set; }
        protected DeviceTypes DeviceType { get; set; }
        protected readonly string DeviceName;
        protected S7.Net.Types.DataItem _r_heartbeat_address;
        protected S7.Net.Types.DataItem _w_sendback_address;
        protected S7.Net.Types.DataItem _w_online_address;
        protected S7.Net.Types.DataItem _w_onlinestate_address;


        protected Task _serverReadTask;
        protected TaskServiceStatus _serviceStatus;
        protected Task _serverHeartbeatTask;

        private bool _stopServer;
        protected bool StopServer
        {
            get { return _stopServer; }
            private set
            {
                _stopServer = value;
                if (value)
                {
                    _serverReadTask.ContinueWith((task) => {
                        //_logger.LogInformation($"{DeviceName} device connection server has stopped");
                    }).Wait();
                }
                else
                {
                    _serviceStatus = TaskServiceStatus.Connecting;
                    _serverHeartbeatTask.Start();
                    _serverReadTask.Start();
                    //_logger.LogInformation($"{DeviceName} device connection server has started");
                }
            }
        }

        protected List<DataItem> _dataItems;
        protected readonly IPAddress? _serverIP;

        protected PlcBase(string ipAddress, string name, DeviceTypes deviceType, IDictionary<string, string> addresses)
        {
            if (!IPAddress.TryParse(ipAddress, out _serverIP))
                throw new FormatException($"{DeviceName} server IP address format is Incorrect: {ipAddress}");
            DeviceName = name;
            DeviceType = deviceType;
            Addresses = addresses;
            _dataItems = new List<DataItem>();

            Init(ipAddress);
            InitialAddresses();
        }
        protected virtual void Init(string ip)
        {
            Server = new Plc(CpuType.S71500, ip, 0, 0);
            Server.ReadTimeout = 3000;
        }
        protected virtual async Task ConnectAsync()
        {
            await Server.OpenAsync();
        }
        protected virtual async Task BeginRead(int millionSeconds)
        {
            _serverReadTask = new Task(async () =>
            {
                while (true)
                {
                    if (_stopServer) break;
                    if (_serviceStatus == TaskServiceStatus.Connected)
                    {
                        try
                        {
                            var grouped = _dataItems.GroupBy(t => t.DB);
                            foreach (var item in grouped)
                            {
                                _ = await Server.ReadMultipleVarsAsync(item.ToList());
                                _dataItems.ForEach(t => _hashes[t.GetHashCode()].Value = t.Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            _serviceStatus = TaskServiceStatus.Connecting;
                            //_logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", DeviceName, _serverIP?.ToString(), ex.Message);
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(millionSeconds));
                }
            }, TaskCreationOptions.LongRunning);
        }
        protected virtual async Task WriteBack()
        {
            //联机状态写回
            _w_online_address.Value = true;
            await Server.WriteAsync(_w_online_address);
            //联机正常状态写回
            _w_onlinestate_address.Value = true;
            await Server.WriteAsync(_w_onlinestate_address);
            //心跳写回
            _w_sendback_address.Value = ((bool)_w_sendback_address!.Value!) != true;
            await Server.WriteAsync(_w_sendback_address);
        }

        protected virtual async Task<object?> GetHeartbeatState()
        {
            //最后读取心跳
            return await Server.ReadAsync(
                _r_heartbeat_address.DataType,
                _r_heartbeat_address.DB,
                _r_heartbeat_address.StartByteAdr,
                _r_heartbeat_address.VarType,
                _r_heartbeat_address.Count);
        }
        protected virtual void InitialAddresses()
        {
            switch (DeviceType)
            {
                case DeviceTypes.NONE:
                    break;
                case DeviceTypes.IZU:
                    _r_heartbeat_address = S7.Net.Types.DataItem.FromAddress(Addresses["R01"]);
                    _w_sendback_address = S7.Net.Types.DataItem.FromAddress(Addresses["W01"]);
                    _w_sendback_address.Value = false;
                    _w_online_address = S7.Net.Types.DataItem.FromAddress(Addresses["W02"]);
                    _w_online_address.Value = false;
                    _w_onlinestate_address = S7.Net.Types.DataItem.FromAddress(Addresses["W03"]);
                    _w_onlinestate_address.Value = false;
                    break;
                case DeviceTypes.HID:
                    _r_heartbeat_address = S7.Net.Types.DataItem.FromAddress(Addresses["R01"]);
                    break;
                case DeviceTypes.AUTODOOR:
                    _r_heartbeat_address = S7.Net.Types.DataItem.FromAddress(Addresses["R01"]);
                    break;
                case DeviceTypes.FIREDOOR:
                    _r_heartbeat_address = S7.Net.Types.DataItem.FromAddress(Addresses["R01"]);
                    break;
                default:
                    break;
            }
        }

        protected virtual void Stop()
        {
            StopServer = true;
            Server.Close();
            _hashes.Clear();
            _dataItems.Clear();
            _serverReadTask.Dispose();
            _serverHeartbeatTask.Dispose();
            _serviceStatus = TaskServiceStatus.NotStarted;
        }
    }
    public class PlcServer2 : PlcBase, IPlcServer
    {
        private readonly ILogger<PlcServer> _logger;
        private int _heart_beat_interval_millionsec = 1000;
        private int _refresh_interval_millionsec = 100;

        private TaskServiceStatus _serviceStatus;

        private IDictionary<int, VariableEntity> _hashes = new Dictionary<int, VariableEntity>();

        private bool _stopServer;
        internal bool StopServer
        {
            get { return _stopServer; }
            private set
            {
                _stopServer = value;
                if (value)
                {
                    _serverReadTask.ContinueWith((task) => {
                        _logger.LogInformation($"{DeviceName} device connection server has stopped");
                    }).Wait();
                }
                else
                {
                    _serviceStatus = TaskServiceStatus.Connecting;
                    _serverHeartbeatTask.Start();
                    _serverReadTask.Start();
                    _logger.LogInformation($"{DeviceName} device connection server has started");
                }
            }
        }
        public string? IP { get { return _serverIP?.ToString(); } }
        public string ConnectionStatus
        {
            get
            {
                return _serviceStatus switch
                {
                    TaskServiceStatus.NotStarted => "not started",
                    TaskServiceStatus.Connecting => "disconnected",
                    TaskServiceStatus.Connected => "normal",
                    _ => "not started"
                };
            }
        }


        void InitialTasks()
        {
            switch (DeviceType)
            {
                case DeviceTypes.NONE:
                    break;
                case DeviceTypes.IZU:
                    _serverHeartbeatTask = new Task(async () =>
                    {
                        while (true)
                        {
                            if (_stopServer) break;
                            if (_serviceStatus == TaskServiceStatus.Connecting)
                            {
                                try
                                {
                                    await ConnectAsync();
                                    _serviceStatus = TaskServiceStatus.Connected;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", DeviceName, _serverIP?.ToString());
                                    //Console.WriteLine("heartbeat detecting status:  normal");
                                }
                                catch (Exception ex)
                                {
                                    _serviceStatus = TaskServiceStatus.Connecting;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", DeviceName, _serverIP?.ToString(), ex.Message);
                                    //Console.WriteLine("heartbeat detecting status:  disconnected");
                                }
                            }
                            else if (_serviceStatus == TaskServiceStatus.Connected)
                            {
                                try
                                {
                                    //状态写回
                                    WriteBack();
                                    //最后读取心跳
                                    var result = GetHeartbeatState();

                                    //Console.SetCursorPosition(0, 30);
                                    //Console.Write(" {0} ", result);
                                    _serviceStatus = TaskServiceStatus.Connected;
                                    //_logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", _deviceName, _serverIP?.ToString());
                                }
                                catch (Exception ex)
                                {
                                    _serviceStatus = TaskServiceStatus.Connecting;
                                    //_logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", _deviceName, _serverIP?.ToString(), ex.Message);
                                }
                            }

                            await Task.Delay(TimeSpan.FromMilliseconds(_heart_beat_interval_millionsec));
                        }
                    }, TaskCreationOptions.LongRunning);
                    break;
                case DeviceTypes.HID:
                    _serverHeartbeatTask = new Task(async () =>
                    {
                        while (true)
                        {
                            if (_stopServer) break;
                            if (_serviceStatus == TaskServiceStatus.Connecting)
                            {
                                try
                                {
                                    await ConnectAsync();
                                    _serviceStatus = TaskServiceStatus.Connected;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", DeviceName, _serverIP?.ToString());
                                    //Console.WriteLine("heartbeat detecting status:  normal");
                                }
                                catch (Exception ex)
                                {
                                    _serviceStatus = TaskServiceStatus.Connecting;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", DeviceName, _serverIP?.ToString(), ex.Message);
                                    //Console.WriteLine("heartbeat detecting status:  disconnected");
                                }
                            }
                            else if (_serviceStatus == TaskServiceStatus.Connected)
                            {
                                try
                                {
                                    //最后读取心跳
                                    var result = GetHeartbeatState();

                                    //Console.Write(" {0} ", result);
                                    _serviceStatus = TaskServiceStatus.Connected;
                                    //_logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", _deviceName, _serverIP?.ToString());
                                }
                                catch (Exception ex)
                                {
                                    _serviceStatus = TaskServiceStatus.Connecting;
                                    //_logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", _deviceName, _serverIP?.ToString(), ex.Message);
                                }
                            }

                            await Task.Delay(TimeSpan.FromMilliseconds(_heart_beat_interval_millionsec));
                        }
                    }, TaskCreationOptions.LongRunning);
                    break;
                case DeviceTypes.AUTODOOR:
                    _serverHeartbeatTask = new Task(async () =>
                    {
                        while (true)
                        {
                            if (_stopServer) break;
                            if (_serviceStatus == TaskServiceStatus.Connecting)
                            {
                                try
                                {
                                    await ConnectAsync();
                                    _serviceStatus = TaskServiceStatus.Connected;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", DeviceName, _serverIP?.ToString());
                                    //Console.WriteLine("heartbeat detecting status:  normal");
                                }
                                catch (Exception ex)
                                {
                                    _serviceStatus = TaskServiceStatus.Connecting;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", DeviceName, _serverIP?.ToString(), ex.Message);
                                    //Console.WriteLine("heartbeat detecting status:  disconnected");
                                }
                            }
                            else if (_serviceStatus == TaskServiceStatus.Connected)
                            {
                                try
                                {
                                    //最后读取心跳
                                    var result = GetHeartbeatState();
                                    _serviceStatus = TaskServiceStatus.Connected;
                                    //_logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", _deviceName, _serverIP?.ToString());
                                }
                                catch (Exception ex)
                                {
                                    _serviceStatus = TaskServiceStatus.Connecting;
                                    //_logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", _deviceName, _serverIP?.ToString(), ex.Message);
                                }
                            }

                            await Task.Delay(TimeSpan.FromMilliseconds(_heart_beat_interval_millionsec));
                        }
                    }, TaskCreationOptions.LongRunning);
                    break;
                case DeviceTypes.FIREDOOR:
                    _serverHeartbeatTask = new Task(async () =>
                    {
                        while (true)
                        {
                            if (_stopServer) break;
                            if (_serviceStatus == TaskServiceStatus.Connecting)
                            {
                                try
                                {
                                    await ConnectAsync();
                                    _serviceStatus = TaskServiceStatus.Connected;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", DeviceName, _serverIP?.ToString());
                                    //Console.WriteLine("heartbeat detecting status:  normal");
                                }
                                catch (Exception ex)
                                {
                                    _serviceStatus = TaskServiceStatus.Connecting;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", DeviceName, _serverIP?.ToString(), ex.Message);
                                    //Console.WriteLine("heartbeat detecting status:  disconnected");
                                }
                            }
                            else if (_serviceStatus == TaskServiceStatus.Connected)
                            {
                                try
                                {
                                    //最后读取心跳
                                    var result = GetHeartbeatState();
                                    //Console.Write(" {0} ", result);
                                    _serviceStatus = TaskServiceStatus.Connected;
                                    //_logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", _deviceName, _serverIP?.ToString());
                                }
                                catch (Exception ex)
                                {
                                    _serviceStatus = TaskServiceStatus.Connecting;
                                    //_logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", _deviceName, _serverIP?.ToString(), ex.Message);
                                }
                            }

                            await Task.Delay(TimeSpan.FromMilliseconds(_heart_beat_interval_millionsec));
                        }
                    }, TaskCreationOptions.LongRunning);
                    break;
                default:
                    break;
            }

        }
        public PlcServer2(ILoggerFactory loggerFactory, DeviceTypes deviceType, string deviceName, string ip, int heartbeatTimeInterval, IDictionary<string, string> addresses)
            : base(ip, deviceName, deviceType, addresses)
        {
            _logger = loggerFactory.CreateLogger<PlcServer>();

            _heart_beat_interval_millionsec = heartbeatTimeInterval;
            _dataItems = new();

            InitialAddresses();

            InitialTasks();

            _ = BeginRead(_refresh_interval_millionsec);
        }


        public void Stop()
        {
            StopServer = true;
            Server.Close();
            _hashes.Clear();
            _dataItems.Clear();
            _serverReadTask.Dispose();
            _serverHeartbeatTask.Dispose();
            _serviceStatus = TaskServiceStatus.NotStarted;
        }


        public void Config(List<VariableEntity> variableEntities)
        {
            if (variableEntities.Count == 0) return;

            _dataItems.Clear();
            foreach (var variable in variableEntities)
            {
                try
                {
                    if (variable.Disabled || variable.FunctionType == FunctionTypes.W) continue;

                    var fake = S7.Net.Types.DataItem.FromAddress(variable.Address);
                    var newItem = new S7.Net.Types.DataItem
                    {
                        DataType = fake.DataType,
                        DB = fake.DB,
                        Count = fake.Count,
                        VarType = (VarType)variable.VariableType,
                        StartByteAdr = fake.StartByteAdr,
                        BitAdr = fake.BitAdr
                    };
                    _dataItems.Add(newItem);
                    _hashes[newItem.GetHashCode()] = variable;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("get address  {0}  {1}  error: {2}", DeviceName, variable.Address, ex.Message);
                }
            }
            if (_heart_beat_interval_millionsec > 20)
            {
                StopServer = false;
            }
            else
            {
                _logger.LogWarning($"heart beat detect time interval is too short, please reconfig it larger than 20 ms");
            }
        }

        public void Refresh(int refreshTimeInterval)
        {
            _refresh_interval_millionsec = refreshTimeInterval;
        }
        public async Task<string> WriteBool(string address, bool boolValue)
        {
            if (!Server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
            {
                _logger.LogWarning($"operation write/bool failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
                return $"server {IP} disconnected!";
            }
            try
            {
                await Server.WriteAsync(address, boolValue);
                return string.Empty;
            }
            catch (Exception ex)
            {
                string exStr = $"operation write/bool failed, server: {_serverIP} address: {address} error: {ex.Message}";
                _logger.LogWarning(exStr);
                return exStr;
            }
        }
        public async Task<string> WriteReal(string address, float realValue)
        {
            if (!Server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
            {
                _logger.LogWarning($"operation write/real failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
                return $"server {IP} disconnected!";
            }
            try
            {
                await Server.WriteAsync(address, realValue.ConvertToUInt());
                return string.Empty;
            }
            catch (Exception ex)
            {
                string exStr = $"operation write/real failed, server: {_serverIP}  address: {address}  error:{ex.Message}";
                _logger.LogWarning(exStr);
                return exStr;
            }
        }
        public async Task<string> WriteInt(string address, int intValue)
        {
            if (!Server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
            {
                _logger.LogWarning($"operation write/int failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
                return $"server {IP} disconnected!";
            }
            try
            {
                await Server.WriteAsync(address, intValue);
                return string.Empty;
            }
            catch (Exception ex)
            {
                string exStr = $"operation write/int failed, server: {_serverIP}  address: {address}  error:{ex.Message}";
                _logger.LogWarning(exStr);
                return exStr;
            }
        }
        public async Task<string> WriteShort(string address, short intValue)
        {
            if (!Server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
            {
                _logger.LogWarning($"operation write/short failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
                return $"server {IP} disconnected!";
            }
            try
            {
                await Server.WriteAsync(address, intValue);
                return string.Empty;
            }
            catch (Exception ex)
            {
                string exStr = $"operation write/short failed, server: {_serverIP}  address: {address}  error:{ex.Message}";
                _logger.LogWarning(exStr);
                return exStr;
            }
        }
        public async Task<string> WriteFloat(string address, float floatValue)
        {
            if (!Server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
            {
                _logger.LogWarning($"operation write/float failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
                return $"server {IP} disconnected!";
            }
            try
            {
                await Server.WriteAsync(address, floatValue);
                return string.Empty;
            }
            catch (Exception ex)
            {
                string exStr = $"operation write/float failed, server: {_serverIP}  address: {address}  error:{ex.Message}";
                _logger.LogWarning(exStr);
                return exStr;
            }
        }
        

        private async Task<object?> GetValue(string dataPath)
        {
            if (!Server.IsConnected) return null;//$"server {IP} disconnected!";

            object? result = null;
            var data = S7.Net.Types.DataItem.FromAddress(dataPath);
            switch (data.VarType)
            {
                case VarType.Bit:
                    try
                    {
                        result = await Server.ReadAsync(dataPath);
                    }
                    catch (Exception ex)
                    {

                    }
                    break;
                case VarType.Byte:
                    result = await Server.ReadAsync(dataPath);
                    break;
                case VarType.Word:
                    result = await Server.ReadAsync(dataPath);
                    break;
                case VarType.DWord:
                    result = await Server.ReadAsync(dataPath);
                    break;
                case VarType.Int:
                    result = await Server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.Int, 1);
                    break;
                case VarType.DInt:
                    result = await Server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.DInt, 1);
                    break;
                case VarType.Real:
                    result = await Server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.Real, 1);
                    break;
                case VarType.LReal:
                    result = await Server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.LReal, 1);
                    break;
                case VarType.String:
                    var val = await Server.ReadAsync(data.DataType, data.DB, data.StartByteAdr + 1, VarType.Byte, 1);
                    if (val != null)
                    {
                        byte count = (byte)val;
                        result = await Server.ReadAsync(data.DataType, data.DB, data.StartByteAdr + 2, VarType.String, count);
                    }
                    break;
                case VarType.S7String:
                    val = await Server.ReadAsync(data.DataType, data.DB, data.StartByteAdr + 1, VarType.Byte, 1);
                    if (val != null)
                    {
                        byte S7StringCount = (byte)val;
                        result = await Server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.S7String, S7StringCount);
                    }
                    break;
                case VarType.S7WString:
                    val = await Server.ReadAsync(data.DataType, data.DB, data.StartByteAdr + 2, VarType.Int, 1);
                    if (val != null)
                    {
                        short S7WStringCount = (short)val;
                        result = await Server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.S7WString, S7WStringCount);
                    }
                    break;
                //case VarType.S5Time:
                //	return plc.Read(data.DataType, data.DbNumber, data.StartByte, VarType.S5Time, 1);
                //	break;
                case VarType.Counter:
                    break;
                case VarType.DateTime:
                    result = await Server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.DateTime, 1);
                    break;
                case VarType.DateTimeLong:
                    result = await Server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.DateTimeLong, 1);
                    break;
            }
            return result;
        }

        public async Task<T?> GetValue<T>(string dataPath)
        {
            object? result = await GetValue(dataPath);
            if (result != null)
            {
                return (T)result;
            }
            return default;
        }

        public async Task<float?> GetFloat(string dataPath)
        {
            object? value = await GetValue(dataPath);
            if (value != null)
            {
                return ((uint)value).ConvertToFloat();
            }
            return null;
        }

        public async Task<bool?> GetBool(string dataPath)
        {
            object? value = await GetValue(dataPath);
            if (value != null)
            {
                return (bool)value;
            }
            return null;
        }

        public async Task<ushort?> GetUShort(string dataPath)
        {
            object? value = await GetValue(dataPath);
            if (value != null)
            {
                return (ushort)value;
            }
            return null;
        }
    }
}

using IZU.Interfaces;
using S7.Net;
using S7.Net.Types;
using System.Net;

namespace IZU.Base
{
    public class PlcServer : IPlcServer
    {
        private readonly ILogger<PlcServer> _logger;
        private S7.Net.Types.DataItem _r_heartbeat_address;
        private S7.Net.Types.DataItem _w_sendback_address;
        private S7.Net.Types.DataItem _w_online_address;
        private S7.Net.Types.DataItem _w_onlinestate_address;
        private int _heart_beat_interval_millionsec = 1000;
        private int _refresh_interval_millionsec = 100;

        private TaskServiceStatus _serviceStatus;

        private readonly IPAddress? _serverIP;
        private Plc _server;
        private Task _serverReadTask;
        private Task _serverHeartbeatTask;
        private readonly string _deviceName;
        private List<DataItem> _dataItems;
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
                        _logger.LogInformation($"{_deviceName} device connection server has stopped");
                    }).Wait();
                }
                else
                {
                    _serviceStatus = TaskServiceStatus.Connecting;
                    _serverHeartbeatTask.Start();
                    _serverReadTask.Start();
                    _logger.LogInformation($"{_deviceName} device connection server has started");
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

        void InitialAddresses(DeviceTypes deviceType, IDictionary<string, string> addressMap)
        {
            switch (deviceType)
            {
                case DeviceTypes.NONE:
                    break;
                case DeviceTypes.IZU:
                    _r_heartbeat_address = S7.Net.Types.DataItem.FromAddress(addressMap["R01"]);
                    _w_sendback_address = S7.Net.Types.DataItem.FromAddress(addressMap["W01"]);
                    _w_sendback_address.Value = false;
                    _w_online_address = S7.Net.Types.DataItem.FromAddress(addressMap["W02"]);
                    _w_online_address.Value = false;
                    _w_onlinestate_address = S7.Net.Types.DataItem.FromAddress(addressMap["W03"]);
                    _w_onlinestate_address.Value = false;
                    break;
                case DeviceTypes.HID:
                    _r_heartbeat_address = S7.Net.Types.DataItem.FromAddress(addressMap["R01"]);
                    break;
                case DeviceTypes.AUTODOOR:
                    _r_heartbeat_address = S7.Net.Types.DataItem.FromAddress(addressMap["R01"]);
                    break;
                case DeviceTypes.FIREDOOR:
                    _r_heartbeat_address = S7.Net.Types.DataItem.FromAddress(addressMap["R01"]);
                    break;
                default:
                    break;
            }
        }

        void InitialTasks(DeviceTypes deviceType)
        {
            switch (deviceType)
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
                                    await _server.OpenAsync();
                                    _serviceStatus = TaskServiceStatus.Connected;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", _deviceName, _serverIP?.ToString());
                                    //Console.WriteLine("heartbeat detecting status:  normal");
                                }
                                catch (Exception ex)
                                {
                                    _serviceStatus = TaskServiceStatus.Connecting;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", _deviceName, _serverIP?.ToString(), ex.Message);
                                    //Console.WriteLine("heartbeat detecting status:  disconnected");
                                }
                            }
                            else if (_serviceStatus == TaskServiceStatus.Connected)
                            {
                                try
                                {
                                    //联机状态写回
                                    _w_online_address.Value = true;
                                    await _server.WriteAsync(_w_online_address);
                                    //联机正常状态写回
                                    _w_onlinestate_address.Value = true;
                                    await _server.WriteAsync(_w_onlinestate_address);
                                    //心跳写回
                                    _w_sendback_address.Value = ((bool)_w_sendback_address!.Value!) != true;
                                    await _server.WriteAsync(_w_sendback_address);
                                    //最后读取心跳
                                    var result = await _server.ReadAsync(
                                        _r_heartbeat_address.DataType,
                                        _r_heartbeat_address.DB,
                                        _r_heartbeat_address.StartByteAdr,
                                        _r_heartbeat_address.VarType,
                                        _r_heartbeat_address.Count);

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
                                    await _server.OpenAsync();
                                    _serviceStatus = TaskServiceStatus.Connected;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", _deviceName, _serverIP?.ToString());
                                    //Console.WriteLine("heartbeat detecting status:  normal");
                                }
                                catch (Exception ex)
                                {
                                    _serviceStatus = TaskServiceStatus.Connecting;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", _deviceName, _serverIP?.ToString(), ex.Message);
                                    //Console.WriteLine("heartbeat detecting status:  disconnected");
                                }
                            }
                            else if (_serviceStatus == TaskServiceStatus.Connected)
                            {
                                try
                                {
                                    //最后读取心跳
                                    var result = await _server.ReadAsync(
                                        _r_heartbeat_address.DataType,
                                        _r_heartbeat_address.DB,
                                        _r_heartbeat_address.StartByteAdr,
                                        _r_heartbeat_address.VarType,
                                        _r_heartbeat_address.Count);

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
                                    await _server.OpenAsync();
                                    _serviceStatus = TaskServiceStatus.Connected;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", _deviceName, _serverIP?.ToString());
                                    //Console.WriteLine("heartbeat detecting status:  normal");
                                }
                                catch (Exception ex)
                                {
                                    _serviceStatus = TaskServiceStatus.Connecting;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", _deviceName, _serverIP?.ToString(), ex.Message);
                                    //Console.WriteLine("heartbeat detecting status:  disconnected");
                                }
                            }
                            else if (_serviceStatus == TaskServiceStatus.Connected)
                            {
                                try
                                {
                                    //最后读取心跳
                                    var result = await _server.ReadAsync(
                                        _r_heartbeat_address.DataType,
                                        _r_heartbeat_address.DB,
                                        _r_heartbeat_address.StartByteAdr,
                                        _r_heartbeat_address.VarType,
                                        _r_heartbeat_address.Count);

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
                                    await _server.OpenAsync();
                                    _serviceStatus = TaskServiceStatus.Connected;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", _deviceName, _serverIP?.ToString());
                                    //Console.WriteLine("heartbeat detecting status:  normal");
                                }
                                catch (Exception ex)
                                {
                                    _serviceStatus = TaskServiceStatus.Connecting;
                                    _logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", _deviceName, _serverIP?.ToString(), ex.Message);
                                    //Console.WriteLine("heartbeat detecting status:  disconnected");
                                }
                            }
                            else if (_serviceStatus == TaskServiceStatus.Connected)
                            {
                                try
                                {
                                    //最后读取心跳
                                    var result = await _server.ReadAsync(
                                        _r_heartbeat_address.DataType,
                                        _r_heartbeat_address.DB,
                                        _r_heartbeat_address.StartByteAdr,
                                        _r_heartbeat_address.VarType,
                                        _r_heartbeat_address.Count);

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

            //只负责读取数据
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
                                _ = await _server.ReadMultipleVarsAsync(item.ToList());
                                _dataItems.ForEach(t => _hashes[t.GetHashCode()].Value = t.Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            _serviceStatus = TaskServiceStatus.Connecting;
                            _logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", _deviceName, _serverIP?.ToString(), ex.Message);
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(_refresh_interval_millionsec));
                }
            }, TaskCreationOptions.LongRunning);
        }
        public PlcServer(ILoggerFactory loggerFactory, DeviceTypes deviceType, string deviceName, string ip, int heartbeatTimeInterval, IDictionary<string, string> addresses)
        {
            _logger = loggerFactory.CreateLogger<PlcServer>();
            _deviceName = deviceName;
            if (IPAddress.TryParse(ip, out _serverIP))
            {
                _server = new Plc(CpuType.S71500, ip, 0, 0);
                _server.ReadTimeout = 3000;
            }
            else
                throw new FormatException($"{_deviceName} server IP address format is Incorrect: {ip}");

            _heart_beat_interval_millionsec = heartbeatTimeInterval;
            _dataItems = new();

            InitialAddresses(deviceType, addresses);

            InitialTasks(deviceType);
        }


        public void Stop()
        {
            StopServer = true;
            _server.Close();
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
                    _logger.LogWarning("get address  {0}  {1}  error: {2}", _deviceName, variable.Address, ex.Message);
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
            if (!_server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
            {
                _logger.LogWarning($"operation write/bool failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
                return $"server {IP} disconnected!";
            }
            try
            {
                await _server.WriteAsync(address, boolValue);
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
            if (!_server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
            {
                _logger.LogWarning($"operation write/real failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
                return $"server {IP} disconnected!";
            }
            try
            {
                await _server.WriteAsync(address, realValue.ConvertToUInt());
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
            if (!_server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
            {
                _logger.LogWarning($"operation write/int failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
                return $"server {IP} disconnected!";
            }
            try
            {
                await _server.WriteAsync(address, intValue);
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
            if (!_server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
            {
                _logger.LogWarning($"operation write/short failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
                return $"server {IP} disconnected!";
            }
            try
            {
                await _server.WriteAsync(address, intValue);
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
            if (!_server.IsConnected && _serviceStatus != TaskServiceStatus.Connected)
            {
                _logger.LogWarning($"operation write/float failed, server: {_serverIP} address: {address} error: server {IP} disconnected!");
                return $"server {IP} disconnected!";
            }
            try
            {
                await _server.WriteAsync(address, floatValue);
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
            if (!_server.IsConnected) return null;//$"server {IP} disconnected!";

            object? result = null;
            var data = S7.Net.Types.DataItem.FromAddress(dataPath);
            switch (data.VarType)
            {
                case VarType.Bit:
                    try
                    {
                        result = await _server.ReadAsync(dataPath);
                    }
                    catch (Exception ex)
                    {

                    }
                    break;
                case VarType.Byte:
                    result = await _server.ReadAsync(dataPath);
                    break;
                case VarType.Word:
                    result = await _server.ReadAsync(dataPath);
                    break;
                case VarType.DWord:
                    result = await _server.ReadAsync(dataPath);
                    break;
                case VarType.Int:
                    result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.Int, 1);
                    break;
                case VarType.DInt:
                    result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.DInt, 1);
                    break;
                case VarType.Real:
                    result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.Real, 1);
                    break;
                case VarType.LReal:
                    result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.LReal, 1);
                    break;
                case VarType.String:
                    var val = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr + 1, VarType.Byte, 1);
                    if (val != null)
                    {
                        byte count = (byte)val;
                        result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr + 2, VarType.String, count);
                    }
                    break;
                case VarType.S7String:
                    val = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr + 1, VarType.Byte, 1);
                    if (val != null)
                    {
                        byte S7StringCount = (byte)val;
                        result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.S7String, S7StringCount);
                    }
                    break;
                case VarType.S7WString:
                    val = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr + 2, VarType.Int, 1);
                    if (val != null)
                    {
                        short S7WStringCount = (short)val;
                        result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.S7WString, S7WStringCount);
                    }
                    break;
                //case VarType.S5Time:
                //	return plc.Read(data.DataType, data.DbNumber, data.StartByte, VarType.S5Time, 1);
                //	break;
                case VarType.Counter:
                    break;
                case VarType.DateTime:
                    result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.DateTime, 1);
                    break;
                case VarType.DateTimeLong:
                    result = await _server.ReadAsync(data.DataType, data.DB, data.StartByteAdr, VarType.DateTimeLong, 1);
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

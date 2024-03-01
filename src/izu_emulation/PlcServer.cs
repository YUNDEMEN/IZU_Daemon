using IZU.Interfaces;
using System.Net;

namespace IZU.Base
{
    public class PlcServer : IPlcServer
    {
        private readonly ILogger<PlcServer> _logger;
        private int _heart_beat_interval_millionsec = 100;
        private int _refresh_interval_millionsec = 100;
        private TaskServiceStatus _serviceStatus;
        private readonly IPAddress? _serverIP;
        private Task _serverReadTask;
        private Task _serverHeartbeatTask;
        private readonly string _deviceName;
        private List<VariableEntity> _variable;
        private bool _stopServer;
        internal bool StopServer
        {
            get { return _stopServer; }
            private set
            {
                _stopServer = value;
                if (value)
                {
                    //_serverReadTask.ContinueWith((task) => {
                    //    _logger.LogInformation($"{_deviceName} device connection server has stopped");
                    //}).Wait();
                }
                else
                {
                    _serviceStatus = TaskServiceStatus.Connecting;
                    //_serverHeartbeatTask.Start();
                    //_serverReadTask.Start();
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
        }

        void InitialTasks(DeviceTypes deviceType)
        {
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
                            _variable.ForEach(t => t.Value = false);

                            //_logger.LogDebug("{0} server {1} heartbeat detecting status:  normal", _deviceName, _serverIP?.ToString());
                        }
                        catch (Exception ex)
                        {
                            //_serviceStatus = TaskServiceStatus.Connecting;
                            //_logger.LogDebug("{0} server {1} heartbeat detecting status:  disconnected ({2})", _deviceName, _serverIP?.ToString(), ex.Message);
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(_refresh_interval_millionsec));
                }
            }, TaskCreationOptions.LongRunning);
        }
        public PlcServer(ILoggerFactory loggerFactory, DeviceTypes deviceType, string deviceName, string ip, int refreshTimeInterval, IDictionary<string, string> addresses)
        {
            _logger = loggerFactory.CreateLogger<PlcServer>();
            _deviceName = deviceName;
            _heart_beat_interval_millionsec = refreshTimeInterval;
            InitialAddresses(deviceType, addresses);
            //InitialTasks(deviceType);
        }


        public void Stop()
        {
            StopServer = true;
            //_serverReadTask.Dispose();
            //_serverHeartbeatTask.Dispose();
            _serviceStatus = TaskServiceStatus.NotStarted;
        }

        public void Config(List<VariableEntity> variableEntities)
        {
            _variable = variableEntities;
            if (_heart_beat_interval_millionsec > 20)
            {
                StopServer = false;
                _serviceStatus = TaskServiceStatus.Connected;
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
            var db=_variable.FirstOrDefault(t => t.Address == address);
            if (db == null)
            {
                _logger.LogWarning($"{nameof(WriteBool)} address is not existed");
                return $"{nameof(WriteBool)} address is not existed";
            }
            else
            {
                db.Value = boolValue;
                return string.Empty;
            }
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

        private async Task<object?> GetValue(string dataPath)
        {
            var db = _variable.FirstOrDefault(t => t.Address == dataPath);
            if (db == null)
            {
                _logger.LogWarning($"{nameof(GetValue)} address is not existed");
                return null;
            }
            else
            {
                return db.Value!;
            }
        }





        public async Task<string> WriteReal(string address, float realValue)
        {
            return string.Empty;
        }
        public async Task<string> WriteInt(string address, int intValue)
        {
            return string.Empty;
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


    }
}

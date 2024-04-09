using IZU.Base;
using IZU.Interfaces;

namespace IZU.Base
{
    public abstract class DeviceBase : IDisposable
    {
        protected readonly ILogger<DeviceEntity> _logger;
        protected readonly ILoggerFactory _loggerFactory;
        private IOperatable? _operatable;
        public IOperatable? Operatable
        {
            get
            {
                if (_operatable == null)
                {
                    switch (DeviceType)
                    {
                        case DeviceTypes.NONE:
                            break;
                        case DeviceTypes.IZU:
                            break;
                        case DeviceTypes.HID:
                            break;
                        case DeviceTypes.AUTODOOR:
                            _operatable = new DeviceFactories.AutoDoor(this);
                            break;
                        case DeviceTypes.FIREDOOR:
                            _operatable = new DeviceFactories.FireDoor(this);
                            break;
                    }
                    if (_operatable == null)
                    {
                        _logger.LogWarning($"unknown device {Name}({DeviceType})");
                        return null;
                    }
                }

                return _operatable;
            }
        }
        public readonly string FromFile;
        /// <summary>
        /// 设备名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 设备类型
        /// </summary>
        public DeviceTypes DeviceType { get; set; }
        /// <summary>
        /// 从设备读取数据刷新时间 (million seconds)
        /// </summary>
        public int PullDataFromDeviceTimeInterval { get; set; }
        /// <summary>
        /// plc服务器
        /// </summary>
        public IPlcServer? Server { get; set; }
        /// <summary>
        /// 变量表
        /// </summary>
        public List<VariableEntity> Variables { get; set; }
        public DeviceBase(ILoggerFactory loggerFactory, string file, string name, List<VariableEntity>? variables = null)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<DeviceEntity>();
            FromFile = file;
            Name = name.ToUpper();
            if (variables == null) variables = new List<VariableEntity>();
            Variables = variables;
            var item = Variables.FirstOrDefault(t => !string.IsNullOrEmpty(t.ServerIP));
            if (item == null)
                //	throw new RowNotInTableException($"Server IP address missing!");
                _logger.LogWarning($"server IP address is not found in {name} ({FromFile})! default IP address is 127.0.0.1");

            PullDataFromDeviceTimeInterval = item!.RefreshInterval;
            DeviceType = item == null ? DeviceTypes.NONE : item.DeviceType;

            //如果设备的变量都被禁用了，则直接不启用 plc 连接服务
            if (variables.All(t => !t.Disabled))
            {
                ActivatePlcService();
            }
        }

        public virtual void ActivatePlcService()
        {
            throw new NotImplementedException();
        }

        protected IDictionary<string, string> GetActionTypes()
        {
            IDictionary<string, string> actionMap = new Dictionary<string, string>();
            foreach (var item in Variables)
            {
                actionMap.Add(item.ActionType, item.Address);
            }
            return actionMap;
        }

        public void Dispose()
        {
            DeviceType = DeviceTypes.NONE;
            Variables.Clear();
            Server?.Stop();
        }

        public void Refresh(int refreshTimeInterval)
        {
            PullDataFromDeviceTimeInterval = refreshTimeInterval;
            Server?.Refresh(PullDataFromDeviceTimeInterval);
        }

        public static DeviceBase DummyDevice => new DeviceEntity(loggerFactory: null!, "sampledata", "dummy");
    }
}

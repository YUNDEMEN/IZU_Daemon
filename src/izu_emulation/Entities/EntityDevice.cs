using IZU.Base;
using IZU.Interfaces;

namespace IZU.Base
{
    public class DeviceEntity : IDisposable
    {
        private readonly ILogger<DeviceEntity> _logger;
        private readonly ILoggerFactory _loggerFactory;

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
        public DeviceEntity(ILoggerFactory loggerFactory, string file, string name, List<VariableEntity>? variables = null)
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
            if(variables.All(t=>!t.Disabled))
            {
                ActivatePlcService();
            }
        }

        public void ActivatePlcService()
        {
            if (Server != null)
                Server.Stop();

            Variables.ForEach(t =>
            {
                if (t.VariableType == VariableTypes.Bool)
                    t.Value = false;
            });
            var item = Variables.FirstOrDefault(t => !string.IsNullOrEmpty(t.ServerIP));
            if (item == null)
                //	throw new RowNotInTableException($"Server IP address missing!");
                _logger.LogWarning($"server IP address is not found in {Name} ({FromFile})! default IP address is 127.0.0.1");

            InitialSimulation();

            Server = new PlcServer(_loggerFactory, DeviceType, Name, item == null ? "127.0.0.1" : item.ServerIP, PullDataFromDeviceTimeInterval, GetActionTypes());
            Server.Config(Variables);
        }

        void InitialSimulation()
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
                    foreach (var vr in Variables)
                    {
                        if (vr.ActionType == "R00") vr.Value = true;
                        if (vr.ActionType == "R02") vr.Value = false;
                        if (vr.ActionType == "R05") vr.Value = false;
                        if (vr.ActionType == "R07") vr.Value = false;
                        if (vr.ActionType == "R04") vr.Value = false;
                        if (vr.ActionType == "R06") vr.Value = false;
                        if (vr.ActionType == "R08") vr.Value = true;
                        if (vr.ActionType == "R03") vr.Value = true;
                    }
                    break;
                case DeviceTypes.FIREDOOR:
                    break;
            }
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

        public static DeviceEntity DummyDevice => new DeviceEntity(loggerFactory: null!, "sampledata", "dummy");
    }
}

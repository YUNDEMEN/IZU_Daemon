using IZU.Base;
using IZU.Interfaces;
using System.Data;
using System.ServiceProcess;

namespace IZU.Entities
{
    public class DeviceEntity : IDisposable
    {
        private readonly ILogger<DeviceEntity> _logger;


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
        public IPlcServer? Server { get; }
        /// <summary>
        /// 变量表
        /// </summary>
        public List<VariableEntity> Variables { get; set; }
        public DeviceEntity(ILoggerFactory loggerFactory, string file, string name, List<VariableEntity>? variables = null)
        {
            _logger = loggerFactory.CreateLogger<DeviceEntity>();
            FromFile = file;
            Name = name.ToLower();
            if (variables == null) variables = new List<VariableEntity>();
            Variables = variables;
            var item = Variables.FirstOrDefault(t => !string.IsNullOrEmpty(t.ServerIP));
            if (item == null)
                //	throw new RowNotInTableException($"Server IP address missing!");
                _logger.LogWarning($"server IP address is not found in {name} ({FromFile})! default IP address is 127.0.0.1");

            PullDataFromDeviceTimeInterval = item!.RefreshInterval;
            DeviceType = item == null ? DeviceTypes.NONE : item.DeviceType;

            Server = new PlcServer(loggerFactory, DeviceType, Name, item == null ? "127.0.0.1" : item.ServerIP, PullDataFromDeviceTimeInterval, GetActionTypes());
            Server.Config(Variables);
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

    public class BroadcastData
    {
        public List<IzuStatus> izu = new();
        public List<HidStatus> hid = new();
        public List<AutodoorStatus> autodoor = new();
        public List<FiredoorStatus> firedoor = new();
    }
    public class IzuStatus
    {
        public string name;
        public object? r01 = null; public object? r02 = null; public object? r03 = null; public object? r04 = null; public object? r05 = null; public object? r06 = null; public object? r07 = null; public object? r08 = null; public object? r09 = null;
        public object? r10 = null; public object? r11 = null; public object? r12 = null; public object? r13 = null; public object? r14 = null; public object? r15 = null; public object? r16 = null; public object? r17 = null; public object? r18 = null; public object? r19 = null;
        public object? r20 = null;
    }
    public class HidStatus
    {
        public string name;
        public object? r00 = null; public object? r01 = null; public object? r02 = null; public object? r03 = null; public object? r04 = null; public object? r05 = null; public object? r06 = null; public object? r07 = null; public object? r08 = null; public object? r09 = null;
        public object? r10 = null; public object? r11 = null;
    }
    public record AutodoorStatus
    {
        public string name; public object? doorState;
        public object? r00 = null; public object? r01 = null; public object? r02 = null; public object? r03 = null; public object? r04 = null; public object? r05 = null; public object? r06 = null; public object? r07 = null; public object? r08 = null; public object? r09 = null;
        public object? r10 = null; public object? r11 = null; public object? r12 = null; public object? r13 = null;
    }
    public record FiredoorStatus
    {
        public string name;
        public object? r00 = null; public object? r01 = null; public object? r02 = null; public object? r03 = null; public object? r04 = null; public object? r05 = null; public object? r06 = null; public object? r07 = null; public object? r08 = null; public object? r09 = null;
        public object? r10 = null; public object? r11 = null; public object? r12 = null; public object? r13 = null;
    }
}

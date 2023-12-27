using IZU.Base;
using IZU.Interfaces;
using System.Data;
using System.ServiceProcess;

namespace IZU.Entities
{
    public class DeviceEntity :  IDisposable
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
        public DeviceEntity(ILoggerFactory loggerFactory, string file, string name, int refreshTimeInterval, List<VariableEntity>? variables = null)
        {
            _logger = loggerFactory.CreateLogger<DeviceEntity>();
            FromFile = file;
            Name = name.ToLower();
            PullDataFromDeviceTimeInterval = refreshTimeInterval;
            if (variables == null) variables = new List<VariableEntity>();
            Variables = variables;
            var item = Variables.FirstOrDefault(t => !string.IsNullOrEmpty(t.ServerIP));
            if (item == null)
                //	throw new RowNotInTableException($"Server IP address missing!");
                _logger.LogWarning($"server IP address is not found in {name} ({FromFile})! default IP address is 127.0.0.1");

            DeviceType = item == null ? DeviceTypes.NONE : item.DeviceType;

            Server = new PlcServer(loggerFactory, DeviceType, Name, item == null ? "127.0.0.1" : item.ServerIP, refreshTimeInterval, GetActionTypes());
            Server.Config(Variables);
        }
        protected IDictionary<ActionTypes, string> GetActionTypes()
        {
            var types = Enum.GetValues(typeof(ActionTypes));
            IDictionary<ActionTypes, string> actionMap = new Dictionary<ActionTypes, string>();
            foreach (var item in types)
            {
                var v = Variables.FirstOrDefault(t => t.ActionType == (ActionTypes)item);
                if (v == null)
                {
                    actionMap[(ActionTypes)item] = string.Empty;
                }
                else
                {
                    actionMap[(ActionTypes)item] = v.Address;
                }
            }
            return actionMap;
            //var v = Variables.FirstOrDefault(t => t.ActionType == actionType);
            //if (v == null || string.IsNullOrEmpty(v.Address))
            //	throw new Exception($"{actionType} action is not marked in {Name}");
            //return v.Address;
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

        public static DeviceEntity DummyDevice => new DeviceEntity(loggerFactory: null!, "sampledata", "dummy", 0);
    }

    public class BroadcastData
    {
        public List<BroadcastAutodoorInfo> autodoor = new List<BroadcastAutodoorInfo>();
        public List<BroadcastIzuInfo> izu = new List<BroadcastIzuInfo>();
        public List<BroadcastFiredoorInfo> firedoor = new List<BroadcastFiredoorInfo>();
    }

    public record BroadcastAutodoorInfo(string? name, object? online, object? status, object? start_sig, object? initial_sig, object? fault, object? mode);
    public record BroadcastIzuInfo(string? name, object? online, object? runningStatus, object? fault);
    public record BroadcastFiredoorInfo(string? name, object? online);
}

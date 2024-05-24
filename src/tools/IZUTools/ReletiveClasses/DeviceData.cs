using IZU.Base;
using TinyCsvParser.Mapping;
using TinyCsvParser.TypeConverter;

namespace IZUTools.ReletiveClasses
{

    public class DeviceDataMapping : CsvMapping<DeviceData>
    {
        public DeviceDataMapping()
            : base()
        {
            MapProperty(0, x => x.ServerIP);
            MapProperty(1, x => x.DeviceType, new EnumConverter<DeviceTypes>());
            MapProperty(2, x => x.DeviceName);
            MapProperty(3, x => x.FunctionType, new EnumConverter<FunctionTypes>());
            MapProperty(4, x => x.Address);
            MapProperty(5, x => x.Description);
            MapProperty(6, x => x.VariableType, new EnumConverter<VariableTypes>());
            MapProperty(7, x => x.Disabled, new DisabledConverter());
            MapProperty(8, x => x.ActionType);
            MapProperty(9, x => x.RefreshInterval, new IntConverter());
        }
    }
    class EnumConverter<T> : ITypeConverter<T> where T : struct
    {
        public Type TargetType => typeof(T);
        public bool TryConvert(string value, out T result)
        {
            if (!Enum.TryParse(value, true, out result))
            {
                result = default;
            }
            return true;
        }
    }

    class DisabledConverter : ITypeConverter<bool>
    {
        public Type TargetType => typeof(bool);

        public bool TryConvert(string value, out bool result)
        {
            result = value?.Trim().ToLower() == "1" || value?.Trim().ToLower() == "true";
            return true;
        }
    }

    class IntConverter : ITypeConverter<int>
    {
        public Type TargetType => typeof(int);

        public bool TryConvert(string value, out int result)
        {
            if (!int.TryParse(value, out result))
                result = 100;
            return true;
        }
    }
    public class DeviceData
    {
        /// <summary>
        /// 设备IP
        /// </summary>
        public string ServerIP { get; set; } = string.Empty;
        /// <summary>
        /// 设备名称（不重复）
        /// </summary>
        public string DeviceName { get; set; } = string.Empty;
        /// <summary>
        /// 设备类型
        /// </summary>
        public DeviceTypes DeviceType { get; set; }
        /// <summary>
        /// 指令读/写
        /// </summary>
        public FunctionTypes FunctionType { get; set; }

        /// <summary>
        /// 变量名
        /// </summary>
        public string Address { get; set; } = string.Empty;
        /// <summary>
        /// 别名
        /// </summary>
        public string ActionType { get; set; }
        /// <summary>
        /// 变量类型
        /// </summary>
        public VariableTypes VariableType { get; set; }
        /// <summary>
        /// 变量描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// 最后一次刷新时间
        /// </summary>
        public DateTime? LastRefreshTime { get; set; }
        public int RefreshInterval { get; set; } = 100;
        /// <summary>
        /// 已禁用
        /// </summary>
        public bool Disabled { get; set; } = false;
        public DeviceData() { }
    }
}

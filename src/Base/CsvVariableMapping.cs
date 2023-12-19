using IZU.Entities;
using TinyCsvParser.Mapping;
using TinyCsvParser.TypeConverter;

namespace IZU.Base
{
    public class CsvVariableMapping : CsvMapping<VariableEntity>
    {
        public CsvVariableMapping()
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
            MapProperty(8, x => x.ActionType, new EnumConverter<ActionTypes>());
            MapProperty(9, x => x.ActionType2);
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
}

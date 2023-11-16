using TinyCsvParser.Mapping;
using TinyCsvParser.TypeConverter;

namespace IZU.Entities
{
	public class CsvVariableMapping : CsvMapping<Variable>
	{
		public CsvVariableMapping()
			: base()
		{
			MapProperty(0, x => x.ServerIP);
			MapProperty(1, x => x.DeviceName);
			MapProperty(2, x => x.FunctionType, new FunctionTypeConverter());
			MapProperty(3, x => x.Name);
			MapProperty(4, x => x.VariableType, new VariableTypeConverter());
			MapProperty(5, x => x.Description);
		}
	}
	class FunctionTypeConverter : ITypeConverter<FunctionTypes>
	{
		public Type TargetType => typeof(FunctionTypes);

		public bool TryConvert(string value, out FunctionTypes result)
		{
			switch (value.ToUpper())
			{
				default:
				case "R":
					result = FunctionTypes.Read;
					break;
				case "W":
					result = FunctionTypes.Write;
					break;
			}
			return true;
		}
	}
	class VariableTypeConverter : ITypeConverter<VariableTypes>
	{
		public Type TargetType => typeof(VariableTypes);

		public bool TryConvert(string value, out VariableTypes result)
		{
			switch (value.ToUpper())
			{
				case "BOOL":
					result = VariableTypes.BOOL;
					break;
				case "INT":
					result = VariableTypes.INT;
					break;
				case "REAL":
					result = VariableTypes.REAL;
					break;
				case "STRING":
					result = VariableTypes.STRING;
					break;
				default:
					result = VariableTypes.NONE;
					break;
			}
			return true;
		}
	}
}

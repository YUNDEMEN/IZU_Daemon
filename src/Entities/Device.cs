namespace IZU.Entities
{
	public class Device 
	{
		public string Name { get; set; }
		public List<Variable> Variables { get; set; }
		public Device(string name, List<Variable>? variables=null)
		{
			Name = name.ToLower();
			if (variables == null) variables = new List<Variable>();
			Variables = variables;
		}
	}

	public class Variable
	{
		public string? ServerIP { get; set; }
		public string? DeviceName { get; set; }
		public FunctionTypes FunctionType { get; set; }
		public string? Name { get; set; }
		public VariableTypes VariableType { get; set; }
		public object? Value { get; set; }
		public string? Description { get; set; }
		public Variable() { }
	}
	public enum FunctionTypes
	{
		Read,
		Write,
	}
	public enum VariableTypes
	{
		NONE,
		BOOL,
		INT,
		REAL,
		STRING
	}
}

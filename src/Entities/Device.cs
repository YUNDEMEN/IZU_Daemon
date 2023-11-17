namespace IZU.Entities
{
	public class Device 
	{
		/// <summary>
		/// 设备名称
		/// </summary>
		public string Name { get; set; }
		/// <summary>
		/// 设备IP地址
		/// </summary>
		public string IP { get; set; } = "127.0.0.1";
		/// <summary>
		/// 变量表
		/// </summary>
		public List<Variable> Variables { get; set; }
		public Device(string name, List<Variable>? variables = null)
		{
			Name = name.ToLower();
			if (variables == null) variables = new List<Variable>();
			Variables = variables;
			var item = Variables.FirstOrDefault(t => !string.IsNullOrEmpty(t.ServerIP));
			if (item != null)
			{
				IP = item.ServerIP;
			}
		}
	}

	public class Variable
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
		/// 指令读/写
		/// </summary>
		public FunctionTypes FunctionType { get; set; }

		/// <summary>
		/// 变量名
		/// </summary>
		public string Name { get; set; } = string.Empty;

		/// <summary>
		/// 变量类型
		/// </summary>
		public VariableTypes VariableType { get; set; }
		/// <summary>
		/// 变量值
		/// </summary>
		public object? Value { get; set; }
		/// <summary>
		/// 变量描述
		/// </summary>
		public string Description { get; set; } = string.Empty;
		/// <summary>
		/// 已禁用
		/// </summary>
		public bool Disabled { get; set; } = false;
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

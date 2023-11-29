using IZU.Base;

namespace IZU.Entities
{
	public class VariableEntity
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
		/// 下发指令用途
		/// </summary>
		public ActionTypes ActionType { get; set; }

		/// <summary>
		/// 变量名
		/// </summary>
		public string Address { get; set; } = string.Empty;

		/// <summary>
		/// 变量类型
		/// </summary>
		public VariableTypes VariableType { get; set; }
		/// <summary>
		/// 变量值
		/// </summary>
		public object? Value
		{
			get => _value;
			set
			{
				if (_value?.ToString() != value?.ToString())
				{
					/*
					 通过将object值转化为string对比其是否变更
					 如果变更则保存变更信息到数据存储 
					 每次重启服务都会记录一次变更信息
					 */
					//string header =                                     "设备名称,       设备类型,        地址,      旧值,    新值,      变量类型,          描述,            记录时间";
					TextRecorder.Instance.EnqueueAsync($"{DeviceName},{DeviceType},{Address},{_value},{value},{VariableType},{Description},{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
					_value = value;
				}
				LastRefreshTime = DateTime.Now;
			}
		}
		private object? _value;	
		/// <summary>
		/// 变量描述
		/// </summary>
		public string Description { get; set; } = string.Empty;
		/// <summary>
		/// 最后一次刷新时间
		/// </summary>
		public DateTime? LastRefreshTime { get; set; }
		/// <summary>
		/// 已禁用
		/// </summary>
		public bool Disabled { get; set; } = false;
		public VariableEntity() { }
	}
}

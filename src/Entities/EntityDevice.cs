using IZU.Base;
using IZU.Interfaces;
using System.Data;
using System.ServiceProcess;

namespace IZU.Entities
{
	public class DeviceEntity : NLogProvider
	{
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
		public DeviceEntity(string file, string name,int refreshTimeInterval, List<VariableEntity>? variables = null)
		{
			FromFile = file;
			Name = name.ToLower();
			PullDataFromDeviceTimeInterval = refreshTimeInterval;
			if (variables == null) variables = new List<VariableEntity>();
			Variables = variables;
			var item = Variables.FirstOrDefault(t => !string.IsNullOrEmpty(t.ServerIP));
			if (item == null)
				//	throw new RowNotInTableException($"Server IP address missing!");
				LogWarn($"server IP address is not found in {name} ({FromFile})! default IP address is 127.0.0.1");

			DeviceType = item == null ? DeviceTypes.NONE : item.DeviceType;

			Server = new PlcServer(Name, item == null ? "127.0.0.1" : item.ServerIP, refreshTimeInterval, GetActionType(ActionTypes.HEARTBEAT));
			Server.Config(Variables);
		}
		protected string GetActionType(ActionTypes actionType)
		{
			var v = Variables.FirstOrDefault(t => t.ActionType == actionType);
			if (v == null || string.IsNullOrEmpty(v.Address))
				throw new Exception($"{actionType} action is not marked in {Name}");
			return v.Address;
		}
		public static DeviceEntity DummyDevice { get { return new DeviceEntity("sampledata", "dummy", 0); } }
	}

}

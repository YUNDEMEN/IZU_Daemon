namespace IZU.Entities
{
	public class IZUConfig
	{
		/// <summary>
		/// appsettings.json 配置节名称
		/// </summary>
		public const string KEY = "IZU";
		/// <summary>
		/// NLog 配置文件
		/// </summary>
		public const string NlogConfig = "nlog.config";
		/// <summary>
		/// 此服务会部署在多台服务器中
		/// 表示从API获取的服务名称
		/// </summary>
		public string Name { get; set; } = string.Empty;
		/// <summary>
		/// 服务的本地IP地址
		/// </summary>
		public string Server { get; set; } = "127.0.0.1";
		/// <summary>
		/// 测试数据文件
		/// </summary>
		public string SampleFiles { get; set; } = "SampleData";
		/// <summary>
		/// 变量表文件夹
		/// </summary>
		public string DeviceFiles { get; set; } = "DeviceTable";
		/// <summary>
		/// 从设备读取数据刷新时间
		/// </summary>
		public int RefreshMillionSeconds { get; set; } = 500;
		/// <summary>
		/// 服务灾后重启时间
		/// </summary>
		public int RecoverySeconds { get; set; } = 10;

		public string izu_backend { get; set; }=string.Empty;
		public string map_verion { get; set; } = string.Empty;


    }
}

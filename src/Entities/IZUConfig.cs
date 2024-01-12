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
        public static int ID { get; set; } = 0;
        /// <summary>
        /// 服务的本地IP地址
        /// </summary>
        public static string Server { get { return $"{ServerIP}:{ServerPort}"; } }
        public static string ServerIP { get; set; } = "127.0.0.1";
        public static int ServerPort { get; set; } = 8031;
        public static string BackendIZUBaseUrl { get; set; } = string.Empty;
        /// <summary>
        /// websocket发布数据时间间隔（毫秒）
        /// </summary>
        public static int PublishMillionSeconds { get; set; } = 500;
        /// <summary>
        /// 默认从数据库中读取变量表
        /// 如果在appsettings.json中配置了 usecsv 节点则使用本地csv文件
        /// </summary>
        public static string DeviceTableFrom = "db";


    }
}

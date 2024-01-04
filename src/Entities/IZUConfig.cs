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
		/// 服务的本地IP地址
		/// </summary>
		public static string Server { get; set; } = "127.0.0.1:80";
        public static string BackendIZUBaseUrl { get; set; } = string.Empty;
        /// <summary>
        /// 从设备读取数据刷新时间（毫秒）
        /// </summary>
        public static int RefreshMillionSeconds { get; set; } = 500;
        /// <summary>
        /// 发布数据时间间隔（毫秒）
        /// </summary>
        public static int PublishMillionSeconds { get; set; } = 500; 



    }
}

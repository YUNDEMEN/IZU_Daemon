using Newtonsoft.Json.Linq;

namespace IZU.Entities
{
    public class IZUConfig
    {
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
        public static string DeviceTableFrom { get; set; } = "db";


        public static string MapVersion { get; set; } = string.Empty;

        public static string MulticastIP { get; set; } = string.Empty;

        public static string Read()
        {
            string appsettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (!File.Exists(appsettingsPath))
            {
                return $"config file [{appsettingsPath}] missing!";
            }
            string json = File.ReadAllText(appsettingsPath);
            try
            {
                JObject configJson = JObject.Parse(json);
                if (configJson["izu_backend"] == null)
                {
                    return "izu_backend node not found!";
                }
                if (configJson["map_version"] == null)
                {
                    return "map_version node not found!";
                }
                if (configJson["multicast_ip"] == null)
                {
                    return "multicast_ip node not found!";
                }
                MulticastIP = configJson["multicast_ip"]!.ToString();
                BackendIZUBaseUrl = configJson!["izu_backend"]!.ToString();
                DeviceTableFrom = configJson["usecsv"] != null ? "localcsv" : "db";
                MapVersion = configJson!["map_version"]!.ToString();
            }
            catch (Exception ex)
            {
                return ex.Message + "\r\n" + ex.StackTrace;
            }
            return string.Empty;
        }
    }
}

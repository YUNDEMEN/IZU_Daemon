using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;


namespace IZU.Entities
{
    public class IZUConfig
    {
        /// <summary>
        /// NLog 配置文件
        /// </summary>
        public const string NlogConfig = "nlog.config";
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


        /// <summary>
        /// 多播端口（报文）
        /// 用于oso接受设备状态
        /// </summary>
        public static int PortMulticastServer;
        /// <summary>
        /// 多播频率（ms）
        /// </summary>
        public static int IntervalMulticastServer;
        /// <summary>
        /// 接受OSO控制设备指令端口
        /// </summary>
        public static int PortNanoCommandServer;
        /// <summary>
        /// 多播端口（json数据）
        /// 用于前端展示设备状态（izu-oso-backend-frontend）
        /// </summary>
        public static int PortMulticastFullDataServer;
        /// <summary>
        /// 多播（json数据）发送设备数据频率（ms）
        /// </summary>
        public static int IntervalMulticastFullDataServer;
        /// <summary>
        /// 单点发送设备数据
        /// 用于本地上位机客户端程序
        /// </summary>
        public static int PortNanoDataServer;
        /// <summary>
        /// 单点发送设备数据频率（ms）
        /// </summary>
        public static int IntervalNanoDataServer;
        /// <summary>
        /// izu_daemon的ipPort
        /// </summary>
        public static int izuId;

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
                    return "izu_backend node not found!";
                if (configJson["map_version"] == null)
                    return "map_version node not found!";
                if (configJson["multicast_ip"] == null)
                    return "multicast_ip node not found!";
                if (configJson["PortMulticastServer"] == null)
                    return "PortMulticastServer node not found!";
                if (configJson["IntervalMulticastServer"] == null)
                    return "IntervalMulticastServer node not found!";
                if (configJson["PortNanoCommandServer"] == null)
                    return "PortNanoCommandServer node not found!";
                if (configJson["PortMulticastFullDataServer"] == null)
                    return "PortMulticastFullDataServer node not found!";
                if (configJson["IntervalMulticastFullDataServer"] == null)
                    return "IntervalMulticastFullDataServer node not found!";
                if (configJson["PortNanoDataServer"] == null)
                    return "PortNanoDataServer node not found!";
                if (configJson["IntervalNanoDataServer"] == null)
                    return "IntervalNanoDataServer node not found!";
                if (configJson["izuId"] == null)
                    return "izuId node not found!";

                MulticastIP = configJson["multicast_ip"]!.ToString();
                BackendIZUBaseUrl = configJson!["izu_backend"]!.ToString();
                DeviceTableFrom = configJson["usecsv"] != null ? "localcsv" : "db";
                MapVersion = configJson!["map_version"]!.ToString();
                PortMulticastServer = Int32.Parse(configJson!["PortMulticastServer"]!.ToString());
                IntervalMulticastServer = Int32.Parse(configJson!["IntervalMulticastServer"]!.ToString());
                PortNanoCommandServer = Int32.Parse(configJson!["PortNanoCommandServer"]!.ToString());
                PortMulticastFullDataServer = Int32.Parse(configJson!["PortMulticastFullDataServer"]!.ToString());
                IntervalMulticastFullDataServer = Int32.Parse(configJson!["IntervalMulticastFullDataServer"]!.ToString());
                PortNanoDataServer = Int32.Parse(configJson!["PortNanoDataServer"]!.ToString());
                IntervalNanoDataServer = Int32.Parse(configJson!["IntervalNanoDataServer"]!.ToString());
                izuId = Int32.Parse(configJson!["izuId"]!.ToString());
            }
            catch (Exception ex)
            {
                return ex.Message + "\r\n" + ex.StackTrace;
            }
            return string.Empty;
        }

        public static bool WriteToAppSetting(string node, int izuId)
        {
            string appsettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (!File.Exists(appsettingsPath))
            {
                return false;
            }
            try
            {
                string json = File.ReadAllText(appsettingsPath);
                JObject configJson = JObject.Parse(json);
                configJson[node] = izuId;
                string convertString = Convert.ToString(configJson);
                File.WriteAllText(appsettingsPath, convertString);
                return true;
            }
            catch { return false; }
        }

        public static bool WriteToAppSetting(string node,string map_version)
        {
            string appsettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (!File.Exists(appsettingsPath))
            {
                return false;
            }
            try
            {
                string json = File.ReadAllText(appsettingsPath);
                JObject configJson = JObject.Parse(json);
                configJson[node] = map_version;
                string convertString = Convert.ToString(configJson);
                File.WriteAllText(appsettingsPath, convertString);
                return true;
            }
            catch { return false; }
        }
    }
}

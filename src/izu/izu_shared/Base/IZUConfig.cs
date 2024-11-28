using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wonder.Infrastructure;


namespace IZU.Base
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

        public static string OSOChannel { get; set; } = "tcp://127.0.0.1:8024";

        /// <summary>
        /// 接受OHT控制设备指令端口
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
        public static int PortDataSend;
        /// <summary>
        /// 单点发送设备数据频率（ms）
        /// </summary>
        public static int IntervalDataSend;
        /// <summary>
        /// izu_daemon的ipPort
        /// </summary>
        public static int izuId;

        /// <summary>
        /// MXview one host
        /// </summary>
        public static string MXviewHost;

        /// <summary>
        /// MXview one token
        /// </summary>
        public static string MXviewToken;

        /// <summary>
        /// MXJob是否启用
        /// </summary>
        public static string MXJobEnable;

        /// <summary>
        /// MX监控设备列表
        /// </summary>
        public static List<MXDev> MXDevList;

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
                if (configJson["izuId"] == null)
                    return "izuId node not found!";

                if (configJson["izu_backend"] == null)
                    return "izu_backend node not found!";

                if (configJson["map_version"] == null)
                    return "map_version node not found!";

                if (configJson["multicast_ip"] == null)
                    return "multicast_ip node not found!";

                if (configJson["PortNanoCommandServer"] == null)
                    return "PortNanoCommandServer node not found!";

                if (configJson["PortMulticastFullDataServer"] == null)
                    return "PortMulticastFullDataServer node not found!";

                if (configJson["IntervalMulticastFullDataServer"] == null)
                    return "IntervalMulticastFullDataServer node not found!";

                if (configJson["PortDataSend"] == null)
                    return "PortDataSend node not found!";

                if (configJson["IntervalDataSend"] == null)
                    return "IntervalDataSend node not found!";

                if (configJson["MXviewHost"] == null)
                    return "MXviewHost node not found!";

                if (configJson["MXviewToken"] == null)
                    return "MXviewToken node not found!";

                if (configJson["MXJobEnable"] == null)
                    return "MXJobEnable node not found!";

                if (configJson["MXDevList"] == null)
                    return "MXDevList node not found!";

                MulticastIP = configJson["multicast_ip"]!.ToString();
                BackendIZUBaseUrl = configJson!["izu_backend"]!.ToString();
                DeviceTableFrom = configJson["usecsv"] != null ? "localcsv" : "db";
                MapVersion = configJson!["map_version"]!.ToString();
                PortNanoCommandServer = Int32.Parse(configJson!["PortNanoCommandServer"]!.ToString());
                PortMulticastFullDataServer = Int32.Parse(configJson!["PortMulticastFullDataServer"]!.ToString());
                IntervalMulticastFullDataServer = Int32.Parse(configJson!["IntervalMulticastFullDataServer"]!.ToString());
                PortDataSend = Int32.Parse(configJson!["PortDataSend"]!.ToString());
                IntervalDataSend = Int32.Parse(configJson!["IntervalDataSend"]!.ToString());
                izuId = Int32.Parse(configJson!["izuId"]!.ToString());
                MXviewHost = configJson!["MXviewHost"]!.ToString();
                MXviewToken = configJson!["MXviewToken"]!.ToString();
                MXJobEnable = configJson!["MXJobEnable"]!.ToString();
                MXDevList = JsonConvert.DeserializeObject<List<MXDev>>(configJson!["MXDevList"]!.ToString());
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

        public static string ToString()
        {
            xPrint printer = new();
            printer.AppendLine($"server endpoint:{IZUConfig.Server}");
            printer.AppendLine($"izu id:{IZUConfig.izuId}");
            printer.AppendLine($"izu backend:{IZUConfig.BackendIZUBaseUrl}");
            printer.AppendLine($"map version:{IZUConfig.MapVersion}");
            printer.AppendLine($"data port:{IZUConfig.PortDataSend}");
            printer.AppendLine($"command port:{IZUConfig.PortNanoCommandServer}");
            printer.AppendLine($"multicast ip:{IZUConfig.MulticastIP}");
            printer.AppendLine($"multicast(json) port:{IZUConfig.PortMulticastFullDataServer}");
            printer.AppendLine($"multicast(json) interval:{IZUConfig.IntervalMulticastFullDataServer} ms");
            printer.AppendLine($"oso channel:{IZUConfig.OSOChannel}");
            printer.AppendLine($"ws push interval:{IZUConfig.PublishMillionSeconds} ms");
            printer.AppendLine($"variables:{IZUConfig.DeviceTableFrom}");
            return printer.ToString();
        }
    }

    public class MXDev
    {
        public string Name { get; set; }

        public string MXDevIP { get; set; }

        public string IsOHT { get; set; }
    }
}

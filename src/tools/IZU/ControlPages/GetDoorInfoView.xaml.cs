using Newtonsoft.Json.Linq;
using NNanomsg.Protocols;
using OHTC.Tools.Tools;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;

namespace OHTC.Tools.ControlPages
{
    public interface IAutoDoorOption
    {
        Dispatcher CurrentDispatcher { get; }
        void Enable(bool enabled);
        void SetAutoSpeed(short speed);
        void SetJogSpeed(short speed);
        void SetPosOpen(short position);
        void SetPosClose(short position);
        void JogOpenStart();
        void JogOpenEnd();
        void JogCloseStart();
        void JogCloseEnd();
        short PositionOpened { get; set; }
        short PositionClosed { get; set; }
        float PositionCurrent { get; set; }
        short SpeedAuto { get; set; }
        short SpeedJog { get; set; }
        string SelectedDoorName { get; }
    }
    public partial class GetDoorInfoView : INotifyPropertyChanged, IAutoDoorOption
    {
        public event PropertyChangedEventHandler? PropertyChanged = delegate { };
        protected void FirePropertyChanged(string propName)
        {
            PropertyChanged!(this, new PropertyChangedEventArgs(propName));
        }
        #region Properties

        private short positionOpened;
        public short PositionOpened { get { return positionOpened; } set { positionOpened = value; FirePropertyChanged("PositionOpened"); } }

        private short positionClosed;
        public short PositionClosed { get { return positionClosed; } set { positionClosed = value; FirePropertyChanged("PositionClosed"); } }

        private float positionCurrent;
        public float PositionCurrent { get { return positionCurrent; } set { positionCurrent = value; FirePropertyChanged("PositionCurrent"); } }

        private short speedAuto;
        public short SpeedAuto { get { return speedAuto; } set { speedAuto = value; FirePropertyChanged("SpeedAuto"); } }

        private short speedJog;
        public short SpeedJog { get { return speedJog; } set { speedJog = value; FirePropertyChanged("SpeedJog"); } }

        public string SelectedDoorName { get { return $"{cb_device.SelectedItem}"; } }

        public Dispatcher CurrentDispatcher { get { return Dispatcher; } }

        #endregion
        public GetDoorInfoView()
        {
            InitializeComponent();
            Loaded += GetDoorInfoView_Loaded;

            DataServer.Instance.SetOptions(this);
        }


        private void GetDoorInfoView_Loaded(object sender, RoutedEventArgs e)
        {
            _ = Task.Factory.StartNew(async () =>
            {
                ConfigSetting.Load();
                using (HttpClient httpClient = new())
                {
                    try
                    {
                        string flag = string.Empty;
                        httpClient.Timeout = TimeSpan.FromSeconds(2);
                        httpClient.BaseAddress = new Uri($"http://{ConfigSetting.izu_backend}:8030");
                        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                        HttpResponseMessage response = await httpClient.GetAsync($"/v1.0/izu/list");
                        if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                        {
                            /*
                               {"data":[{"wspub_interval":"100","ip":"192.168.127.101:8031","id":3,"create_time":"2023-12-21 10:43:36","update_time":"2023-12-29 13:59:50","status":"enabled"}],"ok":true,"message":null,"totalItems":null,"pageNum":0,"pageSize":0}
                            */
                            string result = await response.Content.ReadAsStringAsync();
                            JObject json = JObject.Parse(result);
                            if (json["ok"] != null && !(bool)json["ok"]!)
                            {
                                LogResult("IZU Backend", $"failed to read izu list: ok={json["ok"]}");
                                return;
                            }
                            if (json["data"] == null)
                            {
                                LogResult("IZU Backend", $"failed to read izu list: data is null");
                                return;
                            }
                            JArray arr = json["data"] as JArray;
                            if (!arr.HasValues)
                            {
                                LogResult("IZU Backend", $"failed to read izu list: data is empty");
                                return;
                            }
                            await Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, () =>
                            {
                                var list = from x in arr select x["ip"];
                                foreach (var item in list)
                                {
                                    if (item == null) continue;
                                    if (!IPEndPoint.TryParse(item.ToString(), out var ipend))
                                        continue;

                                    cb_izu_addr.Items.Add($"{ipend.Address.ToString()}:8231");
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LogResult("IZU Backend", $"failed to read izu list: {ex.Message}");
                    }
                }
            });
        }

        void LogResult(string tag, string resultText)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, () =>
            {

                lb.Items.Insert(0, $"{lb.Items.Count + 1}.  [{tag}] {resultText}");
            });
        }

        private void Get_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tb_point.Text.Trim()))
                return;
            //QQQ:VER_1,SRC_0:DES_0,Q1:IP_192.168.127.101:PORT_8231:DOOR_AD01,XXX
            string msg = $"QQQ:VER_1,SRC_0:DES_0,Q1:IZUPOINT_{tb_point.Text.Trim()},XXX";
            string result = SendCommand(msg, ConfigSetting.IZUBackend.ToString());
            if (string.IsNullOrEmpty(result))
                return;

            //ConfigSetting.
            bt_send_open.IsEnabled = false;
            bt_send_open.Tag = string.Empty;
            bt_send_close.IsEnabled = false;
            bt_send_close.Tag = string.Empty;
            if (!OMessageHelper.TryParse(result, out var oMessage))
                LogResult("izu backend", $"{oMessage.Error}. {result}");
            else
            {
                LogResult("izu backend", result);
                if (!oMessage.Args.TryGetValue("IP", out string? ip))
                    LogResult("izu backend", "");
                if (!oMessage.Args.TryGetValue("PORT", out string? _port))
                    LogResult("izu backend", "");
                if (!oMessage.Args.TryGetValue("DOOR", out string? door))
                    LogResult("izu backend", "");
                if (IPAddress.TryParse(ip, out IPAddress? izuAddr))
                {
                    if (int.TryParse(_port, out int port))
                        ConfigSetting.SetIZU(ip, port);
                }
                bt_send_open.Tag = $"Open>>>door:{door}";
                bt_send_close.Tag = $"Close>>>door:{door}";

                bt_send_open.IsEnabled = true;
                bt_send_close.IsEnabled = true;
            }
        }
        private void SendOpen_Click(object sender, RoutedEventArgs e)
        {
            SendCommand(bt_send_open.Tag.ToString().Trim(), ConfigSetting.IZU.ToString());
        }
        private void SendClose_Click(object sender, RoutedEventArgs e)
        {
            SendCommand(bt_send_close.Tag.ToString().Trim(), ConfigSetting.IZU.ToString());
        }
        private void Connect_IZU_Click(object sender, RoutedEventArgs e)
        {
            string result = SendCommand("Info>>>");
            string[] devices = result.Split(',');
            cb_device.ItemsSource = devices;

            _ = SendCommand($"Online>>>ip:{ConfigSetting.data_server_address};port:{ConfigSetting.data_server_port}");
        }
        private void GetError_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            string err = SendCommand($"Error>>>door:{SelectedDoorName}").PadLeft(8, '0');
            tb_err.Text = ErrText.GetErrText(err);
        }

        private void Init_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            SendCommand($"Init>>>door:{SelectedDoorName}");
        }
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            SendCommand($"Reset>>>door:{SelectedDoorName}");
        }
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            SendCommand($"Stop>>>door:{SelectedDoorName}");
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            SendCommand($"Open>>>door:{SelectedDoorName}");
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            SendCommand($"Close>>>door:{SelectedDoorName}");
        }
        private void State_Click(object sender, RoutedEventArgs e)
        {
            string state = SendCommand($"State>>>door:{SelectedDoorName}");
            txt_state.Text = $"{SelectedDoorName}={state}";
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            AutoDoorSettingsWindow wind = new();
            wind.Title = $"{wind.Title} - {SelectedDoorName}";
            wind.Owner = Application.Current.MainWindow;
            wind.SetOption(this);
            wind.Show();
        }
        private void Delay_Click(object sender, RoutedEventArgs e)
        {
            SendCommand($"Delay>>>");
        }

        public void Enable(bool enabled)
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            SendCommand($"SetEnable>>>door:{SelectedDoorName};value:{enabled}");
        }

        public void SetAutoSpeed(short speed)
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            if (speed > 400) speed = 400;
            SendCommand($"SetAutoSpeed>>>door:{SelectedDoorName};value:{speed}");
        }

        public void SetJogSpeed(short speed)
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            if (speed > 100) speed = 100;
            SendCommand($"SetJogSpeed>>>door:{SelectedDoorName};value:{speed}");
        }

        public void SetPosOpen(short position)
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            SendCommand($"SetPositionOpen>>>door:{SelectedDoorName};value:{position}");
        }

        public void SetPosClose(short position)
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            SendCommand($"SetPositionClose>>>door:{SelectedDoorName};value:{position}");
        }

        public void JogOpenStart()
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            SendCommand($"JogOpen>>>door:{SelectedDoorName};jog:true");
        }
        public void JogOpenEnd()
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            SendCommand($"JogOpen>>>door:{SelectedDoorName};jog:false");
        }
        public void JogCloseStart()
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            SendCommand($"JogClose>>>door:{SelectedDoorName};jog:true");
        }
        public void JogCloseEnd()
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            SendCommand($"JogClose>>>door:{SelectedDoorName};jog:false");
        }

        string SendCommand(string command, string ipEnd = "")
        {
            string ipAddressPort = string.Empty;

            if (string.IsNullOrEmpty(ipEnd))
            {
                string ipendpoint = "127.0.0.1:8231";

                if (!string.IsNullOrEmpty(cb_izu_addr.Text))
                {
                    ipendpoint = cb_izu_addr.Text;
                }
                if (!IPEndPoint.TryParse($"{ipendpoint}", out var ipend))
                {
                    LogResult("IZU Server", "IP Address and Port is incorrect");
                    return string.Empty;
                }
                ipAddressPort = ipend.ToString();
            }
            else
            {
                ipAddressPort = ipEnd;
            }

            using RequestSocket requestSocket = new RequestSocket();
            requestSocket.Options.SendTimeout = TimeSpan.FromSeconds(5);
            requestSocket.Options.ReceiveTimeout = TimeSpan.FromSeconds(5);
            requestSocket.Connect($"tcp://{ipAddressPort}");

            requestSocket.Send(System.Text.Encoding.UTF8.GetBytes(command));
            byte[] buffer = requestSocket.Receive();
            if (buffer != null)
            {
                string state = System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                LogResult("IZU Server", state);
                return state;
            }
            else
            {
                LogResult("Timeout", "NULL");
                return string.Empty;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using (TcpClient client = new TcpClient("192.168.127.153", 12346))
            {
                var stream = client.GetStream();

                String orig = "G0_111-0";

                string s = $"QQQ:VER_1,SRC_0:DES_0,G1_111-0,XXX";
                byte[] cmd = Encoding.ASCII.GetBytes(s);
                try
                {
                    stream?.Write(cmd, 0, cmd.Length);
                
                }
                catch (Exception ex)
                {
                }
            }
        }
    }




    public class OMessage
    {
        public string MessageSource { get; set; }
        public string FunctionName { get; set; }
        public string Src { get; set; }
        public string Des { get; set; }
        public string Error { get; set; }
        public bool State { get { return string.IsNullOrEmpty(Error); } }
        public IDictionary<string, string> Args { get; set; }
        public OMessage()
        {
            MessageSource = string.Empty;
            FunctionName = string.Empty;
            Args = new Dictionary<string, string>();
        }
    }
    public class OMessageHelper
    {
        private const char LEVEL_1_SPLITTER = ':';
        private const char LEVEL_2_SPLITTER = ',';
        private const char LEVEL_3_SPLITTER = '_';

        public const string DUMMY = "NULL";
        const string bodyRegex = @"QQQ:\w|,*,XXX";
        public static bool TryParse(string source, out OMessage oMessage)
        {
            oMessage = new OMessage();
            if (string.IsNullOrEmpty(source))
            {
                oMessage.Error = "message is empty";
                return false;
            }
            if (!Regex.IsMatch(source, bodyRegex))
            {
                oMessage.Error = "message format is incorrect: " + source + " . format should be QQQ:name:key1_value,key2_value:XXX";
                return false;
            }
            try
            {
                string body = source[4..(source.Length - 4)];
                string[] funcArr = body.Split(LEVEL_2_SPLITTER);
                if (funcArr.Length < 3)
                {
                    oMessage.Error = "func format is incorrect: " + source + " . format should be  name:key1_value:key2_value";
                    return false;
                }
                string version = funcArr[0];
                string src_des = funcArr[1];
                oMessage.FunctionName = funcArr[2];
                if (oMessage.FunctionName.Trim().ToUpper() == "NULL")
                {
                    oMessage.Error = "internal server error";
                    return false;
                }
                string[] func = oMessage.FunctionName.Split(LEVEL_1_SPLITTER);
                if (func.Length < 2)
                {
                    oMessage.Error = "func format is incorrect: " + source + " . format should be  name:key1_value:key2_value";
                    return false;
                }
                oMessage.FunctionName = func[0];
                string[] args = func.Skip(1).ToArray();
                foreach (string a in args)
                {
                    string[] kv = a.Split(LEVEL_3_SPLITTER);
                    if (kv.Length < 2)
                        continue;
                    oMessage.Args[kv[0]] = kv[1];
                }
                return true;
            }
            catch (Exception ex)
            {
                oMessage.Error = $"message format is incorrect: {ex.Message}. {ex.StackTrace}";
                return false;
            }
        }

        public static string Build(string body, string version = "VER_1", string src = "0", string des = "0")
        {
            return $"QQQ:{version},SRC_{src}:DES_{des},{body},XXX";
        }
    }
    public static class ConfigSetting
    {
        /// <summary>
        /// 本机数据服务IP地址
        /// 不能为localhost 或者 127.0.0.1
        /// 用于启动 TCP Server 
        /// 启动后 izu 会自动连接上此服务, 推送服务数据
        /// </summary>
        public static string data_server_address = "127.0.0.1";
        /// <summary>
        /// 本机数据服务端口
        /// </summary>
        public static int data_server_port = 8131;

        /// <summary>
        /// izu backend IP地址
        /// </summary>
        public static string izu_backend = "127.0.0.1";
        /// <summary>
        /// izu backend TCP通讯端口
        /// </summary>
        public static int izu_backend_port = 8032;


        /// <summary>
        /// izu IP地址
        /// </summary>
        public static string izu_server = "127.0.0.1";
        /// <summary>
        /// izu TCP通讯端口
        /// </summary>
        public static int izu_server_port = 8231;

        public static IPEndPoint SetIZU(string ip, int port)
        {
            izu_server_port = port;

            if (IPAddress.TryParse(ip, out IPAddress? addr))
            {
                izu_server = ip;
                return new IPEndPoint(addr, izu_server_port);
            }

            return new IPEndPoint(IPAddress.Any, izu_server_port);
        }

        public static IPEndPoint IZUBackend
        {
            get
            {
                if (IPAddress.TryParse(izu_backend, out IPAddress? ip))
                    return new IPEndPoint(ip, izu_backend_port);
                else
                    return new IPEndPoint(IPAddress.Any, izu_backend_port);
            }
        }
        public static IPEndPoint IZU
        {
            get
            {
                if (IPAddress.TryParse(izu_server, out IPAddress? ip))
                    return new IPEndPoint(ip, izu_server_port);
                else
                    return new IPEndPoint(IPAddress.Any, izu_server_port);
            }
        }

        static IPAddress? ToIP(this string ip)
        {
            if (IPAddress.TryParse(ip, out IPAddress? addr))
            {
                return addr;
            }
            return null;
        }
        public static void Save()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine($"{data_server_address}:{data_server_port}");
            text.AppendLine($"{izu_backend}:{izu_backend_port}");
            System.IO.File.WriteAllText("ip.config", text.ToString());
        }

        public static void Load()
        {
            if (!System.IO.File.Exists("ip.config"))
                return;
            string[] lines = System.IO.File.ReadAllLines("ip.config");

            if (IPEndPoint.TryParse(lines[0], out IPEndPoint? ipend))
            {
                data_server_address = ipend.Address.ToString();
                data_server_port = ipend.Port;

                if (string.IsNullOrEmpty(data_server_address))
                {
                    var addressList = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList;
                    var ip = addressList.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString();
                    data_server_address = ip;
                }
            }

            if (IPEndPoint.TryParse(lines[1], out IPEndPoint? ipend1))
            {
                izu_backend = ipend1.Address.ToString();
                izu_backend_port = ipend1.Port;
            }
        }
    }
}

using NNanomsg.Protocols;
using System.ComponentModel;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
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
        DataServer dataServer;
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
            Unloaded += delegate
            {
                if (dataServer != null)
                    dataServer.Stop();
            };

            RunServer();

            var addressList = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList;
            var ip = addressList.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString();
            tb_local_addr.Text = ip;
        }
        void LogResult(string tag, string resultText)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, () =>
            {

                lb.Items.Insert(0, $"{lb.Items.Count + 1}.  [{tag}] {resultText}");
            });
        }
        void RunServer()
        {
            dataServer = new DataServer(IPAddress.Any, 8131);
            dataServer.SetDoorOption(this);
            dataServer.Start();
        }

        private void Get_Click(object sender, RoutedEventArgs e)
        {
            string ipAddressPort = string.Empty;
            string address = "127.0.0.1";
            int port = 8032;

            if (!string.IsNullOrEmpty(tb_izu_back_addr.Text))
            {
                address = tb_izu_back_addr.Text;
            }
            if (!string.IsNullOrEmpty(tb_izu_back_port.Text))
            {
                int.TryParse(tb_izu_back_port.Text, out port);
            }
            if (!IPEndPoint.TryParse($"{address}:{port}", out var ipend))
            {
                LogResult("IZU Server", "IP Address and Port is incorrect");
                return;
            }
            ipAddressPort = ipend.ToString();
            if (string.IsNullOrEmpty(tb_point.Text.Trim()))
                return;

            //QQQ:VER_1,SRC_0:DES_0,Q1:IP_192.168.127.101:PORT_8231:DOOR_AD01,XXX
            string msg = $"QQQ:VER_1,SRC_0:DES_0,Q1:IZUPOINT_{tb_point.Text.Trim()},XXX";
            string result = SendCommand(msg, ipAddressPort);
            if (string.IsNullOrEmpty(result))
                return;
            tb_izu_addr1.Text = string.Empty;
            tb_izu_port1.Text = string.Empty;
            tb_cmd_open.Text = string.Empty;
            tb_cmd_close.Text = string.Empty;
            if (!OMessageHelper.TryParse(result, out var oMessage))
                LogResult("izu backend", $"{oMessage.Error}. {result}");
            else
            {
                LogResult("izu backend", result);
                if (!oMessage.Args.TryGetValue("IP", out string? ip))
                    LogResult("izu backend", "");
                if (!oMessage.Args.TryGetValue("PORT", out string? _port))
                    LogResult("izu backend", "");
                tb_izu_addr1.Text = ip;
                tb_izu_port1.Text = _port.ToString();
                if (!oMessage.Args.TryGetValue("DOOR", out string? door))
                    LogResult("izu backend", "");

                tb_cmd_open.Text = $"Open>>>door:{door}";
                tb_cmd_close.Text = $"Close>>>door:{door}";
            }
        }
        private void SendOpen_Click(object sender, RoutedEventArgs e)
        {

            if (!IPEndPoint.TryParse($"{tb_izu_addr1.Text}:{tb_izu_port1.Text}", out IPEndPoint? endP))
            {
                LogResult("IZU Server", "IP Address and Port is incorrect");
                return;
            }
            if (string.IsNullOrEmpty(tb_cmd_open.Text.Trim()))
                return;

            SendCommand(tb_cmd_open.Text.Trim(), endP.ToString());
        }
        private void SendClose_Click(object sender, RoutedEventArgs e)
        {
            if (!IPEndPoint.TryParse($"{tb_izu_addr1.Text}:{tb_izu_port1.Text}", out IPEndPoint? endP))
            {
                LogResult("IZU Server", "IP Address and Port is incorrect");
                return;
            }
            if (string.IsNullOrEmpty(tb_cmd_close.Text.Trim()))
                return;

            SendCommand(tb_cmd_close.Text.Trim(), endP.ToString());
        }
        private void GetDevices_Click(object sender, RoutedEventArgs e)
        {
            string result = SendCommand("Info>>>");
            string[] devices = result.Split(',');
            cb_device.ItemsSource = devices;

            _ = SendCommand($"Online>>>ip:{tb_local_addr.Text}");
        }
        private void GetError_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedDoorName))
                return;
            tb_err.Text = SendCommand($"Error>>>door:{SelectedDoorName}");
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



        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            AutoDoorSettingsWindow wind = new();
            wind.Title = $"{wind.Title} - {SelectedDoorName}";
            wind.Owner = Application.Current.MainWindow;
            wind.SetOption(this);
            wind.ShowDialog();
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
                string address = "127.0.0.1";
                int port = 8231;

                if (!string.IsNullOrEmpty(tb_izu_addr.Text))
                {
                    address = tb_izu_addr.Text;
                }
                if (!string.IsNullOrEmpty(tb_izu_port.Text))
                {
                    int.TryParse(tb_izu_port.Text, out port);
                }
                if (!IPEndPoint.TryParse($"{address}:{port}", out var ipend))
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
                if(oMessage.FunctionName.Trim().ToUpper() == "NULL")
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
}

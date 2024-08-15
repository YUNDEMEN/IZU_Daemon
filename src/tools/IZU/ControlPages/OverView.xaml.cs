using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NNanomsg.Protocols;
using OHTC.Tools.OHTCommand;
using OHTC.Tools.Tools;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OHTC.Tools.ControlPages
{
    public partial class OverView : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = delegate { }; 
        protected void FirePropertyChanged(string propName)
        {
            PropertyChanged!(this, new PropertyChangedEventArgs(propName));
        }
        public string Address { get; set; }
        public OverView()
        {
            InitializeComponent();
            Loaded += delegate
            {
                DataContext = DevicePool.Connect(Address);
            };
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string name = $"{(sender as FrameworkElement).Tag}";
            if (string.IsNullOrEmpty(name)) return;

            using RequestSocket requestSocket = new RequestSocket();
            requestSocket.Options.SendTimeout = TimeSpan.FromSeconds(5);
            requestSocket.Options.ReceiveTimeout = TimeSpan.FromSeconds(5);
            requestSocket.Connect($"tcp://{Address}:8231");

            requestSocket.Send(System.Text.Encoding.UTF8.GetBytes($"Release>>>door:{name}"));
            byte[] buffer = requestSocket.Receive();
            if (buffer != null)
            {
                string state = System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                //LogResult("IZU Server", state);
                //return state;
                Logger.Instance.WriteLine("Release", $"Door {name}, return {state}");
            }
            else
            {
                Logger.Instance.WriteLine("Timeout", string.Empty);
                //return string.Empty;
            }
        }
       
    }

}

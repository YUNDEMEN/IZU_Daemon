using System.Net;

namespace OHTC.Tools
{
    public partial class SettingsPage
    {
        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ControlPages.ConfigSetting.Load();
            tb_local_addr.Text = ControlPages.ConfigSetting.data_server_address;
            tb_local_port.Text = ControlPages.ConfigSetting.data_server_port.ToString();

            tb_izu_back_addr.Text = ControlPages.ConfigSetting.izu_backend;
            tb_izu_back_port.Text = ControlPages.ConfigSetting.izu_backend_port.ToString();
        }

        private void Apply_Clicked(object sender, System.Windows.RoutedEventArgs e)
        {
            bool hasError = false;
            IPAddress addr = IPAddress.Any;
            int port = 0;
            if (!IPAddress.TryParse(tb_local_addr.Text, out addr))
            {
                hasError = true;
            }
            if (!int.TryParse(tb_local_port.Text, out port))
            {
                port = 8132;
            }

            if(ControlPages.ConfigSetting.data_server_address != addr.ToString() || ControlPages.ConfigSetting.data_server_port != port)
            {
                DataServer.Create(addr, ControlPages.ConfigSetting.data_server_port, true);
            }
            ControlPages.ConfigSetting.data_server_address = addr.ToString();
            ControlPages.ConfigSetting.data_server_port = port;


            addr = IPAddress.Any;
            port = 0;
            if (!IPAddress.TryParse(tb_izu_back_addr.Text, out addr))
            {
                hasError = true;
            }
            if (!int.TryParse(tb_izu_back_port.Text, out port))
            {
                port = 8032;
            }

            ControlPages.ConfigSetting.izu_backend = addr.ToString();
            ControlPages.ConfigSetting.izu_backend_port = port;

            if (hasError)
            {

            }
            else
            {
                ControlPages.ConfigSetting.Save();
            }
        }
    }
}

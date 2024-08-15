using OHTC.Tools.ControlPages;
using System.Configuration;
using System.Data;
using System.Net;
using System.Text;
using System.Windows;

namespace OHTC.Tools
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool RepositoryImageMode { get; set; } = false;
        public App()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ConfigSetting.Load();           
        }
    }

}

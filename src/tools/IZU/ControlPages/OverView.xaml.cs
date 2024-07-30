using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OHTC.Tools.OHTCommand;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OHTC.Tools.ControlPages
{
    public partial class OverView : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = delegate { }; 
        protected void FirePropertyChanged(string propName)
        {
            PropertyChanged!(this, new PropertyChangedEventArgs(propName));
        }
        public OverView()
        {
            InitializeComponent();
            Loaded += delegate
            {
                DataContext = DevicePool.Instance;
            };
        }

    }

}

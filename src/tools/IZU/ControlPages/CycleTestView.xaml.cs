using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OHTC.Tools.OHTCommand;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OHTC.Tools.ControlPages
{
    public partial class CycleTestView : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = delegate { }; 
        protected void FirePropertyChanged(string propName)
        {
            PropertyChanged!(this, new PropertyChangedEventArgs(propName));
        }
        public CycleTestView()
        {
            InitializeComponent();
            Loaded += delegate
            {
                DataContext = OhtCycle.Instance;
            };
        }

        private void Schedule_Clicked(object sender, RoutedEventArgs e)
        {
            TransferCommandEditor wind = new();
            wind.Owner = Application.Current.MainWindow;
            wind.DataContext = OhtCycle.Instance;
            wind.OnSave += (s, config) =>
            {
                OhtCycle.Instance.Reload(config);
            };
            wind.ShowDialog();
        }


        private void Start_Clicked(object sender, RoutedEventArgs e)
        {
            FrameworkElement? ctrl = sender as FrameworkElement;
            OhtModel? ohtModel = ctrl!.DataContext as OhtModel;
            if (ohtModel == null) return;

            ohtModel.Init();
            ohtModel.RunTask();
        }

        private void Stop_Clicked(object sender, RoutedEventArgs e)
        {
            FrameworkElement? ctrl = sender as FrameworkElement;
            OhtModel? ohtModel = ctrl!.DataContext as OhtModel;
            if (ohtModel == null) return;

            ohtModel.Cancel();
        }
    }

}

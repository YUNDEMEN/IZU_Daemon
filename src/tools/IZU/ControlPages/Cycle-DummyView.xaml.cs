using System.ComponentModel;
using System.Net;
using System.Windows;

namespace OHTC.Tools.ControlPages
{
    public partial class DummyCycleView : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = delegate { };
        public DummyCycleView()
        {
            InitializeComponent();
            Loaded += delegate
            {
                DataContext = DummyCycleTest.Instance;
            };
        }

        private void Cancel_Transfer_Click(object sender, RoutedEventArgs e)
        {
            DummyCycleTest.Instance.Stop();
        }

        private void Go_Click(object sender, RoutedEventArgs e)
        {
            DataContext = DummyCycleTest.Instance;
            if (!IPEndPoint.TryParse(tb_ip.Text, out var ip))
                return;
            DummyCycleTest.Instance.Connect(ip.ToString());
            DummyCycleTest.Instance.StartCycleTest();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tb_point_from.Text) || string.IsNullOrEmpty(tb_point_to.Text)
                || !int.TryParse(tb_sec.Text, out var sec))
                return;

            DummyCycleTest.Instance.AddCommand(tb_point_from.Text, tb_point_to.Text, sec);
        }
    }

}

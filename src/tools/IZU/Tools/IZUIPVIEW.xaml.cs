using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OHTC.Tools.Tools
{
    /// <summary>
    /// IZUIPVIEW.xaml 的交互逻辑
    /// </summary>
    public partial class IZUIPVIEW : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = delegate { };
        protected void FirePropertyChanged(string propName)
        {
            PropertyChanged!(this, new PropertyChangedEventArgs(propName));
        }
        private object content;
        public object Content { get { return $"{ip.Text}"; } set { content = value; FirePropertyChanged("Content"); } }
        public IZUIPVIEW()
        {
            InitializeComponent();
        }
        public override string ToString()
        {
            return $"{ip.Text}";
        }
    }

    public class IZUITEMVIEW: NavigationViewItem
    {

    }
}

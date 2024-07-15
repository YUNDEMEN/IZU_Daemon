using System.Windows.Media;

namespace OHTC.Tools.ControlPages
{
    public partial class FlipViewPage
    {
        public FlipViewPage()
        {
            InitializeComponent();

            FlipView2.ItemsSource = typeof(Colors).GetProperties().Select(p => p.Name).ToList();
        }
    }
}

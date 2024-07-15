using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OHTC.Tools
{
    /// <summary>
    /// Interaction logic for WindowWithCustomTitleBar.xaml
    /// </summary>
    public partial class TransferScheduleWindow : Window
    {
        public TransferScheduleWindow()
        {
            InitializeComponent();
            Loaded += delegate
            {
                //DataContext = TransferCycleTest.Instance;
            };
        }
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tb_from.Text))
                return;
            else if (string.IsNullOrEmpty(tb_to.Text))
                return;
            else if (int.TryParse(tb_sec.Text.Trim(), out int sec))
            {
                //TransferCycleTest.Instance.AddCommand(tb_from.Text.ToUpper().Trim(), tb_to.Text.ToUpper().Trim(), OHT_ID.Text.Trim().ToUpper(), sec);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("delete?", "alert", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;
            object clicked = (e.OriginalSource as FrameworkElement).DataContext;
            var lbi = lb.ItemContainerGenerator.ContainerFromItem(clicked) as ListBoxItem;
            //TransferCycleTest.Instance.DeleteCommand(lbi.Content as TransferCommandTest);
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            //TransferCycleTest.Init(this.Dispatcher);
        }
    }
}

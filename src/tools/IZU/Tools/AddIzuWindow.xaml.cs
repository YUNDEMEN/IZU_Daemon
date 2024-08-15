using System;
using System.Collections.Generic;
using System.Net;
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
    public partial class AddIzuWindow : Window
    {
        public event EventHandler<string> OnAdd=delegate { };
        public AddIzuWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (IPAddress.TryParse(tb_izu_addr.Text, out var addr)) {
                OnAdd(this, addr.ToString());
            }
        }
    }
}

using MahApps.Metro.Controls;
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
    public partial class AutoDoorSettingsWindow : Window
    {
        OHTC.Tools.ControlPages.IAutoDoorOption doorOption { get; set; }
        public AutoDoorSettingsWindow()
        {
            InitializeComponent();
        }
        public void SetOption(OHTC.Tools.ControlPages.IAutoDoorOption doorOption)
        {
            DataContext = doorOption;
            this.doorOption = doorOption;
        }


        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ModernWpf.Controls.ToggleSwitch c = sender as ModernWpf.Controls.ToggleSwitch;
            doorOption.Enable(c.IsOn);
        }

        private void SetAutoSpeed(object sender, RoutedEventArgs e)
        {
            if (short.TryParse(tb_speed_auto.Text, out var speed))
            {
                doorOption.SetAutoSpeed(speed);
            }
        }

        private void SetJogSpeed(object sender, RoutedEventArgs e)
        {
            if (short.TryParse(tb_speed_jog.Text, out var speed))
            {
                doorOption.SetJogSpeed(speed);
            }
        }

        private void SetOpenPos(object sender, RoutedEventArgs e)
        {
            if (short.TryParse(tb_open_pos.Text, out var position))
            {
                doorOption.SetPosOpen(position);
            }
        }

        private void SetClosePos(object sender, RoutedEventArgs e)
        {
            if (short.TryParse(tb_close_pos.Text, out var position))
            {
                doorOption.SetPosClose(position);
            }
        }

        private void JogOpen_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            doorOption.JogOpenStart();
        }
        private void JogOpen_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            doorOption.JogOpenEnd();
        }
        private void JogClose_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

            doorOption.JogCloseStart();
        }
        private void JogClose_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            doorOption.JogCloseEnd();
        }

    }
}

using System.Windows;

namespace OHTC.Tools
{
    public partial class AutoDoorSettingsWindow : Window
    {
        OHTC.Tools.ControlPages.IAutoDoorOption doorOption { get; set; }
        public AutoDoorSettingsWindow()
        {
            InitializeComponent();
        }
        public void SetOption(OHTC.Tools.ControlPages.IAutoDoorOption doorOption,string address)
        {
            DataContext = DevicePool.Connect(address).GetAutoDoor(doorOption.SelectedDoorName);
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
                if (speed > 450) speed = 450;
                doorOption.SetAutoSpeed(speed);
            }
        }

        private void SetJogSpeed(object sender, RoutedEventArgs e)
        {
            if (short.TryParse(tb_speed_jog.Text, out var speed))
            {
                if (speed > 50) speed = 50;
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

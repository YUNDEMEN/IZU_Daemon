using IZUTools.ReletiveClasses;
using IZUTools.Views;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TinyCsvParser;

namespace IZUTools
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<DeviceData> DeviceDataCollection { get; set; }
    
        public MainWindow()
        {
            InitializeComponent();
            DeviceDataCollection = new ObservableCollection<DeviceData>();
            DataContext = this;
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "csv files (*.csv)|*.csv";
            ofd.Multiselect = true;
            if (!(bool)ofd.ShowDialog())
                return;
            DeviceDataCollection.Clear();
            foreach (var file in ofd.FileNames)
            {
                CsvParserOptions csvParserOptions = new(true, ',');
                DeviceDataMapping deviceMapper = new();
                CsvParser<DeviceData> deviceParser = new(csvParserOptions, deviceMapper);
                using FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                {
                    var csvs = deviceParser.ReadFromStream(stream, Encoding.GetEncoding("GB2312"));
                    foreach (var item in csvs)
                    {
                        if (!item.IsValid || item.Error != null)
                        {
                            continue;
                        }
                        DeviceDataCollection.Add(item.Result);
                    }
                }
            }

            AddressView view = new AddressView();
            view.DataContext = this;
            ToolContent.Content= view;
        }
    }
}
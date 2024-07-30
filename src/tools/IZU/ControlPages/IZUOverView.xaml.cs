using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OHTC.Tools.OHTCommand;
using System.ComponentModel;
using System.Net.Http;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NNanomsg.Protocols;

namespace OHTC.Tools.ControlPages
{
    public partial class IZUOverView : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = delegate { }; 
        protected void FirePropertyChanged(string propName)
        {
            PropertyChanged!(this, new PropertyChangedEventArgs(propName));
        }
        public IZUOverView()
        {
            InitializeComponent();
            Loaded += IZUOverView_Loaded;
        }

        private void IZUOverView_Loaded(object sender, RoutedEventArgs e)
        {
            _ = Task.Factory.StartNew(async () =>
            {
                ConfigSetting.Load();
                using (HttpClient httpClient = new())
                {
                    try
                    {
                        string flag = string.Empty;
                        httpClient.Timeout = TimeSpan.FromSeconds(2);
                        httpClient.BaseAddress = new Uri($"http://{ConfigSetting.izu_backend}:8030");
                        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                        HttpResponseMessage response = await httpClient.GetAsync($"/v1.0/izu/list");
                        if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                        {
                            /*
                               {"data":[{"wspub_interval":"100","ip":"192.168.127.101:8031","id":3,"create_time":"2023-12-21 10:43:36","update_time":"2023-12-29 13:59:50","status":"enabled"}],"ok":true,"message":null,"totalItems":null,"pageNum":0,"pageSize":0}
                            */
                            string result = await response.Content.ReadAsStringAsync();
                            JObject json = JObject.Parse(result);
                            if (json["ok"] != null && !(bool)json["ok"]!)
                            {
                                return;
                            }
                            if (json["data"] == null)
                            {
                                return;
                            }
                            JArray arr = json["data"] as JArray;
                            if (!arr.HasValues)
                            {
                                return;
                            }
                            await Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, () =>
                            {
                                var list = from x in arr select x["ip"];
                                foreach (var item in list)
                                {
                                    if (item == null) continue;
                                    if (!IPEndPoint.TryParse(item.ToString(), out var ipend))
                                        continue;

                                    izu_list.Items.Add($"{ipend.Address.ToString()}");
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        //LogResult("IZU Backend", $"failed to read izu list: {ex.Message}");
                    }
                }
            });
        }


        private void izu_list_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if(e.LeftButton== System.Windows.Input.MouseButtonState.Pressed)
            {
                if (izu_list.SelectedItem == null) return;

                var item = izu_list.SelectedItem;

                using RequestSocket requestSocket = new RequestSocket();
                requestSocket.Options.SendTimeout = TimeSpan.FromSeconds(5);
                requestSocket.Options.ReceiveTimeout = TimeSpan.FromSeconds(5);
                requestSocket.Connect($"tcp://{izu_list.SelectedItem}:8231");

                requestSocket.Send(System.Text.Encoding.UTF8.GetBytes($"Online>>>ip:{ConfigSetting.data_server_address};port:{ConfigSetting.data_server_port}"));
                byte[] buffer = requestSocket.Receive();
                if (buffer != null)
                {
                    string state = System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length);

                    this.NavigationService.Navigate(new OverView());
                }
                else
                {

                }
            }
        }
    }

}

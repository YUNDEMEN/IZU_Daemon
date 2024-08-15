using ModernWpf.Controls;
using ModernWpf.Navigation;
using Newtonsoft.Json.Linq;
using OHTC.Tools.ControlPages;
using OHTC.Tools.Tools;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Navigation;

namespace OHTC.Tools
{
    public partial class MainWindow : Window
    {
        IDictionary<string, DataClient> dataClients;
        public void Online(string address)
        {
            if (dataClients.ContainsKey(address))
            {
                dataClients[address].DisconnectAndStop();
                dataClients.Remove(address);
            }
            DataClient dataClient = new DataClient(address, ConfigSetting.data_server_port);
            dataClient.ConnectAsync();
            dataClients.Add(address, dataClient);
            DevicePool.Connect(address);
        }
        public MainWindow()
        {
            InitializeComponent();
            Logger.OnLogged += Logger_OnLogged;
            ConfigSetting.ConfigUpdated += ConfigSetting_ConfigUpdated;
            dataClients = new Dictionary<string, DataClient>();

            if (OHTC.Tools.App.RepositoryImageMode)
            {
                Width = 1280;
                Height = 640;
                NavView.ClearValue(PaddingProperty);
                NavView.ClearValue(NavigationView.HeaderTemplateProperty);
                NavView.MenuItems.RemoveAt(NavView.MenuItems.Count - 1);
            }

            GetIZUList();
            //NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First();
            //Navigate(NavView.SelectedItem);

            Loaded += delegate
            {
                UpdateAppTitle();
            };
        }

        private void Logger_OnLogged(object? sender, string e)
        {
            Log(e);
        }

        private void ConfigSetting_ConfigUpdated(object? sender, EventArgs e)
        {
            GetIZUList();
        }

        void UpdateAppTitle()
        {
            //ensure the custom title bar does not overlap window caption controls
            Thickness currMargin = AppTitleBar.Margin;
            AppTitleBar.Margin = new Thickness(currMargin.Left, currMargin.Top, TitleBar.GetSystemOverlayRightInset(this), currMargin.Bottom);
        }

        void GetIZUList()
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
                                var menus = NavView.MenuItems.OfType<NavigationViewItem>().ToList();
                                foreach (var item in menus)
                                {
                                    if (item.Tag is OverView)
                                        NavView.MenuItems.Remove(item);
                                }

                                var list = from x in arr select new { id = $"{x["id"]}", ip = $"{x["ip"]}" };
                                int index = 1;
                                foreach (var item in list)
                                {
                                    if (item == null) continue;
                                    if (!IPEndPoint.TryParse(item.ip, out var ipend))
                                        continue;
                                    Online($"{ipend.Address}");

                                    IZUIPVIEW iZUIPVIEW = new IZUIPVIEW();
                                    iZUIPVIEW.DataContext = DevicePool.Connect($"{ipend.Address}");

                                    NavView.MenuItems.Insert(index++, new NavigationViewItem
                                    {
                                        //Content = $"{ipend.Address}",
                                        Content = iZUIPVIEW,
                                        ToolTip = $"izu id = {item.id}",
                                        Tag = new OverView { Address = $"{ipend.Address}" },
                                        Icon = new FontIcon { Glyph = "\xE703" },
                                    });
                                }
                                Logger.Instance.WriteLine("IZU Backend", $"read izu list");
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.WriteLine("IZU Backend", $"failed to read izu list: {ex.Message}");
                    }
                }
            });

        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            var nItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag == e.Content);
            if (nItem == null)
            {
                nItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => GetPageType(x) == e.SourcePageType());
            }
            NavView.SelectedItem = nItem;
        }

        private void UpdateAppTitleMargin(NavigationView sender)
        {
            const int smallLeftIndent = 4, largeLeftIndent = 24;

            Thickness currMargin = AppTitle.Margin;

            if ((sender.DisplayMode == NavigationViewDisplayMode.Expanded && sender.IsPaneOpen) ||
                     sender.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                AppTitle.Margin = new Thickness(smallLeftIndent, currMargin.Top, currMargin.Right, currMargin.Bottom);
            }
            else
            {
                AppTitle.Margin = new Thickness(largeLeftIndent, currMargin.Top, currMargin.Right, currMargin.Bottom);
            }
        }

        private void Navigate(object item)
        {
            if (item is NavigationViewItem menuItem)
            {
                Type pageType = GetPageType(menuItem);
                if (pageType == typeof(OverView))
                {
                    ContentFrame.Navigate(menuItem.Tag);
                }
                else if (pageType != null)
                    ContentFrame.Navigate(pageType);
                //if (ContentFrame.CurrentSourcePageType != pageType)
                //{
                //}
            }
        }

        private void Navigate(Type sourcePageType)
        {
            if (ContentFrame.CurrentSourcePageType != sourcePageType)
            {
                ContentFrame.Navigate(sourcePageType);
            }
        }

        private Type GetPageType(NavigationViewItem item)
        {
            if (item.Tag is OverView)
            {
                return typeof(OverView);
            }
            return item.Tag as Type;
        }
        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            ContentFrame.GoBack();
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                Navigate(typeof(SettingsPage));
            }
            else
            {
                Navigate(args.InvokedItemContainer);
            }
        }

        private void NavView_PaneOpening(NavigationView sender, object args)
        {
            UpdateAppTitleMargin(sender);
        }

        private void NavView_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        {
            UpdateAppTitleMargin(sender);
        }

        private void NavView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            Thickness currMargin = AppTitleBar.Margin;
            if (sender.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                AppTitleBar.Margin = new Thickness((sender.CompactPaneLength * 2), currMargin.Top, currMargin.Right, currMargin.Bottom);

            }
            else
            {
                AppTitleBar.Margin = new Thickness(sender.CompactPaneLength, currMargin.Top, currMargin.Right, currMargin.Bottom);
            }

            UpdateAppTitleMargin(sender);
        }

        public void Log(string message)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, () =>
            {
                lb.Items.Insert(0, $"{lb.Items.Count + 1}. {message}");
            });
        }

        private void FontIcon_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            AddIzuWindow wind = new();
            wind.Owner = Application.Current.MainWindow;
            wind.OnAdd += (s, addr) =>
            {
                NavView.MenuItems.Insert(1, new NavigationViewItem
                {
                    Content = addr,
                    Tag = new OverView { Address = addr },
                    Icon = new FontIcon { Glyph = "\xE703" },
                });
                Online(addr);
            };
            wind.ShowDialog();
        }
    }

    public class Logger
    {
        public static readonly Logger Instance = new Logger();
        public static event EventHandler<string> OnLogged = delegate { };
        public Logger() { }
        public void WriteLine(string tag, string message)
        {
            OnLogged(this, $"[{tag}] {message}");
        }
    }

}
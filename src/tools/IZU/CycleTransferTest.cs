using Newtonsoft.Json;
using NNanomsg.Protocols;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace OHTC.Tools
{
    public class TransferCycleTest : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = delegate { };
        private TransferCommandTest currentCommand;
        public TransferCommandTest CurrentCommand
        {
            get { return currentCommand; }
            set { currentCommand = value; PropertyChanged(this, new PropertyChangedEventArgs("CurrentCommand")); }
        }
        private ObservableCollection<TransferCommandTest> ohtCommandList;
        public ObservableCollection<TransferCommandTest> OhtCommandList
        {
            get { return ohtCommandList; }
            set { ohtCommandList = value; PropertyChanged(this, new PropertyChangedEventArgs("OhtCommandList")); }
        }
        private int currentListIndex;
        public int CurrentListIndex
        {
            get { return currentListIndex; }
            set { currentListIndex = value; PropertyChanged(this, new PropertyChangedEventArgs("CurrentListIndex")); }
        }
        private ObservableCollection<LogText> logs;
        public ObservableCollection<LogText> Logs
        {
            get { return logs; }
            set { logs = value; PropertyChanged(this, new PropertyChangedEventArgs("Logs")); }
        }

        Queue<TransferCommandTest> taskCommands = new Queue<TransferCommandTest>();
        Communication2OSO communication = new Communication2OSO();
        RequestSocket requestSocket;

        Dispatcher uiDispatcher;
        bool isStarted = false;
        public TransferCycleTest()
        {
            this.uiDispatcher = uiDispatcher;
            Logs = new ObservableCollection<LogText>();
            OhtCommandList = new();
            Connect();
        }

        public static TransferCycleTest Instance { get; private set; }

        public static void Init(Dispatcher uiDispatcher = null)
        {
            if (Instance == null)
            {
                Instance = new TransferCycleTest();
            }
            Instance.uiDispatcher = uiDispatcher;
            Instance.LoadCommands();
        }

        void Connect()
        {
            requestSocket = new RequestSocket();
            requestSocket.Options.SendTimeout = new TimeSpan(0, 0, 2);
            requestSocket.Options.ReceiveTimeout = new TimeSpan(0, 0, 2);
            requestSocket.Connect("tcp://ohtc.wonder-inc.cn:8024");
        }

        int LoadCommands()
        {
            taskCommands.Clear();
            OhtCommandList.Clear();
            var ohtCommands = TransferCommandTest.Load();
            foreach (var item in ohtCommands)
            {
                taskCommands.Enqueue(item);
                OhtCommandList.Add(item);
                item.SendCommandToOso+= Transfer_SendCommandToOso;
            }
            return taskCommands.Count;
        }
        public int LoadCommands(string lines)
        {
            taskCommands.Clear();
            OhtCommandList.Clear();
            var ohtCommands = TransferCommandTest.LoadFrom(lines);
            foreach (var item in ohtCommands)
            {
                taskCommands.Enqueue(item);
                OhtCommandList.Add(item);
                item.SendCommandToOso += Transfer_SendCommandToOso;
            }
            return taskCommands.Count;
        }


        public void AddCommand(string from, string to, string oht, int sec)
        {
            if (oht != null)
            {
                if (OhtCommandList.Any())
                {
                    var ohtTemp = OhtCommandList.FirstOrDefault(x => x.OHTId == oht);
                    if (ohtTemp != null)
                    {
                        MessageBox.Show("不能添加相同ID的天车");
                        return;
                    }
                }
            }

            var transfer = new TransferCommandTest(oht, from, to, sec);
            transfer.SendCommandToOso += Transfer_SendCommandToOso;
            OhtCommandList.Add(transfer);
            var data = JsonConvert.SerializeObject(OhtCommandList);
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data.txt");
            File.WriteAllText(path, data);
        }

        private void Transfer_SendCommandToOso(object? sender, CancellationTokenSource cts)
        {
            var cmd = sender as TransferCommandTest;
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    var msg = communication.ModelToJson(new OSOParameter
                    {
                        opname = string.IsNullOrEmpty(cmd.OHTId) ? OSOEnumType.WEB_TRANSFER : OSOEnumType.WEB_TRANS_W_OHT,
                        opparas = new
                        {
                            query_id = "",//待解析TODO
                            priority = 1,
                            user = "OHTC_USER",//待解析TODO
                            oht_id = cmd.OHTId,
                            transferinfos = new[] {
                                    new {
                                        cmd_id= cmd.ToString(),
                                        carrier_id=1,
                                        source= cmd.SourceStation,
                                        dest=cmd.DestStation,
                                    }
                                }
                        }
                    });
                    requestSocket.Send(Encoding.UTF8.GetBytes($"{msg}"));

                    var logText = Log($"send command: {cmd}", uiDispatcher);
                    var result = Encoding.UTF8.GetString(requestSocket.Receive());
                    Log($"received : {result}", uiDispatcher);
                    logText.SetOk();
                    string temp = cmd.DestStation;
                    cmd.DestStation = cmd.SourceStation;
                    cmd.SourceStation = temp;
                    while (true)
                    {
                        await Task.Delay(1000);
                        if (cmd.DurationSecond <= 1)
                            break;
                        cmd.CountDownSecond(uiDispatcher);
                        if (cts.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                    cmd.Reset();
                    if (cts.IsCancellationRequested)
                    {
                        break;
                    }
                }
            });
        }

        public void DeleteCommand(TransferCommandTest item)
        {
            OhtCommandList.Remove(item);
        }

        public LogText Log(string message, Dispatcher? dispatcher = null)
        {
            LogText logText = new LogText(message);
            if (dispatcher != null)
            {
                dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    Logs.Add(logText);
                });
            }
            else
                Logs.Add(logText);
            return logText;
        }

    }
}

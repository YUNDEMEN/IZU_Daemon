using NNanomsg.Protocols;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Threading;

namespace OHTC.Tools
{
    public class DummyCycleTest : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        private MoveCommandTest currentCommand;
        public MoveCommandTest CurrentCommand
        {
            get { return currentCommand; }
            set { currentCommand = value; PropertyChanged(this, new PropertyChangedEventArgs("CurrentCommand")); }
        }
        private ObservableCollection<MoveCommandTest> ohtCommandList;
        public ObservableCollection<MoveCommandTest> OhtCommandList
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

        Queue<MoveCommandTest> taskCommands = new Queue<MoveCommandTest>();
        Communication2OSO communication = new Communication2OSO();
        RequestSocket requestSocket;
        CancellationTokenSource cts = new CancellationTokenSource();
        Dispatcher uiDispatcher;
        bool isStarted = false;
        bool isConnected = false;
        public DummyCycleTest()
        {
            this.uiDispatcher = uiDispatcher;
            Logs = new ObservableCollection<LogText>();
            OhtCommandList = new();
        }

        public static DummyCycleTest Instance { get; private set; }

        public static void Init(Dispatcher uiDispatcher = null)
        {
            if (Instance == null)
            {
                Instance = new DummyCycleTest();
            }
            Instance.uiDispatcher = uiDispatcher;
            Instance.LoadCommands();
        }
        public void Connect(string ip_port)
        {
            if (!isConnected)
            {
                requestSocket = new RequestSocket();
                requestSocket.Connect($"tcp://{ip_port}");
                isConnected = true;
            }
        }

        int LoadCommands()
        {
            taskCommands.Clear();
            OhtCommandList.Clear();
            var ohtCommands = MoveCommandTest.Load();
            foreach (var item in ohtCommands)
            {
                taskCommands.Enqueue(item);
                OhtCommandList.Add(item);
            }
            return taskCommands.Count;
        }

        int ResetCommands()
        {
            foreach (var item in OhtCommandList)
            {
                item.IsRunning = false;
                item.DurationSecond = item.CommandInterval;
                taskCommands.Enqueue(item);
            }
            return OhtCommandList.Count;
        }

        public void AddCommand(string from, string to, int sec)
        {
            var cmd = new MoveCommandTest(from, to, sec);
            OhtCommandList.Add(cmd);
        }
        public void DeleteCommand(MoveCommandTest item)
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

        public void StartCycleTest()
        {
            if (!isConnected) return;

            if (isStarted) return;

            ResetCommands();
            Task task = Task.Factory.StartNew(async () =>
            {
                isStarted = true;
                int total = 0;
                cts = new CancellationTokenSource();
                while (!cts.IsCancellationRequested)
                {
                    if (taskCommands.Count == 0)
                        total = ResetCommands();

                    CurrentCommand = taskCommands.Dequeue();
                    CurrentCommand.IsRunning = true;
                    CurrentCommand.CurrentIndex = total - taskCommands.Count;

                    var msg = communication.ModelToJson(new OSOParameter
                    {
                        opname = OSOEnumType.WEB_MOVE_OHT,
                        opparas = new
                        {
                            query_id = "",//待解析TODO
                            priority = 1,
                            user = "OHTC_USER",//待解析TODO
                            oht_id = "127157",
                            dest = CurrentCommand.DestStation
                        }
                    });

                    requestSocket.Send(Encoding.UTF8.GetBytes($"{msg}"));
                    var logText = Log($"send command: {CurrentCommand}", uiDispatcher);
                    var result = Encoding.UTF8.GetString(requestSocket.Receive());
                    Log($"received : {result}", uiDispatcher);
                    logText.SetOk();

                    while (!cts.IsCancellationRequested)
                    {
                        await Task.Delay(1000);
                        if (CurrentCommand.DurationSecond <= 1)
                            break;
                        CurrentCommand.CountDownSecond(uiDispatcher);
                    }
                    CurrentCommand.IsRunning = false;
                }
            });

            task.ContinueWith(t =>
            {
                isStarted = false;
            });
        }

        public void Stop()
        {
            cts.Cancel();
            taskCommands.Clear();
        }
    }
}

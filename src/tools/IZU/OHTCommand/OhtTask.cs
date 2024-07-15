using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NNanomsg.Protocols;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Windows;

namespace OHTC.Tools.OHTCommand
{
    public class OhtCycle : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = delegate { };

        public static OhtCycle _instance;
        public static OhtCycle Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new OhtCycle();
                }
                return _instance;
            }
        }
        protected void FirePropertyChanged(string propName)
        {
            PropertyChanged!(this, new PropertyChangedEventArgs(propName));
        }
        List<OhtModel> ohts = new List<OhtModel>();
        public List<OhtModel> Ohts { get { return ohts; } set { ohts = value; FirePropertyChanged("Ohts"); } }

        //Dispatcher uiDispatcher;
        public OhtCycle()
        {
            Load();
        }
        public void Load()
        {
            try
            {
                if (!System.IO.File.Exists("oht_task.json"))
                {
                    MessageBox.Show("oht_task.json is missing!");
                    return;
                }
                Ohts = JsonConvert.DeserializeObject<List<OhtModel>>(System.IO.File.ReadAllText("oht_task.json"));
                foreach (var item in Ohts)
                {
                    item.CurrentTaskIndex = item.StartTaskIndex;
                }
            }
            catch (Exception ex)
            {
            }
        }

        public void Reload(string config)
        {
            try
            {
                Ohts = JsonConvert.DeserializeObject<List<OhtModel>>(config);
                foreach (var item in Ohts)
                {
                    item.CurrentTaskIndex = item.StartTaskIndex;
                }
            }
            catch (Exception ex)
            {
            }
        }

        IDictionary<string, Task> oht_tasks = new Dictionary<string, Task>();

        public void Start()
        {
            foreach (var item in Ohts)
            {
                oht_tasks[item.OHTId] = item.RunTask();
            }
        }
    }






    public class OhtModel : INotifyPropertyChanged
    {
        List<string> logs = new List<string>();
        public List<string> TaskLogs { get { return logs; } set { logs = value; FirePropertyChanged("TaskLogs"); } }

        public void WriteLine(string message)
        {
            TaskLogs.Add($"[{DateTime.Now.ToString("MM:dd HH:mm:ss")}]: {message}");
            Console.WriteLine($"[{DateTime.Now.ToString("MM:dd HH:mm:ss")}]: {message}");
        }


        public event PropertyChangedEventHandler? PropertyChanged = delegate { };
        protected void FirePropertyChanged(string propName)
        {
            PropertyChanged!(this, new PropertyChangedEventArgs(propName));
        }

        private string ohtId = string.Empty;
        private int currentIndex = 0;
        private int startIndex = 0;
        private OhtTask? currentTask = null;
        private bool isRunning = false;
        private List<OhtTask> tasks;
        private string lastRunAt = "--";
        private string taskMode = string.Empty;
        private string status="N/A";
        public string Status { get { return status; } set { status = value; FirePropertyChanged("Status"); } }
        public string OHTId { get { return ohtId; } set { ohtId = value; FirePropertyChanged("OHTId"); } }
        public int CurrentTaskIndex { get { return currentIndex; } set { currentIndex = value; FirePropertyChanged("CurrentTaskIndex"); } }
        public int StartTaskIndex { get { return startIndex; } set { startIndex = value; FirePropertyChanged("StartTaskIndex"); } }
        public OhtTask? CurrentTask { get { return currentTask; } set { currentTask = value; FirePropertyChanged("CurrentTask"); } }
        public bool IsRunning
        {
            get { return isRunning; }
            set
            {
                isRunning = value;
                if (!isRunning)
                {
                    LastRunAt = "--";
                    Status = "Stopped";
                }
                else
                {
                    Status = "Started";
                }
                FirePropertyChanged("IsRunning");
            }
        }
        public string LastRunAt { get { return lastRunAt; } set { lastRunAt = value; FirePropertyChanged("LastRunAt"); } }
        public string Mode { get { return taskMode; } set { taskMode = value; FirePropertyChanged("Mode"); } }

        public List<OhtTask> Tasks { get { return tasks; } set { tasks = value; FirePropertyChanged("Tasks"); } }

        RequestSocket requestSocket;
        public OhtModel()
        {
        }

        public void Init()
        {
            if (requestSocket != null) return;
            requestSocket = new RequestSocket();
            requestSocket.Options.SendTimeout = new TimeSpan(0, 0, 2);
            requestSocket.Options.ReceiveTimeout = new TimeSpan(0, 0, 2);
            requestSocket.Connect("tcp://ohtc.wonder-inc.cn:8024");
        }
        void PopTask()
        {
            CurrentTask = null;
            if (StartTaskIndex >= Tasks.Count)
                return;
            if (CurrentTaskIndex >= Tasks.Count)
                return;

            CurrentTask = Tasks[CurrentTaskIndex++];
            CurrentTask.ID = GenCommandID();
        }

        CancellationTokenSource cts;
        public Task RunTask()
        {
            if (IsRunning)
            {
                return Task.CompletedTask;
            }
            IsRunning = true;
            WriteLine($"oht {OHTId} tasks is started");
            return Task.Factory.StartNew(async () =>
            {
                cts = new CancellationTokenSource();
                while (true)
                {
                    LastRunAt = DateTime.Now.ToString("HH:mm:ss");
                    if (cts.IsCancellationRequested)
                    {
                        IsRunning = false;
                        break;
                    }

                    PopTask();
                    WriteLine($"oht {OHTId} current task: {CurrentTask}");

                    if (CurrentTask == null)
                    {
                        WriteLine($"oht {OHTId} task reset");
                        CurrentTaskIndex = 0;
                        PopTask();
                    }

                    if (true)
                    {
                        requestSocket.Send(ToBytes(CurrentTask));
                        WriteLine($"oht {OHTId} sent current task");
                        var result = Encoding.UTF8.GetString(requestSocket.Receive());
                        WriteLine($"oht {OHTId} received {result}, will check task status in 5 seconds");
                        await Task.Delay(5000);
                    }

                    while (true)
                    {
                        LastRunAt = DateTime.Now.ToString("HH:mm:ss");
                        if (cts.IsCancellationRequested)
                        {
                            break;
                        }
                        bool isReady = await CheckCMDStatus(CurrentTask.ID);
                        if (!isReady)
                        {
                            await Task.Delay(1000);
                        }
                        else
                        {
                            IsRunning = true;
                            await Task.Delay(3000);
                            break;
                        }
                    }
                }
            });
        }
        public void Cancel()
        {
            if (cts != null)
                cts.Cancel();
            IsRunning = false;
            CurrentTaskIndex = 0;
            WriteLine($"oht {OHTId} task canceled");
        }
        public string GenCommandID()
        {
            if (CurrentTask == null)
                return "";

            switch (Mode)
            {
                case "move": return $"{OHTId}-{CurrentTask.FromStation}-{CurrentTask.ToStation}-WebMove-{DateTime.Now.Ticks}";
                case "carry": return $"OHTC-{CurrentTask.FromStation}-{CurrentTask.ToStation}-CMD-{DateTime.Now.Ticks}";
                default: return string.Empty;
            }
        }

        public byte[] ToBytes(OhtTask task)
        {
            switch (Mode)
            {
                case "move":
                    {
                        var message = new
                        {
                            opname = "WEB_MOVE_OHT",
                            opparas = new
                            {
                                oht_id = ohtId,
                                cmd_id = task.ID,
                                dest = CurrentTask.ToStation
                            }
                        };
                        var str = JsonConvert.SerializeObject(message, new JsonSerializerSettings
                        {
                            Converters = { new StringEnumConverter() }
                        });
                        WriteLine(str);
                        return Encoding.UTF8.GetBytes(str);
                    }
                case "carry":
                    {
                        var msg = new OSOParameter
                        {
                            opname = string.IsNullOrEmpty(OHTId) ? OSOEnumType.WEB_TRANSFER : OSOEnumType.WEB_TRANS_W_OHT,
                            opparas = new
                            {
                                query_id = "",//待解析TODO
                                priority = 1,
                                user = "OHTC_USER",//待解析TODO
                                oht_id = OHTId,
                                transferinfos = new[] { new
                                    {
                                        cmd_id = $"OHTC-{CurrentTask.FromStation}-{CurrentTask.ToStation}-CMD-{DateTime.Now.Ticks}",
                                        carrier_id = 1,
                                        source = task.FromStation,
                                        dest = task.ToStation,
                                    }
                                }
                            }
                        };
                        var str = JsonConvert.SerializeObject(msg,
                            new JsonSerializerSettings
                            {
                                Converters = { new StringEnumConverter() }
                            });
                        WriteLine(str);
                        return Encoding.UTF8.GetBytes(str);
                    }
            }
            return new byte[0];
        }

        public async Task<bool> CheckCMDStatus(string cmd)
        {
            // v1.0/ohtc/get/transfer/cmds
            // v1.0/ohtc/get/transfer/cmd?cid=
            using (HttpClient httpClient = new())
            {
                try
                {
                    string flag = string.Empty;
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
                    httpClient.BaseAddress = new Uri("http://ohtc.wonder-inc.cn:8000");
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    //var ds = new
                    //{
                    //    field = new string[] { "cmd_id" },
                    //    values = new string[] { cmd },
                    //    queryModel = 1
                    //};
                    //HttpResponseMessage response = await httpClient.PostAsync($"v1.0/ohtc/get/transfer/cmds?PageNum=1&PageSize=1", JsonContent.Create(ds));
                    HttpResponseMessage response = await httpClient.PostAsync($"v1.0/ohtc/get/transfer/cmd?cid={cmd}", null);
                    if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        JObject json = JObject.Parse(result);
                        if (json["ok"] != null && !(bool)json["ok"]!)
                        {
                            WriteLine($"oht {OHTId} checked failed: ok={json["ok"]}");
                            return false;
                        }
                        if (json["data"] == null)
                        {
                            WriteLine($"oht {OHTId} checked failed: data is null");
                            return false;
                        }
                        JArray arr = json["data"] as JArray;
                        if (!arr.HasValues)
                        {
                            WriteLine($"oht {OHTId} checked failed: command is not exist ({cmd})");
                            return false;
                        }
                        JObject data = (JObject)arr.First();
                        flag = $"{data["end_time"]}".Trim().ToLower();
                        if (!string.IsNullOrWhiteSpace(flag))
                        {
                            WriteLine($"oht {OHTId} task endtime={flag}");
                            return true;
                        }
                        else
                        {
                            WriteLine($"oht {OHTId} task executing");
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteLine($"oht {OHTId} checked failed: {ex.Message}");
                }
                return false;
            }
        }

        public async Task<bool> OHTIsReady(string oht)
        {
            using (HttpClient httpClient = new())
            {
                try
                {
                    string status = string.Empty;
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
                    httpClient.BaseAddress = new Uri("http://ohtc.wonder-inc.cn:8000");
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    HttpResponseMessage response = await httpClient.GetAsync($"v1.0/ohtc/get/oht/basic/info/{oht}");
                    if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        JObject json = JObject.Parse(result);
                        if (json["ok"] != null && !(bool)json["ok"]!)
                        {
                            WriteLine($"oht {OHTId} checked failed: ok={json["ok"]}");
                            return false;
                        }

                        if (json["data"] == null)
                        {
                            WriteLine($"oht {OHTId} checked failed: data is null");
                            return false;
                        }

                        JObject data = json["data"] as JObject;
                        status = $"{data["status"]}".Trim().ToLower();
                        if (status == "na")
                        {
                            WriteLine($"oht {OHTId} status={status}");
                            return true;
                        }


                        /*
    {
    "data": {
    "cpu_usage": null,
    "memory_usage": null,
    "disk_usage": null,
    "last_alarm_time": null,
    "oht_id": "127157",
    "operation": "auto",
    "current_point_id": null,
    "status": "na",
    "carrier_id": "",
    "software_version": "24070601",
    "map_version": "VNB-V2.0",
    "cmd_id": null,
    "dest": null,
    "src": null,
    "oht_ip": "192.168.127.157",
    "oht_port": null,
    "type": null,
    "offset": 0,
    "rotate": 0,
    "queued_command_count": 0,
    "queued_command": null
    },
    "ok": true,
    "message": "",
    "totalItems": null,
    "pageNum": 0,
    "pageSize": 0
    }
                         */
                    }
                    WriteLine($"oht {OHTId} status={status}");
                }
                catch (Exception ex)
                {
                    WriteLine($"oht {OHTId} checked failed: {ex.Message}");
                }
            }
            return false;
        }
    }









    public class OhtTask : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = delegate { };
        protected void FirePropertyChanged(string propName)
        {
            PropertyChanged!(this, new PropertyChangedEventArgs(propName));
        }

        private string source;
        private string dest;
        private string id;
        public string ID { get { return id; } set { id = value; FirePropertyChanged("ID"); } }
        public string FromStation { get { return source; } set { source = value; FirePropertyChanged("FromStation"); } }
        public string ToStation { get { return dest; } set { dest = value; FirePropertyChanged("ToStation"); } }

        public override string ToString()
        {
            return $"from {FromStation} to {ToStation}";
        }
    }


}

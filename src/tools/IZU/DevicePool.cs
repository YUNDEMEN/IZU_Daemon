using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Net;

namespace OHTC.Tools
{
    public class DevicePool : INotifyPropertyChanged
    {
        public static ConcurrentDictionary<string, DevicePool> DeviceInstances = new ConcurrentDictionary<string, DevicePool>();
        public event PropertyChangedEventHandler? PropertyChanged = delegate { };
        protected void FirePropertyChanged(string propName)
        {
            PropertyChanged!(this, new PropertyChangedEventArgs(propName));
        }
        private IZUModel izu;
        public IZUModel IZU { get { return izu; } set { izu = value; FirePropertyChanged("IZU"); } }
        private ObservableCollection<HIDModel> hidCollection;
        public ObservableCollection<HIDModel> HIDCollection { get { return hidCollection; } set { hidCollection = value; FirePropertyChanged("HIDCollection"); } }
        private ObservableCollection<AutoDoorModel> autoDoorCollection;
        public ObservableCollection<AutoDoorModel> AutoDoorCollection { get { return autoDoorCollection; } set { autoDoorCollection = value; FirePropertyChanged("AutoDoorCollection"); } }
        public DevicePool()
        {
            IZU = new();
            AutoDoorCollection = new ObservableCollection<AutoDoorModel>();
            HIDCollection = new ObservableCollection<HIDModel>();
        }
        public void Update(JObject root)
        {
            JArray izus = (JArray)root["izu"]!;
            {
                if (izus != null)
                {
                    foreach (var item in izus)
                    {
                        IZU.Update((JObject)item);
                    }
                }
            }
            JArray autodoors = (JArray)root["autodoor"]!;
            {
                if (autodoors != null)
                {
                    var list = autodoors.ToList().OrderBy(t => $"{t["name"]}");
                    foreach (JToken item in list)
                    {
                        string name = $"{item["name"]}";
                        AutoDoorModel? model = AutoDoorCollection.FirstOrDefault(t => t.Name == name);
                        if (model == null)
                        {
                            model = new() { Name = name };
                            AutoDoorCollection.Add(model);
                        }
                        model.Update((JObject)item);
                    }
                }
            }
            JArray hids = (JArray)root["hid"]!;
            {
                if (hids != null)
                {
                    foreach (JObject item in hids)
                    {
                        string name = $"{item["name"]}";
                        HIDModel? model = HIDCollection.FirstOrDefault(t => t.Name == name);
                        if (model == null)
                        {
                            model = new() { Name = name };
                            HIDCollection.Add(model);
                        }
                        model.Update((JObject)item);
                    }
                }
            }
        }

        public AutoDoorModel? GetAutoDoor(string name)
        {
            return AutoDoorCollection.FirstOrDefault(t => t.Name == name);
        }
        public static DevicePool Connect(string address)
        {
            if (DeviceInstances.TryGetValue(address, out var instance))
            {
                return instance;
            }
            else
            {
                var device = new DevicePool();
                device.IZU.IPAddress = address;
                DeviceInstances.TryAdd(address, device);
                return device;
            }
        }
    }

    public class IZUModel : DeviceBase
    {
        private string id;
        private string ipAddress;
        private bool isHeartBeat;
        private bool isHardwareInterfaceNormal;
        private bool isStandby;
        private bool isAutoRunMode;
        private bool isError;
        private bool isPowerSupply;
        public string ID { get { return id; } set { id = value; FirePropertyChanged("ID"); } }
        public string IPAddress { get { return ipAddress; } set { ipAddress = value; FirePropertyChanged("IPAddress"); } }
        public bool IsHeartBeat { get { return isHeartBeat; } set { isHeartBeat = value; FirePropertyChanged("IsHeartBeat"); } }
        public bool IsHardwareInterfaceNormal { get { return isHardwareInterfaceNormal; } set { isHardwareInterfaceNormal = value; FirePropertyChanged("IsHardwareInterfaceNormal"); } }
        public bool IsStandby { get { return isStandby; } set { isStandby = value; FirePropertyChanged("IsStandby"); } }
        public bool IsAutoRunMode { get { return isAutoRunMode; } set { isAutoRunMode = value; FirePropertyChanged("IsAutoRunMode"); } }
        public bool IsError { get { return isError; } set { isError = value; FirePropertyChanged("IsError"); } }
        public bool IsPowerSupply { get { return isPowerSupply; } set { isPowerSupply = value; FirePropertyChanged("IsPowerSupply"); } }
        public void Update(JObject node)
        {
            ConnectionState = $"{node["connection"]}";
            Timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            ID = $"{node["id"]}";
            Name = $"{node["name"]}";
            IsHeartBeat = GetValue<bool>(node, "r01");
            IsHardwareInterfaceNormal = GetValue<bool>(node, "r02");
            IsStandby = GetValue<bool>(node, "r03");
            IsAutoRunMode = GetValue<bool>(node, "r04");
            IsError = GetValue<bool>(node, "r05");
            IsPowerSupply = GetValue<bool>(node, "r08");
        }
    }

    public class AutoDoorModel : DeviceBase
    {
        private bool isPowerSupply;
        //private bool isStandby;
        //private bool isAutoRunMode;
        private int status;
        private int errorCode;
        public bool IsPowerSupply { get { return isPowerSupply; } set { isPowerSupply = value; FirePropertyChanged("IsPowerSupply"); } }
        //public bool IsStandby { get { return isStandby; } set { isStandby = value; FirePropertyChanged("IsStandby"); } }
        //public bool IsAutoRunMode { get { return isAutoRunMode; } set { isAutoRunMode = value; FirePropertyChanged("IsAutoRunMode"); } }
        public int Status { get { return status; } set { status = value; FirePropertyChanged("Status"); } }
        public int ErrorCode { get { return errorCode; } set { errorCode = value; FirePropertyChanged("ErrorCode"); } }


        private bool isInit;
        public bool IsInit { get { return isInit; } set { isInit = value; FirePropertyChanged("IsInit"); } }
        private bool isInitFinished;
        public bool IsInitFinished { get { return isInitFinished; } set { isInitFinished = value; FirePropertyChanged("IsInitFinished"); } }
        private bool isReset;
        public bool IsReset { get { return isReset; } set { isReset = value; FirePropertyChanged("IsReset"); } }
        private bool isEMO;
        public bool IsEMO { get { return isEMO; } set { isEMO = value; FirePropertyChanged("IsEMO"); } }
        private float currentPosition;
        public float CurrentPosition { get { return currentPosition; } set { currentPosition = value; FirePropertyChanged("CurrentPosition"); } }
        private string oht;
        public string Oht { get { return oht; } set { oht = value; FirePropertyChanged("Oht"); } }


        private short positionOpened;
        public short PositionOpened { get { return positionOpened; } set { positionOpened = value; FirePropertyChanged("PositionOpened"); } }

        private short positionClosed;
        public short PositionClosed { get { return positionClosed; } set { positionClosed = value; FirePropertyChanged("PositionClosed"); } }

        private float positionCurrent;
        public float PositionCurrent { get { return positionCurrent; } set { positionCurrent = value; FirePropertyChanged("PositionCurrent"); } }

        private short speedAuto;
        public short SpeedAuto { get { return speedAuto; } set { speedAuto = value; FirePropertyChanged("SpeedAuto"); } }

        private short speedJog;
        public short SpeedJog { get { return speedJog; } set { speedJog = value; FirePropertyChanged("SpeedJog"); } }
        public void Update(JObject node)
        {
            ConnectionState = $"{node["connection"]}";
            Timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Name = $"{node["name"]}";

            Status = GetValue<int>(node, "status");
            Status = GetValue<int>(node, "status");
            CurrentPosition = GetValue<float>(node, "r15");
            Oht = $"{node["oht"]}";
            ErrorCode = GetValue<int>(node, "r09");
            IsPowerSupply = GetValue<bool>(node, "r00");

            IsInit = GetValue<bool>(node, "r10");
            IsInitFinished = GetValue<bool>(node, "r11");
            IsReset = GetValue<bool>(node, "r12");
            IsEMO = GetValue<bool>(node, "r13");

            PositionCurrent = GetValue<float>(node, "r15");
            SpeedJog = GetValue<short>(node, "rw02");
            SpeedAuto = GetValue<short>(node, "rw03");
            PositionOpened = GetValue<short>(node, "rw04");
            PositionClosed = GetValue<short>(node, "rw05");
        }
    }

    public class HIDModel : DeviceBase
    {
        public void Update(JObject node)
        {
            ConnectionState = $"{node["connection"]}";
            Timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Name = $"{node["name"]}";
        }
    }

    public abstract class DeviceBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = delegate { };
        protected void FirePropertyChanged(string propName)
        {
            PropertyChanged!(this, new PropertyChangedEventArgs(propName));
        }
        private string name;
        public string Name { get { return name; } set { name = value; FirePropertyChanged("Name"); } }
        private string timestamp;
        public string Timestamp { get { return timestamp; } set { timestamp = value; FirePropertyChanged("Timestamp"); } }
        private string connectionState;
        public string ConnectionState { get { return connectionState; } set { connectionState = value; FirePropertyChanged("ConnectionState"); } }

        protected T GetValue<T>(JObject node, string propName)
        {
            node.TryGetValue(propName, out JToken? token);

            return ((JValue)token).Value == null ? default(T) : token.Value<T>();
        }
    }
}

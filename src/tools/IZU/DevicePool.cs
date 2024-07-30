using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace OHTC.Tools
{
    public class DevicePool : INotifyPropertyChanged
    {
        public static DevicePool instance = new();
        public static DevicePool Instance { get { return instance; } }
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
            IZU= new();
            AutoDoorCollection = new ObservableCollection<AutoDoorModel>();
            HIDCollection = new ObservableCollection<HIDModel>();
        }
        public void Update(JObject root)
        {
            JArray izus = (JArray)root["izu"]!;
            {
                foreach (var item in izus)
                {
                    IZU.Update((JObject)item);
                }
            }
            JArray autodoors = (JArray)root["autodoor"]!;
            {
                foreach (JObject item in autodoors)
                {
                    string name = $"{item["name"]}";
                    AutoDoorModel? model = AutoDoorCollection.FirstOrDefault(t => t.Name == name);
                    if (model == null)
                    {
                        model = new() { Name = name };
                        AutoDoorCollection.Add(model);
                    }
                    model.Update(item);
                }
            }
            JArray hids = (JArray)root["hid"]!;
            {
                foreach (JObject item in autodoors)
                {
                    string name = $"{item["name"]}";
                    HIDModel? model = HIDCollection.FirstOrDefault(t => t.Name == name);
                    if (model == null)
                    {
                        model = new() { Name = name };
                        HIDCollection.Add(model);
                    }
                }
            }
        }
    }

    public class IZUModel : DeviceBase
    {
        private bool isHeartBeat;
        private bool isHardwareInterfaceNormal;
        private bool isStandby;
        private bool isAutoRunMode;
        private bool isError;
        private bool isPowerSupply;
        public bool IsHeartBeat { get { return isHeartBeat; } set { isHeartBeat = value; FirePropertyChanged("IsHeartBeat"); } }
        public bool IsHardwareInterfaceNormal { get { return isHardwareInterfaceNormal; } set { isHardwareInterfaceNormal = value; FirePropertyChanged("IsHardwareInterfaceNormal"); } }
        public bool IsStandby { get { return isStandby; } set { isStandby = value; FirePropertyChanged("IsStandby"); } }
        public bool IsAutoRunMode { get { return isAutoRunMode; } set { isAutoRunMode = value; FirePropertyChanged("IsAutoRunMode"); } }
        public bool IsError { get { return isError; } set { isError = value; FirePropertyChanged("IsError"); } }
        public bool IsPowerSupply { get { return isPowerSupply; } set { isPowerSupply = value; FirePropertyChanged("IsPowerSupply"); } }
        public void Update(JObject node)
        {
            Name = $"{node["name"]}";
            IsHeartBeat = (bool)node["r01"];
            IsHardwareInterfaceNormal = (bool)node["r02"];
            IsStandby = (bool)node["r03"];
            IsAutoRunMode = (bool)node["r04"];
            IsError = (bool)node["r05"];
            IsPowerSupply = (bool)node["r08"];
        }
    }

    public class AutoDoorModel : DeviceBase
    {
        private bool isPowerSupply;
        private bool isStandby;
        private bool isAutoRunMode;
        private int status;
        private int errorCode;
        public bool IsPowerSupply { get { return isPowerSupply; } set { isPowerSupply = value; FirePropertyChanged("IsPowerSupply"); } }
        public bool IsStandby { get { return isStandby; } set { isStandby = value; FirePropertyChanged("IsStandby"); } }
        public bool IsAutoRunMode { get { return isAutoRunMode; } set { isAutoRunMode = value; FirePropertyChanged("IsAutoRunMode"); } }
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

        public void Update(JObject node)
        {
            Name = $"{node["name"]}";
            IsPowerSupply = (bool)node["r00"];
            IsStandby = (bool)node["r01"];
            IsAutoRunMode = (bool)node["r02"];
            Status = (int)node["status"];
            ErrorCode = (int)node["r09"];
            IsInit = (bool)node["r10"];
            IsInitFinished = (bool)node["r11"];
            IsReset = (bool)node["r12"];
            IsEMO = (bool)node["r13"];
            CurrentPosition = (float)node["r15"];
        }
    }

    public class HIDModel : DeviceBase
    {

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
    }
}

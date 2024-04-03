using IZU.Base;
using IZU.Interfaces;
using NNanomsg.Protocols;
using System.Net;
using Wonder.Service.Framework;

namespace IZU.Tasks
{
    [Regist(RegisterTypes.LongRunningTask | RegisterTypes.Singleton)]
    public class SendDeviceToOhtTask : LongRunningTask, ISendDeviceToOhtTask
    {
        readonly IDictionary<string, InnerTask> _requestSockets;
        private readonly IS7NetService _s7NetService;

        public SendDeviceToOhtTask(ILogger<AnotherDataServer> logger, IS7NetService s7NetService)
            : base(logger)
        {
            _s7NetService = s7NetService;
            _requestSockets = new Dictionary<string, InnerTask>();
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lastExecuteTime = DateTime.Now.ToString();
            await Task.CompletedTask;
        }

        public void Add(OhtInfo oht)
        {
            if (oht == null) return;
            var d = _s7NetService.GetDevice(oht.device);
            if (d == null) return;

            if (_requestSockets.ContainsKey(oht.addr))
            {
                _requestSockets[oht.addr].SetOht(oht, d);
            }
            else if (IPEndPoint.TryParse(oht.addr, out IPEndPoint? serverEndPoint))
            {
                InnerTask innerTask = new InnerTask(oht, serverEndPoint, d);
                _requestSockets[oht.addr] = innerTask;
                innerTask.Run();
            }
        }

        public void Delete(OhtInfo oht)
        {
            if (!_requestSockets.ContainsKey(oht.addr)) return;
            _requestSockets[oht.addr].Cancel();
            //_requestSockets.Remove(oht.addr);
        }
        public void Add(List<OhtInfo> infos)
        {
            foreach (var oht in infos)
            {
                Add(oht);
            }
        }

        public void Delete(List<OhtInfo> infos)
        {
            foreach (var oht in infos)
            {
                Delete(oht);
            }
        }

    }
    internal class InnerTask
    {
        const int timeoutSeconds = 2;
        CancellationTokenSource _cts;
        OhtInfo _oht;
        RequestSocket _requestSocket;
        DeviceEntity _device;
        bool _isRunning = false;
        public InnerTask(OhtInfo oht, IPEndPoint iPEnd, DeviceEntity device)
        {
            _oht = oht;
            _device = device;
            _requestSocket = new RequestSocket();
            _requestSocket.Options.SendTimeout = TimeSpan.FromSeconds(timeoutSeconds);
            _requestSocket.Options.ReceiveTimeout = TimeSpan.FromSeconds(timeoutSeconds);
            _requestSocket.Connect($"tcp://{iPEnd.Address}:{iPEnd.Port}");
        }
        public void SetOht(OhtInfo oht, DeviceEntity device)
        {
            _oht = oht;
            SetDevice(device);
            Run();
        }
        public void SetDevice(DeviceEntity device)
        {
            _device = device;
        }
        public void Run()
        {
            if (_isRunning) return;
            _isRunning = true;
            _requestSocket.Send(System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(new DeviceInfo(1, 3, _oht.device, _oht.pid, 0))));
            _cts = new CancellationTokenSource();
            Task.Factory.StartNew(() =>
            {
                byte[]? buffer = null;
                while (!_cts.IsCancellationRequested)
                {
                    buffer = _requestSocket.Receive();
                    if (buffer == null)
                    {
                        _isRunning = false;
                        break;
                    }
                    else
                    {
                        _requestSocket.Send(System.Text.Encoding.UTF8.GetBytes(
                            Newtonsoft.Json.JsonConvert.SerializeObject(
                                new DeviceInfo(2, 3,
                                _oht.device,
                                _oht.pid,
                                DeviceFactory.CheckAuodoorStatus(_device) ?? 0))));
                    }
                }
                _isRunning = false;
            });
        }

        public void Cancel()
        {
            _requestSocket.Send(System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(new DeviceInfo(0, 3, _oht.device, _oht.pid, 0))));
            _requestSocket.Receive();
            _cts.Cancel();
        }
    }
    public record class OhtInfo(string id, string pid, string addr, string device, int status = 0);
    internal class DeviceInfo
    {
        public int msgtype { get; set; }
        public int type { get; set; }
        public string point { get; set; }
        public string device { get; set; }
        public int status { get; set; }
        public DeviceInfo(int msgtype, int type, string device, string point, int status = 0)
        {
            this.msgtype = msgtype;
            this.type = type;
            this.device = device;
            this.point = point;
            this.status = status;
        }
    }
}

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

        public void Add(List<OhtInfo> infos)
        {
            foreach (var oht in infos)
            {
                var d = _s7NetService.GetDevice(oht.device);
                if (d == null) continue;

                if (_requestSockets.ContainsKey(oht.addr))
                {
                    _requestSockets[oht.addr].SetOht(oht);
                    _requestSockets[oht.addr].Run();
                }
                else if (IPEndPoint.TryParse(oht.addr, out IPEndPoint? serverEndPoint))
                {
                    InnerTask innerTask = new InnerTask(oht, serverEndPoint, d);
                    _requestSockets[oht.addr] = innerTask;
                }
            }
        }

        public void Delete(List<OhtInfo> infos)
        {
            foreach (var oht in infos)
            {
                if (!_requestSockets.ContainsKey(oht.addr)) continue;
                _requestSockets[oht.addr].Cancel();
            }
        }

    }
    internal class InnerTask
    {
        CancellationTokenSource _cts;
        OhtInfo _oht;
        RequestSocket _requestSocket;
        DeviceEntity _device;
        int interval = 100;
        public InnerTask(OhtInfo oht, IPEndPoint iPEnd, DeviceEntity device)
        {
            _oht = oht;
            _device = device;
            _requestSocket = new RequestSocket();
            _requestSocket.Connect($"tcp://{iPEnd.Address}:{iPEnd.Port}");
        }
        public void SetOht(OhtInfo oht)
        {
            _oht = oht;
        }
        public void SetDevice(DeviceEntity device)
        {
            _device = device;
        }
        public void Run()
        {
            _cts = new CancellationTokenSource();
            Task.Factory.StartNew(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    _requestSocket.Send(System.Text.Encoding.UTF8.GetBytes(
                        Newtonsoft.Json.JsonConvert.SerializeObject(
                            new DeviceInfo(3,
                            _oht.device,
                            DeviceFactory.CheckAuodoorStatus(_device) ?? 0))));

                    await Task.Delay(interval);
                }
            });
        }

        public void Cancel()
        {
            _cts.Cancel();
        }
    }
    public record class OhtInfo(string addr, string device, int status = 0);
    internal class DeviceInfo
    {
        public int type { get; set; }
        public string device { get; set; }
        public int status { get; set; }
        public DeviceInfo(int type, string device, int status = 0)
        {
            this.type = type;
            this.device = device;
            this.status = status;
        }
    }
}

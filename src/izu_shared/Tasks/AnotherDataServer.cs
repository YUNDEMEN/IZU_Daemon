using IZU.Entities;
using IZU.Interfaces;
using NNanomsg.Protocols;
using System.Text;
using Wonder;

namespace IZU.Tasks
{
    public class AnotherDataServer : LongRunningTask
    {
        private CancellationTokenSource _cancelNanoDataServer;
        private NNanomsg.NanomsgEndpoint NanomsgEndpoint_DataServer;
        private readonly IS7NetService _s7NetService;
        private PairSocket nanoPairSocketServer;
        public AnotherDataServer(ILogger<AnotherDataServer> logger, IS7NetService s7NetService)
            : base(logger)
        {
            _s7NetService = s7NetService;
        }
        public override void Start()
        {
            ExecutionDelay = IZUConfig.IntervalNanoDataServer;
            _cancelNanoDataServer = new CancellationTokenSource();
            nanoPairSocketServer = new PairSocket();
            NanomsgEndpoint_DataServer = nanoPairSocketServer.Bind($"tcp://{IZUConfig.ServerIP}:{IZUConfig.PortNanoDataServer}");
            base.Start();
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            nanoPairSocketServer.Receive();
            nanoPairSocketServer.Send(Encoding.GetEncoding("GB2312").GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(_s7NetService.GetAllDevices())));
        }
    }
}

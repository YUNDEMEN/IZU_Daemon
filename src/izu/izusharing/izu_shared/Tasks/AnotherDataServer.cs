using IZU.Base;
using IZU.Interfaces;
using NNanomsg.Protocols;
using System.Text;
using Wonder.Service.Framework;

namespace IZU.Tasks
{
    //[Regist(RegisterTypes.LongRunningTask)]
    public class AnotherDataServer : LongRunningTask, IAnotherDataServer
    {
        private readonly IS7NetService _s7NetService;
        private PairSocket? nanoPairSocketServer;

        public AnotherDataServer(ILogger<AnotherDataServer> logger, IS7NetService s7NetService)
            : base(logger)
        {
            _s7NetService = s7NetService;
        }
        public override void Start()
        {
            ExecutionDelay = IZUConfig.IntervalNanoDataServer;
            nanoPairSocketServer = new PairSocket();
            nanoPairSocketServer.Bind($"tcp://{IZUConfig.ServerIP}:{IZUConfig.PortNanoDataServer}");
            base.Start();
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lastExecuteTime = DateTime.Now.ToString();
            nanoPairSocketServer.Receive();
            nanoPairSocketServer.Send(Encoding.GetEncoding("GB2312").GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(_s7NetService.GetAllDevices())));
            await Task.CompletedTask;
        }
    }
}

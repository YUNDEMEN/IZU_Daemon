using IZU.Base;
using IZU.Interfaces;
using NNanomsg.Protocols;
using Wonder.Service.Framework;

namespace IZU.Tasks
{
    [Regist(RegisterTypes.LongRunningTask)]
    public class AlarmReportServer : LongRunningTask
    {
        private readonly IS7NetService s7NetService;
        private RequestSocket reqSock;
        public AlarmReportServer(ILogger<AlarmReportServer> logger, IS7NetService s7NetService)
            : base(logger)
        {
            this.s7NetService = s7NetService;
        }
        public override void Start()
        {
            ExecutionDelay=1000;
            reqSock = new RequestSocket();
            reqSock.Options.SendTimeout = TimeSpan.FromSeconds(2);
            reqSock.Options.ReceiveTimeout = TimeSpan.FromSeconds(1);
            reqSock.Connect(IZUConfig.OSOChannel);
            base.Start();
        }
        protected override bool NoDelay => true;
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lastExecuteTime = DateTime.Now.ToString();
            var hids = s7NetService.GetDevicesByType(DeviceTypes.HID);
            foreach (var hidObject in hids)
            {
                int status = DeviceFactory.CheckHIDStatus(hidObject);
                reqSock.Send(System.Text.Encoding.UTF8.GetBytes($"{{\"opname\":\"IZU_ALARM_PSP\",\"opparas\": {{\"name\":\"{hidObject.Name}\",\"status\":{status}}} }}"));
                byte[] buffer = reqSock.Receive();
            }
        }
    }
}

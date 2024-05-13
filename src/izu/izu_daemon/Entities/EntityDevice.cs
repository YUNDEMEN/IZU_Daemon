using IZU.Interfaces;

namespace IZU.Base
{
    public class DeviceEntity : DeviceBase
    {
        public DeviceEntity(ILoggerFactory loggerFactory, string file, string name, List<VariableEntity>? variables = null) : base(loggerFactory, file, name, variables)
        {
        }

        public override void ActivatePlcService()
        {
            if (Server != null)
                Server.Stop();

            var item = Variables.FirstOrDefault(t => !string.IsNullOrEmpty(t.ServerIP));
            if (item == null)
                //	throw new RowNotInTableException($"Server IP address missing!");
                _logger.LogWarning($"server IP address is not found in {Name} ({FromFile})! default IP address is 127.0.0.1");
            Server = new PlcServer(_loggerFactory, DeviceType, Name, item == null ? "127.0.0.1" : item.ServerIP, HeartbeatTimeInterval, GetActionTypes());
            Server.Config(Variables);
        }

    }
}

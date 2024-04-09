using IZU.Base;
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

            Variables.ForEach(t =>
            {
                if (t.VariableType == VariableTypes.Bool)
                    t.Value = false;
            });
            var item = Variables.FirstOrDefault(t => !string.IsNullOrEmpty(t.ServerIP));
            if (item == null)
                //	throw new RowNotInTableException($"Server IP address missing!");
                _logger.LogWarning($"server IP address is not found in {Name} ({FromFile})! default IP address is 127.0.0.1");

            InitialSimulation();

            Server = new PlcServer(_loggerFactory, DeviceType, Name, item == null ? "127.0.0.1" : item.ServerIP, PullDataFromDeviceTimeInterval, GetActionTypes());
            Server.Config(Variables);
        }

        void InitialSimulation()
        {
            switch (DeviceType)
            {
                case DeviceTypes.NONE:
                    break;
                case DeviceTypes.IZU:
                    break;
                case DeviceTypes.HID:
                    break;
                case DeviceTypes.AUTODOOR:
                    foreach (var vr in Variables)
                    {
                        if (vr.ActionType == "R00") vr.Value = true;
                        if (vr.ActionType == "R02") vr.Value = false;
                        if (vr.ActionType == "R05") vr.Value = false;
                        if (vr.ActionType == "R07") vr.Value = false;
                        if (vr.ActionType == "R04") vr.Value = false;
                        if (vr.ActionType == "R06") vr.Value = false;
                        if (vr.ActionType == "R08") vr.Value = true;
                        if (vr.ActionType == "R03") vr.Value = true;
                    }
                    break;
                case DeviceTypes.FIREDOOR:
                    break;
            }
        }

    }
}

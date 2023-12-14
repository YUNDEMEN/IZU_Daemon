using IZU.Entities;
using IZU.Interfaces;

namespace IZU.DeviceFactories
{
    public class AutoDoor : Device, IAutoDoor
    {
        public string address_start { get; } = string.Empty;
        public string address_stop { get; } = string.Empty;
        public string address_man_open { get; } = string.Empty;
        public string address_man_close { get; } = string.Empty;
        public string address_open { get; } = string.Empty;
        public string address_close { get; } = string.Empty;
        public string address_emergency_stop { get; } = string.Empty;
        public string address_reset { get; } = string.Empty;
        public string address_initial { get; } = string.Empty;
        public string address_switch { get; } = string.Empty;
        public AutoDoor() { }
        public AutoDoor(DeviceEntity deviceEntity) : base(deviceEntity)
        {
            address_start = GetActionType(ActionTypes.START);
            address_stop = GetActionType(ActionTypes.STOP);
            address_open = GetActionType(ActionTypes.OPEN);
            address_man_open = GetActionType(ActionTypes.MOPEN);
            address_man_close = GetActionType(ActionTypes.MCLOSE);
            address_close = GetActionType(ActionTypes.CLOSE);
            address_emergency_stop = GetActionType(ActionTypes.EMERG);
            address_reset = GetActionType(ActionTypes.RESET);
            address_initial = GetActionType(ActionTypes.INITIAL);
            address_switch = GetActionType(ActionTypes.SWITCH);
        }


        public async Task<string> OpenAsync()
        {
            string res = await WriteBool(address_open, true);
            RunAfter(2000, () => { WriteBool(address_open, false); });
            return "";
        }
        public async Task<string> CloseAsync()
        {
            return await WriteBool(address_close, true);
        }
        public async Task<string> OpenManualAsync()
        {
            return await WriteBool(address_man_open, true);
        }
        public async Task<string> CloseManualAsync()
        {
            return await WriteBool(address_man_close, true);
        }
        public async Task<string> EmergencyStopAsync()
        {
            string res = await WriteBool(address_emergency_stop, true);
            RunAfter(2000, () => { WriteBool(address_emergency_stop, false); });
            return "";
        }

        public async Task<string> ResetAsync()
        {
            return await WriteBool(address_reset, true);
        }

        public async Task<string> StartAsync()
        {
            return await WriteBool(address_start, true);
        }

        public async Task<string> StopAsync()
        {
            return await WriteBool(address_stop, false);
        }

        public async Task<string> InitialAsync(bool oper)
        {
            return await WriteBool(address_initial, oper);
        }

        public async Task<string> SwitchAsync(bool oper)
        {
            return await WriteBool(address_switch, oper);
        }
    }
}

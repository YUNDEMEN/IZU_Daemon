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
        public string address_open_signal { get; } = string.Empty;
        public string address_close { get; } = string.Empty;
        public string address_close_signal { get; } = string.Empty;
        public string address_emergency_stop { get; } = string.Empty;
        public string address_reset { get; } = string.Empty;
        public string address_initial { get; } = string.Empty;
        public string address_switch { get; } = string.Empty;
        public string address_start_signal { get; } = string.Empty;
        public AutoDoor() { }
        public AutoDoor(DeviceEntity deviceEntity) : base(deviceEntity)
        {
            address_start = GetActionType(ActionTypes.START);
            address_start_signal = GetActionType(ActionTypes.STARTSIG);
            address_stop = GetActionType(ActionTypes.STOP);
            address_open = GetActionType(ActionTypes.OPEN);
            address_open_signal = GetActionType(ActionTypes.OPENSIG);
            address_man_open = GetActionType(ActionTypes.MOPEN);
            address_man_close = GetActionType(ActionTypes.MCLOSE);
            address_close = GetActionType(ActionTypes.CLOSE);
            address_close_signal = GetActionType(ActionTypes.CLOSESIG);
            address_emergency_stop = GetActionType(ActionTypes.EMERG);
            address_reset = GetActionType(ActionTypes.RESET);
            address_initial = GetActionType(ActionTypes.INITIAL);
            address_switch = GetActionType(ActionTypes.SWITCH);
        }


        public async Task<string> OpenAsync()
        {
            string res = await WriteBool(address_open, true);
            if (string.IsNullOrEmpty(res))
            {
                res = ConditionWrite(address_open_signal, address_open, false);
                return res;
            }
            else
                return res;
        }
        public async Task<string> CloseAsync()
        {
            string res = await WriteBool(address_close, true);
            if (string.IsNullOrEmpty(res))
            {
                res = ConditionWrite(address_close_signal, address_close, false);
                return res;
            }
            else
                return res;
        }
        public async Task<string> OpenManualAsync(bool oper)
        {
            return await WriteBool(address_man_open, oper);
        }
        public async Task<string> CloseManualAsync(bool oper)
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
            string res = await WriteBool(address_start, true);
            if (string.IsNullOrEmpty(res))
            {
                res = ConditionWrite(address_start_signal, address_start, false);
                return res;
            }
            else
                return res;
        }

        public async Task<string> StopAsync()
        {
            return await WriteBool(address_stop, false);
        }

        public async Task<string> InitialAsync(bool oper)
        {
            if(oper){
                await WriteBool(address_start, false);
                await WriteBool(address_stop, false);
                await WriteBool(address_man_open, false);
                await WriteBool(address_man_close, false);
                await WriteBool(address_open, false);
                await WriteBool(address_close, false);
                await WriteBool(address_emergency_stop, false);
                await WriteBool(address_reset, false);
                await WriteBool(address_switch, false);
            }
            return await WriteBool(address_initial, oper);
        }

        public async Task<string> SwitchAsync(bool oper)
        {
            return await WriteBool(address_switch, oper);
        }

    }
}

using IZU.Entities;
using IZU.Interfaces;

namespace IZU.DeviceFactories
{
    public class HID : Device, IHID
	{
		public string address_start { get; } = string.Empty;
		public string address_stop { get; } = string.Empty;
		public string address_emergency_stop { get; } = string.Empty;
		public string address_power_off { get; } = string.Empty;
		public string address_reset { get; } = string.Empty;
		public HID() { }
		public HID(DeviceEntity deviceEntity) : base(deviceEntity)
		{
			address_start = GetActionType(ActionTypes.START);
			address_stop = GetActionType(ActionTypes.STOP);
			address_emergency_stop = GetActionType(ActionTypes.EMERG);
			address_power_off = GetActionType(ActionTypes.POWEROFF);
			address_reset = GetActionType(ActionTypes.RESET);
		}

		public async Task<string> EmergencyStopAsync(bool oper)
		{
			return await WriteBool(address_emergency_stop, oper);
		}

		public async Task<string> PowerOffAsync()
		{
			return await WriteBool(address_power_off, true);
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
	}
}

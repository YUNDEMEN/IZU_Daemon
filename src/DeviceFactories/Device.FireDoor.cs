using IZU.Entities;
using IZU.Interfaces;

namespace IZU.DeviceFactories
{
    public class FireDoor : Device, IFireDoor
	{
		public string address_start { get; } = string.Empty;
		public string address_stop { get; } = string.Empty;
		public string address_open { get; } = string.Empty;
		public string address_close { get; } = string.Empty;
		public string address_emergency_stop { get; } = string.Empty;
		public string address_reset { get; } = string.Empty;
		public FireDoor() { }
		public FireDoor(DeviceEntity deviceEntity) : base(deviceEntity)
		{
			address_start = GetActionType(ActionTypes.START);
			address_stop = GetActionType(ActionTypes.STOP);
			address_open = GetActionType(ActionTypes.OPEN);
			address_close = GetActionType(ActionTypes.CLOSE);
			address_emergency_stop = GetActionType(ActionTypes.EMERG);
			address_reset = GetActionType(ActionTypes.RESET);
		}


		public async Task<string> OpenAsync()
		{
			return await WriteBool(address_open, true);
		}
		public async Task<string> CloseAsync()
		{
			return await WriteBool(address_close, true);
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

        public Task<string> OpenManualAsync()
        {
            throw new NotImplementedException();
        }

        public Task<string> CloseManualAsync()
        {
            throw new NotImplementedException();
        }
    }
}

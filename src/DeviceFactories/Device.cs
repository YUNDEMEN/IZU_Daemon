using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
using System.Timers;

namespace IZU.DeviceFactories
{
    public abstract class Device : NLogProvider, IDevice
	{
		private DeviceEntity _deviceEntity;
        public DeviceEntity DeviceEntity => _deviceEntity;

        public Device() { _deviceEntity = DeviceEntity.DummyDevice; }
        public Device(DeviceEntity deviceEntity)
		{
			_deviceEntity = deviceEntity;
		}

		protected virtual string GetActionType(ActionTypes actionType)
		{
			var v = _deviceEntity.Variables.FirstOrDefault(t => t.ActionType == actionType);
			if (v == null || string.IsNullOrEmpty(v.Address))
				throw new Exception($"{actionType} action is not marked in {_deviceEntity.Name}");
			return v.Address;
		}

		public async Task<string> WriteBool(string address, bool value)
		{
			if (_deviceEntity == null)
				return "device not exist!";
			if (_deviceEntity.Server == null)
				return "device server not exist!";
			return await _deviceEntity.Server.WriteBool(address, value);
		}

		public virtual bool CheckAddress(string address)
        {
			return !string.IsNullOrEmpty(address);
		}

		System.Threading.Timer timer;

        protected void RunAfter(int millionSecs, Action action)
		{
			timer = new System.Threading.Timer((o) =>
		   {
			   action();
			   timer.Change(Timeout.Infinite, Timeout.Infinite);
			   timer.Dispose();
           }, null, millionSecs, Timeout.Infinite);
		}
	}
}
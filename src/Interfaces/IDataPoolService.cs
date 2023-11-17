using IZU.Entities;
using IZU.Service;

namespace IZU.Interfaces
{
    public interface IDataPoolService
    {
        void LoadDevices();

		bool TryAdd(Device value);
        List<string> GetAllDeviceNames();
        List<Device> GetAllDevices();

		Device? GetDevice(string deviceName);
        List<Variable> GetDeviceVariables(string deviceName);
		List<Device> Samples { get; }
    }
}

using IZU.Entities;
using IZU.Service;

namespace IZU.Interfaces
{
    public interface IDataPoolService
    {
        bool TryAdd(Device value);
        List<string> GetAllDeviceNames();
        List<Device> GetAllDevices();

		Device? GetDevice(string deviceName);
        List<Variable> GetDeviceVariables(string deviceName);
        IDataPoolService Samples { get; }
    }
}

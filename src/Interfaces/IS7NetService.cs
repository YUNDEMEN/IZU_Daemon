using IZU.Entities;
using IZU.Service;

namespace IZU.Interfaces
{
    public interface IS7NetService
    {
		Task StartAsync();
        void Stop();
        List<string> GetAllDeviceNames();
        List<DeviceEntity> GetAllDevices();
        List<DeviceEntity> GetAllDevicesBroadcastInfo();
        DeviceEntity? GetDevice(string deviceName);
        List<VariableEntity> GetDeviceVariables(string deviceName);
		List<DeviceEntity> Samples { get; }
        void RefreshConfig(IZUConfig config);
    }
}

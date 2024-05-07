using IZU.Base;
using IZU.Base.dto;

namespace IZU.Interfaces
{
    public interface IS7NetService
    {
        void Start(List<VariableEntity> variables);
        void Stop();
        List<string> GetAllDeviceNames();
        List<DeviceEntity> GetAllDevices();
        DeviceEntity? GetDevice(string deviceName);
        List<VariableEntity> GetDeviceVariables(string deviceName);
        izu_status GetStatus();
    }
}

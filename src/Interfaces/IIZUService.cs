using IZU.Entities;

namespace IZU.Interfaces
{
    public interface IIZUService
    {   
		ServiceRuntime ServiceRuntime { get; }
        void Start();
        void Stop();
		Device? GetDevice(string name);
		List<Device> GetDevices(); 
        List<Device> GetSampleDevices();
	}
}

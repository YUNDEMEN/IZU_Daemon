using IZU.Entities;

namespace IZU.Interfaces
{
    public interface IIZUService
	{
		IZUConfig Config { get; }
		IS7NetService S7netService { get; }
		ServiceRuntime ServiceRuntime { get; }
		Task StartAsync();
        void Stop();
	}
}

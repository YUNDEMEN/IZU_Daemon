using IZU.Entities;

namespace IZU.Interfaces
{
    public interface IIZUService
    {   
		ServiceRuntime ServiceRuntime { get; }
		Task StartAsync();
        void Stop();
	}
}

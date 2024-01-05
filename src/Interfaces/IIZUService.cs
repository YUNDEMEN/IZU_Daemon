using IZU.Entities;

namespace IZU.Interfaces
{
    public interface IIZUService
	{
		IS7NetService S7netService { get; }
		ServiceRuntime ServiceRuntime { get; }
		Task StartAsync();
        void Stop();
		Task ReadConfigFromDBAsync();
		void RefreshConfig();
    }
}

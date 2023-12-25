using IZU.Entities;

namespace IZU.Interfaces
{
    public interface IIZUService
	{
		IS7NetService S7netService { get; }
		ServiceRuntime ServiceRuntime { get; }
		Task StartAsync();
        void Stop();
		Task UploadIZUInfo2DatabaseAsync();
		void RefreshConfig(IZUConfig config);
		IZUConfig Config { get; }
    }
}

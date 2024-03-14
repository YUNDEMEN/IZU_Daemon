namespace IZU.Interfaces
{
    public interface IIZUService
	{
		IS7NetService S7netService { get; }
        Task StartAsync();
        void Stop();
		Task ReadConfigFromDBAsync();
    }
}

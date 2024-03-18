namespace IZU.Interfaces
{
    public interface IIZUService
	{
        Task StartAsync();
        void Stop();
		Task ReadConfigFromDBAsync();
    }
}

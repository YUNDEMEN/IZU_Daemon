namespace Wonder.Service
{
    public interface ITelnetService
    {
        TelnetServer Server { get; }
        void PostLog(string log);
        void Start();
        void Stop();
    }
}

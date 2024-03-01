namespace Wonder.Service
{
    public interface ITelnetCommandService
    {
        IServiceProvider ServiceProvider { get; }
        void CollectCommands();
        string RunCommand(params string[] args);
        void WriteLine(string message);
    }
}

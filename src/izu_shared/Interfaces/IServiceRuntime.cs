namespace IZU.Interfaces
{
    public interface IServiceRuntime
    {
        IDictionary<string, List<string>> Steps { get; }
        string LastStartTime { get; }
        void MarkStarted();
        void Record(string info);
    }
}

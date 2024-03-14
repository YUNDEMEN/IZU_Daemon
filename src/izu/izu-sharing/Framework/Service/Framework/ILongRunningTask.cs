namespace Wonder.Service.Framework
{
    public interface ILongRunningTask
    {
        string Name { get; set; }
        int ID { get; }
        void Start();
        void Stop();
    }
}

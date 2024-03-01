namespace Wonder.Service.Framework
{
    [Flags]
    public enum RegisterTypes
    {
        None = 0,
        Scoped = 1,
        Singleton = 2,
        Transient = 4,
        HostedService = 8,
        LongRunningTask = 16
    }
}

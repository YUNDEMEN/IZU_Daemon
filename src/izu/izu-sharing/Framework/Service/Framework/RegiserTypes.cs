namespace Wonder.Service.Framework
{
    /// <summary>
    /// 服务标记枚举 
    /// <list type="bullet"></list>
    /// 通过标记服务类，可以实现自动注册服务
    /// <para>例：</para>
    /// 标记为 Singleton 和 LongRunningTask ：
    /// <code>
    /// [Regist(RegisterTypes.Singleton | RegisterTypes.LongRunningTask)]
    /// class MyService : LongRunningTask, IMyService
    /// {
    ///      //服务为长任务模式，为抽象类LongRunningTask的派生类，内置一个Task任务
    ///      //同时也可以注册为单例模式，通过 IMyService 接口访问
    ///      
    ///      //目的在于，可以通过 IMyService 访问一个长时间运行服务的内部成员
    /// }
    /// </code>
    /// </summary>
    [Flags]
    public enum RegisterTypes
    {
        None = 0,
        /// <summary>
        /// <para>自动注册服务为 Scoped 模式</para>
        /// <code>
        /// [Regist(RegisterTypes.Scoped)]
        /// class MyService : IMyService 
        /// {
        /// 
        /// }
        /// </code>
        /// </summary>
        Scoped = 1,
        /// <summary>
        /// <para>自动注册服务为 Singleton 模式</para>
        /// <code>
        /// [Regist(RegisterTypes.Singleton)]
        /// class MyService : IMyService 
        /// {
        /// 
        /// }
        /// </code>
        /// </summary>
        Singleton = 2,
        /// <summary>
        /// <para>自动注册服务为 Transient 模式</para>
        /// <code>
        /// [Regist(RegisterTypes.Transient)]
        /// class MyService : IMyService 
        /// {
        /// 
        /// }
        /// </code>
        /// </summary>
        Transient = 4,
        /// <summary>
        /// <para>自动注册服务为 HostedService (BackgroundService)</para>
        /// <code>
        /// [Regist(RegisterTypes.HostedService)]
        /// class MyHostedService : BackgroundService 
        /// {
        /// 
        /// }
        /// </code>
        /// </summary>
        HostedService = 8,
        /// <summary>
        /// <para>自动注册服务为 LongRunningTask 模式</para>
        /// <code>
        /// [Regist(RegisterTypes.LongRunningTask)]
        /// class MyService : LongRunningTask 
        /// {
        /// 
        /// }
        /// </code>
        /// </summary>
        LongRunningTask = 16
    }
}

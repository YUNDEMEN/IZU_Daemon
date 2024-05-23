namespace Wonder.Service.Framework
{
    /// <summary>
    /// 服务加载类型 
    /// </summary>
    [Flags]
    public enum RunTypes
    {
        /// <summary>
        /// 服务加载类型
        /// <para>自动加载</para>
        /// </summary>
        Automatic = 0,
        /// <summary>
        /// 服务加载类型
        /// <para>按需加载</para>
        /// </summary>
        OnDemond = 1
    }
}

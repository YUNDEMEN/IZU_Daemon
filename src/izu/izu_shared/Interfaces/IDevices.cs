using IZU.Base;

namespace IZU.Interfaces
{
    /// <summary>
    /// 设备
    /// </summary>
    public interface IDevice
    {
        /// <summary>
        /// 设备名
        /// </summary>
        DeviceBase DeviceEntity { get; }
    }
    /// <summary>
    /// 启动
    /// </summary>
    public interface ICanStart
    {
        /// <summary>
        /// 启动
        /// </summary>
        /// <returns></returns>
        Task<string> StartAsync();
    }
    /// <summary>
    /// 停止
    /// </summary>
    public interface ICanStop
    {
        /// <summary>
        /// 停止
        /// </summary>
        /// <returns></returns>
        Task<string> StopAsync();
    }
    /// <summary>
    /// 打开
    /// </summary>
    public interface ICanOpen
    {
        /// <summary>
        /// 打开
        /// </summary>
        /// <returns></returns>
        Task<string> OpenAsync();
        Task<string> OpenManualAsync(bool oper);
    }
    /// <summary>
    /// 关闭
    /// </summary>
    public interface ICanClose
    {
        /// <summary>
        /// 关闭
        /// </summary>
        /// <returns></returns>
        Task<string> CloseAsync();
        Task<string> CloseManualAsync(bool oper);
    }
    /// <summary>
    /// 急停
    /// </summary>
    public interface IEmergency
    {
        /// <summary>
        /// 急停
        /// </summary>
        /// <returns></returns>
        Task<string> EmergencyStopAsync(bool oper);
    }

    /// <summary>
    /// 复位
    /// </summary>
    public interface IReset
    {
        /// <summary>
        /// 复位
        /// </summary>
        /// <returns></returns>
        Task<string> ResetAsync(bool oper);
    }

    /// <summary>
    /// 关闭电源
    /// </summary>
    public interface IPowerOff
    {
        /// <summary>
        /// 关闭电源
        /// </summary>
        /// <returns></returns>
        Task<string> PowerOffAsync(bool oper);
    }


    public interface IInitial
    {
        /// <summary>
        /// 关闭电源
        /// </summary>
        /// <returns></returns>
        Task<string> InitialAsync();
    }
    public interface ISwitch
    {
        /// <summary>
        /// 关闭电源
        /// </summary>
        /// <returns></returns>
        Task<string> SwitchAsync(bool oper);
    }

    public interface IOperatable:ICanOpen,ICanClose
    {
        int? GetStatus();
    }
}

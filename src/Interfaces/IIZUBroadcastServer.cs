using IZU.Entities;

namespace IZU.Interfaces
{
    public interface IIZUBroadcastServer
    {
        /// <summary>
        /// 刷新websocket发布数据频率
        /// </summary>
        void Refresh();
        Task Acceptor(HttpContext context, Func<Task> next);
    }
}

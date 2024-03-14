namespace IZU.Interfaces
{
    public interface IIZUWebSocketService
    {
        /// <summary>
        /// 刷新websocket发布数据频率
        /// </summary>
        void Refresh(); 
        void Stop();
        Task Acceptor(HttpContext context, Func<Task> next);
    }
}

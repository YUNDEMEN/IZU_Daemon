namespace IZU.Interfaces
{
    public interface IWebsocketService
    {
        /// <summary>
        /// 刷新websocket发布数据频率
        /// </summary>
        void Refresh(); 
        void Start();
        void Stop();
        Task Acceptor(HttpContext context, Func<Task> next);
    }
}

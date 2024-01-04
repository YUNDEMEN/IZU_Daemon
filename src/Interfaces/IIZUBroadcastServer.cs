using IZU.Entities;

namespace IZU.Interfaces
{
    public interface IIZUBroadcastServer
    {
        void Refresh();
        Task Acceptor(HttpContext context, Func<Task> next);
    }
}

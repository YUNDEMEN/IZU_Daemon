using IZU.Entities;

namespace IZU.Interfaces
{
    public interface IIZUBroadcastServer
    {
        void Refresh(IZUConfig config);
        Task Acceptor(HttpContext context, Func<Task> next);
    }
}

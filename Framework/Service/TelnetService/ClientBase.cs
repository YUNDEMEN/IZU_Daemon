using System.Net.Sockets;

namespace Wonder.Service
{
    public abstract class ClientBase
    {
        protected readonly Dictionary<Socket, TelnetClient> SocketClients;
        public ClientBase()
        {
            SocketClients = new Dictionary<Socket, TelnetClient>();
        }
        protected virtual TelnetClient GetClientBySocket(Socket clientSocket)
        {
            SocketClients.TryGetValue(clientSocket, out TelnetClient? client);
            return client!;
        }
        protected virtual Socket GetSocketByClient(TelnetClient client)
        {
            return SocketClients.FirstOrDefault(x => x.Value.ID == client.ID).Key;
        }
    }
}

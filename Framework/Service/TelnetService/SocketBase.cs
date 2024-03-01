using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Wonder.Service
{
    public abstract class SocketBase : ClientBase
    {
        public const string END_LINE = "\r\n";
        public const string REPLY = "";
        public const string CURSOR = " >> ";
        protected readonly Encoding SocketEncoding = Encoding.GetEncoding("GB2312");
        protected IPAddress ServerIPAddress { get; }
        protected int Port { get; }
        protected bool AcceptIncomingConnections { get; set; }

        protected readonly Socket SocketServer;
        protected readonly int DataSize;
        protected byte[] Data;


        public SocketBase(IPAddress ip, int port, int datasize, bool acceptIncomingConnections = true)
        {
            ServerIPAddress = ip;
            Port = port;
            DataSize = datasize;
            Data = new byte[datasize];
            AcceptIncomingConnections = acceptIncomingConnections;
            SocketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start()
        {
            SocketServer.Bind(new IPEndPoint(ServerIPAddress, Port));
            SocketServer.Listen(0);
            SocketServer.BeginAccept(IncomingConnectionAccepted, SocketServer);
        }

        public void Stop()
        {
            SocketServer.Close();
        }
        protected void CloseSocket(Socket clientSocket)
        {
            clientSocket.Close();
            SocketClients.Remove(clientSocket);
        }
        protected virtual void SendData(IAsyncResult result)
        {
            try
            {
                Socket clientSocket = (Socket)result.AsyncState!;

                clientSocket.EndSend(result);

                clientSocket.BeginReceive(Data, 0, DataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
            }

            catch { }
        }
        protected virtual void ReceiveData(IAsyncResult result)
        {
        }
        protected virtual void IncomingConnectionAccepted(IAsyncResult result)
        {

        }

        protected void Send(Socket sock, string message)
        {
            byte[] data = SocketEncoding.GetBytes(message);
            SendBytes(sock, data);
        }
        protected void SendBytes(Socket sock, byte[] data)
        {
            if (sock == null) return;
            sock.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendData), sock);
        }

        public void Broadcast(string message)
        {
            foreach (Socket s in SocketClients.Keys)
            {
                try
                {
                    TelnetClient c = SocketClients[s];

                    if (c.Status == ClientTypes.LoggedIn)
                    {
                        Send(s, END_LINE + message + END_LINE + CURSOR);
                        c.ResetReceivedData();
                    }
                }

                catch
                {
                    SocketClients.Remove(s);
                }
            }
        }

    }
}

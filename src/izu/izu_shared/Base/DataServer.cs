using System.Net;
using System.Net.Sockets;

namespace IZU.Base
{
    internal class DataServer : Wonder.Service.Tcp.TcpServer
    {
        public static DataServer Instance { get; set; }
        public event EventHandler<DataSession> OnSessionCreated = delegate { };
        public DataServer(IPAddress address, int port) : base(address, port)
        {
        }

        public static DataServer Create(IPAddress address, int port, bool forceCreate = false)
        {
            if (forceCreate || Instance == null)
            {
                Instance = new DataServer(address, port);
                string error = string.Empty;
                try
                {
                    Instance.Start();
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            return Instance;
        }

        protected override Wonder.Service.Tcp.TcpSession CreateSession()
        {
            DataSession session = new(this);
            OnSessionCreated(this, session);
            return session;
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"data server caught an error with code {error}");
        }

        public List<Wonder.Service.Tcp.TcpSession> GetSessions()
        {
            return Sessions.Values.ToList();
        }
    }

    internal class DataSession : Wonder.Service.Tcp.TcpSession
    {
        private readonly System.Text.Encoding GB2312 = System.Text.Encoding.GetEncoding("GB2312");
        public DataSession(Wonder.Service.Tcp.TcpServer server) : base(server)
        {
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"data session with Id {Id} connected!");

            // Send invite message
            string message = $"Greetings from server, id={Id}";
            SendAsync(message);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"data session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
        }


        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"data session caught an error with code {error}");
        }
    }



}

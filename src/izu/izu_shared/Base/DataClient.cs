using System.Net.Sockets;
using System.Text;

namespace IZU.Base
{
    internal class DataClient : Wonder.Service.Tcp.TcpClient
    {
        internal DataClient(string address, int port) :
            base(address, port)
        {
        }

        public void DisconnectAndStop()
        {
            _stop = true;
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"data client connected a new session with Id {Id}");
        }

        protected override void OnDisconnected()
        {
            //Console.WriteLine($"data client disconnected a session with Id {Id}");

            _ = Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1000);
                if (!_stop)
                    ConnectAsync();
            });

        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"data client caught an error with code {error}");
        }

        private bool _stop;
    }
}

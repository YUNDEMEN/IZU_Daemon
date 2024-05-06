/*
 * The following class join the default IP interface to an IP multicast group.
 * They assume the IP multicast group address in the range 224.0.0.0 to 239.255.255.255.
 */
namespace IZU.Base
{
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    public class WonderMulticast
    {
        private const int ttl = 1;
        private const int bufSize = 8 * 1024;
        /// <summary>
        /// Server listening port
        /// set when Server Created (RunAsServer)
        /// </summary>
        private int _port { get; set; } = 0;
        private Socket _socket { get; set; }
        private IPEndPoint _localIPEndpoint { get; set; }
        private WBuffer _wBuffer = new();
        private MulticastOption _option { get; set; }
        public event EventHandler<string> OnDataReceived = delegate { };

        private readonly IPAddress _mcastAddress;
        public WonderMulticast(string mcast_ip)
        {
            _mcastAddress = IPAddress.Parse(mcast_ip);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }
        class WBuffer
        {
            public byte[] buffer = new byte[bufSize];
            public int length;
        }

        /// <summary>
        /// Create Server
        /// Receive messages from Clients
        /// </summary>
        /// <param name="port">listening port</param>
        public void RunAsServer(int port)
        {
            _localIPEndpoint = new IPEndPoint(IPAddress.Any, port);
            _port = port;
            _option = new MulticastOption(_mcastAddress, IPAddress.Any);
            _socket.Bind(_localIPEndpoint);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, ttl);
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, _option);
            HandleReceive();
        }

        void HandleReceive()
        {
            EndPoint remoteEP = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
            AsyncCallback? asyncCall = null;
            _socket.BeginReceiveFrom(_wBuffer.buffer, 0, bufSize, SocketFlags.None, ref remoteEP, asyncCall = (ar) =>
            {
                WBuffer so = (WBuffer)ar.AsyncState!;
                so.length = _socket.EndReceiveFrom(ar, ref remoteEP);
                _socket.BeginReceiveFrom(so!.buffer, 0, bufSize, SocketFlags.None, ref remoteEP, asyncCall, so);
                OnDataReceived(this, Encoding.ASCII.GetString(so.buffer, 0, so.length));
            }, _wBuffer);
        }

        /// <summary>
        /// Create Client
        /// Send Messages to Server
        /// </summary>
        public void RunAsClient(int port)
        {
            _port = port;
            //_localIPEndpoint = new IPEndPoint(IPAddress.Parse(local_ip), 0);
            //_socket.Bind(_localIPEndpoint);
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, ttl);
        }

        /// <summary>
        /// close socket
        /// </summary>
        public void Close()
        {
            if (_socket == null) return;
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership, _option);
            // Close the socket
            _socket.Close();
        }

        /// <summary>
        /// Send message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task SendToAsync(string message)
        {
            var endPoint = new IPEndPoint(_mcastAddress, _port);
            await _socket.SendToAsync(ASCIIEncoding.ASCII.GetBytes(message), SocketFlags.None, endPoint);
        }

#if false
        /*     
                测试代码
        */

        /// <summary>
        /// Server Example
        /// </summary>
        public static void RunAsServer()
        {
            //multicast ip
            WonderMulticast sock = new("224.168.100.2");
            //data receive event
            sock.OnDataReceived += (s, e) =>
            {
                Console.WriteLine("receive message: {0},  at {1}", e, DateTime.Now.ToString("HH:mm:ss:fff"));
            };
            //start listener on port 
            sock.RunAsServer(27001);
        }

        /// <summary>
        /// Client Example
        /// </summary>
        public static void RunAsClient()
        {
            //multicast ip
            WonderMulticast sock = new("224.168.100.2");
            sock.RunAsClient(27001);
            //send message loop
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await sock.SendToAsync($"{DateTime.Now:mm:ss:fff}");
                    Console.WriteLine("send time: {0}", $"{DateTime.Now:mm:ss:fff}");
                    await Task.Delay(1000);
                }
            });
        }
#endif
    }
}


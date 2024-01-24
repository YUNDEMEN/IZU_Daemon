using System.Net.Sockets;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using System;

namespace IZU.Base
{
    public class UDPSocket
    {
        public enum RunModes
        {
            None,
            SERVER,
            CLIENT
        }
        public class State
        {
            public int len;
            public byte[] buffer = new byte[bufSize];
        }
        private Guid _id;
        private Socket _socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private RunModes _mode = RunModes.None;
        private const int bufSize = 8 * 1024;
        private State state = new();
        private EndPoint _endPoint = new IPEndPoint(IPAddress.Any, 0);
        private AsyncCallback? recv = null;

        public event EventHandler OnConnected = delegate { };
        public event EventHandler<string> OnDataReceived = delegate { };
        public bool IsConnected { get { return _socket.Connected; } }
        public Guid Id { get { return _id; } }

        public UDPSocket()
        {
            _id = Guid.NewGuid();
        }

        public void Run(int port)
        {
            _endPoint = new IPEndPoint(IPAddress.Broadcast, port);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            _mode = RunModes.SERVER;
        }
        public void Connect(int port)
        {
            _endPoint = new IPEndPoint(IPAddress.Any, port);
            _socket.Bind(_endPoint);
            _mode = RunModes.CLIENT;
            OnConnected(this, new EventArgs());
            Receive();
        }
        public async Task SendToAsync(string text)
        {
            byte[] data = Encoding.ASCII.GetBytes(text);
            await _socket.SendToAsync(data, SocketFlags.Broadcast, _endPoint);
        }
        public void Send(string text)
        {
            byte[] data = Encoding.ASCII.GetBytes(text);
            _socket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
            {
                State so = (State)ar.AsyncState!;
                int bytes = _socket.EndSend(ar);
            }, state);
        }

        private void Receive()
        {
            _socket.BeginReceiveFrom(state.buffer, 0, bufSize, SocketFlags.None, ref _endPoint, recv = (ar) =>
            {
                if (_mode == RunModes.CLIENT && !_socket.Connected)
                    return;
                State so = (State)ar.AsyncState!;
                so.len = _socket.EndReceiveFrom(ar, ref _endPoint);
                _socket.BeginReceiveFrom(so!.buffer, 0, bufSize, SocketFlags.None, ref _endPoint, recv, so);
                OnDataReceived(this, Encoding.ASCII.GetString(so.buffer, 0, so.len));
            }, state);
        }
    }
}


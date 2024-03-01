using System.Net;
using System.Net.Sockets;

namespace Wonder.Service
{
    public delegate void ConnectionEventHandler(TelnetClient c);
    public delegate void ConnectionBlockedEventHandler(IPEndPoint endPoint);
    public delegate void MessageReceivedEventHandler(TelnetClient c, string message);
    public class TelnetServer : SocketBase
    {
        public const string Logo = @"
____________________________________________________________________________________________________________
  ____    __    ____  ______   .__   __.  _______   _______ .______                 __  .__   __.   ______    
  \   \  /  \  /   / /  __  \  |  \ |  | |       \ |   ____||   _  \               |  | |  \ |  |  /      |   
   \   \/    \/   / |  |  |  | |   \|  | |  .--.  ||  |__   |  |_)  |              |  | |   \|  | |  ,----'   
    \            /  |  |  |  | |  . `  | |  |  |  ||   __|  |      /               |  | |  . `  | |  |        
     \    /\    /   |  `--'  | |  |\   | |  '--'  ||  |____ |  |\  \----.    __    |  | |  |\   | |  `----.   
      \__/  \__/     \______/  |__| \__| |_______/ |_______|| _| `._____|   (__)   |__| |__| \__|  \______|
____________________________________________________________________________________________________________

";
        public event ConnectionEventHandler? ClientConnected = null;
        public event ConnectionEventHandler? ClientDisconnected = null;
        public event ConnectionBlockedEventHandler? ConnectionBlocked = null;
        public event MessageReceivedEventHandler? MessageReceived = null;
        public TelnetServer(IPAddress ip, int port, int dataSize = 1024)
             : base(ip, port, dataSize)
        {
        }

        public void ClearClientScreen(TelnetClient c)
        {
            SendMessage(c, $"\u001B[1J\u001B[H{Logo}{TelnetServer.CURSOR}");
        }

        public void ClientForceOffline(TelnetClient client)
        {
            CloseSocket(GetSocketByClient(client));
            ClientDisconnected!(client);
        }

        public void SendMessage(TelnetClient c, string message)
        {
            Socket clientSocket = GetSocketByClient(c);
            Send(clientSocket, message);
        }

        protected override void IncomingConnectionAccepted(IAsyncResult result)
        {
            try
            {
                Socket oldSocket = (Socket)result.AsyncState!;

                if (AcceptIncomingConnections)
                {
                    Socket newSocket = oldSocket.EndAccept(result);

                    uint clientID = (uint)SocketClients.Count + 1;
                    TelnetClient client = new TelnetClient(clientID, (IPEndPoint)newSocket.RemoteEndPoint!);
                    SocketClients.Add(newSocket, client);

                    SendBytes(
                        newSocket,
                        new byte[] {
                            0xff, 0xfd, 0x01,   // Do Echo
                            0xff, 0xfd, 0x21,   // Do Remote Flow Control
                            0xff, 0xfb, 0x01,   // Will Echo
                            0xff, 0xfb, 0x03    // Will Supress Go Ahead
                        }
                    );

                    client.ResetReceivedData();

                    ClientConnected!(client);

                    SocketServer.BeginAccept(new AsyncCallback(IncomingConnectionAccepted), SocketServer);
                }

                else
                {
                    ConnectionBlocked!((IPEndPoint)oldSocket.RemoteEndPoint!);
                }
            }

            catch { }
        }

        protected override void ReceiveData(IAsyncResult result)
        {
            try
            {
                Socket clientSocket = (Socket)result.AsyncState!;
                TelnetClient client = GetClientBySocket(clientSocket);
                int bytesReceived = clientSocket.EndReceive(result);
                if (bytesReceived == 0)
                {
                    CloseSocket(clientSocket);
                    SocketServer.BeginAccept(new AsyncCallback(IncomingConnectionAccepted), SocketServer);
                }

                else if (Data[0] < 0xF0)
                {
                    string receivedData = client.GetReceivedData();

                    //Console.WriteLine("received from client {0}({1}): {2}", client.GetClientID(), bytesReceived, receivedData);
                    // 0x2E = '.', 0x0D = carriage return, 0x0A = new line
                    if (Data[0] == 0x2E && Data[1] == 0x0D && receivedData.Length == 0 ||
                        Data[0] == 0x0D && Data[1] == 0x0A)
                    {
                        //sendMessageToSocket(clientSocket, "\u001B[1J\u001B[H");
                        MessageReceived!(client, client.GetReceivedData());
                        client.ResetReceivedData();
                    }

                    else
                    {
                        // 0x08 => backspace character
                        if (Data[0] == 0x08)
                        {
                            if (receivedData.Length > 0)
                            {
                                client.RemoveLastCharacterReceived();
                                SendBytes(clientSocket, new byte[] { 0x08, 0x20, 0x08 });
                            }

                            else
                                clientSocket.BeginReceive(Data, 0, DataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                        }

                        // 0x7F => delete character
                        else if (Data[0] == 0x7F)
                            clientSocket.BeginReceive(Data, 0, DataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);

                        else
                        {
                            client.AppendReceivedData(SocketEncoding.GetString(Data, 0, bytesReceived));

                            // Echo back the received character
                            // if client is not writing any password
                            if (client.Status != ClientTypes.Authenticating)
                                SendBytes(clientSocket, new byte[] { Data[0] });

                            // Echo back asterisks if client is
                            // writing a password
                            else
                                Send(clientSocket, "*");

                            clientSocket.BeginReceive(Data, 0, DataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                        }
                    }
                }

                else
                    clientSocket.BeginReceive(Data, 0, DataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
            }
            catch { }
        }

    }
}

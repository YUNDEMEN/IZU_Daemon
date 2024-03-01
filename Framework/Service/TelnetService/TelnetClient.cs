using System.Net;

namespace Wonder.Service
{
    public class TelnetClient
    {
        private uint id;
        private ClientTypes status;
        private string receivedData;

        public readonly IPEndPoint RemoteAddress;
        public readonly System.DateTime ConnectedTime;
        public ClientTypes Status { get { return status; } }
        public uint ID { get { return id; } }

        public TelnetClient(uint clientId, IPEndPoint remoteAddress)
        {
            id = clientId;
            RemoteAddress = remoteAddress;
            ConnectedTime = DateTime.Now;
            status = ClientTypes.Guest;
            receivedData = string.Empty;
        }

        public string GetReceivedData()
        {
            return receivedData;
        }

        public void SetStatus(ClientTypes newStatus)
        {
            status = newStatus;
        }

        public void SetReceivedData(string newReceivedData)
        {
            receivedData = newReceivedData;
        }

        public void AppendReceivedData(string dataToAppend)
        {
            receivedData += dataToAppend;
        }

        public void RemoveLastCharacterReceived()
        {
            receivedData = receivedData.Substring(0, receivedData.Length - 1);
        }

        public void ResetReceivedData()
        {
            receivedData = string.Empty;
        }

        public override string ToString()
        {
            string ip = string.Format("{0}:{1}", RemoteAddress.Address.ToString(), RemoteAddress.Port);

            string res = string.Format("client {0} (From: {1}, Status: {2}, Connection time: {3})", id, ip, status, ConnectedTime);

            return res;
        }
    }
}

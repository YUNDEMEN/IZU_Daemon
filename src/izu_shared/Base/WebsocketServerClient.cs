using System.Net.WebSockets;

namespace IZU.Base
{
    public class WebsocketServerClient
    {
        public WebSocket Socket { get; }
        public Guid SessionId { get; }
        public int Status { get; set; } = 0;
        public string target { get; set; } = string.Empty;

        public WebsocketServerClient(WebSocket socket, Guid sessionId, string t = "")
        {
            Socket = socket;
            SessionId = sessionId;
            target = t;
        }
    }
}

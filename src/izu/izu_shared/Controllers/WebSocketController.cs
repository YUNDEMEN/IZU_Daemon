using IZU.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IZU.Controllers
{
    [Route("")]
    [ApiController]
    public class WebSocketController : ControllerBase
    {
        readonly IWebSocketService _websocketService;
        public WebSocketController(IWebSocketService websocketService)
        {
            _websocketService = websocketService;
        }

        [HttpGet("/ws")]
        public async Task Get()
        {
            await _websocketService.Acceptor(HttpContext, () =>
            {
                return Task.CompletedTask;
            });
        }
    }
}

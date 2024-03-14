using IZU.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IZU.Controllers
{
    [Route("")]
    [ApiController]
    public class WebSocketController : ControllerBase
    {
        readonly IIZUWebSocketService _websocketService;
        public WebSocketController(IIZUWebSocketService websocketService)
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

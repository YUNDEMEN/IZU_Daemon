using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace IZU.Controllers
{
	[ApiController]
	[Route("izu")]
	public class ServiceInfoController : ControllerBase
	{
		private readonly ILogger<ServiceInfoController> _logger;
		private readonly IIZUService _service;

		public ServiceInfoController(ILogger<ServiceInfoController> logger, IIZUService service)
		{
			_logger = logger;
			_service = service;
		}

		[HttpGet]
		public ServiceRuntime Get()
		{
			return new ServiceRuntime
			{
				Name= _service.Name,
				active_time = DateTime.Now
			};
		}
	}
}
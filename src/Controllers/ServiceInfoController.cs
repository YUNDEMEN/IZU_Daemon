using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
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
		public WonderResponse Get()
		{
			return WonderResponse.Create(_service.ServiceRuntime.Set(DateTime.Now));
		}

		[HttpGet("sample")]
		public WonderResponse GetSampleDevices()
		{
			return WonderResponse.Create(_service.GetSampleDevices());
		}
		[HttpGet("devices")]
		public WonderResponse GetDevices()
		{
			return WonderResponse.Create(_service.GetDevices());
		}
		[HttpGet("device")]
		public WonderResponse GetDevice([FromQuery]string name)
		{
			var device = _service.GetDevice(name);
			if(device == null)
			{
				return WonderResponse.Error(1,$"设备名 {name} 不存在");
			}
			else
			{
				return WonderResponse.Create(_service.GetDevice(name));
			}
		}
	}
}
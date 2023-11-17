using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using S7.Net;
using System;

namespace IZU.Controllers
{
    [ApiController]
	[Route("izu")]
	public class ServiceInfoController : ControllerBase
	{
		private readonly ILogger<ServiceInfoController> _logger;
		private readonly IZUConfig _config;
		private readonly IIZUService _service;
		static IZUS7? izuS7 = null;
		public ServiceInfoController(ILogger<ServiceInfoController> logger, IOptions<IZUConfig> cfg, IIZUService service)
		{
			_logger = logger;
			_config = cfg.Value;	
			_service = service;
		}

		[HttpGet]
		public async Task<WonderResponse> GetAsync()
		{
#if false
			if (izuS7 == null)
				izuS7 = new IZUS7(_logger, "169.254.10.100");
			bool result = await izuS7.Init();
			if(result)
			{
				bool? valBool=await izuS7.ReadBool("DB1.DBX0.0");//DBX
				var valInt = await izuS7.ReadInt("DB1.DBW2.0");//DBW
				float? valFloat = await izuS7.ReadReal("DB1.DBD4.0");//DBD
			}
#endif
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
using IZU.Base;
using IZU.DeviceFactories;
using IZU.Entities;
using IZU.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IZU.Controllers
{
	[ApiController]
	[Route("izu")]
	public class ServiceInfoController : IZUControllerBase
	{
		private readonly ILogger<ServiceInfoController> _logger;
		public ServiceInfoController(ILogger<ServiceInfoController> logger, IOptions<IZUConfig> cfg, IIZUService service, IS7NetService s7netService)
			: base(cfg, service, s7netService)
		{
			_logger = logger;
		}

		[HttpGet]
		public WonderResponse Get()
		{
#if false
			if (izuS7 == null)
				izuS7 = new IZUS7(_logger, "169.254.10.100");
			if(!izuS7.Connected())
			{
				await izuS7.Init();
			}

			bool result = await izuS7.Init();
			if(result)
			{
				bool? valBool = await izuS7.GetBool("DB1.DBX0.0");
				ushort? valUshort = await izuS7.GetUShort("DB1.DBW2.0");
				float? valFloat = await izuS7.GetFloat("DB1.DBD4.0");
				izuS7.Getm();
				await izuS7.WriteBool("DB1.DBX0.0", true);
			}
#endif
			return WonderResponse.Create(_izuService.ServiceRuntime.Set(DateTime.Now));
		}

		[HttpGet("sample")]
		public WonderResponse GetSampleDevices()
		{
			return WonderResponse.Create(_s7netService.Samples);
		}
		[HttpGet("devices")]
		public WonderResponse GetDevices()
		{
			return WonderResponse.Create(_s7netService.GetAllDevices());
		}
		[HttpGet("device")]
		public WonderResponse GetDevice([FromQuery]string name)
		{
			var device = _s7netService.GetDevice(name);
			if(device == null)
			{
				return WonderResponse.Error(1,$"设备名 {name} 不存在");
			}
			else
			{
				return WonderResponse.Create(device);
			}
		}


		#region hid Control

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/hid")]
		public WonderResponse DeviceHID([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IHID, HID>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);
			return WonderResponse.Create(deviceObject);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/hid/start")]
		public async Task<WonderResponse> DeviceHIDStartAsync([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IHID, HID>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.StartAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/hid/stop")]
		public async Task<WonderResponse> DeviceHIDStopAsync([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IHID, HID>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.StopAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/hid/emerg")]
		public async Task<WonderResponse> DeviceHIDEmerg([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IHID, HID>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.EmergencyStopAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/hid/poweroff")]
		public async Task<WonderResponse> DeviceHIDPowerOff([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IHID, HID>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.PowerOffAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/hid/reset")]
		public async Task<WonderResponse> DeviceHIDReset([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IHID, HID>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.ResetAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

		#endregion

		#region auto door Control

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/autodoor")]
		public WonderResponse DeviceAutoDoor([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);
			return WonderResponse.Create(deviceObject);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/autodoor/start")]
		public async Task<WonderResponse> DeviceAutoDoorStart([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.StartAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/autodoor/stop")]
		public async Task<WonderResponse> DeviceAutoDoorStop([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.StopAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/autodoor/open")]
		public async Task<WonderResponse> DeviceAutoDoorOpen([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.OpenAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/autodoor/close")]
		public async Task<WonderResponse> DeviceAutoDoorClose([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.CloseAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/autodoor/emerg")]
		public async Task<WonderResponse> DeviceAutoDoorEmerg([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.EmergencyStopAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/autodoor/reset")]
		public async Task<WonderResponse> DeviceAutoDoorReset([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.ResetAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

		#endregion

		#region fire door Control

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/firedoor")]
		public WonderResponse DeviceFireDoor([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);
			return WonderResponse.Create(deviceObject);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/firedoor/start")]
		public async Task<WonderResponse> DeviceFireDoorStart([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.StartAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/firedoor/stop")]
		public async Task<WonderResponse> DeviceFireDoorStop([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.StopAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/firedoor/open")]
		public async Task<WonderResponse> DeviceFireDoorOpen([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.OpenAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/firedoor/close")]
		public async Task<WonderResponse> DeviceFireDoorClose([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.CloseAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/firedoor/emerg")]
		public async Task<WonderResponse> DeviceFireDoorEmerg([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
			if (deviceObject == null)
				return WonderResponse.Error(1, error);

			string result = await deviceObject.EmergencyStopAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

#if ENABLE_AUTH
		[Authorize]
#endif
		[HttpGet("device/firedoor/reset")]
		public async Task<WonderResponse> DeviceFireDoorReset([FromQuery] string name)
		{
			string error = string.Empty;
			var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
			if(deviceObject == null)
				return WonderResponse.Error(1, error); 
			
			string result = await deviceObject.ResetAsync();
			if (string.IsNullOrEmpty(result))
				return WonderResponse.Create("ok");
			else
				return WonderResponse.Error(1, result);
		}

		#endregion

	}
}
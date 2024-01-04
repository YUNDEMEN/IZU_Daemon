using IZU.Base;
using IZU.DeviceFactories;
using IZU.Entities;
using IZU.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NLog.Extensions.Logging;
using System.Xml.Linq;

namespace IZU.Controllers
{
    [ApiController]
    [Route("izu")]
    public class ServiceInfoController : IZUControllerBase
    {
        private readonly ILogger<ServiceInfoController> _logger;
        IServiceProvider _serviceProvider { get; }
        public ServiceInfoController(ILogger<ServiceInfoController> logger, IOptionsSnapshot<IZUConfig> cfg, IIZUService service, IS7NetService s7netService, IServiceProvider serviceProvider)
            : base(cfg, service, s7netService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        //[Authorize]
        [HttpPost("entry")]
        public object Connect([FromQuery] Guid? id)
        {
            if (id == null) id = Guid.NewGuid();
            //var token = $"{Guid.NewGuid()}{Guid.NewGuid()}{Guid.NewGuid()}{Guid.NewGuid()}".Replace("-", "");
            return WonderResponse.Create(new
            {
                server = $"ws://{IZUConfig.Server}/ws?token={id:N}",
                sessionid = id,
            });
        }

        [HttpGet]
        public WonderResponse Get()
        {
            _izuService.RefreshConfig(_config);
            _izuService.ServiceRuntime.Set(_config);
            return WonderResponse.Create(_izuService.ServiceRuntime.Set(DateTime.Now));
        }

        [HttpGet("devices")]
        public WonderResponse GetDevices()
        {
            return WonderResponse.Create(_s7netService.GetAllDevices());
        }
        [HttpGet("device")]
        public WonderResponse GetDevice([FromQuery] string name)
        {
            var device = _s7netService.GetDevice(name);
            if (device == null)
            {
                return WonderResponse.Error($"设备名 {name} 不存在");
            }
            else
            {
                return WonderResponse.Create(device);
            }
        }

        [HttpGet("reload")]
        public async Task<WonderResponse> ReloadDevicesAsync()
        {
            try
            {
                _s7netService.Stop();
                await _s7netService.StartAsync();
                _izuService.RefreshConfig(_config);
                return WonderResponse.Create("已重载配置和变量表");
            }
            catch (Exception ex)
            {
                return WonderResponse.Error($"重载变量表失败: {ex.Message}");
            }
        }

        [HttpGet("upload")]
        public async Task<WonderResponse> UploadInfoAsync()
        {
            try
            {
                await _izuService.UploadIZUInfo2DatabaseAsync();
                return WonderResponse.Create("已上传变量表");
            }
            catch (Exception ex)
            {
                return WonderResponse.Error($"上传变量表失败: {ex.Message}");
            }
        }


        #region hid Control

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/hid")]
        public WonderResponse DeviceHID([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IHID, HID>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);
            return WonderResponse.Create(deviceObject);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/hid/start")]
        public async Task<WonderResponse> DeviceHIDStartAsync([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IHID, HID>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.StartAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/hid/stop")]
        public async Task<WonderResponse> DeviceHIDStopAsync([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IHID, HID>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.StopAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/hid/emerg")]
        public async Task<WonderResponse> DeviceHIDEmerg([FromQuery] string name, [FromQuery] bool o)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IHID, HID>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.EmergencyStopAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/hid/poweroff")]
        public async Task<WonderResponse> DeviceHIDPowerOff([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IHID, HID>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.PowerOffAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/hid/reset")]
        public async Task<WonderResponse> DeviceHIDReset([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IHID, HID>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.ResetAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

        #endregion

        #region auto door Control

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/autodoor/on")]
        public async Task<WonderResponse> DeviceAutoDoorOn([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result1 = await deviceObject.InitialAsync(true);
            string result2 = await deviceObject.StartAsync();

            if (string.IsNullOrEmpty(result1) && string.IsNullOrEmpty(result2))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result1 + " " + result2);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/autodoor")]
        public WonderResponse DeviceAutoDoor([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);
            return WonderResponse.Create(deviceObject);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/autodoor/start")]
        public async Task<WonderResponse> DeviceAutoDoorStart([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.StartAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/autodoor/stop")]
        public async Task<WonderResponse> DeviceAutoDoorStop([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.StopAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/autodoor/open")]
        public async Task<WonderResponse> DeviceAutoDoorOpen([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.OpenAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/autodoor/close")]
        public async Task<WonderResponse> DeviceAutoDoorClose([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.CloseAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/autodoor/mopen")]
        public async Task<WonderResponse> DeviceAutoDoorManOpen([FromQuery] string name, [FromQuery] bool o)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.OpenManualAsync(o);
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/autodoor/mclose")]
        public async Task<WonderResponse> DeviceAutoDoorManClose([FromQuery] string name, [FromQuery] bool o)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.CloseManualAsync(o);
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/autodoor/emerg")]
        public async Task<WonderResponse> DeviceAutoDoorEmerg([FromQuery] string name, [FromQuery] bool o)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.EmergencyStopAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/autodoor/reset")]
        public async Task<WonderResponse> DeviceAutoDoorReset([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.ResetAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/autodoor/initial")]
        public async Task<WonderResponse> DeviceAutoDoorInitial([FromQuery] string name, [FromQuery] bool o)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.InitialAsync(o);
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }
#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/autodoor/switch")]
        public async Task<WonderResponse> DeviceAutoDoorSwitch([FromQuery] string name, [FromQuery] bool o)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IAutoDoor, AutoDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.SwitchAsync(o);
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }
        #endregion

        #region fire door Control

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/firedoor")]
        public WonderResponse DeviceFireDoor([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);
            return WonderResponse.Create(deviceObject);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/firedoor/start")]
        public async Task<WonderResponse> DeviceFireDoorStart([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.StartAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/firedoor/stop")]
        public async Task<WonderResponse> DeviceFireDoorStop([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.StopAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/firedoor/open")]
        public async Task<WonderResponse> DeviceFireDoorOpen([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.OpenAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/firedoor/close")]
        public async Task<WonderResponse> DeviceFireDoorClose([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.CloseAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/firedoor/emerg")]
        public async Task<WonderResponse> DeviceFireDoorEmerg([FromQuery] string name, [FromQuery] bool o)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.EmergencyStopAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

#if ENABLE_AUTH
		[Authorize]
#endif
        [HttpPost("device/firedoor/reset")]
        public async Task<WonderResponse> DeviceFireDoorReset([FromQuery] string name)
        {
            string error = string.Empty;
            var deviceObject = CreateDeviceObject<IFireDoor, FireDoor>(name, ref error);
            if (deviceObject == null)
                return WonderResponse.Error(error);

            string result = await deviceObject.ResetAsync();
            if (string.IsNullOrEmpty(result))
                return WonderResponse.Create("ok");
            else
                return WonderResponse.Error(result);
        }

        #endregion

    }
}
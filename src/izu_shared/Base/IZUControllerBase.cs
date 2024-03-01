using IZU.DeviceFactories;
using IZU.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IZU.Base
{
    public abstract class IZUControllerBase : ControllerBase
	{
		protected readonly IIZUService _izuService;
		protected readonly IS7NetService _s7netService;
		public IZUControllerBase(IIZUService service, IS7NetService s7netService)
		{
			_izuService = service;
			_s7netService = s7netService;
		}

		protected Inherit? CreateDeviceObject<Inherit, T>(string name, ref string error) where T : class, Inherit
		{
			var device = _s7netService.GetDevice(name);
			if (device == null)
			{
				error = $"设备名 {name} 不存在";
			}
			else
			{
				Inherit? deviceObject = DeviceFactory.Create<T>(device, ref error);
				if (deviceObject == null)
					error = $"创建设备对象失败,{device.Name}, 原因: {error}";
				else
					return deviceObject;
			}
			return default;
		}
	}
}

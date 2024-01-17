using IZU.Interfaces;
using IZU.Service;
using System.CommandLine;

namespace IZU.Commands
{
    public class ShowCommand : TelnetCommandBase
    {
        public ShowCommand(ITelnetCommandService commandService, IIZUService service, IS7NetService s7netService)
            : base("show", commandService, service, s7netService)
        {
            var optInfo = new Option<bool>(new string[] { "--info", "-i" }, () => false, "打印当前配置");
            Add(optInfo);
            var optReload = new Option<bool>(new string[] { "--reload", "-r" }, () => false, "重新加载设备变量表，并重启PLC SERVER");            
            Add(optReload);
            this.SetHandler(OnReloadConfig, optInfo, optReload);
        }
        void OnReloadConfig(bool info, bool reload)
        {
            try
            {
                if(reload)
                {
                    _s7netService.Stop();
                    var ex = _izuService.StartAsync().Exception;
                    commandService.WriteLine($"config reloaded successfully");
                }
            }
            catch (Exception ex)
            {
                commandService.WriteLine($"Operation Failed: {ex.StackTrace}");
            }
            if (info)
            {
                commandService.WriteLine($"izu id: ".PadLeft(22) + $"{Entities.IZUConfig.ID}");
                commandService.WriteLine($"server endpoint: ".PadLeft(22) + $"{Entities.IZUConfig.Server}");
                commandService.WriteLine($"izu backend: ".PadLeft(22) + $"{Entities.IZUConfig.BackendIZUBaseUrl}");
                commandService.WriteLine($"publish interval: ".PadLeft(22) + $"{Entities.IZUConfig.PublishMillionSeconds} ms (websocket)");
                commandService.WriteLine($"variables: ".PadLeft(22) + $"{Entities.IZUConfig.DeviceTableFrom}");
            }
        }


    }
}

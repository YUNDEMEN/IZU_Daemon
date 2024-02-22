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

            var deviceNameArg = new Argument<string>("n", () => string.Empty, "设备名称");
            var devicesOption = new Option<bool>(new string[] { "--all", "-all" }, () => false, "所有设备");
            var devicesCommand = new Command("device", "查看设备信息") { deviceNameArg, devicesOption };
            Add(devicesCommand);
            devicesCommand.SetHandler(ShowDevices, deviceNameArg, devicesOption);
        }
        void OnReloadConfig(bool info, bool reload)
        {
            if (reload)
            {
                try
                {
                    _s7netService.Stop();
                    var ex = _izuService.StartAsync().Exception;
                    commandService.WriteLine($"config reloaded successfully");
                }
                catch (Exception ex)
                {
                    commandService.WriteLine($"Operation Failed: {ex.StackTrace}");
                }
            }
            if (info)
            {
                commandService.WriteLine($"izu id".PadRight(w) + $":{Entities.IZUConfig.izuId}");
                commandService.WriteLine($"server endpoint".PadRight(w) + $":{Entities.IZUConfig.Server}");
                commandService.WriteLine($"izu backend".PadRight(w) + $": {Entities.IZUConfig.BackendIZUBaseUrl}");
                commandService.WriteLine($"publish interval".PadRight(w) + $":{Entities.IZUConfig.PublishMillionSeconds} ms (websocket)");
                commandService.WriteLine($"variables".PadRight(w) + $":{Entities.IZUConfig.DeviceTableFrom}");
            }
        }

        const int w = 16;
        void ShowDevices(string name, bool all)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var deviceEntity = _s7netService.GetDevice(name);
                if (deviceEntity == null)
                {
                    commandService.WriteLine($"device [{name}] not exist");
                }
                else
                {
                    commandService.WriteLine("device name".PadRight(w) + ":" + deviceEntity.Name);
                    commandService.WriteLine("device type".PadRight(w) + ":" + deviceEntity.DeviceType);
                    if (deviceEntity.Server != null)
                    {
                        commandService.WriteLine("device server".PadRight(w) + ":" + deviceEntity.Server.IP);
                        commandService.WriteLine("time interval".PadRight(w) + ":" + deviceEntity.PullDataFromDeviceTimeInterval + " ms");
                        commandService.WriteLine("state".PadRight(w) + ":" + deviceEntity.Server.ConnectionStatus);
                    }
                    foreach (var variableEntity in deviceEntity.Variables)
                    {
                        if (!variableEntity.Disabled)
                            commandService.WriteLine(string.Format("{0} = {1}", variableEntity.ActionType,$"{variableEntity.Value}").PadRight(w) + ":" +
                                variableEntity.Address + "(" + variableEntity.FunctionType + ") -"+ variableEntity.VariableType);
                        else
                            commandService.WriteLine($"{variableEntity.ActionType}= ".PadRight(w) + ":" +
                            variableEntity.Address + "(" + variableEntity.FunctionType + ") -disabled");
                    }
                }
            }

            if (all)
            {
                var devices = _s7netService.GetAllDevices();
                commandService.WriteLine("total device number".PadRight(w) + ":" + devices.Count);
                foreach (var deviceEntity in devices)
                {
                    string info = "  " + deviceEntity.Name.PadRight(w) + ":";
                    if (deviceEntity.Server != null)
                    {
                        info += "connection state = " + deviceEntity.Server.ConnectionStatus;
                    }
                    else
                    {
                        info += "-disabled";
                    }
                    commandService.WriteLine(info);
                }
            }
        }

    }
}

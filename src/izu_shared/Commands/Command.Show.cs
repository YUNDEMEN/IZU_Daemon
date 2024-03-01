using IZU.Interfaces;
using IZU.Service;
using System.CommandLine;
using Wonder.Service.Framework;

namespace IZU.Commands
{
    public class ShowCommand : TelnetCommandBase
    {
        readonly IServiceProvider _serviceProvider;
        public ShowCommand(ITelnetCommandService commandService, IIZUService service, IS7NetService s7netService)
            : base("show", commandService, service, s7netService)
        {
            var optInfo = new Option<bool>(new string[] { "config", "-c" }, () => false, "打印当前配置");
            Add(optInfo);
            var optReload = new Option<bool>(new string[] { "--reload", "-r" }, () => false, "重新加载设备变量表，并重启PLC SERVER");
            Add(optReload);
            this.SetHandler(ShowConfig, optInfo, optReload);

            var deviceNameArg = new Argument<string>("n", () => string.Empty, "设备名称");
            var optionAllDevices = new Option<bool>(new string[] { "--all", "-all" }, () => true, "所有设备");
            var devicesCommand = new Command("device", "查看设备信息") { deviceNameArg, optionAllDevices };
            Add(devicesCommand);
            devicesCommand.SetHandler(ShowDevice, deviceNameArg, optionAllDevices);


            var taskIdArg = new Argument<int>("n", () => 0, "任务编号");
            var optionAllTasks = new Option<bool>(new string[] { "--all", "-all" }, () => true, "所有设备");
            var taskCommand = new Command("task", "查看运行的后台服务信息") { taskIdArg, optionAllTasks };
            Add(taskCommand);
            taskCommand.SetHandler(ShowTask, taskIdArg, optionAllTasks);
        }
        void ShowConfig(bool info, bool reload)
        {
            if (reload)
            {
                try
                {
                    _s7netService.Stop();
                    _izuService.StartAsync().Wait();
                    commandService.WriteLine($"config reloaded successfully");
                }
                catch (Exception ex)
                {
                    commandService.WriteLine($"Operation Failed: {ex.StackTrace}");
                }
            }
            if (info)
            {
                int w = 26;
                commandService.WriteLine($"server endpoint".PadRight(w) + $":{Entities.IZUConfig.Server}");
                commandService.WriteLine($"izu backend".PadRight(w) + $":{Entities.IZUConfig.BackendIZUBaseUrl}");
                commandService.WriteLine($"izu id".PadRight(w) + $":{Entities.IZUConfig.izuId}");
                commandService.WriteLine($"map version".PadRight(w) + $":{Entities.IZUConfig.MapVersion}");
                commandService.WriteLine($"multicast ip".PadRight(w) + $":{Entities.IZUConfig.MulticastIP}");
                commandService.WriteLine($"multicast port".PadRight(w) + $":{Entities.IZUConfig.PortMulticastServer}");
                commandService.WriteLine($"multicast interval".PadRight(w) + $":{Entities.IZUConfig.IntervalMulticastServer} ms");
                commandService.WriteLine($"multicast(json) port".PadRight(w) + $":{Entities.IZUConfig.PortMulticastFullDataServer}");
                commandService.WriteLine($"multicast(json) interval".PadRight(w) + $":{Entities.IZUConfig.IntervalMulticastFullDataServer} ms");
                commandService.WriteLine($"publish interval".PadRight(w) + $":{Entities.IZUConfig.PublishMillionSeconds} ms (websocket)");
                commandService.WriteLine($"variables".PadRight(w) + $":{Entities.IZUConfig.DeviceTableFrom}");
            }
        }

        void ShowDevice(string name, bool all)
        {
            int w = 16;
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
                            commandService.WriteLine(string.Format("{0} = {1}", variableEntity.ActionType, $"{variableEntity.Value}").PadRight(w) + ":" +
                                variableEntity.Address + "(" + variableEntity.FunctionType + ") -" + variableEntity.VariableType);
                        else
                            commandService.WriteLine($"{variableEntity.ActionType}= ".PadRight(w) + ":" +
                            variableEntity.Address + "(" + variableEntity.FunctionType + ") -disabled");
                    }
                }
            }

           else if (all)
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

        void ShowTask(int id, bool all)
        {
            if (id > 0)
            {
                var tasks = commandService.ServiceProvider.GetServices<ILongRunningTask>();
                var t = tasks.FirstOrDefault(t => t.ID == id);
                if (t == null)
                    commandService.WriteLine($"Task with ID {t.ID} does not exist.");
                else
                    commandService.WriteLine($"{t.ToString()}");
            }
           else if (all)
            {
                var tasks = commandService.ServiceProvider.GetServices<ILongRunningTask>();
                commandService.WriteLine("total tasks:" + tasks.Count());
                foreach (var t in tasks)
                {
                    commandService.WriteLine($"{t.ID} = {t.Name}");
                }
            }
        }
    }
}

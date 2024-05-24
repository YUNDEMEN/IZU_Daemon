using IZU.Base;
using IZU.Interfaces;
using System.CommandLine;
using Wonder.Infrastructure;
using Wonder.Service;
using Wonder.Service.Framework;

namespace IZU.Commands
{
    public class ShowCommand : TelnetCommandBase
    {
        readonly IIZUService _izuService;
        readonly IS7NetService _s7netService;
        public ShowCommand(ITelnetCommandService commandService)
            : base("show", commandService)
        {
            _izuService = commandService.ServiceProvider.GetService<IIZUService>()!;
            _s7netService = commandService.ServiceProvider.GetService<IS7NetService>()!;

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
        async void ShowConfig(bool required, bool reload)
        {
            if (reload)
            {
                try
                {
                    commandService.WriteLine($"operating");
                    _s7netService.Stop();
                    await _izuService.StartAsync();
                    commandService.WriteLine($"config reloaded successfully");
                }
                catch (Exception ex)
                {
                    commandService.WriteLine($"Operation Failed: {ex.StackTrace}");
                }
            }
            if (required)
            {
                commandService.WriteLine(IZUConfig.ToString());
            }
        }

        void ShowDevice(string name, bool all)
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
                    xPrint printer = new();
                    printer.AppendLine("device name:" + deviceEntity.Name);
                    printer.AppendLine("device type:" + deviceEntity.DeviceType);
                    if (deviceEntity.Server != null)
                    {
                        printer.AppendLine("device server:" + deviceEntity.Server.IP);
                        printer.AppendLine("time interval:" + deviceEntity.PullDataFromDeviceTimeInterval + " ms");
                        printer.AppendLine("state:" + deviceEntity.Server.ConnectionStatus);
                    }
                    foreach (var variableEntity in deviceEntity.Variables)
                    {
                        if (!variableEntity.Disabled)
                            printer.AppendLine(string.Format("{0} = {1}:", variableEntity.ActionType, $"{variableEntity.Value}") +
                                variableEntity.Address + "(" + variableEntity.FunctionType + ") -" + variableEntity.VariableType);
                        else
                            printer.AppendLine($"{variableEntity.ActionType}= :" +
                            variableEntity.Address + "(" + variableEntity.FunctionType + ") -disabled");
                    }
                    commandService.WriteLine(printer.ToString());
                }
            }

            else if (all)
            {
                var devices = _s7netService.GetAllDevices();
                commandService.WriteLine("total device number:" + devices.Count);
                xPrint printer = new();
                foreach (var deviceEntity in devices)
                {
                    string info = "  " + deviceEntity.Name + ":";
                    if (deviceEntity.Server != null)
                    {
                        info += "connection state = " + deviceEntity.Server.ConnectionStatus;
                    }
                    else
                    {
                        info += "-disabled";
                    }
                    printer.AppendLine(info);
                }

                commandService.WriteLine(printer.ToString());
            }
        }

        void ShowTask(int id, bool all)
        {
            if (id > 0)
            {
                var tasks = commandService.ServiceProvider.GetServices<ILongRunningTask>();
                var theTask = tasks.FirstOrDefault(t => t.ID == id);
                if (theTask == null)
                    commandService.WriteLine($"Task with ID {id} does not exist.");
                else
                    commandService.WriteLine($"{theTask}");
            }
            else if (all)
            {
                var tasks = commandService.ServiceProvider.GetServices<ILongRunningTask>();
                commandService.WriteLine("total tasks:" + tasks.Count());
                foreach (var theTask in tasks)
                {
                    commandService.WriteLine($"{theTask.ID} = {theTask.Name}");
                }
            }
        }
    }

    public class TeCommand : TelnetCommandBase
    {
        public TeCommand(ITelnetCommandService commandService)
            : base("system", commandService)
        {
            var optInfo = new Option<bool>(new string[] { "--restart", "-r" }, () => false, "重启服务");
            Add(optInfo);
            this.SetHandler(Restart, optInfo);
        }
        void Restart(bool restart)
        {
            if(restart)
            {
                Environment.Exit(101);
            }
        }
    }
}

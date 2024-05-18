using IZU.Interfaces;
using System.CommandLine;
using System.Reflection;
using Wonder.Infrastructure;
using Wonder.Service;

namespace IZU.Commands
{
    public class SetCommand : TelnetCommandBase
    {
        readonly IIZUService _izuService;
        readonly IS7NetService _s7netService;
        public SetCommand(ITelnetCommandService commandService)
            : base("set", commandService)
        {
            _izuService = commandService.ServiceProvider.GetService<IIZUService>()!;
            _s7netService = commandService.ServiceProvider.GetService<IS7NetService>()!;

            var deviceNameArg = new Argument<string>("n", () => string.Empty, "设备名称");
            var deviceAliasArg = new Argument<string>("a", () => string.Empty, "设备地址别名");
            var valueArg = new Argument<string>("v", () => string.Empty, "值");
            var devicesCommand = new Command("device", "查看设备信息") { deviceNameArg, deviceAliasArg, valueArg };
            Add(devicesCommand);
            devicesCommand.SetHandler(ShowDevice, deviceNameArg, deviceAliasArg, valueArg);
        }


        void ShowDevice(string name, string alias, string @value)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(@value))
            {
                commandService.WriteLine("need {name}, {address alias}  and {value}");
                return;
            }

            var deviceEntity = _s7netService.GetDevice(name);
            if (deviceEntity == null)
            {
                commandService.WriteLine($"device [{name}] not exist");
            }
            else
            {
                _s7netService.SetDevice(name, alias, @value);

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
    }

}

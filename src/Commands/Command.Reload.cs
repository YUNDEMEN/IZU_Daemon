using IZU.Interfaces;
using System.CommandLine;

namespace IZU.Commands
{
    public class ReloadCommand : TelnetCommandBase
    {
        public ReloadCommand(IIZUService service, IS7NetService s7netService)
            : base("izu", service, s7netService)
        {
            //给当前命名添加一个参数
            var arg = new Argument<string>("reload", "重新加载设备变量表，并重启PLC SERVER");
            Add(arg);

            var opt = new Option<bool>(new string[] { "--config", "-c" }, "重新加载设备变量表，并重启PLC SERVER") { IsRequired = true };
            Add(opt);
            this.SetHandler(OnReloadConfig, opt);
            var opts = new Option<string>(new string[] { "--cfg", "-cf" }, "重新加载设备变量表，并重启PLC SERVER");
            Add(opts);
            this.SetHandler(OnReloadConfig, opt, opts);

        }
        void OnReloadConfig(bool reqiured,string o)
        {

        }
        void OnReloadConfig(bool reqiured)
        {
            if (!reqiured) return;
            try
            {
                _s7netService.Stop();
                var ex = _izuService.StartAsync().Exception;
            }
            catch (Exception ex)
            {

            }
        }

        public override string Execute(string[] args)
        {
            return base.Execute(args);
        }

    }
}

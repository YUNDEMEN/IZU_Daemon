using IZU.Interfaces;

namespace IZU.Commands
{
    public class LogCommand : TelnetCommandBase
    {
        public override string Name => "log";
        public LogCommand(IIZUService service, IS7NetService s7netService) : base("log", service, s7netService)
        {
        }
        public override string Execute(string[] args)
        {
            string options = string.Empty;
            switch (options)
            {
                case "-reload":
                    //重新加载本地配置并应用
                    NLog.LogManager.Configuration = NLog.LogManager.Configuration.Reload();
                    NLog.LogManager.ReconfigExistingLoggers();
                    break;
                case "-level":
                    //设置内存配置并应用
                    var rule = NLog.LogManager.Configuration.FindRuleByName("console");
                    rule.SetLoggingLevels(NLog.LogLevel.Info, NLog.LogLevel.Info);
                    NLog.LogManager.ReconfigExistingLoggers();
                    break;
            }

            return base.Execute(args);
        }
    }
}

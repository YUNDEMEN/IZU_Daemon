using IZU.Interfaces;
using NLog;
using System.CommandLine;
using Wonder.Service;

namespace IZU.Commands
{
    public class LogCommand : TelnetCommandBase
    {
        Logger _logger;
        readonly IIZUService _izuService;
        readonly IS7NetService _s7netService;
        public LogCommand(ITelnetCommandService commandService)
            : base("log", commandService)
        {
            _logger = LogManager.GetLogger(nameof(LogCommand));
            _izuService = commandService.ServiceProvider.GetService<IIZUService>()!;
            _s7netService = commandService.ServiceProvider.GetService<IS7NetService>()!;

            Description = "修改 NLog 日志等级\r\n[ Trace=0, Debug=1, Info=2, Warn=3, Error=4, Fatal=5, Off=6 ]";

            var optRuleName = new Option<string>(new string[] { "--rule-name", "-r" }, "设置最小等级") { IsRequired = true };
            Add(optRuleName);
            var optLevelFrom = new Option<int>(new string[] { "--level-min", "-min" }, () => -1, "设置最小等级") { IsRequired = true };
            Add(optLevelFrom);
            var optLevelTo = new Option<int>(new string[] { "--level-max", "-max" }, () => -1, "设置最大等级") { IsRequired = true };
            Add(optLevelTo);
            var optSave = new Option<bool>(new string[] { "--save", "-s" }, () => false, "重新加载设备变量表，并重启PLC SERVER");
            Add(optSave);
            this.SetHandler(SetRuleLogLevel, optRuleName, optLevelFrom, optLevelTo, optSave);

            var infoCommand = new Command("-i", "查看当前日志规则");
            infoCommand.SetHandler(GetInfo);
            Add(infoCommand);

            var testCommand = new Command("-test", "打印所有等级的日志");
            testCommand.SetHandler(TestLog);
            Add(testCommand);
        }

        void SetRuleLogLevel(string ruleName, int levelFrom, int levelTo, bool save)
        {
            //更新内存配置，并应用
            var rule = NLog.LogManager.Configuration.FindRuleByName(ruleName);
            if (string.IsNullOrEmpty(ruleName) ||
                levelFrom < 0 || levelFrom > 6 ||
                levelTo < 0 || levelTo > 6 ||
                rule == null)
            {
                commandService.WriteLine($"rule \"{ruleName}\" updated failed");
                return;
            }

            rule.SetLoggingLevels(NLog.LogLevel.FromOrdinal(levelFrom), NLog.LogLevel.FromOrdinal(levelTo));
            NLog.LogManager.ReconfigExistingLoggers();
            commandService.WriteLine($"rule \"{ruleName}\" updated successfully");
        }

        void GetInfo()
        {
            var rules = from x in NLog.LogManager.Configuration.LoggingRules select $"{x.RuleName,-10}[{string.Join(",", x.Levels.Select(t => t.Name))}]";
            commandService.WriteLine($"日志等级:  Trace=0, Debug=1, Info=2, Warn=3, Error=4, Fatal=5\r\n{string.Join("\r\n", rules)}");

        }
        void TestLog()
        {
            _logger.Trace("this is trace log");
            _logger.Debug("this is debug log");
            _logger.Info("this is info log");
            _logger.Warn("this is warn log");
            _logger.Error("this is error log");
            _logger.Fatal("this is fatal log");
        }

    }
}

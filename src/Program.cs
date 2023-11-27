using IZU;
using System.Text.Json.Nodes;
using Topshelf;

DirectoryInfo dir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startEx"));
if (dir.Exists) dir.Delete(true);
int recoverySeconds = 0;
AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) =>
{
	LogOnce($"{AppDomain.CurrentDomain.BaseDirectory}logs\\{DateTime.Now:yyyyMMddHHmmss}-crash.log", e.ExceptionObject?.ToString());
};



if (!File.Exists("appsettings.json"))
{
	LogOnce($"{dir.FullName}\\startinfo.log", "config file [appsettings.json] missing!");
	return;
}
string json=File.ReadAllText("appsettings.json");
try
{
	JsonNode? jNode = JsonNode.Parse(json,
		new JsonNodeOptions { PropertyNameCaseInsensitive = false },
		new System.Text.Json.JsonDocumentOptions { AllowTrailingCommas = true });

	if (jNode == null)
	{
		LogOnce($"{dir.FullName}\\startinfo.log", "load config file [appsettings.json] failed!");
		return;
	}
	var izuNode = jNode["izu"];
	if (izuNode == null)
	{
		LogOnce($"{dir.FullName}\\startinfo.log", "service node not found!");
		return;
	}
	var recoverySecondsNode = izuNode["recoverySeconds"];
	if (recoverySecondsNode == null)
	{
		LogOnce($"{dir.FullName}\\startinfo.log", "recoverySecondsNode not found!");
		return;
	}
	recoverySeconds = recoverySecondsNode.GetValue<int>();
}
catch (Exception ex)
{
	LogOnce($"{dir.FullName}\\startinfo.log", ex.Message + ex.StackTrace);
	return;
}

HostFactory.New(x =>
{
	x.Service<IZUDaemon>();
	x.SetServiceName("IZU-Daemon");
	x.SetDisplayName("IZU 控制单元");
	x.SetDescription("");
#if true
	x.EnableServiceRecovery(r =>
	{
		if (recoverySeconds > 0.1)
			r.RestartService(TimeSpan.FromSeconds(recoverySeconds));

		//操作限制在2-3个
		//r.RestartComputer(5, "message");
		//r.RestartComputer(1,"restart computer");
		//r.RunProgram(7, "ping www.baidu.com");
		//r.OnCrashOnly();
		//r.SetResetPeriod(1);
	});
#endif
	x.OnException(ex =>
	{
		LogOnce($"{AppDomain.CurrentDomain.BaseDirectory}logs\\{DateTime.Now:yyyyMMddHHmmss}-crash.log", ex.StackTrace);
	});
	x.StartAutomatically();
	x.RunAsLocalService();
	//x.RunAs("administrator", "duan");
	//x.RunAsPrompt();
}).Run();


void LogOnce(string path, string? content)
{
	if (!dir.Exists) dir.Create();
	Console.WriteLine(content);
	File.WriteAllText(path, content);
}
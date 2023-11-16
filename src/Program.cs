using IZU;
using Topshelf;

HostFactory.New(x =>
{
	x.Service<IZUDaemon>();
	x.SetServiceName("IZU-Daemon");
	x.SetDisplayName("IZU 控制单元");
	x.SetDescription("");
#if true
	x.EnableServiceRecovery(r =>
	{
		//r.RestartService(TimeSpan.FromSeconds(5));
		r.RestartService(1);

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
		Console.WriteLine("service error: {0}", ex.Message);
	});
	x.StartAutomatically();
	x.RunAsLocalService();
	//x.RunAs("administrator", "duan");
	//x.RunAsPrompt();
}).Run();

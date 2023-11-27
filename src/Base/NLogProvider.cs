namespace IZU.Base
{
	public abstract class NLogProvider
	{
		protected void LogDebug(string message, params object?[] args)
		{
			NLog.LogManager.GetLogger("izu").Debug(message, args);
		}
		protected void LogWarn(string message, params object?[] args)
		{
			NLog.LogManager.GetLogger("izu").Warn(message, args);
		}
		protected void LogInfo(string message, params object?[] args)
		{
			NLog.LogManager.GetLogger("izu").Info(message, args);
		}
	}
}

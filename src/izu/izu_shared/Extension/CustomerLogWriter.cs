using NLog;
using NLog.Targets;
using System.Collections.Concurrent;

namespace WD.NLog.Extensions.Logging
{
    [Target("CustomerLogWriter")]
    public class CustomerLogWriter : TargetWithLayout
    {
        private string APPROOT_DIR = string.Empty;
        private string LOGS = string.Empty;
        static BlockingCollection<string> logQ = new();

        public CustomerLogWriter()
        {
            APPROOT_DIR = AppDomain.CurrentDomain.BaseDirectory;
            LOGS = Path.Combine(APPROOT_DIR, "logs");

            if (!Path.Exists(LOGS))
                Directory.CreateDirectory(LOGS);
        }

        protected override async void Write(LogEventInfo logEvent)
        {
            string logMessage = this.Layout.Render(logEvent);
            try
            {
                logQ.Add(logMessage);
            }
            catch (Exception)
            {
                // 4.出错,错误信息打印到控制台
                Console.WriteLine(logMessage);
            }
        }

        /// <summary>
        /// 异步发送日志到elk
        /// </summary>
        public static void StartSendLog(string logstashUrl)
        {
            _ = Task.Run(() =>
            {
                var http = new HttpClient();
                http.Timeout = new TimeSpan(0, 0, 1);
                while (true)
                {
                    try
                    {
                        var logMessage = logQ.Take();

                        if (logMessage != null)
                        {
                            var msg = new HttpRequestMessage(HttpMethod.Post, logstashUrl);
                            // 2.把日志发送到logstash
                            msg.Content = new StringContent(logMessage);

                            http ??= new HttpClient();
                            var res = http.Send(msg);
                        }
                    }
                    catch { }
                }
            });
        }

    }
}

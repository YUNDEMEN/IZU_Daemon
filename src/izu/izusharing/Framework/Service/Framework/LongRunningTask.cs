using Wonder.Infrastructure;

namespace Wonder.Service.Framework
{
    public abstract class LongRunningTask : ILongRunningTask
    {
        /// <summary>
        /// 任务延迟时间（毫秒）
        /// 任务处于一个无限循环的状态
        /// 需要设置一个延迟时间，达到一个执行频率
        /// 默认延迟为1000毫秒，则为每秒执行一次任务
        /// </summary>
        protected virtual int ExecutionDelay { get; set; } = 1000;
        /// <summary>
        /// 任务名称（唯一），默认为类型名称
        /// </summary>
        public virtual string Name { get; set; }
        /// <summary>
        /// 是否禁用任务延迟
        /// 当设置为True时，任务为不延迟， 需要在外部设置等待，一般用在执行任务中等待对方响应的场景
        /// 当设置为False时，任务延迟执行
        /// </summary>
        protected virtual bool NoDelay { get; set; } = false;
        /// <summary>
        /// 任务本体
        /// </summary>
        protected Task? theTask = null;
        /// <summary>
        /// 标记任务是否开始执行
        /// True 已开始
        /// False 未开始
        /// </summary>
        protected bool IsStarted { get; private set; }
        protected CancellationTokenSource cancellationTokenSource { get; private set; }
        protected ILogger _logger { get; set; }

        protected string lastExecuteTime = string.Empty;
        public int ID { get { return theTask.Id; } }

        protected LongRunningTask(ILogger logger)
        {
            _logger = logger;
            Name = GetType().Name;
            IsStarted = false;
            cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 任务执行虚函数，需要在派生类中重写
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected virtual async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lastExecuteTime = DateTime.Now.ToString();
            await Task.CompletedTask.WaitAsync(TimeSpan.FromSeconds(ExecutionDelay), cancellationToken);
        }

        public virtual void Start()
        {
            if (IsStarted)
                return;

            IsStarted = true;
            theTask = Task.Factory.StartNew(async () =>
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    await ExecuteAsync(cancellationTokenSource.Token);
                    if (NoDelay) continue;
                    await Task.Delay(ExecutionDelay);
                }
            },
            cancellationTokenSource.Token,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning, TaskScheduler.Default
            ).Unwrap();
            theTask.ConfigureAwait(false);
            theTask.ContinueWith(x =>
            {
                IsStarted = false;
                _logger.LogError($"Long Running Task Failed: {this.GetType().Name} ({theTask.Status}). {GetRealExceptions(x.Exception)}");
            },
            TaskContinuationOptions.NotOnCanceled
            );

            _logger.LogInformation($"Long Running Task Started: {this.GetType().Name} ({theTask.Status}).");
        }

        public virtual void Stop()
        {
            cancellationTokenSource.Cancel();
            IsStarted = false;
            _logger.LogInformation($"{this.GetType().Name} Task Cancelled({theTask?.Status}).");
        }

        static string GetRealExceptions(Exception? ex)
        {
            List<string> exs = new();
            GetRealException(ex, ref exs);
            return string.Join(". ", exs);
        }

        static void GetRealException(Exception? ex, ref List<string> exs)
        {
            if (ex == null) return;

            if (ex is AggregateException aex)
            {
                foreach (var innerEx in aex.InnerExceptions)
                {
                    GetRealException(innerEx, ref exs);
                }
            }
            else
            {
                exs.Add(ex.Message);
            }
        }

        public override string ToString()
        {
            xPrint printer = new();
            printer.AppendLine($"Task: {Name} ({(IsStarted ? "Started" : "Not started")})");
            printer.AppendLine($"Duration: {ExecutionDelay}ms");
            printer.AppendLine($"NoDelay: {NoDelay}");
            printer.AppendLine($"Task Status: {theTask?.Status}");
            printer.AppendLine($"Run State: {lastExecuteTime}");
            return printer.ToString();
        }
    }
}

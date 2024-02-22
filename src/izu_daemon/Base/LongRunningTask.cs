namespace IZU.Base
{
    public abstract class LongRunningTask
    {
        protected virtual int ExecutionLoopDelayMs { get; set; } = 1000;
        protected Task? theTask = null;
        protected bool IsStarted { get; private set; }
        protected CancellationTokenSource cancellationTokenSource { get; private set; }

        protected LongRunningTask()
        {
            IsStarted = false;
            cancellationTokenSource = new CancellationTokenSource();
        }

        protected abstract void ExecutionCore(CancellationToken cancellationToken);

        public void Start()
        {
            if (IsStarted)
                return;

            IsStarted = true;
            theTask = Task.Factory.StartNew(async () =>
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    ExecutionCore(cancellationTokenSource.Token);
                    await Task.Delay(ExecutionLoopDelayMs);
                }
            },
            cancellationTokenSource.Token,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning, TaskScheduler.Default
            ).Unwrap();
            theTask.ConfigureAwait(false);
            theTask.ContinueWith(x =>
            {
                IsStarted = false;
                Console.WriteLine($"{this.GetType().Name} Task Failed({theTask.Status}). {GetRealExceptions(x.Exception)}");
            },
            TaskContinuationOptions.NotOnCanceled
            );

            Console.WriteLine($"{this.GetType().Name} Task Started({theTask.Status}).");
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            IsStarted = false;
            Console.WriteLine($"{this.GetType().Name} Task Canceled({theTask?.Status}).");
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
    }
}

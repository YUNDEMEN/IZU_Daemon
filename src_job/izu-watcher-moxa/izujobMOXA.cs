using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace izu_watcher_moxa
{
    public static class HttpExtension
    {

    }
    public class izujobMOXA : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

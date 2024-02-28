using System;
using System.Collections.Generic;
using System.Text;

namespace Wonder
{
    internal static class Extensions
    {
        public static IServiceCollection AddTasks(this IServiceCollection service)
        {
            Wonder.LongRunningTask.GetTasks().ForEach(t => service.AddSingleton(t.Service!, t.InheriteFrom));
            return service;
        }
    }
}

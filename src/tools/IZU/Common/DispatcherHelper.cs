using System;
using System.Windows;
using System.Windows.Threading;

namespace SamplesCommon
{
    public static class DispatcherHelper
    {
        public static void RunOnMainThread(Action action)
        {
            RunOnUIThread(Application.Current, action);
        }

        public static void RunOnUIThread(this DispatcherObject d, Action action)
        {
            Dispatcher? dispatcher = d?.Dispatcher;
            if (dispatcher == null) return;
            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(DispatcherPriority.Background, action);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OHTC.Tools
{
    public class LogText : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = delegate { };
        private string text;
        private bool isOk = false;
        public string Text { get { return text; } set { text = value; PropertyChanged(this, new PropertyChangedEventArgs("Text")); } }
        public bool IsOk { get { return isOk; } set { isOk = value; PropertyChanged(this, new PropertyChangedEventArgs("IsOk")); } }

        public LogText(string text)
        {
            this.text = text;
        }
        public void SetOk(bool isOk = false, Dispatcher? dispatcher = null)
        {
            if (dispatcher != null)
            {
                dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    IsOk = isOk;
                });
            }
            else
                IsOk = isOk;
        }
    }
}

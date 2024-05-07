namespace IZU.Base
{
    public class HeartsBeating
    {
        static readonly IDictionary<int, Beating> beats;
        static HeartsBeating()
        {
            beats = new Dictionary<int, Beating>();
        }
        public static void New(int millionSeconds, Action? action)
        {
            Beating beat = new(millionSeconds, action);
            beats[beats.Count + 1] = beat;
        }
    }
    internal class Beating
    {
        internal System.Threading.Timer _timer;
        internal Action? _action;
        public Beating(int millionSeconds, Action? action)
        {
            _action = action;
            _timer = new Timer(Tick!, null, 100, millionSeconds);
        }
        void Tick(object state)
        {
            if (_action != null)
            {
                try
                {
                    _action();
                }
                catch
                {
                }
            }
        }
    }
}

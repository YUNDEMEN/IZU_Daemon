using IZU.Interfaces;

namespace IZU.Entities
{
    public class ServiceRuntime : IServiceRuntime
    {
        private string _lastStartTime = string.Empty;
        private IDictionary<string, List<string>> _steps;
        public IDictionary<string, List<string>> Steps { get { return _steps; } }
        public string LastStartTime { get { return _lastStartTime; } }
        public ServiceRuntime()
        {
            _steps = new Dictionary<string, List<string>>();
        }

        public void MarkStarted()
        {
            _lastStartTime = DateTime.Now.ToString("yyyy-MM-dd_HH:mm:ss");
            if (!_steps.ContainsKey(_lastStartTime))
            {
                _steps[_lastStartTime] =new List<string>();
            }
        }

        public void Record(string info)
        {
            MarkStarted();

            List<string> infos = _steps[_lastStartTime];
            infos.Add(info);
        }
    }
	

}
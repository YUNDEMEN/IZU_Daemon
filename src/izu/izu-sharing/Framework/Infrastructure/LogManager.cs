namespace Wonder.Infrastructure
{
    public class LogManager
    {
        private static ILoggerFactory _factory;

        public static ILoggerFactory ConfigureLogger(ILoggerFactory factory)
        {
            _factory = factory;
            return _factory;
        }
        public static ILoggerFactory Factory
        {
            get
            {
                if (_factory == null)
                {
                    _factory = new LoggerFactory();
                    ConfigureLogger(_factory);
                }
                return _factory;
            }
            set { _factory = value; }
        }
    }
}

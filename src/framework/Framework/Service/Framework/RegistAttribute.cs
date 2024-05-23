namespace Wonder.Service.Framework
{
    public class RegistAttribute : Attribute
    {
        private RunTypes _runType;
        private RegisterTypes _registerType;
        private bool _isScoped;
        private bool _isSingleton;
        private bool _isTransient;
        private bool _isHostedService;
        private bool _isLongRunningTask;
        public RegisterTypes RegisterType { get { return _registerType; } }
        public RunTypes RunType { get { return _runType; } }
        public bool IsScoped { get { return _isScoped; } }
        public bool IsSingleton { get { return _isSingleton; } }
        public bool IsTransient { get { return _isTransient; } }
        public bool IsHostedService { get { return _isHostedService; } }
        public bool IsLongRunningTask { get { return _isLongRunningTask; } }
        public RegistAttribute(RegisterTypes registerType)
        {
            _registerType = registerType;
            _runType = RunTypes.OnDemond;
            _isScoped = registerType.HasFlag(RegisterTypes.Scoped);
            _isSingleton = registerType.HasFlag(RegisterTypes.Singleton);
            _isTransient = registerType.HasFlag(RegisterTypes.Transient);
            _isLongRunningTask = registerType.HasFlag(RegisterTypes.LongRunningTask);
            _isHostedService = registerType.HasFlag(RegisterTypes.HostedService);
        }

        public RegistAttribute(RegisterTypes registerType, RunTypes runType)
        {
            _registerType = registerType;
            _runType = runType;
            _isScoped = registerType.HasFlag(RegisterTypes.Scoped);
            _isSingleton = registerType.HasFlag(RegisterTypes.Singleton);
            _isTransient = registerType.HasFlag(RegisterTypes.Transient);
            _isLongRunningTask = registerType.HasFlag(RegisterTypes.LongRunningTask);
            _isHostedService = registerType.HasFlag(RegisterTypes.HostedService);
        }
    }
}

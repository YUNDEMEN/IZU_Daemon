namespace IZU.Base
{
    public class DeviceEntity : DeviceBase
    {
        TextRecorder recorder { get; set; }
        public DeviceEntity(ILoggerFactory loggerFactory, string file, string name, List<VariableEntity>? variables = null) : base(loggerFactory, file, name, variables)
        {
            if (!string.IsNullOrEmpty(name))
                recorder = new TextRecorder(name);
            else
            {
                _logger.LogWarning($"device name can not be empty, file : {file}");
            }
        }

        public override void ActivatePlcService()
        {
            if (Server != null)
                Server.Stop();

            Variables.ForEach(t => t.ValueChanged += OnValueChanged);
            var item = Variables.FirstOrDefault(t => !string.IsNullOrEmpty(t.ServerIP));
            if (item == null)
                //	throw new RowNotInTableException($"Server IP address missing!");
                _logger.LogWarning($"server IP address is not found in {Name} ({FromFile})! default IP address is 127.0.0.1");
            Server = new PlcServer(_loggerFactory, DeviceType, Name, item == null ? "127.0.0.1" : item.ServerIP, HeartbeatTimeInterval, GetActionTypes());
            Server.Config(Variables);
        }

        private void OnValueChanged(object? sender, ValueChangedEventArgs e)
        {
            string task = string.Format(sender.ToString(), e.OldValue, e.NewValue);
            recorder.EnqueueAsync(string.Format(sender.ToString(), e.NewValue, e.OldValue));
        }
    }
}

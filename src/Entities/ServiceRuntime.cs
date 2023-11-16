namespace IZU.Entities
{
    public class ServiceRuntime
    {
        public string? Name { get; set; }
		public string? IP { get; set; }
        public DateTime active_time { get; set; }
        public ServiceRuntime Set(DateTime dateTime)
        {
            active_time = dateTime;
            return this;
        }
    }
}
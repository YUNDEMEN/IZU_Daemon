using System.Text.Json;
using System.Text.Json.Serialization;

namespace IZU.Entities
{
    public class ServiceRuntime
    {
        public string? Name { get; set; }
		public string? IP { get; set; }
		public string? RecoverySeconds { get; set; }		
		public string? RefreshInterval{ get; set; }
		public DateTime access_time { get; set; }
        public ServiceRuntime Set(DateTime dateTime)
        {
			access_time = dateTime;
            return this;
        }
    }
	

}
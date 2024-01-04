using System.Text.Json;
using System.Text.Json.Serialization;

namespace IZU.Entities
{
    public class ServiceRuntime
    {
		public DateTime access_time { get; set; }
        internal ServiceRuntime Set(DateTime dateTime)
        {
			access_time = dateTime;
            return this;
        }
    }
	

}
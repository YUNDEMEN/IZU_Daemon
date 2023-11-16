using IZU.Entities;
using IZU.Interfaces;

namespace IZU.Service
{
    public class IZUConfigService : IIZUConfigService
    {
        public const string KEY = "IZU";
        public const string NlogConfig = "nlog.config";
		public IZUConfig Config { get; set; }
		public IZUConfigService(IConfiguration configuration) {
			Config = configuration.GetSection(KEY).Get<IZUConfig>();
		}
	}
 
}

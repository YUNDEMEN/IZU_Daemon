namespace izu.config.mod
{
    using Microsoft.Extensions.Logging;
    //using Wonder.Service.Framework;
    public interface IConfigService
    {

    }
    //[Regist(RegisterTypes.Singleton)]
    public class ConfigService : IConfigService
    {
        private readonly ILogger<ConfigService> logger;
        public ConfigService(ILogger<ConfigService> logger)
        {
            this.logger = logger;
            logger.LogInformation("mod config initialzed");
        }
    }
}

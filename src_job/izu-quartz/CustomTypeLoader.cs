using Quartz.Spi;

namespace izu.quartz;

public class CustomTypeLoader : ITypeLoadHelper
{
    private readonly ILogger<CustomTypeLoader> logger;

    public CustomTypeLoader(ILogger<CustomTypeLoader> logger)
    {
        this.logger = logger;
    }

    public void Initialize()
    {
    }

    public Type? LoadType(string? name)
    {
        Type? targetJobType = Type.GetType(name!);
        if (targetJobType == null)
            logger.LogWarning("Requested to load type {0} failed", name);
        return targetJobType;
    }
}

using Quartz.Spi;
using System.Reflection;
using System.Xml.Linq;

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
        {
            string[] typeInfo= name!.Split(new string[] { ","},StringSplitOptions.RemoveEmptyEntries);
            if(typeInfo.Length > 1)
            {
                string fileName = Path.HasExtension(typeInfo[1]) ? typeInfo[1] : $"{typeInfo[1]}.dll";
                if(!File.Exists(fileName))
                {
                    logger.LogWarning("Requested to load type [{0}] failed, File {1} NOT exist", name, fileName);
                }
                var dll = Assembly.Load(File.ReadAllBytes(fileName));
                targetJobType = dll.GetTypes().FirstOrDefault(t => name.StartsWith(t.ToString()));
            }

            /*
            var appdir=Directory.GetParent(Assembly.GetExecutingAssembly().Location);
            var nexeame = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "job-moxa.dll");            
            var loadedfiles = AppDomain.CurrentDomain.GetAssemblies();
            */
        }
        if(targetJobType ==null)
        {
            logger.LogWarning("Requested to load type [{0}] failed", name);
        }
        return targetJobType;
    }
}

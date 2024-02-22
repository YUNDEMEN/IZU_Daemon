using IZU.Entities;

namespace IZU.DeviceFactories
{
    public class DeviceFactory
    {
		public static T? Create<T>(DeviceEntity deviceEntity, ref string error) where T : class
		{
			try
			{
				error = string.Empty;
				return Activator.CreateInstance(typeof(T), deviceEntity) as T;
			}
			catch (Exception ex)
			{
				if(ex.InnerException!=null)
				{
					error = ex.InnerException.Message;
				}
				else
				{
					error = ex.Message;
				}
				return null;
			}
		}
    }
}

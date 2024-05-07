namespace IZU.Base
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

		public static int? CheckAuodoorStatus(DeviceEntity deviceEntity)
        {
            var opening = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R05")?.Value;
            var opened = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R07")?.Value;
            var openState = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value;
            var closing = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R06")?.Value;
            var closed = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R08")?.Value;
            var closeState = deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R03")?.Value;
            if (opening == null || opened == null || openState == null || closing == null || closed == null || closeState == null)
                return null;
            else
            {
                if (// 关到位
/* R06=false*/(bool)closing == false
/* R08=true*/&& (bool)closed
/* R03=true*/&& (bool)closeState
/* R05=false*/&& (bool)opening == false
/* R07=false*/&& (bool)opened == false
/* R04=false*/&& (bool)openState == false)
                    return 0;



                else if (// 正在关
/* R06=true*/ (bool)closing
/* R08=false*/&& (bool)closed == false
/* R03=false*/&& (bool)closeState == false
/* R05=false*/&& (bool)opening == false
/* R07=false*/&& (bool)opened == false
/* R04=false*/&& (bool)openState == false)
                    return 1;



                else if (// 正在开
/* R06=false*/(bool)closing == false
/* R08=false*/&& (bool)closed == false
/* R03=false*/&& (bool)closeState == false
/* R05=true*/&& (bool)opening
/* R07=false*/&& (bool)opened == false
/* R04=false*/&& (bool)openState == false)
                    return 2;



                else if (// 开到位
/* R06=false*/(bool)closing == false
/* R08=false*/&& (bool)closed == false
/* R03=false*/&& (bool)closeState == false
/* R05=false*/&& (bool)opening == false
/* R07=true*/&& (bool)opened
/* R04=true*/&& (bool)openState)
                    return 3;


                else
                    return null;
            }
        }
    }
}

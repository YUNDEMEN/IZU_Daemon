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
                if (ex.InnerException != null)
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

        /// <summary>
        /// 获取自动门状态（0关到位 1正在关 2正在开 3开到位）
        /// <code>
        /// 2 开门
        /// 3 开到位
        /// 1 关门
        /// 0 关到位
        /// </code>
        /// </summary>
        /// <param name="deviceEntity"></param>
        /// <returns></returns>
		public static int? CheckAuodoorStatus(DeviceEntity deviceEntity)
        {
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R05")?.Value}", out bool opening);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R07")?.Value}", out bool opened);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value}", out bool openState);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R06")?.Value}", out bool closing);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R08")?.Value}", out bool closed);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R03")?.Value}", out bool closeState);
            if (// 关到位
 /* R06=false*/closing == false
 /* R08=true*/&& closed
 /* R03=true*/&& closeState
 /* R05=false*/&& opening == false
 /* R07=false*/&& opened == false
 /* R04=false*/&& openState == false)
                return 0;



            else if (// 正在关
/* R06=true*/ closing
/* R08=false*/&& closed == false
/* R03=false*/&& closeState == false
/* R05=false*/&& opening == false
/* R07=false*/&& opened == false
/* R04=false*/&& openState == false)
                return 1;



            else if (// 正在开
/* R06=false*/closing == false
/* R08=false*/&& closed == false
/* R03=false*/&& closeState == false
/* R05=true*/&& opening
/* R07=false*/&& opened == false
/* R04=false*/&& openState == false)
                return 2;



            else if (// 开到位
/* R06=false*/closing == false
/* R08=false*/&& closed == false
/* R03=false*/&& closeState == false
/* R05=false*/&& opening == false
/* R07=true*/&& opened
/* R04=true*/&& openState)
                return 3;


            else
                return null;
        }

        /// <summary>
        /// 获取HID状态
        /// <code>
        /// 0 正常
        /// 1 故障失电
        /// 2 急停失电
        /// 3 高温失电
        /// 4 火灾
        /// </code>
        /// </summary>
        /// <param name="deviceEntity"></param>
        /// <returns></returns>
        public static int CheckHIDStatus(DeviceEntity deviceEntity)
        {
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R10")?.Value}", out bool fireAlarm);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R08")?.Value}", out bool error);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R11")?.Value}", out bool emerg);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R06")?.Value}", out bool loseEnergy);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value}", out bool highTemp);

            if (// 火灾
 /* R10=true*/fireAlarm)
                return 4;
            else if (//高温失电
/* R06=true*/loseEnergy
/* R04=true*/&& highTemp)
                return 3;
            else if (//急停失电
/* R06=true*/loseEnergy
/* R11=true*/&& emerg)
                return 2;
            else if (//故障失电
/* R06=true*/loseEnergy
/* R08=true*/&& error)
                return 1;
            return 0;
        }
    }
}

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
        /// 0 关到位
        /// 1 关门中
        /// 2 开门中
        /// 3 开到位
        /// </code>
        /// </summary>
        /// <param name="deviceEntity"></param>
        /// <returns></returns>
        public static int? CheckAuodoorStatus(DeviceEntity deviceEntity, ILogger? logger = null)
        {
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R02")?.Value}", out bool autoRunning);
            if (!autoRunning) return null;

            //开门   正在开=True    正在关=False    开到位=False    关到位=True
            //问题 : 在开门时,   瞬间关到位变为true
            //正确的结果应该为:  关到位=False


            //关门   正在开=False   正在关=True     开到位=True     关到位=False
            //问题 : 在关门时,   瞬间开到位 变为true
            //正确的结果应该为:  开到位=False
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R05")?.Value}", out bool opening);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R06")?.Value}", out bool closing);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R07")?.Value}", out bool opened);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R08")?.Value}", out bool closed);
#if DEBUGs
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R03")?.Value}", out bool isclosed);
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value}", out bool isopened);
            Console.WriteLine($"关闭={isclosed}  开启={isopened}  开门中={opening}  关门中={closing}  开门完成={opened}  关门完成={closed}");
#endif
            if (// 关到位
 /* R06=false*/closing == false
 /* R08=true*/&& closed
 /* R05=false*/&& opening == false
 /* R07=false*/&& opened == false)
                return 0;



            else if (// 正在关
/* R06=true*/ closing
/* R08=false*/&& closed == false
/* R05=false*/&& opening == false
/* R07=false*/&& opened == false)
                return 1;



            else if (// 正在开
/* R06=false*/closing == false
/* R08=false*/&& closed == false
/* R05=true*/&& opening
/* R07=false*/&& opened == false)
                return 2;



            else if (// 开到位
/* R06=false*/closing == false
/* R08=false*/&& closed == false
/* R05=false*/&& opening == false
/* R07=true*/&& opened)
                return 3;


            else
            {
                if (logger != null)
                {
                    logger.LogError($"closing={closing}, closed={closed}, opening={opening}, opened={opened}");
                }
                return null;
            }
        }


        /// <summary>
        /// 获取HID状态
        /// <code>
        /// 0 正常
        /// 1 火灾
        /// 2 高温失电
        /// 3 维修
        /// 4 失电
        /// 5 故障失电
        /// </code>
        /// </summary>
        /// <param name="deviceEntity"></param>
        /// <returns></returns>
        public static int CheckHIDStatus(DeviceEntity deviceEntity)
        {
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R10")?.Value}", out bool fireAlarm);//火警信号
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R08")?.Value}", out bool error);//PSP故障报警状态
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R11")?.Value}", out bool emerg);//紧急停止返回信号
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R06")?.Value}", out bool loseEnergy);//电柜失电状态信号
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value}", out bool highTemp);//PSP温度异常过高
            bool.TryParse($"{deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R02")?.Value}", out bool stop);//PSP停止状态信号

            if (fireAlarm)//火灾
                return 1;
            else if (highTemp)//高温失电
                return 2;
            else if (stop)//逆变器停止
                return 3;
            else if (error)//失电
                return 4;
            else if (loseEnergy)//故障失电
                return 5;
            return 0;
        }
    }
}

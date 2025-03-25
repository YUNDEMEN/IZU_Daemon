using IZU.Base;
using IZU.Interfaces;
using Wonder.Infrastructure;

namespace IZU.DeviceFactories
{
    public class AutoDoor : Device, IAutoDoor
    {
        public readonly (string RW01, string RW02, string RW03, string RW04, string RW05, string R00, string R01, string R02, string R03, string R04, string R05, string R06, string R07, string R08, string R09, string R10, string R11, string R12, string R13, string R14,string R15, string R16, string W01, string W02, string W03, string W04, string W05, string W06, string W07, string W08, string W09, string W10) address_tup = new();

        public AutoDoor() { }
        public AutoDoor(DeviceBase deviceEntity) : base(deviceEntity)
        {
            address_tup.RW01 = GetActionType("RW01");  //        使能
            address_tup.RW02 = GetActionType("RW02");  //        手动速度
            address_tup.RW03 = GetActionType("RW03");  //        自动速度
            address_tup.RW04 = GetActionType("RW04");  //        开点位
            address_tup.RW05 = GetActionType("RW05");  //        关点位

            address_tup.R00 = GetActionType("R00");  //        上电完成
            address_tup.R01 = GetActionType("R01");  //        系统待机状态
            address_tup.R02 = GetActionType("R02");  //        系统自动运行状态
            address_tup.R03 = GetActionType("R03");  //        门关闭状态
            address_tup.R04 = GetActionType("R04");  //        门开启状态
            address_tup.R05 = GetActionType("R05");  //        开门中
            address_tup.R06 = GetActionType("R06");  //        关门中
            address_tup.R07 = GetActionType("R07");  //        开门完成
            address_tup.R08 = GetActionType("R08");  //        关门完成
            address_tup.R09 = GetActionType("R09");  //        Auto Door 故障报错状态
            address_tup.R10 = GetActionType("R10");  //        初始化回原点中
            address_tup.R11 = GetActionType("R11");  //        初始化回原点完成
            address_tup.R12 = GetActionType("R12");  //        故障复位返回信号
            address_tup.R13 = GetActionType("R13");  //        紧急停止返回信号
            address_tup.R16 = GetActionType("R16");  //        检测到天车
            address_tup.R14 = GetActionType("R14");  //        自动/手动模式返回信号
            address_tup.R15 = GetActionType("R15");  //        当前位置
            address_tup.W01 = GetActionType("W01");  //        启动运行
            address_tup.W02 = GetActionType("W02");  //        停止运行
            address_tup.W03 = GetActionType("W03");  //        开门按钮
            address_tup.W04 = GetActionType("W04");  //        关门按钮
            address_tup.W05 = GetActionType("W05");  //        紧急停止按钮
            address_tup.W06 = GetActionType("W06");  //        故障复位
            address_tup.W07 = GetActionType("W07");  //        上位触发开门
            address_tup.W08 = GetActionType("W08");  //        上位触发关门
            address_tup.W09 = GetActionType("W09");  //        初始化回原点按钮
            address_tup.W10 = GetActionType("W10");  //        自动 / 手动模式
        }

        /// <summary>
        /// 获取自动门状态（0关到位 1正在关 2正在开 3开到位）
        /// <code>
        /// 0->2 开门
        /// 2->3 开到位
        /// 3->1 关门
        /// 1->0 关到位
        /// </code>
        /// </summary>
        /// <returns></returns>
        public int? GetStatus(ILogger? logger = null)
        {
            return DeviceFactory.CheckAuodoorStatus((DeviceEntity)_deviceEntity, logger);
        }


        public async Task<string> InitialAsync()
        {
            Ref<bool> @ref = new();

            // 是否上电
            string state = await GetBool(address_tup.R00, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (!@ref.Value) return "No power!";

            string ret = await WriteBool(address_tup.W09, true);
            if (!string.IsNullOrEmpty(ret))
                return ret;

            await ConditionWriteAsync(address_tup.R11, false, address_tup.W09, false);

            return string.Empty;
        }

        public async Task<string> StartAsync()
        {
            Ref<bool> @ref = new();

            // 禁止重复启动
            string state = await GetBool(address_tup.R02, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@ref.Value)
                return "It is running automatically now!";

            // 禁止故障状态下启动
            state = await GetBool(address_tup.R09, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@ref.Value)
                return "There is a fault alarm, the start command cannot be executed!";

            // 禁止急停状态下启动
            state = await GetBool(address_tup.R13, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@ref.Value)
                return "The equipment is in emergency stop state, the start command cannot be executed!";

            // 禁止手动状态下启动
            state = await GetBool(address_tup.R14, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@ref.Value)
                return "It is in manual mode now, the start command cannot be executed!";

            string ret = await ConditionWriteAsync(address_tup.R11, address_tup.W01, true, condValue: true);
            if (!string.IsNullOrEmpty(ret))
                return ret;
            return await ConditionWriteAsync(address_tup.R02, address_tup.W01, false, condValue: true);
        }

        public async Task<string> StopAsync()
        {
            //string ret = await WriteBool(address_tup.W02, true);
            //if (!string.IsNullOrEmpty(ret))
            //    return ret;
            return await DelayWriteAsync(address_tup.W02, true, address_tup.W02, false, 1000);
        }

        public async Task<string> OpenAsync()
        {
            Ref<bool> @ref = new();

            // 防止重复操作
            Ref<bool> @r05 = new();
            string state = await GetBool(address_tup.R05, @r05);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@r05.Value)
                return "door is opening!";

            // 防止重复操作
            Ref<bool> @r07 = new();
            state = await GetBool(address_tup.R07, @r07);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@r07.Value)
                return "door is opened!";

            // 将关门写false，双重保险
            await WriteBool(address_tup.W08, false);

            string ret = await WriteBool(address_tup.W07, true);
            if (!string.IsNullOrEmpty(ret))
                return ret;
            _ = ConditionWriteAsync(address_tup.R04, true, address_tup.W07, false);
            return string.Empty;
        }

        public async Task<string> CloseAsync()
        {
            /*
             防止夹车逻辑：
            如果收到天车开门指令，则在DoorAtions中添加一条门对应天车的记录
            收到天车关门指令，则移除DoorActions中门对应天车的记录

            在关门的时候调用函数判断该门是否能关闭
             Tasks.DoorActions.CanClose(DOORNAME)
             */
            //if (!Tasks.DoorActions.CanClose(DeviceEntity.Name))
            //    return $"cannot close door, oht ({Tasks.DoorActions.Ohts(DeviceEntity.Name)}) will pass through {DeviceEntity.Name}";


            // 防止重复操作
            Ref<bool> @r08 = new();
            string state = await GetBool(address_tup.R08, @r08);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@r08.Value)
                return "door is closed!";

            //检测到天车            
            if(address_tup.R16 != string.Empty)
            {
                Ref<bool> @r16 = new();
                string OHTDetection = await GetBool(address_tup.R16, @r16);
                if (!string.IsNullOrEmpty(OHTDetection)) return OHTDetection;
                if (@r16.Value)
                    return $"There is an Oht in Autodoor! R16:{@r16.Value}.";
            }

            // 将开门写false，双重保险
            //await WriteBool(address_tup.W07, false);
            string ret = await WriteBool(address_tup.W08, true);
            if (!string.IsNullOrEmpty(ret))
                return ret;
            _ = ConditionWriteAsync(address_tup.R03, true, address_tup.W08, false);
            return string.Empty;
        }

        public async Task<string> OpenManualAsync(bool o)
        {
            return await WriteBool(address_tup.W03, o);
        }

        public async Task<string> CloseManualAsync(bool o)
        {
            return await WriteBool(address_tup.W04, o);
        }

        public async Task<string> SwitchAsync(bool o)
        {
            // 自动运行状态下禁止切换至手动
            Ref<bool> @ref = new();
            string state = await GetBool(address_tup.R02, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@ref.Value)
                return "It is currently in automatic mode. Please stop running and then switch to manual mode!";

            return await WriteBool(address_tup.W10, o);
        }

        public async Task<string> EmergencyStopAsync(bool o)
        {
            return await WriteBool(address_tup.W05, o);
        }

        public async Task<string> ResetAsync(bool o)
        {
            string w1 = await WriteBool(address_tup.W01, false);
            string w2 = await WriteBool(address_tup.W02, false);
            string w3 = await WriteBool(address_tup.W03, false);
            string w4 = await WriteBool(address_tup.W04, false);
            string w5 = await WriteBool(address_tup.W05, false);
            string w6 = await WriteBool(address_tup.W06, false);
            string w7 = await WriteBool(address_tup.W07, false);
            string w8 = await WriteBool(address_tup.W08, false);
            string w9 = await WriteBool(address_tup.W09, false);
            string w10 = await WriteBool(address_tup.W10, false);
            return await DelayWriteAsync(address_tup.W06, true, address_tup.W06, false, 2000);
        }

        public async Task<string> Enable(bool enabled)
        {
            return await WriteBool(address_tup.RW01, enabled);
        }
        public async Task<string> JogSpeed(short speed)
        {
            return await WriteShort(address_tup.RW02, speed);
        }
        public async Task<string> AutoSpeed(short speed)
        {
            return await WriteShort(address_tup.RW03, speed);
        }
        public async Task<string> OpenedPosition(short pos)
        {
            return await WriteShort(address_tup.RW04, pos);
        }
        public async Task<string> ClosedPosition(short pos)
        {
            return await WriteShort(address_tup.RW05, pos);
        }

        void TimeoutClose()
        {
            return;
            DateTime start_time = DateTime.Now;
            int retry = 3;
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    if ((DateTime.Now - start_time).TotalMilliseconds > 8 * 1000)
                    {
                        if (GetStatus() == 3)
                        {
                            string rc = await CloseAsync();
                            if (string.IsNullOrEmpty(rc))
                            {
                                //Tasks.DoorActions.Remove(DeviceEntity, oht.ToInt32(0));
                                break;
                            }
                        }
                        if (--retry < 1)
                        {
                            break;
                        }
                    }
                    await Task.Delay(100);
                }
            });
        }

    }
}

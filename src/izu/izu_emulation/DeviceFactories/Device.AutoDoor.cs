using IZU.Base;
using IZU.Interfaces;
using Wonder.Infrastructure;

namespace IZU.DeviceFactories
{
    public class AutoDoor : Device, IAutoDoor
    {
        public readonly (string R00, string R01, string R02, string R03, string R04, string R05, string R06, string R07, string R08, string R09, string R10, string R11, string R12, string R13, string R14, string W01, string W02, string W03, string W04, string W05, string W06, string W07, string W08, string W09, string W10) address_tup = new();

        public AutoDoor() { }
        public AutoDoor(DeviceBase deviceEntity) : base(deviceEntity)
        {
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
            address_tup.R14 = GetActionType("R14");  //        自动/手动模式返回信号
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
        public int? GetStatus()
        {
            var opening = _deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R05")?.Value;
            var opened = _deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R07")?.Value;
            var openState = _deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R04")?.Value;
            var closing = _deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R06")?.Value;
            var closed = _deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R08")?.Value;
            var closeState = _deviceEntity.Variables.FirstOrDefault(p => p.ActionType == "R03")?.Value;
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

        public async Task<string> InitialAsync()
        {
            Ref<bool> @ref = new();
            string state = await GetBool(address_tup.R00, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (!@ref.Value) return "No power!";

            state = await GetBool(address_tup.R02, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@ref.Value)
                return "Initialization is prohibited in automatic state, please stop running and then initialize!";

            string w1 = await WriteBool(address_tup.W01, false);
            string w2 = await WriteBool(address_tup.W02, false);
            string w3 = await WriteBool(address_tup.W03, false);
            string w4 = await WriteBool(address_tup.W04, false);
            string w5 = await WriteBool(address_tup.W05, false);
            string w6 = await WriteBool(address_tup.W06, false);
            string w7 = await WriteBool(address_tup.W07, false);
            string w8 = await WriteBool(address_tup.W08, false);
            string w9 = await WriteBool(address_tup.W10, false);
            if (!string.IsNullOrEmpty(w1) || !string.IsNullOrEmpty(w2) || !string.IsNullOrEmpty(w3) || !string.IsNullOrEmpty(w4) || !string.IsNullOrEmpty(w5) || !string.IsNullOrEmpty(w6) || !string.IsNullOrEmpty(w7) || !string.IsNullOrEmpty(w8) || !string.IsNullOrEmpty(w9))
                return "Failed to reset related parameters during initialization!";

            string ret = await WriteBool(address_tup.W09, true);
            if (!string.IsNullOrEmpty(ret))
                return ret;
            //simulation
            await WriteBool(address_tup.R10, true);
            await WriteBool(address_tup.R01, true);
            return await ConditionWriteAsync(address_tup.R10, address_tup.W09, false);
        }

        public async Task<string> StartAsync()
        {
            //simulation
            await WriteBool(address_tup.R02, true);
            return string.Empty;
            //simulation
            string res = await ConditionWriteAsync(address_tup.R11, address_tup.W01, true, condValue: true);
            if (!string.IsNullOrEmpty(res))
                return res;
            return await ConditionWriteAsync(address_tup.R02, address_tup.W01, false, condValue: true);
        }

        public async Task<string> StopAsync()
        { 
            //simulation
            await WriteBool(address_tup.R02, false);
            return string.Empty;
            //simulation
            string res = await WriteBool(address_tup.W02, true);
            if (!string.IsNullOrEmpty(res))
                return res;
            return await ConditionWriteAsync(address_tup.R02, address_tup.W02, false, condValue: false);
        }
#if false
        public async Task<string> OpenAsync()
        {
            string? state = await GetBool(address_tup.R02);
            if (string.IsNullOrEmpty(state) || state == false.ToString())
                return "It is not running automatically now!";

            // 防止重复操作
            string? r05 = await GetBool(address_tup.R05);
            string? r07 = await GetBool(address_tup.R07);
            string? r04 = await GetBool(address_tup.R04);
            if (string.IsNullOrEmpty(r05) || string.IsNullOrEmpty(r07) || string.IsNullOrEmpty(r04))
                return "Read open door signal null!";
            if (bool.Parse(r05) || bool.Parse(r07) || bool.Parse(r04))
                return "door is opening!";

            //禁止关门未完成时执行开门
            string? r06 = await GetBool(address_tup.R06);
            string? r08 = await GetBool(address_tup.R08);
            string? r03 = await GetBool(address_tup.R03);
            if (string.IsNullOrEmpty(r06) || string.IsNullOrEmpty(r08) || string.IsNullOrEmpty(r03))
                return "Read close door signal null!";
            if (bool.Parse(r06) == false && bool.Parse(r08) && bool.Parse(r03))
            {
                // 将关门写false，双重保险
                await WriteBool(address_tup.W08, false);

                string res = await WriteBool(address_tup.W07, true);
                if (!string.IsNullOrEmpty(res))
                    return res;
                return await ConditionWriteAsync(address_tup.R05, address_tup.W07, false);
            }
            else
                return "Closing in progress! Do not open door!";
        }

        public async Task<string> CloseAsync()
        {
            string? state = await GetBool(address_tup.R02);
            if (string.IsNullOrEmpty(state) || state == false.ToString())
                return "It is not running automatically now!";

            // 防止重复操作
            string? r06 = await GetBool(address_tup.R06);
            string? r08 = await GetBool(address_tup.R08);
            string? r03 = await GetBool(address_tup.R03);
            if (string.IsNullOrEmpty(r06) || string.IsNullOrEmpty(r08) || string.IsNullOrEmpty(r03))
                return "Read close door signal null!";
            if (bool.Parse(r06) || bool.Parse(r08) || bool.Parse(r03))
                return "door is closing!";

            //禁止开门未完成时执行关门
            string? r05 = await GetBool(address_tup.R05);
            string? r07 = await GetBool(address_tup.R07);
            string? r04 = await GetBool(address_tup.R04);
            if (string.IsNullOrEmpty(r05) || string.IsNullOrEmpty(r07) || string.IsNullOrEmpty(r04))
                return "Read open door signal null!";
            if (bool.Parse(r05) == false && bool.Parse(r07) && bool.Parse(r04))
            {
                // 将开门写false，双重保险
                await WriteBool(address_tup.W07, false);

                string res = await WriteBool(address_tup.W08, true);
                if (!string.IsNullOrEmpty(res))
                    return res;
                return await ConditionWriteAsync(address_tup.R06, address_tup.W08, false);
            }
            else
                return "Opening in progress! Do not close door!";
        }

#endif
        public async Task<string> OpenAsync()
        {
            Ref<bool> @ref = new();
            string state = await GetBool(address_tup.R02, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (!@ref.Value) return "It is not running automatically now!";

            Ref<bool> @r04 = new();
            Ref<bool> @r05 = new();
            Ref<bool> @r07 = new();
            // 防止重复操作
            state = await GetBool(address_tup.R05, @r05);
            if (!string.IsNullOrEmpty(state)) return state;
            state = await GetBool(address_tup.R07, @r07);
            if (!string.IsNullOrEmpty(state)) return state;
            state = await GetBool(address_tup.R04, @r04);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@r05.Value || @r07.Value || @r04.Value)
                return "door is opening!";


            Ref<bool> @r06 = new();
            Ref<bool> @r08 = new();
            Ref<bool> @r03 = new();
            //禁止关门未完成时执行开门
            state = await GetBool(address_tup.R06, @r06);
            if (!string.IsNullOrEmpty(state)) return state;
            state = await GetBool(address_tup.R08, @r08);
            if (!string.IsNullOrEmpty(state)) return state;
            state = await GetBool(address_tup.R03, @r03);
            if (!string.IsNullOrEmpty(state)) return state;
            if (!@r06.Value && @r08.Value && @r03.Value)
            {
                // 将关门写false，双重保险
                await WriteBool(address_tup.W08, false);

                string res = await WriteBool(address_tup.W07, true);
                if (!string.IsNullOrEmpty(res))
                    return res;

                //simulation
                await WriteBool(address_tup.R05, true);
                _ = Task.Factory.StartNew(async () =>
                {
                    await WriteBool(address_tup.R06, false);
                    await WriteBool(address_tup.R08, false);
                    await WriteBool(address_tup.R03, false);
                    await Task.Delay(3000);
                    await WriteBool(address_tup.R05, false);
                    await WriteBool(address_tup.R07, true);
                    await WriteBool(address_tup.R04, true);
                });
                //simulation

                return await ConditionWriteAsync(address_tup.R05, address_tup.W07, false, true);
            }
            else
            {
                return "Closing in progress! Do not open door!";
            }
        }

        public async Task<string> CloseAsync()
        {
            Ref<bool> @ref = new();
            string state = await GetBool(address_tup.R02, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (!@ref.Value) return "It is not running automatically now!";

            Ref<bool> @r06 = new();
            Ref<bool> @r08 = new();
            Ref<bool> @r03 = new();
            // 防止重复操作
            state = await GetBool(address_tup.R06, @r06);
            if (!string.IsNullOrEmpty(state)) return state;
            state = await GetBool(address_tup.R08, @r08);
            if (!string.IsNullOrEmpty(state)) return state;
            state = await GetBool(address_tup.R03, @r03);
            if (!string.IsNullOrEmpty(state)) return state;

            if (@r06.Value || @r08.Value || @r03.Value)
                return "door is closing!";

            Ref<bool> @r04 = new();
            Ref<bool> @r05 = new();
            Ref<bool> @r07 = new();
            // 防止重复操作
            state = await GetBool(address_tup.R05, @r05);
            if (!string.IsNullOrEmpty(state)) return state;
            state = await GetBool(address_tup.R07, @r07);
            if (!string.IsNullOrEmpty(state)) return state;
            state = await GetBool(address_tup.R04, @r04);
            if (!string.IsNullOrEmpty(state)) return state;

            if (!@r05.Value && @r07.Value && @r04.Value)
            {
                // 将开门写false，双重保险
                await WriteBool(address_tup.W07, false);

                string res = await WriteBool(address_tup.W08, true);
                if (!string.IsNullOrEmpty(res))
                    return res;

                //simulation
                await WriteBool(address_tup.R06, true);
                _ = Task.Factory.StartNew(async () =>
                {
                    await WriteBool(address_tup.R05, false);
                    await WriteBool(address_tup.R07, false);
                    await WriteBool(address_tup.R04, false);
                    await Task.Delay(3000);
                    await WriteBool(address_tup.R06, false);
                    await WriteBool(address_tup.R08, true);
                    await WriteBool(address_tup.R03, true);
                });
                //simulation

                return await ConditionWriteAsync(address_tup.R06, address_tup.W08, false);
            }
            else
            {
                return "Opening in progress! Do not close door!";
            }
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
            Ref<bool> @ref = new();
            await GetBool(address_tup.R14, @ref);
            await WriteBool(address_tup.R14, !@ref.Value);
            return string.Empty;
             @ref = new();
            string state = await GetBool(address_tup.R02, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@ref.Value)
                return "It is currently in automatic mode. Please stop running and then switch to manual mode!";
            return await WriteBool(address_tup.W10, o);
        }

        public async Task<string> EmergencyStopAsync(bool o)
        {
            Ref<bool> @ref = new();
            await GetBool(address_tup.R13, @ref);
            await WriteBool(address_tup.R13, !@ref.Value);
            return await WriteBool(address_tup.W05, o);
        }

        public async Task<string> ResetAsync(bool o)
        {
            return await DelayWriteAsync(address_tup.W06, true, address_tup.W06, false, 2000);
        }
    }
}

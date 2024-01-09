using IZU.Entities;
using IZU.Interfaces;
using System.Net.NetworkInformation;

namespace IZU.DeviceFactories
{
    public class AutoDoor : Device, IAutoDoor
    {
        public readonly (string R00, string R01, string R02, string R03, string R04, string R05, string R06, string R07, string R08, string R09, string R10, string R11, string W01, string W02, string W03, string W04, string W05, string W06, string W07, string W08, string W09, string W10) address_tup = new();

        public AutoDoor() { }
        public AutoDoor(DeviceEntity deviceEntity) : base(deviceEntity)
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

        public async Task<string> OpenAsync()
        {
            string res = await WriteBool(address_tup.W07, true);
            if (string.IsNullOrEmpty(res))
            {
                res = await ConditionWriteAsync(address_tup.R05, address_tup.W07, false);
                return res;
            }
            else
                return res;
        }

        public async Task<string> CloseAsync()
        {
            string res = await WriteBool(address_tup.W08, true);
            if (string.IsNullOrEmpty(res))
            {
                res = await ConditionWriteAsync(address_tup.R06, address_tup.W08, false);
                return res;
            }
            else
                return res;
        }

        public async Task<string> OpenManualAsync(bool oper)
        {
            return await WriteBool(address_tup.W03, oper);
        }

        public async Task<string> CloseManualAsync(bool oper)
        {
            return await WriteBool(address_tup.W04, oper);
        }

        public async Task<string> EmergencyStopAsync(bool oper)
        {
            string res = await WriteBool(address_tup.W05, oper);
            return "";
        }

        public async Task<string> ResetAsync()
        {
            return await DelayWriteAsync(address_tup.W06, true, address_tup.W06, false,2000);
        }

        public async Task<string> StartAsync()
        {
            string res =await ConditionWriteAsync(address_tup.R11, address_tup.W01, true, condValue: true);
             //res = await WriteBool(address_tup.W01, true);
            if (string.IsNullOrEmpty(res))
            {
                res = await ConditionWriteAsync(address_tup.R02, address_tup.W01, false, condValue:true);
                return res;
            }
            else
                return res;
        }

        public async Task<string> StopAsync()
        {
            string res = await WriteBool(address_tup.W02, true);
            if (string.IsNullOrEmpty(res))
            {
                res = await ConditionWriteAsync(address_tup.R02, address_tup.W02, false, condValue:false);
                return res;
            }
            else
                return res;
        }

        public async Task<string> InitialAsync(bool oper)
        {
            var status = await GetBool(address_tup.R00);
            if (status == "False")
                return "未上电";
            if (oper)
            {
                await WriteBool(address_tup.W01, false);
                await WriteBool(address_tup.W02, false);
                await WriteBool(address_tup.W03, false);
                await WriteBool(address_tup.W04, false);
                await WriteBool(address_tup.W07, false);
                await WriteBool(address_tup.W08, false);
                await WriteBool(address_tup.W05, false);
                await WriteBool(address_tup.W06, false);
                await WriteBool(address_tup.W10, false);
            }
            string res = await DelayWriteAsync(address_tup.W09, true, address_tup.W09, false, 2000);
            //string res = await WriteBool(address_tup.W09, true);
            //if (string.IsNullOrEmpty(res))
            //{
            //    res = ConditionWrite(address_tup.R10, address_tup.W09, false);
            //}
            return res;
        }

        public async Task<string> SwitchAsync(bool oper)
        {
            return await WriteBool(address_tup.W10, oper);
        }
    }
}

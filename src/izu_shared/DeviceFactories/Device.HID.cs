using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
using Newtonsoft.Json.Linq;
using System.CommandLine.Binding;

namespace IZU.DeviceFactories
{
    public class HID : Device, IHID
    {
        public readonly (string R00, string R01, string R02, string R03, string R04, string R05, string R06, string R07, string R08, string R09, string R10, string R11, string W01, string W02, string W03, string W04, string W05) address_tup = new();

        public string? fireAlarmInfo = string.Empty;
        public HID() { }
        public HID(DeviceEntity deviceEntity) : base(deviceEntity)
        {
            address_tup.R01 = GetActionType("R01");  //   PSP待机状态
            address_tup.R02 = GetActionType("R02");  //   PSP停止状态信号
            address_tup.R03 = GetActionType("R03");  //   PSP运行状态信号
            address_tup.R04 = GetActionType("R04");  //   PSP温度异常过高
            address_tup.R05 = GetActionType("R05");  //   PSP温度正常
            address_tup.R06 = GetActionType("R06");  //   电柜失电状态信号
            address_tup.R07 = GetActionType("R07");  //   电柜送电状态信号
            address_tup.R08 = GetActionType("R08");  //   PSP故障报警状态
            address_tup.R00 = GetActionType("R00");  //   PSP上电完成
            address_tup.R10 = GetActionType("R10");  //   火警信号
            address_tup.R11 = GetActionType("R11");  //   紧急停止返回信号
            address_tup.R09 = GetActionType("R09");  //   电柜当前温度值
            address_tup.W01 = GetActionType("W01");  //   启动PSP运行
            address_tup.W02 = GetActionType("W02");  //   停止PSP运行
            address_tup.W03 = GetActionType("W03");  //   故障报警复位PSP运行     按住写true，放开写false
            address_tup.W04 = GetActionType("W04");  //   PSP紧急停止             按一下写true，再按一下写false
            address_tup.W05 = GetActionType("W05");  //   火灾报警关闭PSP电源     按住写true，放开写false
        }

        public async Task<string> StartAsync()
        {
            Ref<bool> @ref = new();

            // 是否上电
            string state = await GetBool(address_tup.R00, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (!@ref.Value)
                return "No power!";

            // 禁止故障状态下启动
            state = await GetBool(address_tup.R08, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@ref.Value)
                return "There is a fault alarm, the start command cannot be executed!";

            // 禁止急停状态下启动
            state = await GetBool(address_tup.R11, @ref);
            if (!string.IsNullOrEmpty(state)) return state;
            if (@ref.Value)
                return "The equipment is in emergency stop state, the start command cannot be executed!";

            // 停止运行写false，启动运行写true，双保险
            string ret = await WriteBool(address_tup.W02, false);
            if (!string.IsNullOrEmpty(ret))
                return ret;

            ret = await WriteBool(address_tup.W01, true);
            if (!string.IsNullOrEmpty(ret))
                return ret;
            return await ConditionWriteAsync(address_tup.R03, address_tup.W01, false);
        }

        public async Task<string> StopAsync()
        {
            // 启动运行写false，停止运行写true，双保险
            string ret = await WriteBool(address_tup.W01, false);
            if (!string.IsNullOrEmpty(ret))
                return ret;

            ret = await WriteBool(address_tup.W02, true);
            if (!string.IsNullOrEmpty(ret))
                return ret;
            return await ConditionWriteAsync(address_tup.R02, address_tup.W02, false);
        }

        public async Task<string> ResetAsync(bool o)
        {
            return await WriteBool(address_tup.W03, o);
        }

        public async Task<string> EmergencyStopAsync(bool o)
        {
            return await WriteBool(address_tup.W04, o);
        }
    }
}

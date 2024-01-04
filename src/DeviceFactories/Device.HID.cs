using IZU.Entities;
using IZU.Interfaces;

namespace IZU.DeviceFactories
{
    public class HID : Device, IHID
    {
        public readonly (string R01, string R02, string R03, string R04, string R05, string R06, string R07, string R08, string R09, string W01, string W02, string W03, string W04, string W05) address_tup = new();

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
            address_tup.R09 = GetActionType("R09");  //   电柜当前温度值
            address_tup.W01 = GetActionType("W01");  //   启动PSP运行
            address_tup.W02 = GetActionType("W02");  //   停止PSP运行
            address_tup.W03 = GetActionType("W03");  //   故障报警复位PSP运行
            address_tup.W04 = GetActionType("W04");  //   PSP紧急停止
            address_tup.W05 = GetActionType("W05");  //   火灾报警关闭PSP电源
        }

        public async Task<string> EmergencyStopAsync()
        {
            string res = await WriteBool(address_tup.W04, true);
            RunAfter(2000, () => { WriteBool(address_tup.W04, false); });
            return "";
        }

        public async Task<string> PowerOffAsync()
        {
            return await WriteBool(address_tup.W05, true);
        }

        public async Task<string> ResetAsync()
        {
            return await WriteBool(address_tup.W03, true);
        }

        public async Task<string> StartAsync()
        {
            string res = await WriteBool(address_tup.W01, true);
            if (string.IsNullOrEmpty(res))
            {
                res = ConditionWrite(address_tup.R03, address_tup.W01, false);
                return res;
            }
            else
                return res;
        }

        public async Task<string> StopAsync()
        {
            return await WriteBool(address_tup.W02, false);
        }
    }
}

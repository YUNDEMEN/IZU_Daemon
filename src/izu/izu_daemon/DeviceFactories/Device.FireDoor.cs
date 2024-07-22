using IZU.Base;
using IZU.Interfaces;

namespace IZU.DeviceFactories
{
    public class FireDoor : Device, IFireDoor
    {
        public readonly (string R01, string R02, string R03, string R04, string R05, string R06, string R07, string R08, string R09, string R10, string R11, string W01, string W02, string W03, string W04, string W05, string W06, string W07, string W08, string W09, string W10) address_tup = new();

        public FireDoor() { }
        public FireDoor(DeviceBase deviceEntity) : base(deviceEntity)
        {
            address_tup.R01 = GetActionType("R01");  //        系统开机状态
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
        public int? GetStatus(ILogger? logger = null)
        {
            return 0;
        }

        public async Task<string> OpenAsync()
        {
            return await WriteBool(address_tup.W07, true);
        }

        public async Task<string> CloseAsync()
        {
            return await WriteBool(address_tup.W08, true);
        }

        public async Task<string> EmergencyStopAsync(bool o)
        {
            string res = await WriteBool(address_tup.W05, o);
            return "";
        }

        public async Task<string> ResetAsync(bool o)
        {
            return await WriteBool(address_tup.W06, true);
        }

        public async Task<string> StartAsync()
        {
            return await WriteBool(address_tup.W01, true);
        }

        public async Task<string> StopAsync()
        {
            return await WriteBool(address_tup.W02, false);
        }

        public Task<string> OpenManualAsync(bool o)
        {
            throw new NotImplementedException();
        }

        public Task<string> CloseManualAsync(bool o)
        {
            throw new NotImplementedException();
        }
    }
}

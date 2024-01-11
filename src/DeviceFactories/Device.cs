using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Timers;
using static TinyCsvParser.Tokenizer.RFC4180.Reader;

namespace IZU.DeviceFactories
{
    public abstract class Device : IDevice
    {
        private DeviceEntity _deviceEntity;
        public DeviceEntity DeviceEntity => _deviceEntity;

        public Device() { _deviceEntity = DeviceEntity.DummyDevice; }
        public Device(DeviceEntity deviceEntity)
        {
            _deviceEntity = deviceEntity;
        }
        ILogger<Device> _logger;
        protected virtual string GetActionType(string actionType)
        {
            _logger = IZULogging.Factory.CreateLogger<Device>();
            var v = _deviceEntity.Variables.FirstOrDefault(t => t.ActionType == actionType);
            if (v == null || string.IsNullOrEmpty(v.Address))
                throw new Exception($"{actionType} action is not marked in {_deviceEntity.Name}");
            return v.Address;
        }

        public async Task<string> WriteBool(string address, bool value)
        {
            if (_deviceEntity == null)
                return "device not exist!";
            if (_deviceEntity.Server == null)
                return "device server not exist!";
            return await _deviceEntity.Server.WriteBool(address, value);
        }

        public async Task<string?> GetBool(string address)
        {
            if (_deviceEntity == null)
                return "device not exist!";
            if (_deviceEntity.Server == null)
                return "device server not exist!";
            return (await _deviceEntity.Server.GetBool(address)).ToString();
        }

        /// <summary>
        /// 根据提供的 address_condition 判断其值是否为 condValue
        /// 如果是，则按照提供的 address_write 写如对应的 value
        /// </summary>
        /// <param name="address_condition">条件地址</param>
        /// <param name="address_write">写入地址</param>
        /// <param name="value">写入值</param>
        /// <param name="condValue">用于和address_condition的值进行比较的值</param>
        /// <returns></returns>
        protected async Task<string> ConditionWriteAsync(string address_condition, string address_write, bool value, bool condValue = true)
        {
            DateTime startTime = DateTime.Now;
            string result = string.Empty;

            bool? cond = false;
            while (true)
            {
                if (DateTime.Now - startTime > TimeSpan.FromSeconds(5))
                {
                    result = "读取超时";
                    break;
                }

                cond = await _deviceEntity!.Server!.GetBool(address_condition);
                if ((bool)cond != condValue)
                {
                    await Task.Delay(10);
                    continue;
                }
                else if ((bool)cond == condValue)
                {
                    break;
                }
            }
            if (string.IsNullOrWhiteSpace(result))
                await _deviceEntity.Server!.WriteBool(address_write, value);
            else
            {
                _logger.LogWarning("{0}, 地址{1}读取失败，{2}", _deviceEntity.Name, address_condition, result);
            }
            return result;
        }

        protected async Task<string> DelayWriteAsync(string address_write, bool value, string delay_address_write, bool delayValue, int delay)
        {
            await _deviceEntity.Server!.WriteBool(address_write, value);
            string result = string.Empty;
            await Task.Delay(delay);
            result = await _deviceEntity.Server!.WriteBool(delay_address_write, delayValue);
            return result;
        }

        public virtual bool CheckAddress(string address)
        {
            return !string.IsNullOrEmpty(address);
        }

        //protected void RunAfter(int millionSecs, Action action)
        //{
        //    System.Threading.Timer timer;
        //    timer = new System.Threading.Timer((o) =>
        //    {
        //        action();
        //        timer.Change(Timeout.Infinite, Timeout.Infinite);
        //        timer.Dispose();
        //    }, null, millionSecs, Timeout.Infinite);
        //}
        protected void RunAfter(int million_seconds, Action action)
        {
            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(million_seconds);
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Exception from {_deviceEntity.Name}, {ex.Message}", ex);
                }
                finally { }
            });
        }
    }
}
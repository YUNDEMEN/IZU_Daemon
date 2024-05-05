using IZU.Base;
using Newtonsoft.Json.Linq;
using System.Net;
using System.ServiceProcess;

namespace IZU.Base
{
    public class KeyValueObject
    {
        /// <summary>
        /// 数据地址映射为上位机变量
        /// </summary>
        public string ActionType { get; set; }
        private object? _value;
        public object? Value
        {
            get => _value;
            set
            {
                if (_value?.ToString() != value?.ToString())
                {
                    OnValueChanged(value, _value);
                    _value = value;
                }
            }
        }
        protected virtual void OnValueChanged(object? newValue,object? oldValue)
        {

        }
    }
    public class VariableEntity : KeyValueObject
    {
        /// <summary>
        /// 设备IP
        /// </summary>
        public string ServerIP { get; set; } = string.Empty;
        /// <summary>
        /// 设备名称（不重复）
        /// </summary>
        public string DeviceName { get; set; } = string.Empty;
        /// <summary>
        /// 设备类型
        /// </summary>
        public DeviceTypes DeviceType { get; set; }
        /// <summary>
        /// 指令读/写
        /// </summary>
        public FunctionTypes FunctionType { get; set; }

        /// <summary>
        /// 变量名
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// 变量类型
        /// </summary>
        public VariableTypes VariableType { get; set; }

        protected override void OnValueChanged(object? newValue, object? oldValue)
        {
            /*
             通过将object值转化为string对比其是否变更
             如果变更则保存变更信息到数据存储 
             每次重启服务都会记录一次变更信息
             */
            if (ActionType != "R01")
            {
                //string header =                                     "设备名称,       设备类型,        地址,           操作              旧值,         新值,           变量类型,          描述,                记录时间";
                TextRecorder.Instance.EnqueueAsync($"{DeviceName},{DeviceType},{Address},{FunctionType},{oldValue},{newValue},{VariableType},{Description},{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            LastRefreshTime = DateTime.Now;
        }
        /// <summary>
        /// 变量描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// 最后一次刷新时间
        /// </summary>
        public DateTime? LastRefreshTime { get; set; }
        public int RefreshInterval { get; set; } = 100;
        /// <summary>
        /// 已禁用
        /// </summary>
        public bool Disabled { get; set; } = false;
        public VariableEntity() { }
    }


}

using Newtonsoft.Json.Converters;
using System.Text.Json.Serialization;

namespace OHTC.Tools
{
    /// <summary>
    /// 通讯参数
    /// </summary>
    public class OSOParameter
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public OSOEnumType opname { get; set; }
        public object opparas { get; set; }
    }

    /// <summary>
    /// WEB 与OSO 通讯枚举定义
    /// </summary>
    public enum OSOEnumType
    {
        WEB_GET_LAYOUT_DATA,
        WEB_GET_OHT_DATA,
        WEB_MOVE_OHT,
        WEB_TRANS_W_OHT,
        WEB_TRANSFER,
        WEB_GET_PORT_DATA,
        WEB_GET_SEGMENT_DATA,
        WEB_GET_IZU_DATA,
        WEB_GET_BAY_DATA,
        WEB_GET_EQ_DATA,
        WEB_GET_OHT_LIST,
        PUB_DEL_OHT,//webserver 2.3.5
        PUB_SW_OHT_OP,//webserver 2.3.7
        PUB_SET_POINT,//webserver 2.4.1
        PUB_SET_STATION,//webserver 2.4.2
        PUB_SET_SEGMENT,// 2.4.3
        PUB_SET_IZU,// 3.5.2
        PUB_SET_BAY,
        PUB_SET_EQ,//webserver 3.7.2
        OHT_COMMAND,
        WEB_MAP_DOWNLOAD,
        WEB_MAP_UPDATE,
        GEM_CANCEL,
        GEM_ABORT,
        GEM_STATE_CTL
    }
}

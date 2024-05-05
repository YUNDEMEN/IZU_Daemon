using System;
using System.Collections.Generic;
using System.Text;

namespace IZU.Base.dto
{
    public class info
    {
        public string ip { get; set; }
        public string address { get; set; }
        public string description { get; set; }
    }

    public class info_count
    {
        public int count { get; set; }
        public List<info> info { get; set; } = new List<info>();
    }

    public class error
    {
        public info_count izu { get; set; } = new info_count();
        public info_count autodoor { get; set; } = new info_count();
        public info_count hid { get; set; } = new info_count();
    }

    public class izu_status
    {
        public error error { get; set; }
        public bool offline { get; set; }
    }
}

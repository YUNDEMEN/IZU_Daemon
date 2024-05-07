using System;

namespace Wonder.Service.Framework
{
    public class TypeService
    {
        public string Key { get; set; }
        public Type? Service { get; set; }
        public Type? Implementation { get; set; }
        public TypeService(string Key, Type? Service, Type Implementation)
        {
            this.Key = Key;
            this.Service = Service;
            this.Implementation = Implementation;
        }
    }
}

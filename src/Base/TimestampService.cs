namespace IZU.Base
{
    public class TimestampService
    {
        static readonly DateTime time1970 = new(1970, 1, 1, 0, 0, 0, 0);
        static long _timestamp;

        public static long Current()
        {
            return Convert.ToInt64((DateTime.UtcNow - time1970).TotalMilliseconds); 
        }

        public static long Pinning()
        {
            _timestamp = Current();
            return _timestamp;
        }

        public static long Difference()
        {
            return Current() - _timestamp;
        }
    }
}

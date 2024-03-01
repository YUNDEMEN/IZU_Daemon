using System.Text.Json.Serialization;
using System.Text.Json;

namespace Wonder.Infrastructure
{
    public static class ParseExtensions
    {
        public static int ToInt32(this string value, int defaultValue = 0)
        {
            if (int.TryParse(value, out int result))
                return result;
            return defaultValue;
        }
    }
}

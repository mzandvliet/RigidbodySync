using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace RamjetAnvil.Unity.Utility
{
    public static class EnumUtils
    {
        public static T[] GetValues<T>() where T : struct, IComparable, IConvertible, IFormattable {
            return Enum.GetValues(typeof (T)).Cast<T>().ToArray();
        }

        public static T Parse<T>(string name) {
            return (T)Enum.Parse(typeof (T), name);
        }

        public static T FromString<T>(string value)
        {
            return (T) Enum.Parse(typeof (T), value);
        }

        public static string ToPrettyString<TEnum>(TEnum @enum)
        {
            var prettyStr = Regex.Replace(@enum.ToString(), "([A-Z])", " $1");
            prettyStr = prettyStr.Trim();
            return prettyStr.Substring(0, 1).ToUpperInvariant() + prettyStr.Substring(1).ToLowerInvariant();
        }
    }
}
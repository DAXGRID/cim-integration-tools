using System.Collections.Specialized;

namespace DAX.Util
{
    public static class Configuration
    {
        private static NameValueCollection _configuration = null;

        public static string GetConnectionString(string stringOrConnectionStringName)
        {
            return stringOrConnectionStringName;
        }

        public static NameValueCollection GetConfiguration()
        {
            return _configuration;
        }
    }
}

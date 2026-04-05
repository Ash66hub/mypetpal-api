using NanoidDotNet;

namespace mypetpal.Data.Common
{
    public static class PublicIdGenerator
    {
        public static string NewId()
        {
            return Nanoid.Generate(size: 21);
        }
    }
}
using NanoidDotNet;

namespace mypetpal.Data.Common
{
    public static class IdGenerator
    {
        private const int DefaultSize = 10;

        public static string NewId(int size = DefaultSize)
        {
            if (size < 8 || size > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "ID length must be between 8 and 12 characters.");
            }

            return Nanoid.Generate(size: size);
        }
    }
}

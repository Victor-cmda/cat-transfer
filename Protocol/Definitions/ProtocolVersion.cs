namespace Protocol.Definitions
{
    public static class ProtocolVersion
    {
        public const int Major = 1;
        public const int Minor = 0;
        public const int Patch = 0;
        public static string Current => $"{Major}.{Minor}.{Patch}";
        public static readonly Version MinSupported = new(1, 0, 0);
        public static readonly Version MaxSupported = new(1, 0, 0);
        public static bool IsCompatible(Version version)
        {
            return version >= MinSupported && version <= MaxSupported;
        }

        public static bool IsCompatible(string versionString)
        {
            if (Version.TryParse(versionString, out var version))
            {
                return IsCompatible(version);
            }
            return false;
        }

        public static Version Parse(string versionString)
        {
            if (!Version.TryParse(versionString, out var version))
            {
                throw new FormatException($"Invalid version format: {versionString}");
            }
            return version;
        }
    }
}

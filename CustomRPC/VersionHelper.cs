using System;

namespace CustomRPC
{
    internal static class VersionHelper
    {
        /// <summary>
        /// Helper class to get a proper version object.
        /// </summary>
        /// <param name="version">A string representing version.</param>
        /// <returns>A <see cref="Version"/> object with no fields set to -1.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static Version GetVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                throw new ArgumentNullException("version");

            if (version.StartsWith("v"))
                version = version.Substring(1);

            var array = version.Split('.');

            if (array.Length < 2 || array.Length > 4)
                throw new ArgumentException($"Version has {array.Length} part(s)!", "version");

            switch (array.Length)
            {
                case 2:
                    return Version.Parse(version + ".0.0");
                case 3:
                    return Version.Parse(version + ".0");
            }

            return Version.Parse(version);
        }

        /// <summary>
        /// Helper class to get a version string (major.minor.build; revision only if non-zero).
        /// </summary>
        /// <param name="version">A version object.</param>
        /// <returns>A display string (ex: 2.0.0 or 1.19.2).</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static string GetVersionString(Version version)
        {
            if (version == null)
                throw new ArgumentNullException("version");

            int build = version.Build >= 0 ? version.Build : 0;
            string res = version.Major + "." + version.Minor + "." + build;

            if (version.Revision > 0)
                res += "." + version.Revision;

            return res;
        }

        /// <summary>
        /// Helper class to get a version string (major.minor.build; revision only if non-zero).
        /// </summary>
        /// <param name="version">A string representing version.</param>
        /// <returns>A display string (ex: 2.0.0 or 1.19.2).</returns>
        public static string GetVersionString(string version)
        {
            return GetVersionString(GetVersion(version));
        }
    }
}

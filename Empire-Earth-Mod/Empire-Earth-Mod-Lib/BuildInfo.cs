using System;
using System.Reflection;

namespace Empire_Earth_Mod_Lib
{
    /// <summary>
    /// Version of the Empire Earth tools, read from the assembly attributes that
    /// SharedAssemblyInfo.cs (repository root) defines for every project.
    /// </summary>
    public static class BuildInfo
    {
        /// <summary>
        /// Full product version including a pre-release tag, if any (e.g. "1.0.0"),
        /// taken from <see cref="AssemblyInformationalVersionAttribute"/>. Falls back
        /// to the assembly version if the attribute is missing.
        /// </summary>
        public static string InformationalVersion
        {
            get
            {
                Assembly assembly = typeof(BuildInfo).Assembly;
                var attribute = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    assembly, typeof(AssemblyInformationalVersionAttribute));
                return attribute != null
                    ? attribute.InformationalVersion
                    : assembly.GetName().Version.ToString();
            }
        }
    }
}

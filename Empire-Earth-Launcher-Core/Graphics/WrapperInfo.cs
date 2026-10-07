using System;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Graphics
{
    /// <summary>Which DirectX wrapper the setup installed, as far as the components of the setup tell.</summary>
    public enum WrapperKind
    {
        /// <summary>No wrapper: the game talks to DirectDraw of Windows ("Native" on the setup's graphics card page).</summary>
        None,

        /// <summary>The component <c>dx7</c>: DDrawCompat.</summary>
        DirectX7,

        /// <summary>The component <c>dx9</c>: the wrapper of GOG.</summary>
        DirectX9,

        /// <summary>One of the dgVoodoo components <c>dx11_lvl10</c> to <c>dx12_lvl12</c>.</summary>
        DgVoodoo,

        /// <summary>A wrapper component this launcher does not know, or the wrapper without its variant.</summary>
        Other
    }

    /// <summary>Where the answer of <see cref="WrapperInfo"/> comes from (the wrapper rule of contract 3.3).</summary>
    public enum WrapperSource
    {
        /// <summary>The components of <c>install.ini</c>.</summary>
        InstallInfo,

        /// <summary>The components of the uninstall key.</summary>
        UninstallKey,

        /// <summary>No component information: the wrapper files in the game folder.</summary>
        GameFolder
    }

    /// <summary>
    /// The DirectX wrapper of an installation for the graphics page (read-only): from the components of <c>install.ini</c>,
    /// else of the uninstall key, and only without component information from the wrapper files in the game folder (contract
    /// 3.3, <see cref="ComputedValues.DirectXWrapper"/>). The launcher never changes the wrapper: that means adding or
    /// removing game files, which only the setup does (contract 2.5, ADR 0014).
    /// </summary>
    public sealed class WrapperInfo
    {
        private static readonly Regex DgVoodooComponent = new Regex(@"^dx(?<version>\d+)_lvl(?<level>\d+(?:_\d+)?)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private WrapperInfo(WrapperKind kind, WrapperSource source, string component, string directXVersion, string apiLevel,
            string wrapperFile)
        {
            Kind = kind;
            Source = source;
            Component = component;
            DirectXVersion = directXVersion;
            ApiLevel = apiLevel;
            WrapperFile = wrapperFile;
        }

        public WrapperKind Kind { get; }

        public WrapperSource Source { get; }

        /// <summary>The name below <c>additional\directx_wrapper\</c> as the setup wrote it (<c>dx11_lvl10_1</c>); null if there is none.</summary>
        public string Component { get; }

        /// <summary>For dgVoodoo the DirectX version of the component (<c>11</c>, <c>12</c>); else null.</summary>
        public string DirectXVersion { get; }

        /// <summary>For dgVoodoo the API (feature) level of the component (<c>10</c>, <c>10.1</c>, <c>11</c>, <c>12</c>); else null.</summary>
        public string ApiLevel { get; }

        /// <summary>For <see cref="WrapperSource.GameFolder"/> the wrapper file that was found (<c>DDraw.dll</c>), else null.</summary>
        public string WrapperFile { get; }

        /// <summary>True for a wrapper that is installed; false for <see cref="WrapperKind.None"/>.</summary>
        public bool IsInstalled
        {
            get { return Kind != WrapperKind.None; }
        }

        /// <summary>The wrapper of <paramref name="installation"/>.</summary>
        public static WrapperInfo Describe(Installation installation, IFileSystem fileSystem)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            RasterizerRecommendation rule = ComputedValues.DirectXWrapper(installation, Game.EmpireEarth, fileSystem);
            WrapperSource source = SourceOf(rule.Reason);
            if (!ComputedValues.IsWrapper(rule.Reason))
                return new WrapperInfo(WrapperKind.None, source, null, null, null, null);
            if (source == WrapperSource.GameFolder)
                return new WrapperInfo(WrapperKind.Other, source, null, null, null, rule.WrapperFile);

            string prefix = ContractNames.DirectXWrapperComponent + @"\";
            string component = installation.Components.Names
                .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.Length > prefix.Length)
                .Select(name => name.Substring(prefix.Length))
                .FirstOrDefault();
            if (string.Equals(component, "dx7", StringComparison.OrdinalIgnoreCase))
                return new WrapperInfo(WrapperKind.DirectX7, source, component, null, null, null);
            if (string.Equals(component, "dx9", StringComparison.OrdinalIgnoreCase))
                return new WrapperInfo(WrapperKind.DirectX9, source, component, null, null, null);
            Match dgVoodoo = component == null ? Match.Empty : DgVoodooComponent.Match(component);
            if (dgVoodoo.Success)
                return new WrapperInfo(WrapperKind.DgVoodoo, source, component, dgVoodoo.Groups["version"].Value,
                    dgVoodoo.Groups["level"].Value.Replace('_', '.'), null);
            return new WrapperInfo(WrapperKind.Other, source, component, null, null, null);
        }

        private static WrapperSource SourceOf(RasterizerReason reason)
        {
            switch (reason)
            {
                case RasterizerReason.WrapperInInstallInfo:
                case RasterizerReason.NoWrapperInInstallInfo:
                    return WrapperSource.InstallInfo;
                case RasterizerReason.WrapperInUninstallKey:
                case RasterizerReason.NoWrapperInUninstallKey:
                    return WrapperSource.UninstallKey;
                default:
                    return WrapperSource.GameFolder;
            }
        }

        public override string ToString()
        {
            return Kind + (Component == null ? string.Empty : " " + Component) + (WrapperFile == null ? string.Empty : " " + WrapperFile) +
                   " (" + Source + ")";
        }
    }
}

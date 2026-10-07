using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Empire_Earth_Launcher.Core.Graphics
{
    /// <summary>One key of a <c>dgVoodoo.conf</c> that still has the value of a preset before setup 1.1.0; <see cref="Value"/> null = not set.</summary>
    public sealed class PresetFinding
    {
        internal PresetFinding(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; }

        /// <summary>The value as written in the file; null if the conf does not have the key.</summary>
        public string Value { get; }

        public override string ToString()
        {
            return Key + " = " + (Value ?? "(not set)");
        }
    }

    /// <summary>
    /// Whether a <c>dgVoodoo.conf</c> still has the window settings of a setup before 1.1.0 (setup ADR 0005, amendment
    /// 2026-10-07): <c>Version</c> below <c>0x287</c> or missing, <c>DeferredScreenModeSwitch = true</c>,
    /// <c>DisableAltEnterToToggleScreenMode = false</c>, or <c>FullscreenAttributes</c> missing or without the item
    /// <c>fake</c>. Read only: the launcher never changes the file (ADR 0014); the setup writes the current settings.
    /// </summary>
    /// <remarks>
    /// A missing <c>DeferredScreenModeSwitch</c> or <c>DisableAltEnterToToggleScreenMode</c> is not outdated: dgVoodoo's
    /// defaults are <c>false</c> and <c>true</c>, which are the current values.
    /// </remarks>
    public static class DgVoodooPreset
    {
        /// <summary>The <c>Version</c> of dgVoodoo 2.87 (<c>0x287</c>), the first with the window settings of setup 1.1.0.</summary>
        public const int CurrentVersion = 0x287;

        public const string VersionKey = "Version";
        public const string DeferredKey = "DeferredScreenModeSwitch";
        public const string AltEnterKey = "DisableAltEnterToToggleScreenMode";
        public const string FullscreenAttributesKey = "FullscreenAttributes";

        private const string FakeItem = "fake";

        /// <summary>The keys of <paramref name="conf"/> that have an outdated value, in the order Version, Deferred, Alt+Enter, FullscreenAttributes.</summary>
        public static IReadOnlyList<PresetFinding> OutdatedKeys(DgVoodooConf conf)
        {
            if (conf == null)
                throw new ArgumentNullException(nameof(conf));
            var findings = new List<PresetFinding>();

            // Version stands before the first section.
            DgVoodooConfEntry version = conf.Find(VersionKey, string.Empty);
            if (version == null || !IsCurrentVersion(version.Value))
                findings.Add(new PresetFinding(VersionKey, version?.Value));

            DgVoodooConfEntry deferred = conf.Find(DeferredKey);
            if (deferred != null && string.Equals(deferred.Value, "true", StringComparison.OrdinalIgnoreCase))
                findings.Add(new PresetFinding(DeferredKey, deferred.Value));

            DgVoodooConfEntry altEnter = conf.Find(AltEnterKey);
            if (altEnter != null && string.Equals(altEnter.Value, "false", StringComparison.OrdinalIgnoreCase))
                findings.Add(new PresetFinding(AltEnterKey, altEnter.Value));

            DgVoodooConfEntry attributes = conf.Find(FullscreenAttributesKey);
            if (attributes == null || !attributes.Value.Split(',').Any(item => string.Equals(item.Trim(), FakeItem, StringComparison.OrdinalIgnoreCase)))
                findings.Add(new PresetFinding(FullscreenAttributesKey, attributes?.Value));

            return new ReadOnlyCollection<PresetFinding>(findings);
        }

        /// <summary>True for a hexadecimal number (with or without <c>0x</c>) of at least <see cref="CurrentVersion"/>; anything else is outdated.</summary>
        private static bool IsCurrentVersion(string text)
        {
            string digits = (text ?? string.Empty).Trim();
            if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                digits = digits.Substring(2);
            return int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int value) && value >= CurrentVersion;
        }
    }
}

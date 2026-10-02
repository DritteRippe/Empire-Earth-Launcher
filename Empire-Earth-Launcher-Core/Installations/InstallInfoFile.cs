using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Contract;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>
    /// The install info file <c>&lt;root&gt;\_setupdata_&lt;Product&gt;\install.ini</c> of a setup since v2 (contract 1.2):
    /// what the last setup run installed. Read-only for the launcher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The format is a Windows INI file. The setup writes pure ASCII with CRLF; this reader accepts what the contract
    /// requires of readers: a UTF-8 BOM, LF or CRLF line ends, unknown sections and keys (ignored). Section and key
    /// names are compared ignoring case, as Windows does; the first occurrence of a key wins, as with
    /// <c>GetPrivateProfileString</c>; lines starting with <c>;</c> are comments; lines without <c>=</c> are ignored.
    /// </para>
    /// <para>
    /// Parsing never fails: whatever the content, the result has the values that could be read (missing values are
    /// null, a missing or invalid contract version is 0). The install root is not in the file: it is the parent of
    /// the setup data folder.
    /// </para>
    /// </remarks>
    public sealed class InstallInfoFile
    {
        /// <summary>
        /// The largest file the launcher reads (1 MiB). The setup writes a few hundred bytes plus one line per file in
        /// <c>[MissingAfterInstall]</c>; a larger file is not an install info file of the setup.
        /// </summary>
        public const long MaxFileBytes = 1024 * 1024;

        private InstallInfoFile(IDictionary<string, string> install, IList<string> missingAfterInstall)
        {
            ContractVersionText = Get(install, ContractNames.ContractVersionName);
            ContractVersion = ParseContractVersion(ContractVersionText);
            ProductId = Get(install, ContractNames.ProductName);
            AppId = Get(install, ContractNames.AppIdName);
            InstallModeText = Get(install, ContractNames.InstallModeName);
            InstallMode = InstallModes.Parse(InstallModeText);
            GameVersion = Get(install, ContractNames.GameVersionName);
            SetupVersion = Get(install, ContractNames.SetupVersionName);
            SetupBuild = Get(install, ContractNames.SetupBuildName);
            Components = SetupNameList.Parse(Get(install, ContractNames.ComponentsName));
            Tasks = SetupNameList.Parse(Get(install, ContractNames.TasksName));
            Written = Get(install, ContractNames.WrittenName);
            MissingAfterInstall = new ReadOnlyCollection<string>(missingAfterInstall);
        }

        /// <summary>
        /// The contract version of the setup run; 0 when it is missing or not a number (then the installation is not of
        /// the kind <see cref="InstallationKind.Community"/>).
        /// </summary>
        public int ContractVersion { get; }

        /// <summary><c>ContractVersion</c> as written, for the log; null if missing.</summary>
        public string ContractVersionText { get; }

        /// <summary>True if <c>ContractVersion</c> exists but is not a non-negative number (it then counts as 0).</summary>
        public bool HasInvalidContractVersion
        {
            get { return ContractVersionText != null && !TryParseContractVersion(ContractVersionText, out _); }
        }

        /// <summary><c>Product</c> as written (the setup data folder names the product as well); null if missing.</summary>
        public string ProductId { get; }

        /// <summary><c>AppId</c> (without braces); null if missing.</summary>
        public string AppId { get; }

        /// <summary><c>InstallMode</c> as written; null if missing.</summary>
        public string InstallModeText { get; }

        /// <summary><see cref="InstallModeText"/> as a mode; <see cref="Installations.InstallMode.Unknown"/> if missing or unknown.</summary>
        public InstallMode InstallMode { get; }

        /// <summary><c>GameVersion</c>; null if missing.</summary>
        public string GameVersion { get; }

        /// <summary><c>SetupVersion</c>; null if missing.</summary>
        public string SetupVersion { get; }

        /// <summary><c>SetupBuild</c> (optional); null if missing.</summary>
        public string SetupBuild { get; }

        /// <summary><c>Components</c>; empty if missing.</summary>
        public SetupNameList Components { get; }

        /// <summary><c>Tasks</c>; empty if missing.</summary>
        public SetupNameList Tasks { get; }

        /// <summary><c>Written</c> (local time of the run, informative only); null if missing.</summary>
        public string Written { get; }

        /// <summary>
        /// The manifest paths of <c>[MissingAfterInstall]</c>, ordered by their keys <c>1</c>, <c>2</c>, ...; keys that are
        /// not numbers and empty values are ignored. Empty if the section is absent.
        /// </summary>
        public IReadOnlyList<string> MissingAfterInstall { get; }

        /// <summary>Reads the bytes of the file (UTF-8 with or without BOM; the setup writes ASCII).</summary>
        public static InstallInfoFile Parse(byte[] content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            // Invalid UTF-8 becomes U+FFFD instead of an exception: such a file still yields what can be read.
            return Parse(new UTF8Encoding(false, false).GetString(content));
        }

        /// <summary>Reads the text of the file.</summary>
        public static InstallInfoFile Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var install = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var missing = new List<KeyValuePair<long, string>>();
            string section = null;
            foreach (string rawLine in text.TrimStart('﻿').Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == ';')
                    continue;
                if (line[0] == '[')
                {
                    int end = line.IndexOf(']');
                    section = end < 0 ? line.Substring(1).Trim() : line.Substring(1, end - 1).Trim();
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0 || section == null)
                    continue;
                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                if (string.Equals(section, ContractNames.InstallInfoSectionName, StringComparison.OrdinalIgnoreCase))
                {
                    if (!install.ContainsKey(key))
                        install.Add(key, value);
                }
                else if (string.Equals(section, ContractNames.MissingAfterInstallSectionName, StringComparison.OrdinalIgnoreCase))
                {
                    if (value.Length > 0 &&
                        long.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out long number) &&
                        missing.All(entry => entry.Key != number))
                    {
                        missing.Add(new KeyValuePair<long, string>(number, value));
                    }
                }
            }

            return new InstallInfoFile(install, missing.OrderBy(entry => entry.Key).Select(entry => entry.Value).ToList());
        }

        private static string Get(IDictionary<string, string> values, string key)
        {
            return values.TryGetValue(key, out string value) ? value : null;
        }

        private static int ParseContractVersion(string text)
        {
            return text != null && TryParseContractVersion(text, out int version) ? version : 0;
        }

        private static bool TryParseContractVersion(string text, out int version)
        {
            return int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out version);
        }
    }
}

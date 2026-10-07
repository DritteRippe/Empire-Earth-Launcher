using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Integrity;

namespace Empire_Earth_Launcher.RealMachineTests.Harness
{
    /// <summary>
    /// What the harness may print: the CI job uploads its console output and result files, which must hold no hash of a game
    /// file (or anything else derived from the game data). Findings are written as <c>path|class|kind</c>, and every message
    /// passes <see cref="Redact(string)"/>, which replaces any token of 64 hex digits.
    /// </summary>
    internal static class SafeText
    {
        /// <summary>What a SHA-256 is replaced with.</summary>
        public const string HashPlaceholder = "<sha256>";

        private static readonly Regex Sha256 = new Regex(@"(?<![0-9A-Fa-f])[0-9A-Fa-f]{64}(?![0-9A-Fa-f])", RegexOptions.CultureInvariant);

        /// <summary><paramref name="text"/> with every token of 64 hex digits replaced by <see cref="HashPlaceholder"/>.</summary>
        public static string Redact(string text)
        {
            return text == null ? null : Sha256.Replace(text, HashPlaceholder);
        }

        /// <summary>Every line redacted.</summary>
        public static IReadOnlyList<string> Redact(IEnumerable<string> lines)
        {
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));
            return lines.Select(Redact).ToList();
        }

        /// <summary>A finding as <c>path|class|kind</c>, without the hashes the finding carries.</summary>
        public static string Finding(IntegrityFinding finding)
        {
            if (finding == null)
                throw new ArgumentNullException(nameof(finding));
            return Redact(finding.Path) + "|" + FileClassifier.Name(finding.Class) + "|" + finding.Kind;
        }
    }
}

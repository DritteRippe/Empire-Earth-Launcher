using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>Why <see cref="WinPath.TryResolveManifestPath"/> refused a path of the integrity manifest.</summary>
    public enum ManifestPathError
    {
        /// <summary>The path is valid.</summary>
        None,
        /// <summary>Empty.</summary>
        Empty,
        /// <summary>Starts with <c>/</c> or <c>\</c> (absolute or UNC).</summary>
        Absolute,
        /// <summary>Starts with a drive, e.g. <c>C:</c>.</summary>
        Drive,
        /// <summary>Contains a <c>:</c> (also alternate data streams like <c>file:stream</c>).</summary>
        Colon,
        /// <summary>Contains a <c>\</c>; the manifest separator is <c>/</c>.</summary>
        Backslash,
        /// <summary>Contains a character Windows does not allow in names (control characters, <c>&lt;&gt;"|?*</c>).</summary>
        InvalidCharacter,
        /// <summary>Two separators in a row or a separator at the end.</summary>
        EmptySegment,
        /// <summary>A <c>..</c> segment.</summary>
        ParentSegment,
        /// <summary>A <c>.</c> segment or a segment of dots and spaces only.</summary>
        DotSegment,
        /// <summary>A name that ends with a dot or a space, which Windows would silently remove.</summary>
        TrailingDotOrSpace,
        /// <summary>A reserved device name such as <c>CON</c>, <c>NUL</c> or <c>COM1</c> (also with an extension).</summary>
        ReservedName,
        /// <summary>The resolved path is not below the install root (defence in depth; the checks above prevent it).</summary>
        OutsideRoot
    }

    /// <summary>
    /// Windows path rules as pure string logic (ADR 0006): the core uses it for every path of an installation
    /// instead of <see cref="System.IO.Path"/>, so that it behaves the same on Windows and in the tests under
    /// Mono on Linux, where <see cref="System.IO.Path"/> has other separators and no drives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Normal form (<see cref="Normalize"/>): <c>/</c> becomes <c>\</c>; runs of separators become one (the leading
    /// <c>\\</c> of a UNC path stays); <c>.</c> segments are removed and <c>..</c> segments remove the segment
    /// before them, never going above the root; trailing dots and spaces of a name are removed, as Windows does;
    /// no trailing separator except for a drive root (<c>C:\</c>). A bare drive <c>C:</c> means its root
    /// <c>C:\</c>: the contract stores roots and volumes without a trailing backslash ("Installed From Volume").
    /// </para>
    /// <para>
    /// Comparisons are ordinal and ignore case (<see cref="StringComparison.OrdinalIgnoreCase"/>), independent
    /// of the current culture, like the file systems of Windows.
    /// </para>
    /// </remarks>
    public static class WinPath
    {
        /// <summary>The separator of Windows paths.</summary>
        public const char Separator = '\\';

        private static readonly char[] InvalidNameCharacters = { '<', '>', '"', '|', '?', '*' };

        private static readonly HashSet<string> ReservedNames = new HashSet<string>(
            new[] { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" }
                .Concat(Enumerable.Range(1, 9).Select(i => "COM" + i))
                .Concat(Enumerable.Range(1, 9).Select(i => "LPT" + i)),
            StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Equality of paths after <see cref="Normalize"/>, ignoring case; for dictionaries keyed by path (e.g.
        /// install roots, contract 1.4 "Merge"). null equals only null.
        /// </summary>
        public static IEqualityComparer<string> Comparer { get; } = new PathComparer();

        /// <summary>The normal form of <paramref name="path"/> (see the remarks of <see cref="WinPath"/>).</summary>
        /// <remarks>Surrounding white space is removed (paths from the registry or typed by the user).</remarks>
        public static string Normalize(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            Parse(path, out string root, out List<string> segments);
            return Join(root, segments);
        }

        /// <summary>True if both paths name the same file or folder (normal form, ignoring case).</summary>
        public static bool IsSamePath(string first, string second)
        {
            if (first == null)
                throw new ArgumentNullException(nameof(first));
            if (second == null)
                throw new ArgumentNullException(nameof(second));
            return string.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True for a path with a drive and root (<c>C:\...</c>, also a bare <c>C:</c>) or a UNC path
        /// (<c>\\server\share\...</c>); false for relative paths, <c>\folder</c> and <c>C:folder</c>.
        /// </summary>
        public static bool IsFullyQualified(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            Parse(path, out string root, out _);
            return IsDriveRoot(root) || root.StartsWith(@"\\", StringComparison.Ordinal);
        }

        /// <summary>The drive of <paramref name="path"/>, e.g. <c>C:</c> (as written), or null if it has none.</summary>
        public static string GetDrive(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            string trimmed = path.Trim();
            return HasDrive(trimmed) ? trimmed.Substring(0, 2) : null;
        }

        /// <summary>
        /// The folder that contains <paramref name="path"/> in normal form, or null for a root (<c>C:\</c>,
        /// <c>\\server\share</c>) or a path of a single relative name.
        /// </summary>
        public static string GetParent(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            Parse(path, out string root, out List<string> segments);
            if (segments.Count == 0 || (segments.Count == 1 && root.Length == 0))
                return null;
            segments.RemoveAt(segments.Count - 1);
            return Join(root, segments);
        }

        /// <summary>The last name of <paramref name="path"/> in normal form; empty for a root.</summary>
        public static string GetFileName(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            Parse(path, out _, out List<string> segments);
            return segments.Count == 0 ? string.Empty : segments[segments.Count - 1];
        }

        /// <summary>The extension of the last name including the dot (<c>.exe</c>), or empty if it has none.</summary>
        public static string GetExtension(string path)
        {
            string name = GetFileName(path);
            int dot = name.LastIndexOf('.');
            return dot < 0 ? string.Empty : name.Substring(dot);
        }

        /// <summary>
        /// <paramref name="relativePath"/> (<c>\</c> or <c>/</c> as separators) appended to
        /// <paramref name="basePath"/>, in normal form.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="relativePath"/> is rooted or has a drive.</exception>
        public static string Combine(string basePath, string relativePath)
        {
            if (basePath == null)
                throw new ArgumentNullException(nameof(basePath));
            if (relativePath == null)
                throw new ArgumentNullException(nameof(relativePath));
            string relative = relativePath.Trim().Replace('/', Separator);
            if (relative.StartsWith(@"\", StringComparison.Ordinal) || HasDrive(relative))
                throw new ArgumentException("A relative path is required: " + relativePath, nameof(relativePath));
            return relative.Length == 0 ? Normalize(basePath) : Normalize(basePath + Separator + relative);
        }

        /// <summary>
        /// True if <paramref name="path"/> is inside <paramref name="directory"/> (at any depth, not the folder
        /// itself). Both must be fully qualified; otherwise the answer is false. <c>C:\Games\EE2</c> is not below
        /// <c>C:\Games\EE</c>; <c>C:\Games\EE\..\x</c> is not below it either.
        /// </summary>
        public static bool IsBelow(string path, string directory)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));
            if (!IsFullyQualified(path) || !IsFullyQualified(directory))
                return false;

            string normalizedPath = Normalize(path);
            string normalizedDirectory = Normalize(directory);
            string prefix = normalizedDirectory.EndsWith(@"\", StringComparison.Ordinal)
                ? normalizedDirectory
                : normalizedDirectory + Separator;
            return normalizedPath.Length > prefix.Length &&
                   normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary><see cref="IsSamePath"/> or <see cref="IsBelow"/>.</summary>
        public static bool IsSameOrBelow(string path, string directory)
        {
            return IsBelow(path, directory) ||
                   (IsFullyQualified(path) && IsFullyQualified(directory) && IsSamePath(path, directory));
        }

        /// <summary>
        /// Checks a path of the integrity manifest (contract 2.2: relative to the install root, <c>/</c> as
        /// separator) and returns the full path of the file. The manifest never makes the launcher open a file
        /// outside the install root: absolute paths, drives, <c>:</c>, <c>\</c>, <c>..</c> and every other
        /// form that Windows could resolve elsewhere are refused.
        /// </summary>
        /// <param name="root">Fully qualified install root.</param>
        /// <param name="manifestPath">Path as written in the manifest.</param>
        /// <param name="fullPath">The full path below <paramref name="root"/>; null unless the result is
        /// <see cref="ManifestPathError.None"/>.</param>
        /// <exception cref="ArgumentException"><paramref name="root"/> is not fully qualified.</exception>
        public static ManifestPathError TryResolveManifestPath(string root, string manifestPath, out string fullPath)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            if (!IsFullyQualified(root))
                throw new ArgumentException("The install root must be a full path: " + root, nameof(root));

            fullPath = null;
            ManifestPathError error = CheckManifestPath(manifestPath);
            if (error != ManifestPathError.None)
                return error;

            string combined = Combine(root, manifestPath);
            if (!IsBelow(combined, root))
                return ManifestPathError.OutsideRoot;
            fullPath = combined;
            return ManifestPathError.None;
        }

        /// <summary>
        /// Checks a path of the integrity manifest without an install root (contract 2.2): the rules of
        /// <see cref="TryResolveManifestPath"/> except the final "below the root" check, which needs the root. Used by
        /// the manifest reader, which refuses the whole manifest for one such path.
        /// </summary>
        public static ManifestPathError CheckManifestPath(string manifestPath)
        {
            if (string.IsNullOrEmpty(manifestPath))
                return ManifestPathError.Empty;
            if (manifestPath[0] == '/' || manifestPath[0] == Separator)
                return ManifestPathError.Absolute;
            if (HasDrive(manifestPath))
                return ManifestPathError.Drive;
            if (manifestPath.IndexOf(':') >= 0)
                return ManifestPathError.Colon;
            if (manifestPath.IndexOf(Separator) >= 0)
                return ManifestPathError.Backslash;
            if (manifestPath.Any(c => c < ' ') || manifestPath.IndexOfAny(InvalidNameCharacters) >= 0)
                return ManifestPathError.InvalidCharacter;

            foreach (string segment in manifestPath.Split('/'))
            {
                if (segment.Length == 0)
                    return ManifestPathError.EmptySegment;
                if (segment == "..")
                    return ManifestPathError.ParentSegment;
                if (segment.All(c => c == '.' || c == ' '))
                    return ManifestPathError.DotSegment;
                if (segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal))
                    return ManifestPathError.TrailingDotOrSpace;
                string baseName = segment.Split('.')[0].TrimEnd(' ');
                if (ReservedNames.Contains(baseName))
                    return ManifestPathError.ReservedName;
            }
            return ManifestPathError.None;
        }

        private static bool HasDrive(string path)
        {
            return path.Length >= 2 && path[1] == ':' && IsAsciiLetter(path[0]);
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        private static bool IsDriveRoot(string root)
        {
            return root.Length == 3 && HasDrive(root) && root[2] == Separator;
        }

        /// <summary>
        /// Splits <paramref name="path"/> into its root (<c>C:\</c>, <c>\\server\share</c>, <c>\</c>, <c>C:</c>
        /// for drive-relative paths, or empty) and its resolved segments.
        /// </summary>
        private static void Parse(string path, out string root, out List<string> segments)
        {
            string p = path.Trim().Replace('/', Separator);
            string rest;
            if (p.StartsWith(@"\\", StringComparison.Ordinal))
            {
                string[] parts = p.Substring(2).Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries);
                root = @"\\" + string.Join(@"\", parts.Take(2));
                rest = string.Join(@"\", parts.Skip(2));
            }
            else if (HasDrive(p) && (p.Length == 2 || p[2] == Separator))
            {
                root = p.Substring(0, 2) + Separator;
                rest = p.Substring(2);
            }
            else if (HasDrive(p))
            {
                root = p.Substring(0, 2);
                rest = p.Substring(2);
            }
            else if (p.StartsWith(@"\", StringComparison.Ordinal))
            {
                root = @"\";
                rest = p;
            }
            else
            {
                root = string.Empty;
                rest = p;
            }

            segments = new List<string>();
            foreach (string raw in rest.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (raw == ".")
                    continue;
                // ".." and every other name of dots and spaces only, which Windows may resolve in surprising ways,
                // go up: a path is never considered deeper than it might be.
                if (raw.All(c => c == '.' || c == ' '))
                {
                    if (segments.Count > 0 && segments[segments.Count - 1] != "..")
                        segments.RemoveAt(segments.Count - 1);
                    else if (root.Length == 0)
                        segments.Add("..");
                    continue;
                }
                segments.Add(raw.TrimEnd('.', ' '));
            }
        }

        private static string Join(string root, List<string> segments)
        {
            if (segments.Count == 0)
                return root;
            var result = new StringBuilder(root);
            if (root.Length > 0 && !root.EndsWith(@"\", StringComparison.Ordinal) && root.StartsWith(@"\\", StringComparison.Ordinal))
                result.Append(Separator);
            result.Append(string.Join(@"\", segments));
            return result.ToString();
        }

        private sealed class PathComparer : IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                if (x == null || y == null)
                    return x == null && y == null;
                return IsSamePath(x, y);
            }

            public int GetHashCode(string obj)
            {
                return obj == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Normalize(obj));
            }
        }
    }
}

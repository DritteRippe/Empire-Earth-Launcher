using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Integrity
{
    /// <summary>One line of the integrity manifest: a file below the install root and its SHA-256.</summary>
    public sealed class ManifestEntry
    {
        internal ManifestEntry(string path, string hash)
        {
            Path = path;
            Hash = hash;
            Class = FileClassifier.Classify(path);
        }

        /// <summary>The path as written: relative to the install root, <c>/</c> as separator, checked (contract 2.2).</summary>
        public string Path { get; }

        /// <summary>The SHA-256, 64 lowercase hex digits.</summary>
        public string Hash { get; }

        /// <summary>The class of the file (contract 2.4).</summary>
        public FileClass Class { get; }

        public override string ToString()
        {
            return Hash + "  " + Path;
        }
    }

    /// <summary>Why <see cref="ManifestReader"/> refused a manifest; <see cref="None"/> for a valid one.</summary>
    public enum ManifestProblem
    {
        /// <summary>The manifest is valid.</summary>
        None,

        /// <summary>The bytes are not UTF-8 (the setup writes ASCII).</summary>
        Encoding,

        /// <summary>A line is not <c>&lt;64 hex digits&gt;&lt;space&gt;&lt;space or *&gt;&lt;path&gt;</c>.</summary>
        InvalidLine,

        /// <summary>A path is absolute, has a drive, a <c>:</c>, a <c>\</c>, a <c>..</c> segment or another unsafe form.</summary>
        UnsafePath,

        /// <summary>A path is listed twice (compared ignoring case, as Windows does).</summary>
        DuplicatePath
    }

    /// <summary>The result of <see cref="ManifestReader.Parse(byte[])"/>: the entries, or why the manifest is invalid.</summary>
    public sealed class ManifestParseResult
    {
        private ManifestParseResult(IReadOnlyList<ManifestEntry> entries, ManifestProblem problem, int lineNumber,
            ManifestPathError pathError, string line)
        {
            Entries = entries;
            Problem = problem;
            LineNumber = lineNumber;
            PathError = pathError;
            Line = line;
        }

        /// <summary>True if every line is valid; only then are there <see cref="Entries"/>.</summary>
        public bool IsValid
        {
            get { return Problem == ManifestProblem.None; }
        }

        /// <summary>The entries in the order of the file; empty for an invalid manifest.</summary>
        public IReadOnlyList<ManifestEntry> Entries { get; }

        public ManifestProblem Problem { get; }

        /// <summary>The number (from 1) of the first invalid line; 0 for a valid manifest or an encoding problem.</summary>
        public int LineNumber { get; }

        /// <summary>For <see cref="ManifestProblem.UnsafePath"/>: what is wrong with the path.</summary>
        public ManifestPathError PathError { get; }

        /// <summary>The first invalid line (at most 200 characters, for the log); null for a valid manifest.</summary>
        public string Line { get; }

        internal static ManifestParseResult Valid(IList<ManifestEntry> entries)
        {
            return new ManifestParseResult(new ReadOnlyCollection<ManifestEntry>(entries), ManifestProblem.None, 0,
                ManifestPathError.None, null);
        }

        internal static ManifestParseResult Invalid(ManifestProblem problem, int lineNumber, string line,
            ManifestPathError pathError = ManifestPathError.None)
        {
            string shown = line == null ? null : line.Length > 200 ? line.Substring(0, 200) + "..." : line;
            return new ManifestParseResult(new ManifestEntry[0], problem, lineNumber, pathError, shown);
        }

        /// <summary>One line for the log.</summary>
        public override string ToString()
        {
            if (IsValid)
                return Entries.Count.ToString(CultureInfo.InvariantCulture) + " files";
            if (Problem == ManifestProblem.Encoding)
                return "not UTF-8";
            return Problem + (Problem == ManifestProblem.UnsafePath ? " (" + PathError + ")" : string.Empty) + " in line " +
                   LineNumber.ToString(CultureInfo.InvariantCulture) + ": \"" + Line + "\"";
        }
    }

    /// <summary>
    /// Reads the integrity manifest <c>files.sha256</c> (contract 2.2), the <c>sha256sum</c> text format:
    /// <c>&lt;SHA-256&gt;&lt;space&gt;&lt;space&gt;&lt;path&gt;</c> per line. Read-only, pure logic: it never opens a file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Accepted, as the contract requires of readers: a UTF-8 BOM, LF and CRLF line ends, empty lines (ignored), the hex
    /// digits in any case (kept lowercase), and the binary marker (<c>&lt;hash&gt; *&lt;path&gt;</c>). The setup writes pure
    /// ASCII with LF and two spaces.
    /// </para>
    /// <para>
    /// One invalid line makes the whole manifest invalid (state Unknown): a line of another form, a path that is absolute,
    /// has a drive, a <c>:</c>, a <c>\</c>, a <c>..</c> or <c>.</c> segment, an empty segment, a character Windows does not
    /// allow, a trailing dot or space or a device name (<see cref="WinPath.CheckManifestPath"/>), and a path listed twice.
    /// So the manifest can never make the launcher open a file outside the install root.
    /// </para>
    /// </remarks>
    public static class ManifestReader
    {
        /// <summary>
        /// The largest manifest the launcher reads (16 MiB): the setup writes about 100 bytes per installed file, a few
        /// thousand files. A larger file is not a manifest of the setup (state Unknown).
        /// </summary>
        public const long MaxFileBytes = 16 * 1024 * 1024;

        /// <summary>A line: the hash, one space, a space or the binary marker, then the path (at least one character).</summary>
        private static readonly Regex Line = new Regex(@"^(?<hash>[0-9A-Fa-f]{64}) [ *](?<path>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);

        /// <summary>Reads the bytes of the file (UTF-8 with or without BOM; invalid UTF-8 makes it invalid).</summary>
        public static ManifestParseResult Parse(byte[] content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(content);
            }
            catch (DecoderFallbackException)
            {
                return ManifestParseResult.Invalid(ManifestProblem.Encoding, 0, null);
            }
            return Parse(text);
        }

        /// <summary>Reads the text of the file.</summary>
        public static ManifestParseResult Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (text.Length > 0 && text[0] == '﻿')
                text = text.Substring(1);

            var entries = new List<ManifestEntry>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].EndsWith("\r", StringComparison.Ordinal) ? lines[i].Substring(0, lines[i].Length - 1) : lines[i];
                if (line.Length == 0)
                    continue;

                Match match = Line.Match(line);
                if (!match.Success)
                    return ManifestParseResult.Invalid(ManifestProblem.InvalidLine, i + 1, line);
                string path = match.Groups["path"].Value;
                ManifestPathError error = WinPath.CheckManifestPath(path);
                if (error != ManifestPathError.None)
                    return ManifestParseResult.Invalid(ManifestProblem.UnsafePath, i + 1, line, error);
                if (!paths.Add(path))
                    return ManifestParseResult.Invalid(ManifestProblem.DuplicatePath, i + 1, line);
                entries.Add(new ManifestEntry(path, match.Groups["hash"].Value.ToLowerInvariant()));
            }
            return ManifestParseResult.Valid(entries);
        }
    }
}

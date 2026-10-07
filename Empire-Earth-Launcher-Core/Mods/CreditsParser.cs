using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Empire_Earth_Launcher.Core.Mods
{
    /// <summary>What the <c>CREDITS</c> file of a dreXmod preset says about it (<see cref="CreditsParser"/>).</summary>
    public sealed class ModCredits
    {
        internal ModCredits(string name, string lastEdit, DateTime? lastEditDate, string createdBy)
        {
            Name = name;
            LastEdit = lastEdit;
            LastEditDate = lastEditDate;
            CreatedBy = createdBy;
        }

        /// <summary>The line <c>Name:</c>; null if there is none or it is empty.</summary>
        public string Name { get; }

        /// <summary>The text of the line <c>Last Edit:</c> as written (<c>21/10/2023</c>); null if there is none or it is empty.</summary>
        public string LastEdit { get; }

        /// <summary>The date of <see cref="LastEdit"/>; null if it is no date (the template says <c>XX/XX/XXXX</c>).</summary>
        public DateTime? LastEditDate { get; }

        /// <summary>The line <c>Created by:</c>; null if there is none or it is empty.</summary>
        public string CreatedBy { get; }

        /// <summary>True if the file named none of the three.</summary>
        public bool IsEmpty
        {
            get { return Name == null && LastEdit == null && CreatedBy == null; }
        }
    }

    /// <summary>
    /// Reads the head of the <c>CREDITS</c> file of a preset in <c>Data\dxm\mods\&lt;name&gt;</c>: the lines <c>Name:</c>,
    /// <c>Last Edit:</c> (day/month/year) and <c>Created by:</c>. Everything else in the file is free text, and the file itself
    /// says that its format may be changed completely, so the parser takes what it finds and never fails: a missing line is
    /// null, a date it cannot read stays text.
    /// </summary>
    /// <remarks>
    /// Only the first <see cref="HeaderLines"/> lines are looked at, so that a line of the free text below the header that
    /// begins with <c>Name:</c> is never taken for the name; the first line of each kind counts. There is no version and no
    /// description in the file: the launcher shows what exists.
    /// </remarks>
    public static class CreditsParser
    {
        /// <summary>The name of the file in a preset folder.</summary>
        public const string FileName = "CREDITS";

        /// <summary>How many lines from the top are looked at.</summary>
        public const int HeaderLines = 40;

        private static readonly Regex Line = new Regex(
            @"^\s*(?<key>Name|Last\s*Edit|Created\s*by)\s*[:=]\s*(?<value>.*?)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly string[] DateFormats =
            { "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "d.M.yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "yyyy/MM/dd" };

        /// <summary>Reads the text of a <c>CREDITS</c> file; null counts as empty.</summary>
        public static ModCredits Parse(string text)
        {
            string name = null;
            string lastEdit = null;
            string createdBy = null;
            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length && i < HeaderLines; i++)
            {
                Match match = Line.Match(lines[i].TrimStart('﻿'));
                if (!match.Success || match.Groups["value"].Length == 0)
                    continue;
                string value = match.Groups["value"].Value;
                string key = Regex.Replace(match.Groups["key"].Value, @"\s+", string.Empty).ToLowerInvariant();
                if (key == "name" && name == null)
                    name = value;
                else if (key == "lastedit" && lastEdit == null)
                    lastEdit = value;
                else if (key == "createdby" && createdBy == null)
                    createdBy = value;
            }
            return new ModCredits(name, lastEdit, ParseDate(lastEdit), createdBy);
        }

        private static DateTime? ParseDate(string text)
        {
            if (text != null && DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out DateTime date))
                return date;
            return null;
        }
    }
}

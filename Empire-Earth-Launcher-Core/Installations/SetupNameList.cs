using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>
    /// The components or the tasks a setup run selected: <c>Components</c> and <c>Tasks</c> of <c>install.ini</c>
    /// (contract 1.2) or <c>Inno Setup: Selected Components</c> and <c>Inno Setup: Selected Tasks</c> of the
    /// uninstall key (contract 1.3), as comma separated names. Names are compared ignoring case, independent of the
    /// culture (contract 1.2).
    /// </summary>
    public sealed class SetupNameList
    {
        /// <summary>A list without names.</summary>
        public static readonly SetupNameList Empty = new SetupNameList(new string[0]);

        private readonly ReadOnlyCollection<string> names;

        private SetupNameList(IList<string> names)
        {
            this.names = new ReadOnlyCollection<string>(names);
        }

        /// <summary>The names in the order of the text, without surrounding white space and without empty names.</summary>
        public IReadOnlyList<string> Names
        {
            get { return names; }
        }

        /// <summary>Reads a comma separated list; null or white space gives <see cref="Empty"/>.</summary>
        public static SetupNameList Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Empty;
            return new SetupNameList(text.Split(',')
                                         .Select(name => name.Trim())
                                         .Where(name => name.Length > 0)
                                         .ToList());
        }

        /// <summary>True if the list contains <paramref name="name"/> (ignoring case).</summary>
        public bool Contains(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            return names.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The Art of Conquest was installed: the component <c>gameaoc</c> (contract 1.2).
        /// </summary>
        public bool HasArtOfConquest
        {
            get { return Contains(ContractNames.ArtOfConquestComponent); }
        }

        /// <summary>
        /// A DirectX wrapper was installed: the component <c>additional\directx_wrapper</c> or a name that starts with
        /// it and a backslash (contract 1.2, wrapper rule of 3.3).
        /// </summary>
        public bool HasDirectXWrapper
        {
            get
            {
                string prefix = ContractNames.DirectXWrapperComponent + @"\";
                return names.Any(name =>
                    string.Equals(name, ContractNames.DirectXWrapperComponent, StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// The game language: the rest of the first name <c>language\&lt;language&gt;</c>, e.g. <c>pt_BR</c> (contract 1.2),
        /// as written; null if there is none.
        /// </summary>
        public string GameLanguage
        {
            get
            {
                string prefix = ContractNames.LanguageComponentPrefix;
                string component = names.FirstOrDefault(name =>
                    name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                return component?.Substring(prefix.Length);
            }
        }

        /// <summary>The names, comma separated, as the setup writes them.</summary>
        public override string ToString()
        {
            return string.Join(",", names);
        }
    }
}

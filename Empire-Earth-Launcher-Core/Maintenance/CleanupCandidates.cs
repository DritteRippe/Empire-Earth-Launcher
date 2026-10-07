using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Platform;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Core.Maintenance
{
    /// <summary>What the registry cleanup may do with a key of its list (ADR 0007, contract 3.8).</summary>
    public enum CleanupScope
    {
        /// <summary>
        /// An HKCU key the launcher offers to delete, after a <c>.reg</c> backup, when it is stale (the conditions of the ADR 0007
        /// amendment of the design review): no installation of its product was found, and the folder its "Installed From"
        /// values name is missing on a present, fixed, local drive.
        /// </summary>
        LauncherDeletes,

        /// <summary>
        /// An HKLM key: the launcher never writes HKLM and never asks for elevation (contract 4.1), so it is only shown; when it
        /// is stale under the same conditions, with the advice to export it with the Registry Editor and then to delete it there
        /// as an administrator.
        /// </summary>
        AdviceOnly,

        /// <summary>
        /// <c>Software\Sierra</c>: it contains the NeoEE CD keys (<c>Software\Sierra\CDKeys</c>), so it is shown as "do not
        /// delete" and never named for deletion in any state (ADR 0007 plan review, forum report table 8 row 7).
        /// </summary>
        Protected
    }

    /// <summary>
    /// One key of the cleanup list: an explicit key (hive, view and path, no wildcard), what may be done with it, the product
    /// and game whose "Installed From" values it holds, and the evidence that such keys are leftovers worth removing.
    /// </summary>
    /// <remarks>
    /// The constructor refuses an entry that could reach a protected key: a key the cleanup may delete or advise to delete must
    /// not be the CD keys, the install record, the uninstall keys, a key below them or an ancestor of them in canonical form
    /// (<see cref="RegistryWritePolicy.ProtectionOf"/>), and a <see cref="CleanupScope.Protected"/> entry must be an ancestor of
    /// the CD keys. The launcher deletes only HKCU keys; HKLM keys are advice only.
    /// </remarks>
    public sealed class CleanupEntry
    {
        /// <summary>The pattern every evidence must contain: a forum thread or post id, or a file of the setup.</summary>
        public static readonly Regex EvidencePattern = new Regex(@"t=\d+|p=\d+|setup:", RegexOptions.CultureInvariant);

        internal CleanupEntry(string id, RegistryLocation key, CleanupScope scope, Product product, Game game,
            bool shownWhenKept, string evidence)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("An entry needs an id.", nameof(id));
            Key = key ?? throw new ArgumentNullException(nameof(key));
            if (key.IsRoot || key.Path.IndexOfAny(new[] { '*', '?' }) >= 0)
                throw new ArgumentException("An entry names one explicit key, no hive and no wildcard: " + key, nameof(key));
            if (evidence == null || !EvidencePattern.IsMatch(evidence))
                throw new ArgumentException("Every entry needs evidence (t=, p= or setup:): " + key, nameof(evidence));
            RegistryWriteDenial protection = RegistryWritePolicy.ProtectionOf(key);
            if (scope == CleanupScope.Protected)
            {
                if (protection != RegistryWriteDenial.CdKeys)
                    throw new ArgumentException("A protected entry is an ancestor of the CD keys: " + key, nameof(scope));
                if (product != null || game != null)
                    throw new ArgumentException("A protected entry belongs to no product and no game.", nameof(product));
            }
            else
            {
                if (protection != RegistryWriteDenial.None)
                    throw new ArgumentException("The key is protected (" + protection + ") and can never be a cleanup target: " + key,
                        nameof(key));
                if (scope == CleanupScope.LauncherDeletes && key.Hive != RegistryHive.CurrentUser)
                    throw new ArgumentException("The launcher deletes only HKCU keys (contract 3.6): " + key, nameof(scope));
                if (scope == CleanupScope.AdviceOnly && key.Hive != RegistryHive.LocalMachine)
                    throw new ArgumentException("Advice only is for HKLM keys: " + key, nameof(scope));
                if (product == null || game == null)
                    throw new ArgumentException("A key that may be removed belongs to a product and a game.", nameof(product));
            }

            Id = id;
            Scope = scope;
            Product = product;
            Game = game;
            ShownWhenKept = shownWhenKept;
            Evidence = evidence;
        }

        /// <summary>A short, stable name of the entry (logs and tests).</summary>
        public string Id { get; }

        /// <summary>The key with hive and view, as it is written in the registry.</summary>
        public RegistryLocation Key { get; }

        public CleanupScope Scope { get; }

        /// <summary>
        /// The product whose installation keeps the key (contract 3.1: the SSSI and Mad Doc keys belong to EE, which retail, GOG
        /// and older installations also use; the Neo keys to NeoEE); null for <see cref="CleanupScope.Protected"/>.
        /// </summary>
        public Product Product { get; }

        /// <summary>The game whose "Installed From" values the key holds; null for <see cref="CleanupScope.Protected"/>.</summary>
        public Game Game { get; }

        /// <summary>
        /// True if the key is listed read-only when it is not stale. False for the game settings keys in HKCU: while an
        /// installation uses them they are the player's settings, not a leftover (ARCHITECTURE 4.6, "Otherwise: not shown").
        /// </summary>
        public bool ShownWhenKept { get; }

        /// <summary>Why the key is on the list: forum thread and post ids or the setup file (checked against <see cref="EvidencePattern"/>).</summary>
        public string Evidence { get; }

        public override string ToString()
        {
            return Id + " " + Key + " (" + Scope + ")";
        }
    }

    /// <summary>
    /// The cleanup list of the launcher (R5, contract 3.8, ADR 0007 and its amendments): explicit keys of old and foreign
    /// installations, each with its evidence. It is the table of <c>docs/ARCHITECTURE.md</c> section 4.6 (a test compares
    /// them); a key without evidence is not added but noted in the test plan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The evidence names mostly HKLM keys (t=12082 p=49553, t=10577 p=46301) and vendor names without a hive (t=1036 p=4756),
    /// so the part the launcher deletes is small: the game settings keys of contract 3.1 in HKCU and their registry
    /// VirtualStore copies, each only when it is stale. Everything else is shown read-only, HKLM keys with advice.
    /// </para>
    /// <para>
    /// Never on the list: <c>Software\Sierra\CDKeys</c> and every ancestor as something to delete (forum 4.19: deleting
    /// <c>Software\Sierra</c> loses the NeoEE CD keys), the install record, the uninstall keys, vendor roots
    /// (<c>Software\Mad Doc Software</c>: p=4756 "make sure to only get ones for ee and aoc if you have other Mad Doc games"),
    /// and Stainless Steel Studios keys, whose path no sample confirms yet (test plan).
    /// </para>
    /// </remarks>
    public static class CleanupCandidates
    {
        private const string VirtualStoreMachine = @"Software\Classes\VirtualStore\MACHINE\SOFTWARE\";
        private const string Wow6432Node = @"WOW6432Node\";
        private const string Sierra = "Sierra";
        private const string SssiEmpireEarth = @"SSSI\Empire Earth";
        private const string MadDocArtOfConquest = @"Mad Doc Software\EE-AOC";

        private const string SssiEvidence = "t=1036 p=4756 (SSSI keys), t=12082 p=49553, t=10577 p=46301";
        private const string MadDocEvidence = "t=1036 p=4756 (Mad Doc keys, only those of EE and AoC)";
        private const string NeoEvidence = "setup: config_neoee.iss (game settings keys of NeoEE, contract 3.1); t=10577 p=46302";
        private const string VirtualStoreEvidence =
            "t=12082 p=49553, t=1036 p=4756 (the HKLM keys of SSSI and Mad Doc; Windows keeps the HKLM writes of a non-elevated 32-bit game in HKCU)";
        private const string SierraEvidence =
            "t=12082 p=49553, t=10950 (CD key invalid), forum report table 8 row 7; setup: authtools.dll writes Software\\Sierra\\CDKeys";

        /// <summary>The list, in the order the Tools page shows it: what the launcher may delete first, then the HKLM keys.</summary>
        public static IReadOnlyList<CleanupEntry> All { get; } = new ReadOnlyCollection<CleanupEntry>(Build());

        /// <summary>
        /// The rules the write policy needs for the cleanup (ADR 0007): <c>DeleteSubKeyTree</c> of exactly the keys of
        /// <paramref name="entries"/> the launcher deletes, as they are written. The policy still refuses every protected key
        /// first, whatever such a rule names.
        /// </summary>
        public static IEnumerable<RegistryWriteRule> WriteRules(IEnumerable<CleanupEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            return entries.Where(entry => entry.Scope == CleanupScope.LauncherDeletes).Select(entry => DeleteRuleFor(entry.Key));
        }

        /// <summary>The rule that allows deleting <paramref name="key"/> (an HKCU key) with its subkeys.</summary>
        internal static RegistryWriteRule DeleteRuleFor(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Hive != RegistryHive.CurrentUser)
                throw new ArgumentException("The launcher deletes only HKCU keys: " + key, nameof(key));
            return new RegistryWriteRule(key.Path, RegistryOperation.DeleteSubKeyTree);
        }

        private static List<CleanupEntry> Build()
        {
            var entries = new List<CleanupEntry>();

            // The game settings keys of contract 3.1 in HKCU: the player's settings while an installation uses them, a leftover
            // of a removed one otherwise.
            foreach (Product product in new[] { Product.EE, Product.NeoEE })
            {
                foreach (Game game in Game.All)
                {
                    string evidence = product == Product.NeoEE ? NeoEvidence : game == Game.EmpireEarth ? SssiEvidence : MadDocEvidence;
                    entries.Add(new CleanupEntry("hkcu-" + product.Id.ToLowerInvariant() + "-" + game.Id.ToLowerInvariant(),
                        RegistryLocation.CurrentUser(product.GetGameSettingsKey(game)), CleanupScope.LauncherDeletes, product, game,
                        false, evidence));
                }
            }

            // Their registry VirtualStore copies: where the HKLM writes of a non-elevated retail game land (both views).
            foreach (string view in new[] { string.Empty, Wow6432Node })
            {
                string suffix = view.Length == 0 ? string.Empty : "-wow64";
                entries.Add(new CleanupEntry("vs-sssi-ee" + suffix,
                    RegistryLocation.CurrentUser(VirtualStoreMachine + view + SssiEmpireEarth), CleanupScope.LauncherDeletes,
                    Product.EE, Game.EmpireEarth, true, VirtualStoreEvidence));
                entries.Add(new CleanupEntry("vs-maddoc-aoc" + suffix,
                    RegistryLocation.CurrentUser(VirtualStoreMachine + view + MadDocArtOfConquest), CleanupScope.LauncherDeletes,
                    Product.EE, Game.ArtOfConquest, true, VirtualStoreEvidence));
            }

            // The HKLM keys of retail installations: advice only.
            foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                string suffix = view == RegistryView.Registry32 ? "32" : "64";
                entries.Add(new CleanupEntry("hklm" + suffix + "-sssi-ee",
                    new RegistryLocation(RegistryHive.LocalMachine, view, @"Software\" + SssiEmpireEarth), CleanupScope.AdviceOnly,
                    Product.EE, Game.EmpireEarth, true, SssiEvidence));
                entries.Add(new CleanupEntry("hklm" + suffix + "-maddoc-aoc",
                    new RegistryLocation(RegistryHive.LocalMachine, view, @"Software\" + MadDocArtOfConquest), CleanupScope.AdviceOnly,
                    Product.EE, Game.ArtOfConquest, true, MadDocEvidence));
            }

            // Software\Sierra: never deleted, never advised for deletion; it holds the CD keys of NeoEE (HKLM in the admin
            // mode of the setup, HKCU otherwise, contract 3.8) and of retail installations, and their VirtualStore copies.
            entries.Add(new CleanupEntry("hklm32-sierra", RegistryLocation.LocalMachine32(@"Software\" + Sierra),
                CleanupScope.Protected, null, null, true, SierraEvidence));
            entries.Add(new CleanupEntry("hklm64-sierra", RegistryLocation.LocalMachine64(@"Software\" + Sierra),
                CleanupScope.Protected, null, null, true, SierraEvidence));
            entries.Add(new CleanupEntry("hkcu-sierra", RegistryLocation.CurrentUser(@"Software\" + Sierra),
                CleanupScope.Protected, null, null, true, SierraEvidence));
            entries.Add(new CleanupEntry("vs-sierra", RegistryLocation.CurrentUser(VirtualStoreMachine + Sierra),
                CleanupScope.Protected, null, null, true, SierraEvidence));
            entries.Add(new CleanupEntry("vs-sierra-wow64", RegistryLocation.CurrentUser(VirtualStoreMachine + Wow6432Node + Sierra),
                CleanupScope.Protected, null, null, true, SierraEvidence));
            return entries;
        }

        /// <summary>The CD-key key below <paramref name="sierraKey"/> (a <see cref="CleanupScope.Protected"/> entry), for the existence check.</summary>
        internal static RegistryLocation CdKeysBelow(RegistryLocation sierraKey)
        {
            if (sierraKey == null)
                throw new ArgumentNullException(nameof(sierraKey));
            string name = ContractNames.CdKeysKey.Substring(ContractNames.CdKeysKey.LastIndexOf('\\') + 1);
            return sierraKey.Child(name);
        }
    }
}

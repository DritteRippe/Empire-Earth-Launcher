# 0007 Registry write scope, protected keys and .reg backups

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review; implementation in L-WP2; plan review;
implementation in L-WP5 and L-WP8; security review), see the Amendment sections

## Context

v2 writes per-user game defaults (R1), resets game settings (R4), cleans up stale registry entries (R5) and
resets the WON login (R6). The forum shows both the value and the danger of such actions: deleting old
Sierra/SSSI/Mad Doc keys helped some players (t=1036 p=4756, t=12082 p=49553), but `authtools.dll` keeps
the NeoEE CD keys below `Software\Sierra\CDKeys`, and deleting `Software\Sierra` makes online play fail with
"CD key invalid" (forum 4.19, t=10950, t=11021). The contract (3.6, 3.8) says the launcher writes only HKCU
of its own account, backs up before overwriting and never changes the protected keys.

## Decision

- **Write scope (allow-list)**, enforced in `Platform.WindowsRegistry`'s callers through one
  `RegistryWritePolicy` that every write and delete passes:
  - HKCU game settings keys of contract 3.1, only the values of the contract table 3.2;
  - HKCU `Software\Empire Earth Community\GameDefaults\<Product>` (marker, contract 3.5);
  - HKCU `Software\Microsoft\DirectX\UserGpuPreferences`, only value names that are a game program path of a
    discovered installation (contract 3.4);
  - HKCU `Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers`, only those value names, only the
    layers `DWM8And16BitMitigation`, `HIGHDPIAWARE`, `HeapClearAllocation`, `WIN7RTM`/`WINXPSP3`, other
    entries of the value kept, `RUNASADMIN` never added (contract 3.7);
  - deletes of the cleanup list (below).
  Everything else (HKLM, other hives, the install record, uninstall keys) is refused by the policy and is a
  programming error.
- **Protected keys**: `Software\Sierra\CDKeys` and its ancestors `Software` and `Software\Sierra`, the install
  record keys, the uninstall keys and the HKLM compatibility values can never be deleted or written, whatever a
  list says. A unit test runs the policy against every protected key in every hive and view.
- **Cleanup list**: an explicit, versioned list of HKCU keys (no wildcards, no parent of a protected key),
  each entry with the reason and the forum evidence; a key is offered only if it is stale (its "Installed
  From" points to no existing folder, or it belongs to no discovered installation). HKLM and InstallShield
  leftovers are listed read-only with advice, because the launcher never writes HKLM and never asks for
  elevation (contract 2.5, 3.6, 4.1).
- **Backups before every overwrite or delete**: `Backup.RegFileWriter` exports the affected keys with
  subkeys as a `.reg` file ("Windows Registry Editor Version 5.00", UTF-16 LE with BOM, CRLF; `REG_SZ` with
  `\` and `"` escaped, `dword:`, `hex(b):`, `hex(2):`, `hex(7):`, `hex:`) into
  `%LOCALAPPDATA%\Empire Earth Launcher\Backups\<yyyy-MM-dd_HHmmss>_<what>\`. If the backup cannot be written
  completely, nothing is changed. Double-clicking the file restores the values with the Registry Editor.
- **WON login reset**: the files are moved (not deleted) into a backup folder; files listed in the manifest
  are never moved.

## Evidence

- CONTRACT.md 3.6 ("Reset ... a `.reg` backup ... If the backup fails, nothing is changed"; "The launcher
  writes only HKCU of the account that runs it"), 3.8 (protected keys, explicit cleanup list), 3.7 (no
  `RUNASADMIN`), 1.4 ("Read-only").
- Forum: t=1036 p=4756 (keys of Sierra, SSSI, Mad Doc, Stainless Steel Studios), t=12082 p=49553
  (`HKLM\Software\Sierra`, `HKLM\Software\SSSI\Empire Earth`), forum report table 8 row 7 (deleting
  `Software\Sierra` as a whole removes the CD keys), p=83519 (WON login files).
- Own writer instead of `reg.exe export`: `reg.exe` output cannot be unit-tested, depends on a process and
  on policies, and exports whole keys only; the own writer is tested with golden files and exports exactly what
  will change.

## Consequences

- No action of the launcher can damage the CD keys or another account.
- Cleanup of old HKLM entries stays a manual step (documented in the advice text and the test plan).
- The backup folder grows; the Tools page has "open backup folder"; the launcher never deletes backups.

## Alternatives considered

- **`reg.exe export`/`import`**: see evidence. Rejected for export; restoring by double-click needs no tool.
- **Cleanup by wildcard or vendor root key**: exactly the dangerous variant of the forum. Rejected.
- **Offer elevation for HKLM cleanup**: contradicts contract 4.1/2.5 ("MUST NOT ask for elevation").
  Rejected.

## Amendment 2026-10-02 (design review)

The review found three gaps in the protection of the CD keys, a cleanup rule that could delete the
player's active settings, and a restore promise that `.reg` files cannot keep.

- **Canonical paths before every policy check.** `RegistryWritePolicy` first maps every path to a canonical
  form, then checks it:
  - case folded (ordinal ignore case), `/` treated as `\`, doubled and trailing backslashes removed;
  - a `WOW6432Node` segment after `Software` is removed and turns the location into the 32-bit view
    (`HKLM64\Software\WOW6432Node\Sierra\CDKeys` is the same key as `HKLM32\Software\Sierra\CDKeys`);
  - `HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\[WOW6432Node\]<rest>` (registry virtualization: where
    HKLM writes of non-elevated 32-bit legacy programs land) is mapped to the HKLM equivalent and is protected
    exactly like it.
  Protected is the **whole subtree** of `Software\Sierra\CDKeys` (subkeys and values) and every ancestor, in
  every hive, view and alias, for every operation (`SetValue`, `DeleteValue`, `DeleteSubKeyTree`,
  `CreateSubKey`). A table test runs every alias against every operation; the allow-list is checked after the
  canonical form, so an alias can never reach an allowed path either.
- **Cleanup conditions** (R5). The game settings keys of contract 3.1 are never offered while an installation
  of that product is found, nor while the folder their "Installed From" values name is on a drive that is
  missing, removable or a network drive (a missing USB or network drive is not a stale installation). The
  cleanup list lives as a table in `ARCHITECTURE.md` (key, hive, evidence, condition); every code entry has a
  mandatory `Evidence` field that a unit test checks against `t=\d+|p=\d+|setup:`. The evidence found so far
  names mostly HKLM keys (p=49553) and vendor names without hive (p=4756), so the HKCU part may stay small or
  empty; then the feature is honestly "display only". Registry-VirtualStore leftovers of Sierra, SSSI and Mad
  Doc below `HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE` are shown (they are in HKCU), with the
  CD-key subtree excluded by the rule above.
- **Exact restore.** For every value an action creates (it did not exist before), the backup gets a delete
  line `"<name>"=-` below its key, so importing the `.reg` file brings back the previous state of the values.
  Keys are never deleted by a backup file (a key the action created stays, empty after the import). The
  writer also covers: `REG_SZ` with CR, LF or NUL as `hex(1):` (UTF-16 LE), `REG_NONE` as `hex(0):`, unknown
  types as `hex(<n>):`, the default value as `@`, value names with `\` and `"` escaped. Each case has a golden
  file; a test checks that delete lines come after the key header and before the next key.
- **Backups contain login material** (moved `_wonlogin.ks`, `_wonkver.pub`): the README and the Tools page
  say so; the backup folder is under the user's own profile and is never part of the diagnostics report.

## Amendment 2026-10-02 (implementation, L-WP2)

The canonical form and the policy exist in the core (`Platform.RegistryPath`, `Platform.RegistryWritePolicy`), and
`Platform.PolicyCheckedRegistry` wraps any `IRegistry` so that every change passes the policy; a refused change
throws `RegistryWriteDeniedException` (a programming error, ADR 0013). Refinements made while implementing, all
keeping the decision:

- **VirtualStore mapping generalized**: `HKCU\Software\Classes\VirtualStore\MACHINE\<rest>` becomes
  `HKLM64\<rest>` (then `WOW6432Node` makes it HKLM32), so `...\VirtualStore\MACHINE` itself is the HKLM root.
  `HKCU\Software\Classes\VirtualStore` and `HKCU\Software\Classes` hold the virtualized CD keys and are refused
  as their ancestors.
- **Consecutive `WOW6432Node` segments** after `Software` are all removed (the redirector does not nest them).
- **One ancestor rule for every protected key**: the registry records (`Software\Empire Earth Community\Installations`)
  and the uninstall keys are refused with their subtree and their ancestors, like the CD keys, so neither
  `Software\Empire Earth Community` nor `Software\Microsoft` can be deleted as a tree. None of the allowed keys
  (markers, `UserGpuPreferences`, `Layers`) is such an ancestor.
- **The allow-list is matched against the key as it is written** (ignoring case and empty segments), not against
  its canonical form. The canonical form is deliberately wider than Windows (it treats `/` as a separator), so an
  allow rule matched on it could let the launcher write a physically different key than the rule names. An alias
  therefore reaches a listed key only if the list names that alias itself, and a protected key is refused even
  then. This also lets the cleanup (L-WP8) list VirtualStore copies of SSSI or Mad Doc keys explicitly while the
  CD-key subtree below the VirtualStore stays refused.
- **Allow-list of L-WP2 at key level**: the game settings keys of contract 3.1 and their `Game Options`, the two
  defaults markers, `UserGpuPreferences` and `Layers`, each for setting and deleting values and creating the key;
  no tree deletion. The restriction to the value names of the contract tables (3.2, program paths, layer content)
  comes with the game settings package (L-WP5), the cleanup entries with L-WP8.

Evidence: `Architecture/RegistryAliasPolicyTests` runs 928 cases (21 CD-key aliases with two subkeys each, 24
ancestors, 12 record and 11 uninstall spellings, 6 keys of other hives; each with all four operations, with the
launcher's policy and with an allow-list that names the alias). Disabling the `WOW6432Node` rule locally made 371
cases of the table and of `RegistryPathTests` fail, disabling the VirtualStore rule 200 (not committed).

## Amendment 2026-10-02 (plan review)

The review of the work package plan before L-WP5 found that the decision still allowed `WINXPSP3`, which the
contract no longer allows, and that the advice text of the cleanup table named `Software\Sierra` as a whole.

- **Compatibility layers** (replaces "`WIN7RTM`/`WINXPSP3`" of the decision). The layer entries the launcher may
  add to an HKCU `Layers` value are exactly those of the rows `compatibility` (`DWM8And16BitMitigation`,
  `HIGHDPIAWARE`, `HeapClearAllocation`) and `compatibility_windows` (`WIN7RTM`) of contract 3.7, and only on
  Windows 8 (NT 6.2) and later, the Windows versions of that table. Contract 3.7 and O7: no compatibility values
  on Windows Vista and 7; `WINXPSP3` on Windows 7 caused black screens and runtime errors (t=4280 p=30477); the
  setup stopped writing it and removes the old values (setup commit 56e012b, `RemoveLegacyVistaCompatValue`,
  `setup_is6.iss` line 1776).
  - The policy checks the **content** of a written `Layers` value, not only its name: compared with the current
    value, only the listed entries may be added or removed; every other entry stays as it is. Adding
    `WINXPSP3`, `RUNASADMIN` or any other entry is refused. Deleting the value is allowed when no entry would be
    left, or when it is exactly `~ RUNASADMIN` (contract 3.7 MAY, like the setup's `RemoveLegacyRunAsAdmin`).
  - **Windows 7** (and Wine, where the setup offers no task either): the Game settings page shows no layer
    switches. It offers only removing an HKCU value that is exactly `~ RUNASADMIN`, and it shows old values of
    HKLM and HKCU that the setup's `IsLegacyVistaCompatValue` list names, read-only, with the advice to run the
    current setup, which removes them. Removing these old values from HKCU by the launcher itself would extend
    contract 3.7; that needs a contract change in both repositories and is not part of v2.
  - Tests: the policy refuses `WINXPSP3` and `RUNASADMIN` as added entries, also next to allowed ones and with
    other entries kept; with `FakeSystemInfo` 6.1 and under Wine no layer is offered.
- **Advice for HKLM leftovers.** No advice ever names `Software`, `Software\Sierra` or another ancestor of
  `Software\Sierra\CDKeys` as something to delete. `Software\Sierra` (HKLM64, HKLM32 and their registry
  VirtualStore copies) is shown as "do not delete: contains the NeoEE CD keys"; subkeys of `Software\Sierra`
  other than `CDKeys` are listed one by one with their full path, and only with evidence. Advice to remove a key
  with the Registry Editor as administrator (after exporting it there) exists only for the SSSI and Mad Doc keys
  of the table, each with its evidence. A unit test runs the advice of every table entry (codes and parameters)
  through the canonical form of the policy and fails if a key named for removal is protected or an ancestor of a
  protected key. The test plan's cleanup case checks that `Software\Sierra\CDKeys` still exists afterwards.
- **Cleanup without candidates.** The HKCU part of the list may stay small or empty (amendment of the design
  review). Then the Tools page shows "nothing to clean up" and the read-only list, and no enabled delete button
  (test of the UI mapping, ADR 0014); README and CHANGELOG describe R5 as "cleanup of HKCU entries; HKLM entries
  are only shown, with advice". Test-plan cases collect real HKCU leftovers of retail and GOG installations as
  evidence for later entries.

## Amendment 2026-10-02 (implementation, L-WP5)

The game settings package writes the first backups and narrows the allow-list to the value names, as planned.
Refinements made while implementing, all keeping the decision:

- **Writer and export** (`Backup.RegFileWriter`, `Backup.RegistryExport`): the export reads a key with all its subkeys
  through `IRegistry` and fails as a whole if one of them cannot be read; then nothing is changed. Golden files of
  every case of the design review amendment are in `Empire-Earth-Launcher.Tests/Core/Backup/Golden/`;
  `.gitattributes` marks `*.reg` as binary, so git keeps the UTF-16 LE bytes and the CRLF. `Backup.BackupLocations`
  creates `Backups\<yyyy-MM-dd_HHmmss>_<what>` (`_2`, `_3`, ... if that folder exists), writes each file through a
  temporary file and reads it back; a file that differs or cannot be read is a failed backup.
- **What a backup holds**: one file per game, `<yyyy-MM-dd_HHmmss>_<Product>_<EE|AoC>.reg`, with the game settings key
  and its subkeys, a delete line for every value the action will create, and the values outside that key that the
  action changes (GPU preference, marker), as they are or as a delete line. Removing `~ RUNASADMIN` writes
  `<yyyy-MM-dd_HHmmss>_Layers.reg` with the values it removes. A test imports each backup into the in-memory registry
  (a test-only importer) and compares the result with the state before the action.
- **Where no backup is written**: creating a missing value (first run, class S at start) changes nothing that
  existed. The synchronization of class S before a game starts and the compatibility switches overwrite without a
  backup and log every value with its old and new data: the first runs before every Play and would fill the backup
  folder with copies of two values derived from the folder, the second is undone by the same switch on the same
  page (ARCHITECTURE 14). Every overwrite of D or P values (display settings, the answer "replace", reset) and the
  removal of `~ RUNASADMIN` write one.
- **Allow-list with value names** (`GameSettings.LauncherWritePolicy`, in place of `RegistryWritePolicy.Default`,
  which is removed): each game settings key and its `Game Options` subkey allow exactly the value names of contract
  3.2 for that key, the marker keys `EE` and `AoC`; `UserGpuPreferences` and `Layers` allow a value name only if it
  is a fully qualified, normalized path whose file name is `Empire Earth.exe` or `EE-AOC.exe`. The policy does not
  know the discovery, so "of a discovered installation" stays a duty of the callers, which only use the program
  paths of an `Installation`. The list lives with the game settings because it is built from their table; the
  platform keeps the mechanism (`RegistryWriteRule`, `RegistryWritePolicy`) and the protected keys.
- **Layer content**: `PolicyCheckedRegistry` gives the policy the new value and a function that reads the current
  one. Only the launcher's entries may be added or removed; every other entry keeps its order and spelling and a
  `~` prefix stays (`CompatibilityLayers.IsAllowedChange`). A current value that cannot be read is refused.
  `LauncherWritePolicy.For(ISystemInfo)` allows no entry below Windows 8 and under Wine; then only deleting a value
  that is exactly `~ RUNASADMIN` or has no entries remains. The denials have their own codes (`ValueNotAllowed`,
  `LayerContent`), so log and tests tell them from the key denials.
- **Contract 3.7, revision 2**: the read-only list of old values is not shown for an installation whose `Tasks`
  contain the setup's opt-in task `compatibility_legacy`; such a value is the task's own.

Evidence: `Core/Backup/RegFileWriterTests` (23 tests with the golden files), `RegistryExportTests`,
`BackupLocationsTests`, `Core/GameSettings/LauncherWritePolicyTests` (62), `GameDefaultsServiceTests` (restore by
import, a failed backup changes nothing), `CompatibilityOptionsTests`; `Architecture/RegistryAliasPolicyTests`
(936 cases) now runs against `LauncherWritePolicy.Default`. Allowing `WINXPSP3` in `CompatibilityLayers` locally made
12 tests fail; ignoring the task `compatibility_legacy` made 2 fail (both not committed).

## Amendment 2026-10-02 (implementation, L-WP8)

The maintenance package implements the cleanup list, the deletes of the allow-list and the file backups. Refinements
made while implementing, all keeping the decision and the amendments above:

- **The list is code** (`Maintenance.CleanupCandidates`, 17 entries, the table of ARCHITECTURE 4.6): eight HKCU keys
  the launcher may delete (the four game settings keys of contract 3.1 and the four registry VirtualStore copies of the
  SSSI and Mad Doc keys, with and without `WOW6432Node`), four HKLM keys with advice only, and five `Software\Sierra`
  keys (HKLM32, HKLM64, HKCU and both VirtualStore copies) that are protected. An entry cannot be built with a hive
  root, a wildcard, evidence that does not match `t=\d+|p=\d+|setup:`, a delete or advice target that the policy
  protects, an HKLM key the launcher would delete, an HKCU key with advice only, or a protected entry that is not an
  ancestor of `CDKeys`. The Mad Doc entries are narrowed to `Mad Doc Software\EE-AOC`, the vendor root stays off the
  list (p=4756: "only ones for ee and aoc if you have other Mad Doc games").
- **Stale means both conditions** of the design review: no installation of the entry's product was found, and the
  folder of its own `Installed From Volume`/`Installed From Directory` is missing on a present, fixed, local drive.
  This replaces the "or" of the decision ("points to no existing folder, or belongs to no discovered installation"):
  a key whose folder exists, lies on a missing, removable or network drive, or is not named at all is kept and shown
  with that reason, because nothing proves that it is a leftover. HKLM entries get the advice "export, then delete with
  the Registry Editor as administrator" under the same conditions, the Sierra entries always "do not delete: contains
  the NeoEE CD keys" with only whether `CDKeys` exists. `CleanupAdvice` names a deletion target only for those two
  codes and throws if `RegistryWritePolicy.ProtectionOf` (the protection check on the canonical form, now public)
  protects it.
- **Deletes of the allow-list**: `LauncherWritePolicy` takes `DeleteSubKeyTree` rules for exactly the eight HKCU keys
  from `CleanupCandidates.WriteRules`, matched as written; the protected keys are refused first, so no list entry and
  no alias of one reaches the CD keys, an install record or an uninstall key. These rules allow nothing but the tree
  deletion; the game settings keys keep their value rules of L-WP5, and nothing else below the keys is opened.
- **Order of a cleanup** (`RegistryCleanup.Delete`): only offered items of the scan -> mutation guard -> every
  selected key checked again (one that is no longer stale, for example because a drive came back, stops everything)
  -> one `.reg` file of all selected keys with their subkeys in `Backups\<time>_registry-cleanup\` -> `DeleteSubKeyTree`
  in the order of the list. If the backup cannot be written and read back completely, nothing is deleted. Whether each
  `CDKeys` key exists is logged before and after, never a value; one that existed before and is missing afterwards
  would be logged as an error. On 32-bit Windows both HKLM views are one key; the HKLM64 entry is then listed once, as
  its HKLM32 twin (compared through their `.reg` bytes).
- **File backups** (`Backup.FileBackup`): the WON login reset moves files, as the decision says, by copying each into
  `Backups\<time>_won-login-reset\`, reading the copy back and writing `moved-files.txt` (UTF-8 with BOM, CRLF, one
  line `<copy> <- <original>` per file); only when every copy is confirmed are the originals removed. A file Windows
  does not let the launcher remove stays and is reported (`Partial`); a failed copy removes nothing. Files listed in
  the manifest are never moved; when the manifest exists but cannot be read, nothing is moved. The import of saved
  games uses the same class to copy the files it is about to replace into `Backups\<time>_import-saved-games\`
  (`CopyIntoBackup`); a file whose old version could not be backed up is not written. The limit per file is 64 MiB,
  the limit of an imported saved game.

Evidence: `Core/Maintenance/CleanupCandidatesTests` (42, among them `TheCodeTable_EqualsTheTableOfArchitecture_4_6`,
`EveryEntry_HasEvidence_OfTheForumOrTheSetup` and `AManipulatedEntry_CannotBeBuilt`), `CleanupAdviceTests` (11,
`NoAdvice_NamesAProtectedKeyOrAnAncestorOfTheCdKeys_ForDeletion` through the canonical form),
`RegistryCleanupTests` (40: backup before delete, a failed backup deletes nothing, the CD keys unchanged after a
cleanup, blocked by setup and game), `LauncherWritePolicyTests` (71), `RegistryWritePolicyTests` (36, `ProtectionOf`),
`Architecture/RegistryAliasPolicyTests` (939 cases, with `AliasesOfTheCleanupKeys_AreNotDeleted`), `FileBackupTests`
(11, also with real files), `WonLoginResetTests` (15), `Launcher/CleanupViewTests` (the UI mapping without
candidates). Letting the cleanup go on after a failed backup file, moving a manifest file in the WON reset, or
enabling the delete button without an offered key made one test fail each (locally, not committed).

## Amendment 2026-10-02 (security review after L-WP9)

The security review found three ways around "backup first, never the CD keys" that the decision did not name. Closed,
keeping the decision:

- **No line injection into a `.reg` backup**: the writer escaped only `\` and `"`, so a value or subkey name with a line
  break (possible in any HKCU key) ended its line and could add lines such as `[-HKEY_LOCAL_MACHINE\SOFTWARE\Sierra\CDKeys]`
  to a backup, which a restore by double-click would run with administrator rights. `RegFileWriter` refuses names with a
  control character and key names with `]` (the format has no escape for them); `RegistryExport` reports such a tree as
  `InvalidName`, so the backup fails and nothing is changed.
- **Symbolic registry links are never followed**: `RegistryKey` follows `REG_LINK` keys, so a link below a stale key
  (for example to `HKCU\Software\Sierra`) would have been exported into the backup, CD keys included, and deleted with the
  tree. `IRegistry.IsLink` opens each key with `REG_OPTION_OPEN_LINK`; `RegistryExport` refuses a tree with a link (the
  backup fails, nothing is deleted), and the cleanup reads each tree again right before deleting it. Links in the parents
  of a fixed cleanup key are not looked at (the keys of the list are named by the setup and the games).
- **"The folder is missing" means missing**: `Directory.Exists` is also false when access is denied, so a folder the
  player may not look at counted as missing and its key was offered. A folder now counts as missing only if its parent
  can be listed without it, or the parent is missing in the same sense; otherwise the key is kept with the new state
  `FolderUnknown` and its own text.

Evidence: `Core/Backup/RegFileWriterTests` (`NamesWithControlCharacters_AreRefused`,
`KeyNamesWithAClosingBracket_AreRefused_ValueNamesMayHaveOne`), `RegistryExportTests` (`ReadTree_FailsForANameTheRegFileCannotHold`,
`ReadTree_FailsForASymbolicLink`), `Core/Maintenance/RegistryCleanupTests` (`Delete_WhenANameCannotBeWrittenToTheBackup_NothingIsDeleted`,
`Delete_WhenTheTreeHoldsASymbolicLink_NothingIsDeleted`, `Delete_WhenASymbolicLinkAppearsAfterTheBackup_TheKeyIsNotDeleted`,
`Scan_AFolderThatMayNotBeLookedAt_IsKept`, `Scan_AFolderMissingFromAParentThatCanBeListed_IsStale`); test plan WP8-17 (a
link on real Windows) and WP8-18 (a folder with a deny ACL).

# 0007 Registry write scope, protected keys and .reg backups

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review; implementation in L-WP2; plan review), see
the Amendment sections

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

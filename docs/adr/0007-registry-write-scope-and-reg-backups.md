# 0007 Registry write scope, protected keys and .reg backups

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review, see the Amendment section)

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

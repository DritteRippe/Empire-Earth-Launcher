# 0015 Game settings: target folders and when the launcher writes

Status: **Accepted** (2026-10-02), amended 2026-10-02 (implementation of the discovery in L-WP4; plan review;
implementation in L-WP5; contract revision 3), see the Amendment sections

## Context

The contract (3.2, 3.6) lets the launcher write the class S values ("Installed From"), the defaults of the
first run and a reset into the HKCU game settings key of the current account. The design review found two
ways in which the first design could break a working installation:

- **Wrong folder.** The first design computed `Installed From Directory` from the install root plus the
  fixed folder name `Empire Earth`. Foreign installations (retail, GOG, copies) found through the
  "Installed From" values (contract 1.4 source 4: "They name the EE folder; the root is its parent") may have
  another folder name (`C:\Games\EE`) or sit directly below a drive (`D:\Empire Earth`, root `D:`). The old
  `GameDirectoryLocator` worked with the real EE folder. AoC reads the base game's values (t=2825 p=19423).
- **Shared key, automatic write.** Community EE, retail and GOG installations share
  `Software\SSSI\Empire Earth` (contract 1.4 source 4). Running the first run at every launcher start for an
  automatically selected installation would point "Installed From" away from the installation the player
  starts with their own shortcut; the forum shows exactly this kind of mixed state (t=4723 p=33045 "Mega
  Installer" bending the registry for parallel installations; mixed patch levels t=1868, t=2195).

## Decision

- **Real folders.** An `Installation` carries the real `EeFolder` and the optional `AocFolder` (contract 1.4
  already names them as separate attributes), not only the root. For community installations they are
  `<root>\Empire Earth` and `<root>\Empire Earth - The Art of Conquest`; for foreign ones the folders the
  "Installed From" values or the user's choice name.
- **S values from the real game folder.** `Installed From Volume` = the first two characters of the game
  folder; `Installed From Directory` = the parent of the game folder without its drive, upper-cased, with
  exactly one `\` before the folder name, then the folder name (not upper-cased) and `\`. For community
  installations this is byte-identical to the contract formula of 3.3; for `D:\Empire Earth` it is
  `\Empire Earth\`. A game folder that does not start with `<letter>:` gets no values and a warning
  (contract 3.3).
- **Upper-casing stays as in the contract** (`ToUpperInvariant`), comparisons are ordinal ignore case after
  normalization. The setup's `UpperCase` changes only ASCII letters (`setup_is6.iss` lines 1146-1148), so for a
  root with `ü` the bytes differ (`Ü` vs `ü`) while the comparison treats them as equal: no rewrite loop, and
  NTFS and the game open both. Changing the contract text to "ASCII only" would need a change in both
  repositories; it is noted as an open point, not done here. Tests run comparisons and upper-casing with
  `CurrentCulture` `tr-TR`. A game folder with characters outside the ANSI code page of the system gets an
  information hint (the ANSI game cannot open it; forum report test case 20).
- **When the launcher writes:**
  - **at launcher start: never class S.** The first run (P, D, GPU preference, marker; contract 3.6) runs at
    start only if the installation is **unambiguous** for that game settings key: the user chose it
    (source 1), or every installation found that uses that key is this one. Otherwise the first run waits for
    the first Play of that game or an explicit "apply defaults" by the user.
  - **before every start** (contract 3.6): class S for the game that is started, then the first run if the
    marker is missing. S is written only if the normalized values differ; a foreign installation whose values
    already name its own existing game folder is left untouched (test).
  - **reset**: only on the user's confirmation, as in ADR 0007.
  - When more than one installation found shares a game settings key, the Launcher page says so: starting one
    from the launcher points the game's "Installed From" values at it.
- **Higher contract version** (contract 5): if the installation's `ContractVersion` is higher than the
  launcher knows, the launcher offers no reset and no defaults, keeps class S, and suggests a launcher update.
- **Consistency warnings** (contract 3.6) are computed at every start and always listed on the Game settings
  page. On the Play page they are a non-modal info bar that the user can hide per value and content (stored in
  `settings.json` as value name plus value); a changed value shows the warning again. `Unknown` integrity of
  `community-legacy` installations is a badge, never a dialog. Reason: deliberate deviations helped players
  (16 bit 2019 on Windows 10, t=5848 p=81313; "Direct3D" instead of TnL for FPS problems, t=3935 p=48202).
- **Compatibility layers**: if the HKLM value (written by an admin setup) already contains a Windows version
  layer, the launcher offers no other version layer in HKCU and explains that the setup set it for all users
  and that a custom run of the setup without the task removes it (contract 3.7 keeps HKLM read-only for the
  launcher; mixed version layers would contradict each other).

## Evidence

- CONTRACT.md 1.4 (sources, EE folder attribute, source 4 wording), 3.2 ("overwrite if different"), 3.3, 3.6,
  3.7, 5 ("offers no reset and suggests a launcher update").
- `Empire Earth Launcher/GameDirectoryLocator.cs` on `v2` (real EE folder from "Installed From").
- `Empire-Earth-Setup/setup_is6.iss` lines 1137-1148 (`GetInstallWithoutDriveLetter`, `UpperCase`).
- Forum: t=2825 p=19423, t=4723 p=33045, t=1868, t=2195, t=5848 p=81313, t=3935 p=48202, t=4280 p=30477;
  forum report section 8 test cases 8 and 20.

## Consequences

- Discovery, defaults and Play use the same `Installation` model; tests cover `C:\Games\EE`, `D:\Empire Earth`,
  a user choice of the AoC folder, two installations on the SSSI key (launcher start writes nothing) and the
  foreign no-op case.
- A player with several installations on one key gets the defaults only when they start a game from the
  launcher or confirm it; this is slower but never moves another installation's values silently.

## Alternatives considered

- **First run at every start for the selected installation** (first design): see context. Rejected.
- **Never write class S for foreign installations**: AoC of a GOG installation then still depends on the
  base game having been started once; the contract requires S before every start. Rejected.
- **ASCII-only upper-casing now**: would deviate from the contract text in one repository. Deferred to the next
  contract change.

## Amendment 2026-10-02 (implementation, L-WP4)

The discovery of the core (`Installations.InstallationDiscovery`) gives every `Installation` its real `EeFolder` and
`AocFolder`; the S values, the first run and Play (L-WP5, L-WP6) take the folders from there. Details decided while
implementing, keeping the decision:

- **Key before hive** for "Installed From" (`InstalledFromReader`): `Software\Neo\Empire Earth` in HKCU, HKLM32, HKLM64,
  then `Software\SSSI\Empire Earth` in the same order, with a table test (also "SSSI in HKCU + Neo in HKLM32 -> Neo
  first"). The contract text stays as it is until the next contract change (ARCHITECTURE 14).
- **AoC folder of foreign installations**: the "Installed From" values of the AoC key of the same product **in the same
  hive and view** as the EE values that found the installation, and only if `EE-AOC.exe` is there; a folder of another
  hive could belong to another installation (a retail installation in HKLM32 and a copy in HKCU). A user choice of an
  AoC folder supplies it as well when no source names one.
- **The choice that is saved** when the player picks an installation of the list is its EE folder, not its root: for
  `C:\Games\EE` the root `C:\Games` alone would not say which folder holds the game once the "Installed From" values
  point elsewhere.
- **Unambiguous** (the rule for the first run at launcher start) is `DiscoveryResult.IsUnambiguous`: chosen by the user,
  or no other installation found has the same product (all EE installations, also retail and GOG, share the SSSI key).
  The Launcher page shows the hint of this ADR per product with more than one installation
  (`DiscoveryResult.ProductsWithSharedSettings`); its text says that the settings, including the game folder stored
  there, are the same for all of them, which is true before and after Play writes them (L-WP6).
- **Higher contract version**: `Installation.HasNewerContract`; the Launcher page already shows the hint to update the
  launcher; defaults and reset honour it in L-WP5.

Evidence: `Core/Installations/InstalledFromReaderTests` (order table), `DiscoveryContractTests` (every rule of contract
1.4 and 1.5), `InstallationDiscoveryTests` (`C:\Games\EE`, `D:\Empire Earth` with root `D:\`, the user choice of root,
EE folder and AoC folder, also when they do not exist, shared keys and `IsUnambiguous`), `Launcher/InstallationServiceTests`
(the EE folder is saved).

## Amendment 2026-10-02 (plan review)

"Class S is never written at start" left R1 open for an account that opens the launcher once and then starts AoC
through its desktop shortcut: AoC needs the base game's "Installed From" values (t=2825 p=19423, forum report
section 8 test case 7), and that account never pressed Play. It also deviates from contract 3.6 (first run, step 1
is class S) without being listed as a clarification.

- **At launcher start, class S is created, never changed**: for an installation that is unambiguous for its game
  settings key (`DiscoveryResult.IsUnambiguous`) and that the mutation guard allows, the launcher writes the two
  class S values of each installed game if **both** are missing. Values that are missing point to no
  installation, so this cannot redirect another one, also not a copy the discovery did not find. Existing values,
  also differing ones, are left to the sync before Play. The values come from the real game folder as decided
  above; the write is logged.
- Tests: second account, unambiguous, both values missing -> created; values present and different -> untouched
  at start; ambiguous -> nothing written; only one of the two values missing -> nothing written at start (logged,
  Play syncs it); blocked by the mutation guard -> nothing written.
- The difference to contract 3.6 (at start only creation, and only for unambiguous installations) is listed in
  ARCHITECTURE 14 as a clarification for the next contract change in both repositories. The test plan gets the
  case "second account opens the launcher, closes it, starts AoC through the shortcut".

## Amendment 2026-10-02 (implementation, L-WP5)

The game settings package implements the decision and the plan review amendment in `GameSettings.GameDefaultsService`,
`ConsistencyChecker`, `HintVisibility` and `CompatibilityOptions`. Details decided while implementing, keeping the
decision:

- **When**: the launcher-start part runs after **every** discovery (`GameSettingsModel.ApplyAfterDiscoveryAsync`), so a
  folder the player chooses on the Launcher page gets its defaults at once (a choice makes the installation
  unambiguous). The first run of an ambiguous installation waits for the first Play (L-WP6, `ApplyDefaultsIfNeeded`)
  or the reset, which the Settings page offers with the reason ("several installations share these settings"); there
  is no separate "apply defaults" button, the reset is that action.
- **Class S** at start: created only when both values of a game are missing; one value missing -> nothing, logged;
  values present -> untouched. Before Play (`SynchronizeInstalledFrom`, public for L-WP6): written only if the
  normalized values differ, also for an installation of a newer contract (contract 5 keeps class S). A game folder
  without drive letter gets no values and a hint.
- **Display question**: one question for the differing D values of every game of every unambiguous installation,
  non-blocking (info bar of the Play page and the Settings page). Both answers write the markers of these games; if
  the backup for "replace" fails, nothing is written, so the question comes again. The markers of a game wait for the
  answer, so a launcher closed without answering asks again at the next start (ARCHITECTURE 14).
- **Hidden hints**: stored in `settings.json` as `HiddenHints` entries with the finding and the game settings key
  (`BitDepthMismatch HKCU\Software\Neo\Empire Earth`, `screen` for the screen) and the values the finding is about
  (`Game Bit Depth=16; Texture Bit Depth=32`, the folder for the folder hints, the screen size). A change of these
  values shows the hint again; the Settings page lists every hint and its checkbox "Play page" hides or shows it.
- **Folder outside the ANSI code page**: `ISystemInfo.IsInAnsiCodePage` (the system code page with an exception
  fallback, so no best-fit mapping); the hint is information only, the values are written as for any folder.
- **HKLM version layer**: with a Windows version mode in the HKLM value (or another one in HKCU) no other version mode
  is offered; one that is on in HKCU can still be switched off.
- **Newer contract**: no defaults, no display settings and no reset (`NewerContract` result and status); class S stays.

Evidence: `Core/GameSettings/GameDefaultsServiceTests` (second account, unambiguous, both values missing -> created;
present and different -> untouched; ambiguous -> nothing; one value missing -> nothing and a log line; blocked by
the guard -> nothing; `C:\Games\EE`, `D:\Empire Earth`, network folder; newer contract), `ComputedValuesTests`
(`tr-TR`), `ConsistencyChecksTests`, `HintVisibilityTests`, `CompatibilityOptionsTests`, `Launcher/GameSettingsModelTests`;
test plan WP5-01 to WP5-04 and WP5-19.

## Amendment 2026-10-02 (contract revision 3)

Contract revision 3 (both repositories in one step, contract 5; still version 1, draft) states the readings this record
kept for the next contract change (ARCHITECTURE 14); the launcher already implemented them, its code did not change:

- **1.4**: "Installed From" key before hive (amendment of L-WP4); the EE folder of a `foreign` installation is the real
  folder its sources name, its AoC folder comes from the AoC key in the same hive and view or from the user choice,
  which may be an AoC folder (decision "Real folders", amendment of L-WP4).
- **3.6**: at the launcher start class S is only created when both values are missing, and like the first run only for
  an installation that is unambiguous for its game settings key (amendment of the plan review); the display question
  is asked until the user answers, and the marker follows the answer (amendment of L-WP5, contract 3.2 and 3.5 too);
  class S before every game start while no other game runs (ADR 0016).
- **Not in revision 3**: ASCII-only upper-casing (Alternatives considered). It would change the launcher's code, so it
  is no clarification of what the launcher does; it stays an open point of ARCHITECTURE 14.

Evidence: `docs/CONTRACT.md` 1.4, 3.2, 3.5 and 3.6 and its history (section 5); the tests named in the amendments
above, unchanged.

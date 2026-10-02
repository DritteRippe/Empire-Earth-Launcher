# Setup and launcher contract

What the Empire Earth Community Setup leaves on a computer and what the Empire Earth Launcher may rely
on and change. This file exists twice, identical, as `docs/CONTRACT.md` in
[Empire-Earth-Setup](https://github.com/EE-modders/Empire-Earth-Setup) and
[Empire-Earth-Launcher](https://github.com/EE-modders/Empire-Earth-Launcher). Change it only in both
repositories at once (same text, same commit subject), see [5. Versioning](#5-versioning).

| | |
|---|---|
| Contract version | **1** |
| Status | **Draft**: specified for setup v2 and launcher v2, not implemented by a release yet |
| Based on | setup `setup_is6.iss`, `config_ee.iss`, `config_neoee.iss`, `utils.iss` (branch `v2` at fb375aa) and the setup's decision records 0004, 0005, 0007 and 0008 (`docs/adr`, branch `v2` at 1f86fb3), launcher `GameDirectoryLocator.cs` (branch `v2` at 79464d4), the official setups 1.7.2 |

The key words MUST, MUST NOT, SHOULD and MAY are used as in RFC 2119. "Setup" means the EE and the
NeoEE setup of every build variant, including their uninstallers; "launcher" means the Empire Earth
Launcher. Placeholders are written `<like this>`. Open questions are collected in
[6. Open questions](#6-open-questions) and referenced as **O1**, **O2**, ... References like
`t=2825 p=19423` are topic and post ids of the save-ee.com support forum (2009 to 2019), where the
problems these rules address were reported.

Contents: [0. Terms and fixed names](#0-terms-and-fixed-names) ·
[1. Install record](#1-install-record) · [2. Integrity manifest](#2-integrity-manifest) ·
[3. Per-user default game settings](#3-per-user-default-game-settings) ·
[4. Repair hand-off](#4-repair-hand-off) · [5. Versioning](#5-versioning) ·
[6. Open questions](#6-open-questions) · [7. Implementation checklist](#7-implementation-checklist)

## 0. Terms and fixed names

### Products

| | EE | NeoEE |
|---|---|---|
| Product id (`InstallType` of the setup) | `EE` | `NeoEE` |
| `AppName` | `Empire Earth` | `NeoEE` |
| `Publisher` in the uninstall key | `Empire Earth Community` | `Empire Earth Community & NeoEE` |
| Default install root | `{autopf32}\Empire Earth` | `{autopf32}\Neo Empire Earth` |
| Game settings key of Empire Earth | `Software\SSSI\Empire Earth` | `Software\Neo\Empire Earth` |
| Game settings key of The Art of Conquest | `Software\Mad Doc Software\EE-AOC` | `Software\Neo\Art of Conquest` |
| Setup data folder | `_setupdata_EE` | `_setupdata_NeoEE` |
| Setup mutex (`SetupMutex`) | `EE_Setup` | `NeoEE_Setup` |
| File that only NeoEE installs into the game folders | none | `neoee.dll` |

Every product has an AppId (a GUID, build switches `EE_AppID` and `NeoEE_AppID`) that is passed at
build time and is not part of either repository. The launcher MUST NOT hard-code AppIds: it reads
them from the install record ([1.1](#11-registry-record)) or from the name of the uninstall key
([1.3](#13-uninstall-key-informative)), see **O1**.

### Install modes

| Mode | Build variant | Inno Setup | `HKA` | Default install root |
|---|---|---|---|---|
| `admin` | Regular | administrative install mode | HKLM | `{autopf32}\<folder>`, e.g. `C:\Program Files (x86)\<folder>` |
| `user` | Regular | non-administrative install mode (`PrivilegesRequiredOverridesAllowed`) | HKCU | `{autopf32}\<folder>` = `%LOCALAPPDATA%\Programs\<folder>` |
| `portable` | Portable | `PrivilegesRequired=lowest`, no uninstaller, no uninstall key | HKCU | `<folder of the setup>\<folder> Portable` |

### Folders, programs and mutexes

- **Install root**: `{app}` of the setup, a full path without trailing backslash.
- **EE folder**: `<root>\Empire Earth`, program `Empire Earth.exe`.
- **AoC folder**: `<root>\Empire Earth - The Art of Conquest`, program `EE-AOC.exe` (only if AoC is
  installed).
- **Setup data folder**: `<root>\_setupdata_<Product>`, hidden, removed by the uninstaller. Setups up to
  1.7.2 used `<root>\<AppId>` instead; setups since v2 remove that old folder.
- **Game mutexes**, created by the running games and used by the setup as `AppMutex`:
  `StainlessSteelStudiosPresentsEmpireEarth` (Empire Earth) and
  `MadDocSoftwarePresentsEmpireEarthExpansion` (The Art of Conquest).

### Registry views

The games are 32-bit programs, but the setup runs in 64-bit install mode on 64-bit Windows
(`ArchitecturesInstallIn64BitMode=x64 arm64 ia64`, also in 1.7.2): its HKLM entries are in the
**64-bit view** (not below `WOW6432Node`). `HKCU\Software` is the same in both views. This document
writes HKLM64 and HKLM32 for the two views of HKLM; 32-bit Windows has only one view. The launcher
MUST open every HKLM key with an explicit view (`RegistryKey.OpenBaseKey(hive, view)`) and MUST NOT
depend on its own process bitness.

## 1. Install record

### 1.1 Registry record

Written by setups since v2 in the install modes `admin` and `user`. Portable setups write no registry
record (**O5**).

Key: `Software\Empire Earth Community\Installations\<Product>`

| Install mode | Hive and view |
|---|---|
| `admin` | HKLM, 64-bit view on 64-bit Windows (`HKA` in 64-bit install mode) |
| `user` | HKCU |
| `portable` | not written |

| Value | Type | Example | Meaning |
|---|---|---|---|
| `ContractVersion` | REG_DWORD | `1` | version of this contract the setup implements |
| `InstallPath` | REG_SZ | `C:\Program Files (x86)\Neo Empire Earth` | install root (`{app}`), full path, no trailing backslash |
| `InstallMode` | REG_SZ | `admin` | `admin` or `user` |
| `AppId` | REG_SZ | `00000000-0000-0000-0000-000000000AEE` | AppId without braces: uninstall key `{<AppId>}_is1`, `product` of the update API ([4.3](#43-where-the-user-gets-the-setup)) |
| `GameVersion` | REG_SZ | `2.0.0.5` | `MyAppVersion` of the setup |
| `SetupVersion` | REG_SZ | `2.0.0` | `MySetupVersion` of the setup |
| `SetupBuild` | REG_SZ | `a1b2c3d` | optional: build identifier of the setup (build switch `SetupBuild`, e.g. the short Git commit, test builds `test<TestID>-<commit>`); absent if the build sets none; informative only, e.g. to tell test builds of the same `SetupVersion` apart |

- Every run of the setup (first installation, update, repair, change of components) writes the record
  after the files are installed. The uninstaller removes it (`uninsdeletekey`; the parent keys
  `Installations` and `Empire Earth Community` with `uninsdeletekeyifempty`).
- One record per product and hive: a second installation of the same product in the same mode replaces
  it (Inno Setup treats it as the same application).
- Only the setup writes the record. The launcher MUST treat it as read-only.
- If a value differs from `install.ini` ([1.2](#12-install-info-file)), `install.ini` wins: it is next to
  the files.

### 1.2 Install info file

Written by setups since v2 in **every** install mode, including portable:
`<root>\_setupdata_<Product>\install.ini`. Windows INI format. The setup writes pure ASCII with CRLF line
ends (`SaveStringToFile`), which is valid UTF-8 without BOM (**O3**). Readers MUST accept a UTF-8 BOM
and LF or CRLF line ends, and MUST ignore unknown sections and keys.

```ini
[Install]
ContractVersion=1
Product=NeoEE
AppId=00000000-0000-0000-0000-000000000AEE
InstallMode=admin
GameVersion=2.0.0.5
SetupVersion=2.0.0
SetupBuild=a1b2c3d
Components=game,gameaoc,additional,additional\directx_wrapper,additional\directx_wrapper\dx11_lvl11,language,language\de
Tasks=compatibility,compatibility_windows,firewallexception,neoee_cdkeys
Written=2026-10-02 18:04:31

[MissingAfterInstall]
1=Empire Earth/DDraw.dll
```

| Key | Meaning |
|---|---|
| `ContractVersion`, `Product`, `AppId`, `GameVersion`, `SetupVersion`, `SetupBuild` | as in [1.1](#11-registry-record) (`AppId` also for portable setups; `SetupBuild` optional) |
| `InstallMode` | `admin`, `user` or `portable` |
| `Components` | the `[Components]` names selected in this run, comma separated, as `WizardSelectedComponents(False)` returns them |
| `Tasks` | the `[Tasks]` names selected in this run, comma separated, as `WizardSelectedTasks(False)` returns them |
| `Written` | local time of the run, `yyyy-mm-dd hh:nn:ss`, informative only |
| `[MissingAfterInstall]` | files the run processed that were gone when the manifest was written ([2.3](#23-which-files)), as manifest paths under the keys `1`, `2`, ...; the section is absent if there are none |

- Component and task names are compared case-insensitively. AoC is installed if `Components` contains
  `gameaoc`. A DirectX wrapper is installed if it contains `additional\directx_wrapper` or a name that
  starts with `additional\directx_wrapper\`. The game language is the name `language\<language>`, e.g.
  `language\pt_BR`.
- The install root is not stored: it is the parent folder of the setup data folder. This keeps the file
  ASCII (the root may contain any character).
- A path that is not ASCII is never written, also not with replaced characters: it switches the
  manifest off ([2.2](#22-format)) and is left out of `[MissingAfterInstall]`; the setup logs it.
- Lifetime: deleted at the start of the installation step (`ssInstall`), written at the end of
  `ssPostInstall` together with the manifest ([2.1](#21-location-and-lifetime)), removed by the
  uninstaller with the setup data folder. Only the setup writes it.

### 1.3 Uninstall key (informative)

Written by Inno Setup itself in the Regular variants, by every community setup version including 1.7.2:
`<HKA>\Software\Microsoft\Windows\CurrentVersion\Uninstall\{<AppId>}_is1` (`admin`: HKLM, 64-bit view on
64-bit Windows; `user`: HKCU; `portable`: none). The values this contract uses:

| Value | Content |
|---|---|
| `Inno Setup: App Path` | install root, no trailing backslash |
| `InstallLocation` | install root with trailing backslash |
| `Publisher` | see [Products](#products) |
| `DisplayName` | `<AppName> v<game version> - Setup v<setup version>` |
| `DisplayVersion` | game version |
| `Inno Setup: Selected Components`, `Inno Setup: Selected Tasks` | as `Components` and `Tasks` in `install.ini` |
| `Empire Earth Community: ContractVersion` | REG_DWORD, the contract version; written by setups since v2 (Regular variants), not by Inno Setup |

Inno Setup deletes this key and writes it again on every run of a setup. Setups since v2 therefore
write `Empire Earth Community: ContractVersion` at the end of `ssPostInstall`, after the manifest
([2.1](#21-location-and-lifetime)); the value is missing if a setup up to 1.7.2 ran over the
installation afterwards ([2.5](#25-verification-by-the-launcher)). Portable setups have no uninstall
key, so there such a later run cannot be detected.

Other readers of this key: the setup itself (previous installation, certificate) and Empire Earth
Diagnostic (its shortcut passes `{<AppId>}_is1`). The launcher MUST NOT write it.

### 1.4 Discovery by the launcher

The launcher builds a list of installations. Each has a product, an install root, an EE folder, an AoC
folder (optional), an install mode, an AppId (optional), versions, its source and a kind:
`community` (setup since v2, with contract version), `community-legacy` (community setup up to 1.7.2) or
`foreign` (anything else, e.g. retail CD, GOG, an old patch chain).

Sources, in the order of default preference:

1. **User choice**: the folder chosen in the launcher settings. It may be the install root or the EE
   folder. It always wins and is kept even if it does not exist (any more), so that the user sees it.
2. **Registry records** ([1.1](#11-registry-record)): product NeoEE before EE; per product HKCU, then
   HKLM64, then HKLM32.
3. **Uninstall keys** of community setups (every version, including 1.7.2): the subkeys of
   `Software\Microsoft\Windows\CurrentVersion\Uninstall` in HKCU, HKLM64 and HKLM32 whose name has the
   form `{<GUID>}_is1` and whose `Publisher` is exactly one of the two publishers of
   [Products](#products) (which also gives the product); NeoEE before EE. Root: `Inno Setup: App Path`,
   else `InstallLocation`. AppId: the GUID of the key name.
4. **"Installed From" values** ([3.3](#33-computed-values)) of the Empire Earth settings key: HKCU, then
   HKLM32, then HKLM64; `Software\Neo\Empire Earth` (NeoEE) before `Software\SSSI\Empire Earth` (EE).
   They name the EE folder; the root is its parent. Retail, GOG and older installations also use the
   SSSI key.
5. **Launcher folder**: the folder of the launcher or its parent, if it is an EE folder or an install
   root.

Rules:

- **Validity**: a candidate is listed if its install root exists (source 4 and the user choice: the EE
  folder). If `Empire Earth.exe` is missing, the installation is listed as **damaged** and leads to the
  repair advice ([2.5](#25-verification-by-the-launcher), [4](#4-repair-hand-off)); it is not dropped,
  because an antivirus deletion must not end in "no installation found".
- **Merge**: candidates with the same install root (full path, compared case-insensitively after
  normalizing separators and trailing backslashes) are one installation. The data of the most specific
  source wins (2 before 3 before 4 before 5); the user choice only selects.
- **Kind**: for every root the launcher reads `_setupdata_NeoEE\install.ini` and
  `_setupdata_EE\install.ini` if they exist (this also finds portable installations and those found by
  sources 1, 4 and 5). `ContractVersion` 1 or higher: `community`. Otherwise a match of source 3:
  `community-legacy`. Otherwise `foreign`; its product is NeoEE if the EE folder contains `neoee.dll`,
  else EE.
- **Two products in one root** (EE and NeoEE installed into the same folder, two `install.ini` files):
  the launcher uses the one modified last, warns that the two products share one folder and reports the
  integrity state as unreliable (**O11**).
- **AoC folder**: `<root>\Empire Earth - The Art of Conquest` if `EE-AOC.exe` exists there or
  `Components` contains `gameaoc` (then a missing program means damaged); for `foreign` installations
  also the folder named by the "Installed From" values of the AoC settings key.
- **Default selection**: without a user choice the launcher uses the first installation in the order of
  the sources. It SHOULD show every installation it found and let the user choose; the choice is saved
  as source 1.
- **Errors**: a missing key or value, denied access or an invalid path only drops that candidate (logged);
  discovery never fails as a whole.
- **Read-only**: the launcher MUST NOT write, repair or delete any of these sources. The only values it
  writes are those of [3](#3-per-user-default-game-settings) in HKCU.

### 1.5 Installations of setups up to 1.7.2

| What exists | Use |
|---|---|
| uninstall key (`admin`: HKLM64, `user`: HKCU) with `Publisher` | source 3, with AppId, versions, components and tasks |
| HKCU game settings keys with "Installed From", **only for the account that ran the setup** | source 4 |
| `<root>\<AppId>\` with `EEStatsSetup.dll` | not used |
| registry record, `install.ini`, manifest, defaults marker | do not exist |

Consequences: the integrity state is **Unknown** with the advice to run the current setup (update or
repair), which keeps the AppId and therefore the uninstall key, removes `<root>\<AppId>\` and adds the
v2 data. The default settings ([3](#3-per-user-default-game-settings)) work with root, product and the
components of the uninstall key. 1.7.2 also wrote the AoC values of `Wait for VSync` and of the window
size when only Empire Earth was installed; the launcher ignores AoC values without an AoC folder.

A setup up to 1.7.2 that runs over a v2 installation (same AppId, same folder) replaces the files but
leaves the registry record, `install.ini` and the manifest of the v2 run as they are, and recreates the
uninstall key without `Empire Earth Community: ContractVersion` ([1.3](#13-uninstall-key-informative)).
The launcher detects that by the rule in [2.5](#25-verification-by-the-launcher).

## 2. Integrity manifest

### 2.1 Location and lifetime

`<root>\_setupdata_<Product>\files.sha256`, written by setups since v2 in every install mode.

- **Deleted** at the start of the installation step (`ssInstall`), so that an aborted run leaves no
  manifest that looks valid.
- **Written** at the end of `ssPostInstall`, after every other step (random maps, NeoEE CD keys),
  together with `install.ini`: first as `files.sha256.tmp`, then renamed. After it the Regular variants
  write `Empire Earth Community: ContractVersion` into the uninstall key
  ([1.3](#13-uninstall-key-informative)).
- **Complete on every run**: first installation, update, repair, change of components and download of
  localized files all write a new manifest of everything that run processed ([2.3](#23-which-files)).
  Running the setup again is the only way to refresh it.
- **Removed** by the uninstaller with the setup data folder.
- Only the setup writes it.

### 2.2 Format

The `sha256sum` text format, as the setup's `data\localized-text.sha256`:

```
<SHA-256: 64 hex digits, lowercase><space><space><path>
```

Example (made-up hashes):

```
27a84712e4b22c415fc544d55cdee82327a829f96d03329457f76ebf9af4dcaa  Empire Earth/Empire Earth.exe
b92ba4ea2e226f3515984dc79734a114e60501b2cf7929a803d6a4f154b31380  Empire Earth - The Art of Conquest/Data/WONLobby Resources/_LobbyResource.cfg
be3f1b44776624b9c37b661e9711ac1d8f51628b80c33e3b60b6a56fea088c9b  Tools/Diagnostic/EE-Diagnostic.exe
```

- **Path**: relative to the install root, `/` as separator, no leading `/` or `./`. Each file once.
- **Order**: the setup SHOULD sort by path (ordinal, ignoring case); readers MUST NOT depend on the order.
- **Encoding**: the setup writes pure ASCII with LF line ends (`SaveStringToFile`), which is valid UTF-8
  without BOM (**O3**). If a path is not ASCII, the setup writes **no manifest** and logs the path; it
  never replaces characters. The state is then Unknown ([2.5](#25-verification-by-the-launcher)).
  Readers MUST accept a BOM, LF and CRLF, uppercase hex digits and the binary marker
  (`<hash> *<path>`), and MUST ignore empty lines.
- **Invalid manifest**: a line of another form, or a path that is absolute, contains a drive, a `:`, a
  `\` or a `..` segment, makes the whole manifest invalid (state Unknown). The launcher never opens a
  file outside the install root because of the manifest.

### 2.3 Which files

- **Included**: every file below the install root that the setup run **processed** from `[Files]`,
  including the verified online files from `{tmp}\verified`. Processed means installed, or kept because
  Inno Setup skipped copying it: Inno Setup calls `AfterInstall` in both cases. The setup records the
  destination of every such file (`AfterInstall` of the `[Files]` entries, `CurrentFileName`), once per
  destination: a later entry that overwrites the same file (e.g. the `NeoEE - Admin` or `NeoEE - User`
  version of `Empire Earth.exe` over the base one) replaces the earlier one. The hash is computed from
  the final file when the manifest is written.
- **Kept files are the exception**: every `[Files]` entry below the install root has `ignoreversion`,
  and none has `onlyifdoesntexist`, `promptifolder` or `confirmoverwrite` (checked by the setup's
  contract check).
- **Excluded**: files with `deleteafterinstall` (`_wonkver.pub`), everything outside the install root
  (`{tmp}`, `{sys}`), the setup data folder itself, the uninstaller (`unins*.exe`, `unins*.dat`, not
  installed by `[Files]`), the `[Files]` entries that only copy existing configuration files onto
  themselves to set permissions (`external` with `Permissions`), and every file the run did not process
  (player files, files the game creates, leftovers of earlier runs).
- **Gone before the manifest is written** (typically deleted or quarantined by an antivirus during the
  installation): not in the manifest but in `[MissingAfterInstall]` of `install.ini`; at the end of the
  installation the setup shows a localized hint (antivirus exception, then repair).

### 2.4 File classes

By the extension of the last name of the path, compared case-insensitively:

| Class | Extensions | Why |
|---|---|---|
| `code` | `exe dll asi ocx sys drv scr com pif cpl efi ax acm mui flt m3d bat cmd ps1 psm1 psd1 vbs vbe js jse wsf wsh wsc sct hta msi msp mst msc appx msix jar lnk url scf reg inf chm hlp` | files that can contain code: `CodeFileExtensions` of the setup's `utils.iss`, also the download policy of the setup; a change of that list changes this table |
| `mutable` | `cfg ini conf config log` | changed by the game or the player by design; the setup gives all users modify rights on `*.cfg`, `*.config`, `*.conf` and `*.ini` in the game folders (**O6**) |
| `data` | everything else | game data, textures, sounds, maps, civilizations, documents |

### 2.5 Verification by the launcher

- **Quick check**, SHOULD run at every start in the background: existence of every listed file and the
  hash of every `code` file.
- **Full check**, on request of the user: the hashes of every `code` and `data` file.

| Finding | Class | State |
|---|---|---|
| file missing, hash differs, or listed in `[MissingAfterInstall]` | `code` | **Damaged** |
| file missing or listed in `[MissingAfterInstall]` | `data`, `mutable` | **Incomplete** |
| hash differs | `data` | **Modified** (informative: mods, HD packs, edited civilizations) |
| hash differs | `mutable` | not reported |
| no manifest, invalid manifest, `ContractVersion` higher than the launcher knows | | **Unknown** |
| manifest present, but the uninstall key lacks `Empire Earth Community: ContractVersion` (see below) | | **Unknown** |
| none of the above | | **OK** |

- The worst finding gives the state of the installation: Damaged, then Incomplete, then Modified, then
  OK. Unknown applies when there is no usable manifest, which includes the following rule.
- **Later run of an older setup** (install modes `admin` and `user`): the launcher opens the uninstall
  key `{<AppId>}_is1` of the installation (`AppId` and `InstallMode` of `install.ini`; `admin`: HKLM64,
  on 32-bit Windows HKLM; `user`: HKCU). If the key exists, its `Inno Setup: App Path` is the install
  root (compared as in [1.4](#14-discovery-by-the-launcher), Merge) and it has no value
  `Empire Earth Community: ContractVersion`, a setup up to 1.7.2 ran after the setup that wrote the
  manifest ([1.5](#15-installations-of-setups-up-to-172)): the manifest no longer describes the files
  and the state is Unknown. If the key is missing, cannot be read or names another root (the `user`
  installation of another account, a later installation of the same product in another folder), this
  rule does not apply. Portable installations have no uninstall key: there such a later run is not
  detectable.
- **Damaged** or **Incomplete**: a localized message (English, German, French) that names the files, says
  that antivirus programs often delete or quarantine game files (t=11045 p=48037, t=41147 p=80317),
  suggests an exception for the install root, and offers the repair ([4](#4-repair-hand-off)).
- **Modified**: only listed in the diagnostics.
- **Unknown**: kind `community` whose uninstall key lacks the value (see above): "an older setup ran
  after the current one, run the current setup"; other kind `community` (the last setup run did not
  finish, or the setup could not write the manifest): the repair advice; kind `community-legacy`:
  "installed by an older setup, run the current setup to enable the check"; kind `foreign`: no check
  and no message.
- Files in the game folders that are not in the manifest are not reported. The diagnostics MAY list
  such `code` files (ASI mods, DLLs of other packs) as information.
- The launcher MUST NOT refuse to start a game because of a finding (only a missing program makes that
  game impossible to start), MUST NOT change, delete, restore or download game files to fix a finding,
  and MUST NOT ask for elevation. It logs every finding with path, class, expected and actual hash.

### 2.6 Expected changes after the installation

- **NeoEE updater** (`NeoEE Updater\NeoEEUp.exe` in both game folders, NeoEE only): it may replace
  NeoEE files (**O2**). Until that is clarified the launcher words differences of `code` files in NeoEE
  installations neutrally ("changed since the installation"), with the same repair offer.
- **Mods installed by the launcher**: files the launcher's mod manager changed itself MUST be shown as
  belonging to that mod, not as Damaged or Modified (**O9**).
- **Game updates** come only from the setup, which writes a new manifest.

## 3. Per-user default game settings

**Source of truth**: the `GameSettings` block of the setup's `setup_is6.iss` (`[Registry]`), the
`UserGpuPreferences` entries next to it and `GetScreenResolutionWidth`/`GetScreenResolutionHeight`. A
change there changes this section in the same commit.

The setup writes these values into HKCU of the account that runs it. In `admin` mode that is the
account that elevated the setup, which need not be the player (over-the-shoulder elevation, other
Windows accounts). The launcher applies the same values for the account that runs the launcher.

### 3.1 Keys

Always HKCU (no view distinction); The Art of Conquest only if AoC is installed.

| Product | Empire Earth | The Art of Conquest |
|---|---|---|
| EE | `Software\SSSI\Empire Earth` | `Software\Mad Doc Software\EE-AOC` |
| NeoEE | `Software\Neo\Empire Earth` | `Software\Neo\Art of Conquest` |

Values below are relative to this game settings key; `Game Options\` is its subkey.

### 3.2 Values

Classes: **S** = bound to the installation, kept in sync; **D** = display defaults; **P** = player
defaults.

| Value | Type | Data | Class |
|---|---|---|---|
| `Installed From Volume` | REG_SZ | see [3.3](#33-computed-values) | S |
| `Installed From Directory` | REG_SZ | see [3.3](#33-computed-values) | S |
| `Rasterizer Name` | REG_SZ | see [3.3](#33-computed-values) | D |
| `Wait for VSync` | REG_DWORD | `0` | D |
| `Game Window Width` | REG_DWORD | see [3.3](#33-computed-values) | D |
| `Game Window Height` | REG_DWORD | see [3.3](#33-computed-values) | D |
| `Game Bit Depth` | REG_DWORD | `32` (0x20) | D |
| `Texture Bit Depth` | REG_DWORD | `32` (0x20) | D |
| `AutoSave In Milliseconds` | REG_DWORD | `1200000` (0x124F80, 20 minutes) | P |
| `Music Volume` | REG_DWORD | `44` (0x2C) | P |
| `Sound Volume` | REG_DWORD | `60` (0x3C) | P |
| `Take JPG Screenshots` | REG_DWORD | `1` | P |
| `Game Options\Map Type` | REG_SZ | `Continental` | P |
| `Game Options\Map Size` | REG_DWORD | `2` | P |
| `Game Options\Starting Resources` | REG_DWORD | `3` | P |
| `Game Options\Starting Epoch` | REG_DWORD | `0` | P |
| `Game Options\Ending Epoch` | REG_DWORD | Empire Earth `13` (0xD), The Art of Conquest `14` (0xE): the last epoch of the game | P |
| `Game Options\Game Unit Limit` | REG_DWORD | `1200` (0x4B0) | P |
| `Game Options\Wonders For Victory` | REG_DWORD | `0` | P |
| `Game Options\Game Variant` | REG_DWORD | `2` | P |
| `Game Options\Difficulty Level` | REG_DWORD | `0` | P |
| `Game Options\Game Speed` | REG_DWORD | `3` | P |
| `Game Options\Reveal Map` | REG_DWORD | `0` | P |
| `Game Options\Allow Custom Civs` | REG_DWORD | `1` | P |
| `Game Options\Lock Teams` | REG_DWORD | `1` | P |
| `Game Options\Lock Speed` | REG_DWORD | `1` | P |
| `Game Options\Cheat Codes` | REG_DWORD | `0` | P |

Who writes what, and when:

| Class | Setup (account that runs it, every run) | Launcher, first run ([3.5](#35-defaults-marker) marker missing) | Launcher, before every game start | Launcher, reset |
|---|---|---|---|---|
| S | overwrite | overwrite if different | overwrite if different | overwrite |
| D | overwrite (`deletevalue`) | create if missing; offer once to overwrite values that differ | | overwrite |
| P | create if missing (`createvalueifdoesntexist`) | create if missing | | overwrite |
| GPU preference ([3.4](#34-gpu-preference)) | overwrite | create if missing | | overwrite |

- **Defaults applied once**: D, P and the GPU preference at the first run, only where values are missing;
  existing D values only with the user's consent.
- **Reset only**: overwriting existing D and P values (and the GPU preference) happens only on an
  explicit reset by the user.
- **Always in sync**: S follows the installation that is started.
- "Create if missing" tests only whether the value exists, like Inno Setup. "Overwrite" deletes a value
  of another type first, like `deletevalue`.
- Values that are not in the table (player names, addresses, everything else the game writes) are never
  changed or deleted, neither by the defaults nor by a reset.

### 3.3 Computed values

- **`Installed From Volume`**: the first two characters of the install root, e.g. `C:` (setup:
  `GetInstallDriveLetter`).
- **`Installed From Directory`**: the install root without its first two characters, upper-cased, then
  `\`, the name of the game folder (not upper-cased) and `\` (setup:
  `GetInstallWithoutDriveLetterBase` and `\<game folder>\`). Example for the root
  `C:\Program Files (x86)\Neo Empire Earth`: Empire Earth
  `\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth\`, The Art of Conquest
  `\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth - The Art of Conquest\`.
  - The games read both values from HKCU; with them The Art of Conquest starts without starting Empire
    Earth first (t=2825 p=19423).
  - The setup upper-cases with Pascal Script's `UpperCase`, the launcher with the invariant culture.
    Readers compare case-insensitively, after normalizing separators and doubled backslashes.
    "Different" in [3.2](#32-values) means different after this normalization.
  - A root that does not start with `<drive letter>:` (a network path) cannot be expressed: the launcher
    then writes neither value and warns.
- **`Rasterizer Name`**: `Direct3D` under Wine or when a DirectX wrapper is installed, otherwise
  `Direct3D Hardware TnL`.
  - Wine: the setup asks `EEStatsSetup.dll`, the launcher checks whether `ntdll.dll` exports
    `wine_get_version`.
  - DirectX wrapper: from `Components` ([1.2](#12-install-info-file)), else from
    `Inno Setup: Selected Components` of the uninstall key; without component information, if one of
    `DDraw.dll`, `D3DImm.dll`, `D3D8.dll`, `D3D9.dll` is in the game folder (the wrapper files the setup
    removes in `[InstallDelete]` before it installs a wrapper; the game itself ships none of them).
- **`Game Window Width`**: the width of the primary screen in physical pixels, limited to 1024 to 1920.
  **`Game Window Height`**: its height, limited to 768 to 1080. Each dimension is limited on its own
  (setup: `GetSystemMetrics(SM_CXSCREEN/SM_CYSCREEN)`, `MinGameWindowWidth` ... `MaxGameWindowHeight`).
  The launcher MUST measure physical pixels (DPI-aware process or `EnumDisplaySettings`), **O4**. A
  screen lower than 768 pixels gets a warning in the setup and in the launcher (t=3863).

### 3.4 GPU preference

Windows 10 and later, only if the task `compatibility_windows` was selected (`Tasks` of `install.ini` or
of the uninstall key): `HKCU\Software\Microsoft\DirectX\UserGpuPreferences`, value name = full path of
the program (`<root>\Empire Earth\Empire Earth.exe`, and `<root>\Empire Earth - The Art of
Conquest\EE-AOC.exe` if AoC is installed), REG_SZ `GpuPreference=2;` (high-performance graphics card).
The uninstaller removes it for the account that uninstalls (`uninsdeletevalue`).

### 3.5 Defaults marker

`HKCU\Software\Empire Earth Community\GameDefaults\<Product>`, REG_DWORD values `EE` and `AoC`: the
contract version whose defaults were applied to this game for this account; missing = never.

- **Setup** (Regular variants, install modes `admin` and `user`): writes `EE` (component `game`) and
  `AoC` (component `gameaoc`) on every run, for the account that runs it. The uninstaller removes
  `GameDefaults\<Product>` for the account that uninstalls (and the parent keys if they are empty).
  Markers of other accounts stay; that is harmless.
- **Portable setups write no marker**: they have no uninstaller that could remove it. The launcher's
  first run ([3.6](#36-launcher-procedures)) then finds the D values the setup has just written and
  does not ask.
- **Launcher**: writes the marker after the first run of [3.6](#36-launcher-procedures) (whatever the
  user answered) and after a reset.
- **Marker present**: only class S is kept in sync; D and P values are not touched (a value the player
  deleted stays deleted). If the marker is lower than the launcher's contract version, the launcher
  creates the values added since that version if they are missing, and raises the marker. A higher
  contract version never overwrites existing values by itself.

### 3.6 Launcher procedures

- **First run**, per account, product and game (marker missing):
  1. class S;
  2. P and the GPU preference: create if missing;
  3. D: create if missing; if an existing D value differs from its default, ask once, without blocking
     the start: "Apply the recommended display settings?" Yes: `.reg` backup, then overwrite D;
  4. write the marker.
- **Before every game start**: class S for the game that is started; old values that change are logged.
- **Reset**, after the user confirmed it: a `.reg` backup of the game settings key with its
  subkeys (e.g. `reg.exe export`) into `%LOCALAPPDATA%\Empire Earth Launcher\Backups\`, file name with
  date and time, product and game; then overwrite S, D, P and the GPU preference; then the marker. If
  the backup fails, nothing is changed.
- **Consistency checks** at every start, shown with the offer of a reset, never fixed by themselves:
  `Game Bit Depth` differs from `Texture Bit Depth` (white, unreadable main menu; comment of the setup);
  16 bit on Windows 8 and later (freezes, t=10931 p=47182, t=11042 p=48016); `Rasterizer Name` does
  not match the wrapper rule of [3.3](#33-computed-values); the window is larger than the screen.
- The launcher writes only HKCU of the account that runs it: never other accounts' hives, never HKLM.

### 3.7 Compatibility flags

Reference for the compatibility options of the launcher. Key
`Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers`, value name = full path of the
program (`Empire Earth.exe` with the component `game`, `EE-AOC.exe` with `gameaoc`), REG_SZ, removed by
the uninstaller. The setup writes it into HKLM in `admin` mode (all users) and into HKCU in `user` and
`portable` mode. The value is `~`, then the values of every row of the table below that applies (task
selected, Windows version and install mode match), in the order of the table, separated by spaces
(setup: `BuildCompatibilityFlags` in `utils.iss` and the compatibility entries of `setup_is6.iss`). If
no row applies, the setup writes no value.

| Task | Values | Windows versions | Root (admin/user/portable) |
|---|---|---|---|
| `everyoneadminstart` | `RUNASADMIN` | all | HKLM / - / - |
| `compatibility` | `DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation` | 8 and later | HKLM / HKCU / HKCU |
| `compatibility_windows` | `WIN7RTM` | 8 and later | HKLM / HKCU / HKCU |

- **Windows versions**: `all` = every Windows the setup runs on (Windows 7 SP1 and later); `8 and later`
  = Windows 8 (NT 6.2) and later, below it the setup neither shows nor selects the task (`MinVersion`).
  **Root**: the root in the install modes `admin` / `user` / `portable`; `-` = the task does not exist
  in that mode.
- `everyoneadminstart` is opt-in (unchecked); `compatibility` and `compatibility_windows` are selected by
  default (their page is only shown with the custom settings). Under Wine the setup offers none of the
  three tasks.
- Outside `admin` mode `compatibility_windows` applies only together with `compatibility`.
- Examples: Windows 10, `admin`, default tasks:
  `~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WIN7RTM`; Windows 7, `admin`, with
  `everyoneadminstart`: `~ RUNASADMIN`; Windows 7, `user`: no value.
- **Windows Vista and 7: no compatibility values** except the opt-in `~ RUNASADMIN` (**O7**). Earlier
  setups wrote values there (official 1.7.2: `EE-AOC.exe`
  `~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3`, `Empire Earth.exe` none). On
  Windows Vista and 7 every run of the setup removes, in the root of its install mode and for both
  programs, a value that is exactly one of `~ WINXPSP3`, `~ RUNASADMIN WINXPSP3`,
  `~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation`,
  `~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3`,
  `~ RUNASADMIN DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation` and
  `~ RUNASADMIN DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3` (setup:
  `IsLegacyVistaCompatValue`). Every other value stays, e.g. one the player set.

The launcher:

- MAY offer the values of the rows `compatibility` and `compatibility_windows` as options, and SHOULD
  offer them only on the Windows versions of the table; it writes them into HKCU only, keeps every other
  entry of the value, and shows the HKLM value read-only;
- MUST NOT offer `RUNASADMIN`: running the game elevated is opt-in through the setup only, the online
  lobby should not run elevated;
- MAY remove the HKCU value if it is exactly `~ RUNASADMIN` (the default of setups up to 1.7.2), like
  the setup's `RemoveLegacyRunAsAdmin`;
- MUST start the games with shell execute semantics (`UseShellExecute = true`) and the game folder as
  working folder, so that every layer applies, including an elevation the user chose (a plain
  `CreateProcess` fails with error 740 then).

### 3.8 Protected keys and files

The launcher MUST NOT delete or change, also not in a cleanup of old registry entries:

- `Software\Sierra\CDKeys` in HKCU, HKLM64 and HKLM32: the NeoEE CD keys that `authtools.dll` registers
  during the setup (HKLM in `admin` mode, HKCU otherwise). Without them online play fails with "CD key
  invalid" (t=10950, t=11021). The launcher MAY check whether the key exists, for the diagnostics, and
  MUST NOT log or show its values (**O8**);
- the registry record ([1.1](#11-registry-record)), the uninstall keys
  ([1.3](#13-uninstall-key-informative)), the setup data folder ([1.2](#12-install-info-file),
  [2](#2-integrity-manifest)) and the HKLM compatibility values ([3.7](#37-compatibility-flags)).

A cleanup of leftovers of other installations (retail, Sierra, InstallShield; t=1036 p=4756,
t=12082 p=49553) makes a `.reg` backup first and uses an explicit list of keys that contains none of the
above.

## 4. Repair hand-off

### 4.1 Principle

The setup is the only repair tool: files, registry values, firewall rules and NeoEE CD keys. The
launcher MUST NOT call `authtools.dll` or reimplement the CD-key registration, MUST NOT download or
start the setup itself, and MUST NOT ask for elevation. It sends the user to the setup download and
explains what to do. A repair is a run of the current setup over the installation: the same AppId
preselects the same folder (`UsePreviousAppDir`) and the previous components and tasks, and the run
rewrites the values of [3](#3-per-user-default-game-settings) for that account, the record, `install.ini`,
the manifest and `Empire Earth Community: ContractVersion` in the uninstall key.

### 4.2 Running setup

While a setup runs it holds its setup mutex (`EE_Setup` or `NeoEE_Setup`, the names of `SetupMutex`,
without `Global\`). While one of them exists the launcher MUST NOT start a game and SHOULD show that a
setup is running;
when it is gone, the launcher runs the discovery ([1.4](#14-discovery-by-the-launcher)) and the quick
check ([2.5](#25-verification-by-the-launcher)) again. The other way round, the setup does not install
while a game mutex ([Folders, programs and mutexes](#folders-programs-and-mutexes)) exists.

### 4.3 Where the user gets the setup

1. If the installation has an AppId: `GET https://api.empireearth.eu/setup/?product=<AppId>`. An answer
   with HTTP 200 contains (trimmed) the download URL of the latest setup.
2. That URL is accepted only if it passes the setup's `IsAllowedUpdateUrl` (`utils.iss`): `https://`; no
   user information, port, backslash, space, control or non-ASCII character; host `empireearth.eu`,
   `neoee.net` or a subdomain of either, or `github.com` with a path that starts with `/EE-modders/`
   (ignoring case) and contains neither `..` nor `%`.
3. Otherwise (no AppId, no answer, URL refused): `https://empireearth.eu/download`.
4. The URL opens in the default browser with the rights of the launcher (not elevated).

Requests use HTTPS with certificate validation (TLS 1.2 or newer), never fall back to HTTP, have
timeouts and send nothing but the query above (no telemetry).

### 4.4 What the launcher tells the user

Localized (English, German, French; other languages fall back to English):

- close the game, then run the downloaded setup; it detects the installation and offers to update or
  repair it;
- keep the same folder (`<root>`) and the same install mode: "for all users" if the mode is `admin`;
- NeoEE: keep the task "Register NeoEE CDKeys" (`neoee_cdkeys`, message `TaskNeoEECDKeys`) selected;
  this is the way to repair the CD keys;
- if files were deleted ([2.5](#25-verification-by-the-launcher)): add an antivirus exception for
  `<root>` first;
- `foreign` installations (retail CD, GOG): the community setup installs its own copy, it does not
  repair them.

### 4.5 Update check (optional)

The same API as the setup's `CheckUpdate`, only for installations with an AppId:

| Request (appended to `https://api.empireearth.eu/setup/?product=<AppId>`) | Answer (HTTP 200, trimmed) |
|---|---|
| `&type=game&version=<GameVersion>` | `false` if this game version is outdated |
| `&type=setup&version=<SetupVersion>` | `false` if this setup version is outdated |
| `&type=game` or `&type=setup` | the latest version; shown only if it has at most 32 characters of `0-9 . - _ space A-Z a-z`, else `?` |

An available update uses the hand-off of [4.3](#43-where-the-user-gets-the-setup).

## 5. Versioning

- The contract version is an integer, here **1**. Setups write it as `ContractVersion` (registry record,
  `install.ini`); the defaults marker stores it per game ([3.5](#35-defaults-marker)). Missing means 0:
  a setup up to 1.7.2.
- **Compatible changes** keep the version: new optional values, keys, sections or files, new default
  values. Writers add them, readers ignore names they do not know.
- **Incompatible changes** raise the version: renaming or removing anything, a new meaning, format or
  location.
- Readers support every version from 1 to the newest they know. For a higher version the launcher uses
  only the install root, the product and the AppId, reports the integrity state Unknown, offers no reset
  and suggests a launcher update.
- A setup writes exactly the version this document describes for its release.
- **Draft**: until the first release implements version 1, version 1 may still change. After that, only
  by the rules above.
- **Two copies**: the text is identical in both repositories. A change is committed to both with the same
  subject and adds a line to the history below; `ci/compare_contract.py` of the setup repository checks
  that the two copies are identical (**O12**).

| Contract version | Date | Change | Setup | Launcher |
|---|---|---|---|---|
| 1 (draft) | 2026-10-02 | first version | v2 (planned) | v2 (planned) |
| 1 (draft) | 2026-10-02 | revision 2026-10-02 (review of the setup v2 plan): optional `SetupBuild` (1.1, 1.2); the setup writes ASCII, the manifest with LF, `install.ini` with CRLF, and no manifest if a path is not ASCII (1.2, 2.2, O3); `Empire Earth Community: ContractVersion` in the uninstall key, Unknown if it is missing after a later run of an older setup (1.3, 1.5, 2.1, 2.5); the manifest lists every processed file (2.3); no defaults marker from portable setups (3.5); table of the compatibility values, none on Windows Vista/7 (3.7, O7); O4, O11 and O12 answered | v2 (planned) | v2 (planned) |

## 6. Open questions

- **O1 AppIds**: the launcher recognizes community installations without AppIds (by `Publisher`,
  [1.4](#14-discovery-by-the-launcher)) and takes the AppId from the record or the key name. Should it
  also know the AppIds of the official builds, which are public in every installation? Proposal: no.
- **O2 NeoEE updater**: does `NeoEEUp.exe` still update files, and which ones? Needed to classify
  differences in NeoEE installations ([2.6](#26-expected-changes-after-the-installation)); question for
  the NeoEE team.
- **O3 File encoding of Inno Setup 6.2** (answered): `SaveStringsToUTF8File` writes a BOM and CRLF, and
  the Pascal Script of Inno Setup 6.2.2 has no `UTF8Encode`. The setup therefore writes pure ASCII with
  `SaveStringToFile`, which is valid UTF-8 without BOM: the manifest with LF, `install.ini` with CRLF. A
  path that is not ASCII switches the manifest off ([2.2](#22-format)); all installed paths are ASCII
  today. Readers still accept a BOM, LF and CRLF. The setup unit-tests the bytes.
- **O4 Physical pixels**: does `GetSystemMetrics` in the Inno Setup 6.2.2 setup return physical pixels on
  scaled displays (DPI awareness of the setup)? Setup and launcher must compute the same window size.
  The setup declares itself system-DPI-aware, so it should get physical pixels at the scaling of the
  sign-in. The game sees physical pixels only with `HIGHDPIAWARE` (task `compatibility`,
  [3.7](#37-compatibility-flags)); without it, it is DPI-virtualized at 150 % and sees logical pixels.
  The setup's test plan therefore runs the case at 150 % twice, with and without the task
  `compatibility`. If the window only fits with `HIGHDPIAWARE`, [3.3](#33-computed-values) says so (on
  Windows Vista and 7 the setup no longer writes it).
- **O5 Portable**: portable setups write no registry record; the launcher finds them through the user
  choice, its own folder or the HKCU "Installed From" values. Should they write an HKCU record anyway?
  Proposal: no.
- **O6 Mutable files**: does the game rewrite installed files other than `cfg ini conf config log` (e.g.
  in `Data\WONLobby Resources`)? Test on Windows: play, then run the full check and list the
  differences.
- **O7 Defaults under review** (decided, setup ADR 0005): no compatibility values on Windows Vista and 7
  except the opt-in `~ RUNASADMIN`, unchanged values on Windows 8 and later, see the table in
  [3.7](#37-compatibility-flags). Official 1.7.2 wrote no value for `Empire Earth.exe` on Windows 7,
  and the forum evidence is weak (t=4280 p=30477, p=30479, p=30480, p=30485; t=1827 p=12147). The
  DirectX wrapper preselection stays, so the `Rasterizer Name` rule of [3.3](#33-computed-values) does
  not change. A later change of these defaults changes [3](#3-per-user-default-game-settings) in the
  same commit, in both copies.
- **O8 CD keys**: which values `authtools.dll` writes below `Sierra\CDKeys` is unknown (closed source).
  Until it is known the launcher only checks that the key exists.
- **O9 Launcher mods**: the mod manager needs its own record of the files it changed, so that
  [2.5](#25-verification-by-the-launcher) can attribute them.
- **O10 Launcher in the setup**: if the setup installs the launcher one day: its files in the manifest,
  closing the launcher before a repair (`AppMutex`), and its folder as a primary source. Not part of
  version 1.
- **O11 One folder for EE and NeoEE** (decided): the setup allows it (separate setup data folders), but
  the integrity check of the product installed first becomes useless. The setup asks a Yes/No question
  when the user leaves the folder page and the folder already holds the other product
  (`_setupdata_<other product>` exists, or the other product's uninstall key has this folder as
  `Inno Setup: App Path`): it names the consequences (the integrity check of the other product;
  uninstalling one removes files and firewall rules of the other) and recommends another folder. "Yes"
  (default) stays on the folder page, "No" continues. Silent installations only log it. The launcher
  rule of [1.4](#14-discovery-by-the-launcher) stays.
- **O12 Copy check** (answered locally): CI has no access to the other repository. The setup repository
  has `ci/compare_contract.py <path of the other clone>`, which compares the SHA-256 of both copies
  (exit code 0: identical, 1: different, both hashes printed, 2: a file is missing). Every change of
  this file is one step in both repositories and runs it.

## 7. Implementation checklist

Setup v2:

- registry record ([1.1](#11-registry-record)) with `Root: HKA`, Regular variants only, removed by the
  uninstaller, with the optional `SetupBuild`;
- defaults marker ([3.5](#35-defaults-marker)) in HKCU with the components `game` and `gameaoc`, Regular
  variants only;
- `Empire Earth Community: ContractVersion` in the uninstall key after the manifest
  ([1.3](#13-uninstall-key-informative)), Regular variants only;
- recording of the processed files (`AfterInstall`), deleting `install.ini` and `files.sha256` at
  `ssInstall`, writing both as ASCII at the end of `ssPostInstall` (temporary file, then rename), with
  `[MissingAfterInstall]` and the localized antivirus hint;
- compatibility values as in the table of [3.7](#37-compatibility-flags), including the removal of the
  old values on Windows Vista and 7;
- unit tests (`ci/tests/unit_tests.iss`) for the manifest line, the path conversion, the INI values, the
  ASCII check and the old compatibility values;
- `GameSettings`, `BuildCompatibilityFlags`, the compatibility entries, `CodeFileExtensions` and this
  document changed together.

Launcher v2, in the UI-free core library with unit tests (fake registry and file system, no network):

- discovery ([1.4](#14-discovery-by-the-launcher)) with all five sources, setups up to 1.7.2, foreign and
  damaged installations, merging;
- manifest reader and checks ([2](#2-integrity-manifest)): BOM, CRLF, invalid lines, paths outside the
  root, classes, states, the uninstall key rule of [2.5](#25-verification-by-the-launcher);
- defaults, marker, consistency checks and reset with backup ([3](#3-per-user-default-game-settings));
- repair hand-off and update check ([4](#4-repair-hand-off)) with the URL cases of the setup's unit tests;
- setup and game mutexes, starting the games with shell execute.

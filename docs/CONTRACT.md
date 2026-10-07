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
| Based on | setup `setup_is6.iss`, `config_ee.iss`, `config_neoee.iss`, `utils.iss` (branch `v2` at 2ce68ee, plus the task `compatibility_legacy` that revision 2 adds) and the setup's decision records 0004, 0005, 0007, 0008 and 0010 (`docs/adr`, branch `v2` at 2ce68ee), launcher `GameDirectoryLocator.cs` (branch `v2` at 79464d4) and the launcher's decision record 0016 (branch `v2` at ec02afa), the official setups 1.7.2; revision 3 also on setup `environment.iss` (branch `v2` at 3a9498d) and the launcher v2 core library with its decision records 0015 and 0016 (branch `v2` at 1b49410); revision 4 also on the setup's decision record 0013 (suite installer) and `setup_is6.iss` (branch `v2` at 332d877) and launcher `SingleInstance.cs` (branch `v2` at 19386bb); revision 5 also on the suite `suite/suite_record.iss` (branch `v2` at 85736cf), launcher `UninstallKeyScanner.cs` (branch `v2` at d454079) and the laptop test TP-93 of the setup's test plan; revision 6 also on the suite `suite/suite_common.iss` and `suite/suite_run.iss` (branch `v2`: the log parser at 826525a, the process runner at 0bf9681, the progress display at db8be16, the runner fixes at 04f80ac), the line `Install step: ...` of setup `setup_is6.iss` and the suite's point of no return (branch `v2` at 83f4528), the freeze before every stop of a product setup in `suite/suite_common.iss` and `suite/suite_run.iss` (branch `v2` at ab4451e), the download lines of setup `downloads.iss` and `utils.iss` (branch `v2` at cbd64c8), the download pages of setup `utils.iss` and `setup_is6.iss` (branch `v2` at ed5584d), the one shortcut of the suite `suite/suite_shortcuts.iss` (branch `v2` at 53d145d, its helpers in `suite/suite_common.iss` at 8c14f54), launcher `ResolutionOptions.cs` and `GameDefaultsService.cs` (branch `v2` at e8f7f98) and launcher `ProductChoices.cs`, `DiscoveryResult.cs`, `InstallationService.cs`, `InstanceForwarding.cs`, `PlayModel.cs` and `SetupDownloadPage.cs` (branch `v2` at 132bbd4), the window size limits of setup `utils.iss` (branch `v2` at ae777f5), the record value `ComponentDefaults` of setup `setup_is6.iss` (branch `v2` at 1f0c5eb) and launcher `ComputedValues.cs` and `ResolutionOptions.cs` (branch `v2` at fc46565) |

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

### Suite and launcher

Since revision 4 an optional third setup, the **suite** "Empire Earth Community" (setup repository,
folder `suite/`, setup decision record 0013), installs the launcher and runs the unchanged EE and NeoEE
setups ([1.7](#17-how-the-suite-runs-a-product-setup-informative)). The suite is always installed in the
mode `admin` (HKLM, 64-bit view on 64-bit Windows), never per user and never portable. The **suite root**
is its `{app}`; it is no install root of a product.

| Name | Value |
|---|---|
| Suite `AppName` | `Empire Earth Community` |
| Default suite root | `{autopf}\Empire Earth Community`, e.g. `C:\Program Files\Empire Earth Community` |
| Suite setup mutex (`SetupMutex`) | `EmpireEarthCommunity_Suite` |
| Launcher mutex | `EmpireEarthCommunityLauncher` |
| Suite `AppMutex` | `StainlessSteelStudiosPresentsEmpireEarth`, `MadDocSoftwarePresentsEmpireEarthExpansion`, `EmpireEarthCommunityLauncher` |
| Launcher program | `{app}\Empire Earth Launcher.exe` |
| Suite record key | `Software\Empire Earth Community\Suite` |
| Suite uninstall key marker | `Empire Earth Community: Suite`, REG_DWORD `1` ([1.3](#13-uninstall-key-informative)) |

- The **launcher mutex** is created by the running launcher, one per Windows session (session
  namespace, without `Global\`). It is neither a setup mutex nor a game mutex, so it never blocks a game
  start or a product setup. The suite names it in its `AppMutex` together with the game mutexes, so the
  suite and its uninstaller do not run while a launcher or a game runs.
- The **suite setup mutex** is held by the suite and by its uninstaller for their whole run, also
  between two product setups ([4.2](#42-running-setup)).
- The suite's own uninstall key `{<suite AppId>}_is1` ([1.3](#13-uninstall-key-informative), HKLM64) has
  the `Publisher` of EE, `Empire Earth Community`, which Windows "Apps" shows for the suite. Since
  revision 5 the suite marks it with the value of the row "Suite uninstall key marker", so that the
  launcher never takes it for an installation of EE ([1.4](#14-discovery-by-the-launcher), source 3).
- The suite record ([1.6](#16-suite-record-optional)), the marker of its uninstall key and the suite
  shortcuts ([1.7](#17-how-the-suite-runs-a-product-setup-informative)) are the only other things of the
  suite that this contract describes.

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
| `AppId` | REG_SZ | `00000000-0000-0000-0000-000000000AEE` | AppId without braces: uninstall key `{<AppId>}_is1`, `product` of the update API ([4.5](#45-update-check-optional)) |
| `GameVersion` | REG_SZ | `2.0.0.5` | `MyAppVersion` of the setup |
| `SetupVersion` | REG_SZ | `2.0.0` | `MySetupVersion` of the setup |
| `SetupBuild` | REG_SZ | `a1b2c3d` | optional: build identifier of the setup (build switch `SetupBuild`, e.g. the short Git commit, test builds `test<TestID>-<commit>`); absent if the build sets none; informative only, e.g. to tell test builds of the same `SetupVersion` apart |
| `ComponentDefaults` | REG_DWORD | `1` | optional, since revision 6: the revision of the setup's default components that this installation has received; `1` = the intro movies (`additional\movies`) belong to the installation types `full` and `compact`. A setup of 1.1.0 or later that updates an installation of the type `custom` whose record has no value or a lower one selects the components that became defaults since then once (not with `/TYPE` or `/COMPONENTS` on the command line, not for the type `raw`), then writes its own revision. Missing: a setup before 1.1.0. Setup-internal; the launcher ignores it |

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
| `Empire Earth Community: Suite` | REG_DWORD `1`, only in the uninstall key of the suite ([Suite and launcher](#suite-and-launcher)); written by the suite since revision 5, not by Inno Setup and never by the EE or the NeoEE setup |

Inno Setup deletes this key and writes it again on every run of a setup. Setups since v2 therefore
write `Empire Earth Community: ContractVersion` at the end of `ssPostInstall`, after the manifest
([2.1](#21-location-and-lifetime)), and only if that run replaced `install.ini` and the manifest; the
value is missing if a setup up to 1.7.2 ran over the installation afterwards, or if the last run could
not replace the two files ([2.5](#25-verification-by-the-launcher)). Portable setups have no uninstall
key, so there neither case can be detected.

For the same reason the suite ([Suite and launcher](#suite-and-launcher)) writes
`Empire Earth Community: Suite` in every run at `ssPostInstall` into its own key `{<suite AppId>}_is1`
in HKLM64, only if that key exists; its uninstaller removes the value with the key. The key keeps the
`Publisher` of EE, `Empire Earth Community`, which Windows "Apps" shows for the suite and by which
the EE and NeoEE setups do not report it as a foreign installation (setup decision record 0007); the
value is what tells it apart from a key of EE. A suite built before revision 5 has no such value
([1.4](#14-discovery-by-the-launcher), source 3).

Other readers of this key: the setup itself (previous installation, certificate) and Empire Earth
Diagnostic (its shortcut passes `{<AppId>}_is1`). The launcher MUST NOT write it.

### 1.4 Discovery by the launcher

The launcher builds a list of installations. Each has a product, an install root, an EE folder, an AoC
folder (optional), an install mode, an AppId (optional), versions, its source and a kind:
`community` (setup since v2, with contract version), `community-legacy` (community setup up to 1.7.2) or
`foreign` (anything else, e.g. retail CD, GOG, an old patch chain).

Sources, in the order of default preference:

1. **User choice**: the folder chosen in the launcher settings. It may be the install root, the EE
   folder or the AoC folder. It always wins and is kept even if it does not exist (any more), so that the
   user sees it.
2. **Registry records** ([1.1](#11-registry-record)): product NeoEE before EE; per product HKCU, then
   HKLM64, then HKLM32.
3. **Uninstall keys** of community setups (every version, including 1.7.2): the subkeys of
   `Software\Microsoft\Windows\CurrentVersion\Uninstall` in HKCU, HKLM64 and HKLM32 whose name has the
   form `{<GUID>}_is1` and whose `Publisher` is exactly one of the two publishers of
   [Products](#products) (which also gives the product); NeoEE before EE. Root: `Inno Setup: App Path`,
   else `InstallLocation`. AppId: the GUID of the key name. Since revision 5 the uninstall key of the
   suite is no candidate, although its `Publisher` is that of EE ([Suite and launcher](#suite-and-launcher)):
   a key with the value `Empire Earth Community: Suite` (any type and data,
   [1.3](#13-uninstall-key-informative)) is skipped, and so is, for a suite built before revision 5, a key
   in HKLM whose root is the `InstallPath` of the suite record ([1.6](#16-suite-record-optional)) and
   whose AppId is neither its `EEAppId` nor its `NeoEEAppId`. A skipped key is logged; without the value
   and without the record a key counts as before.
4. **"Installed From" values** ([3.3](#33-computed-values)) of the Empire Earth settings key, key before
   hive like the product order of sources 2 and 3: `Software\Neo\Empire Earth` (NeoEE) in HKCU, then
   HKLM32, then HKLM64, then `Software\SSSI\Empire Earth` (EE) in the same order. They name the EE
   folder, whose name need not be `Empire Earth` (e.g. `C:\Games\EE`); the root is its parent. Retail,
   GOG and older installations also use the SSSI key.
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
  sources 1, 4 and 5). `ContractVersion` 1 or higher: `community`; without a readable `install.ini`,
  a registry record ([1.1](#11-registry-record)) of the root with `ContractVersion` 1 or higher also
  means `community`. Otherwise a match of source 3: `community-legacy`. Otherwise `foreign`; its product
  is NeoEE if the EE folder contains `neoee.dll`, else EE. The EE folder of `community` and
  `community-legacy` installations is `<root>\Empire Earth`; that of a `foreign` installation is the real
  folder its sources name (in the order of the Merge rule: the "Installed From" values of source 4, the
  launcher folder, then the user choice), else `<root>\Empire Earth`.
- **Two products in one root** (EE and NeoEE installed into the same folder, two `install.ini` files):
  the launcher uses the one modified last, warns that the two products share one folder and reports the
  integrity state as unreliable (**O11**).
- **AoC folder**: `<root>\Empire Earth - The Art of Conquest` if `EE-AOC.exe` exists there or
  `Components` contains `gameaoc` (then a missing program means damaged); for `foreign` installations
  otherwise the folder named by the "Installed From" values of the AoC settings key of the same product
  in the same hive and view as the EE values that found the installation (a folder of another hive can
  belong to another installation), or the AoC folder the user chose; either only if `EE-AOC.exe` is
  there.
- **Default selection**: without a user choice the launcher uses the first installation in the order of
  the sources. It SHOULD show every installation it found and let the user choose; the choice is saved
  as source 1. Since revision 6 the launcher MAY instead save one choice per product and the product the
  user chose last (launcher 1.1.0: the four games of its Play page, Empire Earth and The Art of Conquest
  of each product): the folder chosen for EE and the folder chosen for NeoEE are then both source 1, and
  the launcher uses the installation of the chosen product that the folder chosen for that product
  selects, else the first installation of that product in the order of the sources. Without a chosen
  product, or without an installation of it, the rule above applies. A folder chosen for a product that
  does not exist (any more) is listed as an installation of that product and stays its choice; a chosen
  folder whose installation is now of the other product is no choice for either product (it stays
  listed). Since revision 4 the launcher MAY accept the command-line argument `--product=EE` or
  `--product=NeoEE` (the shortcuts of suite 1.0.0, [1.7](#17-how-the-suite-runs-a-product-setup-informative)):
  it then selects, for this session only, the installation that the rules above give for that product
  (the folder chosen for that product first). The argument is not saved, does not change the saved
  choices and changes neither the sources nor their order; without an installation of that product, or
  with another value, it is ignored (logged) and the rules above apply. A second launcher started with
  the argument MAY hand it to the running launcher; since revision 6 a second launcher started without an
  argument MAY ask the running launcher to come to the front instead, which keeps its selection.
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

### 1.6 Suite record (optional)

Written since revision 4 by the suite ([Suite and launcher](#suite-and-launcher)), never by the EE or the
NeoEE setup, and never for the install modes `user` and `portable`. Key `Software\Empire Earth
Community\Suite` in HKLM, 64-bit view on 64-bit Windows (the suite runs in 64-bit install mode like the
products).

| Value | Type | Example | Meaning |
|---|---|---|---|
| `ContractVersion` | REG_DWORD | `1` | version of this contract the suite implements |
| `SuiteVersion` | REG_SZ | `1.0.0` | `AppVersion` of the suite |
| `InstallPath` | REG_SZ | `C:\Program Files\Empire Earth Community` | suite root (`{app}`), full path, no trailing backslash |
| `Products` | REG_SZ | `EE,NeoEE` | the product ids whose setup succeeded in this or an earlier run of the suite, comma separated, EE before NeoEE |
| `SourceDir` | REG_SZ | `C:\Users\Anna\Downloads\Empire Earth Community` | folder the suite was started from in its last run (`{src}`): the extracted package with `Empire Earth Community Setup.exe` and its `.bin` files |
| `EEAppId` | REG_SZ | `00000000-0000-0000-0000-0000000000EE` | AppId of the EE setup the suite embeds, without braces |
| `NeoEEAppId` | REG_SZ | `00000000-0000-0000-0000-000000000AEE` | AppId of the NeoEE setup the suite embeds, without braces |
| `Written` | REG_SZ | `2026-10-05 18:04:31` | local time of the last run, `yyyy-mm-dd hh:nn:ss`, informative only |

- Every run of the suite (first installation, repair, update) writes the record after the product
  setups. `Products` lists a product only if its setup succeeded
  ([1.7](#17-how-the-suite-runs-a-product-setup-informative)); a product that an earlier run listed stays
  listed. The suite's uninstaller removes the key (`uninsdeletekey`; the parent key `Empire Earth
  Community` with `uninsdeletekeyifempty`).
- The record is no discovery source. The products keep their own registry records, `install.ini` files
  and uninstall keys ([1.1](#11-registry-record) to [1.3](#13-uninstall-key-informative)), and the
  launcher finds them as before ([1.4](#14-discovery-by-the-launcher), sources and order unchanged;
  since revision 5 `InstallPath`, `EEAppId` and `NeoEEAppId` only help source 3 to recognise the
  uninstall key of a suite built before revision 5). A product in `Products` may have been removed since
  through its own entry in Windows "Apps", and the folder `SourceDir` may be gone.
- The launcher MAY read the record, read-only and with an explicit view
  ([Registry views](#registry-views)), for the repair advice ([4.4](#44-what-the-launcher-tells-the-user))
  and, since revision 5, for source 3 of [1.4](#14-discovery-by-the-launcher). It MUST work without it,
  also if a value is missing or invalid, and MUST NOT write or delete it.

### 1.7 How the suite runs a product setup (informative)

What the suite of revision 4 (setup decision record 0013) does, so that the launcher and the product
setups know what to expect; the EE and NeoEE setups do not change for it. The suite embeds both product
setups byte for byte (their AppIds, SHA-256 and size fixed at build time) and installs, in one elevated
run, the products the user selects and the launcher:

1. **Order**: the product setups run in the installation step of the suite (`CurStepChanged(ssInstall)`),
   EE before NeoEE, before Inno Setup processes the suite's own files, shortcuts and registry values, so
   that these can depend on the result of each product. If this timing is disproved on Windows (setup
   decision record 0013, Evidence), the suite creates its shortcuts and the record in code at
   `ssPostInstall` instead, with the same result.
2. **Per product**: check the game mutexes; extract the product setup to `{tmp}`; compare its SHA-256
   and size with the values fixed at build time (a mismatch stops the suite before any product setup
   runs); run it and wait until it ends; delete it. Since revision 6 the suite starts the product setup with a
   process handle and keeps its window alive while it waits (informative): until the product setup has logged
   the line `Install step: the game folder is changed from here on` (point 5) the user can cancel; the suite
   then stops the product setup and everything it started (the setup program is only the loader of the real
   setup) and starts no further product setup. The suite takes that decision on a frozen product setup: it
   suspends every process of the product setup, reads its log to the end and stops it only if the log shows
   neither that line nor the start of it and Windows confirmed that the stop happened; a product setup that
   logged the line meanwhile is resumed and runs on (the cancel is too late), one that cannot be frozen, whose
   log cannot be read or that was not stopped is resumed and runs on as well (the cancel is not carried out and
   the user can repeat it), and one that was stopped and shows the line in its log afterwards counts as failed and
   possibly only partly installed (the next run of the suite repairs it).
   A product that finished before stays installed: the suite then
   finishes its own part for it (the launcher, the shortcuts and the record of point 8 and
   [1.6](#16-suite-record-optional), written for the products that succeeded) and the last page says that the
   user cancelled the other one; only if no product succeeded in the run, nothing of the run is installed, and
   the suite ends with exit code 3 without writing anything of its own. A cancelled repair or update leaves the
   product exactly as it was (the next run adopts or repairs it, [4.1](#41-principle)); what the stopped product
   setup had downloaded stays in its own `%TEMP%\is-*.tmp` folder. From that line on the suite does not offer to
   cancel, because a stopped product setup would leave a game without install state or half installed: the
   product setup logs that line as the first statement of its installation step, before it deletes
   `install.ini` and `files.sha256`, moves the downloads and replaces the shipped random maps, so until then a
   repair or an update has changed nothing in the game folder (it only downloaded into its own `%TEMP%`). A
   product setup whose log does not grow for 10 minutes is reported to the user once (a silent run keeps
   waiting; "stop" is checked against the log again, and a product setup that has ended or has meanwhile logged
   the line above is not stopped for the first question). One that runs for more than 90 minutes (the downloads
   count) before it has logged the line above is stopped and counts as failed like any other failure, so the
   suite goes on with the next product; after that line the 90 minutes are only logged, because a killed
   installation would leave a half installed game. The advanced mode (point 3) has no limits and no cancel of
   the suite: the user cancels in the product setup.
3. **Parameters**, default: `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /ALLUSERS /LANG=<suite language>
   /NOICONS /MERGETASKS="!desktopicon" /LOG="<suite root>\Logs\<Product>-<yyyyMMdd-HHmm>.log"`, plus
   `/TYPE=full` for the first installation of a product. Since revision 6 the suite passes `/VERYSILENT`
   (up to revision 5 `/SILENT`): a product setup then shows no window and no taskbar button of its own,
   the window of the suite is the only one, while `/SILENT` showed a progress window for each product.
   The product setups behave the same in both modes (they ask nothing, `/SUPPRESSMSGBOXES` and
   `/NORESTART` still apply), but a product setup without a window cannot be cancelled by the user
   directly. A repair or an update passes neither `/TYPE`
   nor `/DIR`, so the product setup keeps its folder (`UsePreviousAppDir`) and the components and tasks
   of its previous run, `neoee_cdkeys` included ([4.1](#41-principle)). In the suite's advanced mode only
   `/ALLUSERS`, `/LANG`, `/NOICONS`, `/MERGETASKS="!desktopicon"` and `/LOG` are passed (`/ALLUSERS` only
   hides the product setup's dialog "for all users / only for me": the suite is admin-only), and the
   product setup shows its full wizard (no silent switch). A silent run of the suite (`/VERYSILENT`) needs the list of products and, with NeoEE,
   an explicit decision about `neoee_cdkeys` in the arguments for the NeoEE setup; without them it ends
   with an error before any product setup runs.
4. **Legal texts**: a product setup that runs silently skips its legal question (its `ConfirmLegalCopy`
   returns early), so the suite itself shows the question of the message `LegalQuestion`, the EULA and
   the NeoEE rules before any product setup runs.
5. **Result**: a product succeeded if its setup ended with exit code 0 and its uninstall key
   `{<AppId>}_is1` ([1.3](#13-uninstall-key-informative)) exists in HKLM64 with an `UninstallString`. For
   NeoEE the suite reads, read-only, the line `CD Keys generation result: <n>` of the product's log and
   shows the result; without that line it shows the result as unknown, with the advice that running
   the setup again repairs the registration. This log line is therefore an interface: a change of its
   text in the NeoEE setup changes this section in the same commit, in both copies. The suite never
   calls `authtools.dll` and never changes `Software\Sierra\CDKeys`
   ([3.8](#38-protected-keys-and-files)); the CD-key registration stays the NeoEE setup's own.

   **Progress lines** (since revision 6): while a product setup runs, the suite also reads its log, read-only
   and without disturbing it (it opens the file for reading and sharing, because the product setup holds it
   open for writing, and neither changes nor deletes it before the product setup has ended), and shows what
   the product setup is doing. The lines below are an interface exactly like the CD key line: a change of
   their text in a product script changes this table in the same commit, in both copies, and the suite's
   own check (`ci/check_suite.py`) fails if a script no longer writes one. The suite uses them for display
   only (phase, files downloaded, file being downloaded, an estimate of the installation, the CD key
   number); success stays decided as described above, and a product setup that logs none of them is run and
   judged as before, without progress detail. A line of the log starts with its time stamp
   `yyyy-mm-dd hh:nn:ss.zzz` and three blanks (26 characters); a continuation line starts with 26 blanks and
   is ignored; the first line of the file starts with the UTF-8 byte order mark.

   | Line (after the time stamp) | Written by | Meaning for the suite |
   |---|---|---|
   | `Online files server <URL>: ...` | `downloads.iss` | the setup asks its online files servers |
   | `English language selected, no need to download online files.` | `setup_is6.iss` | no downloads in this run |
   | `Downloading <N> online files, one at a time` | `downloads.iss` | the downloads start, N files |
   | `Downloading pinned online file ... from <URL>: <target>` | `downloads.iss` | the file being downloaded (the last part of the URL), WinHTTP transport of the pinned files from a server with an invalid certificate |
   | `Downloading temporary file from <URL>: <target>` | Inno Setup | the same for a file of the validated TLS transport (Inno Setup's own wording, written when its download page starts a file) |
   | `<X> of <Y> bytes done.` (two blanks before X) | `downloads.iss` | bytes of the file being downloaded, written at every 10 percent by both transports (the callback of the download page for the validated one, the read loop for WinHTTP) |
   | `Online file downloaded, ...`, `Online file not downloaded, it failed on both servers: ...`, `Online file not downloaded, not retried: ...`, `Online file not downloaded, unexpected error: ...`, `Online file skipped, downloads stopped by the user: ...` | `downloads.iss` | one file is finished |
   | `Online files: <n> downloaded with validated TLS ...`, `All <n> online files accepted`, `<m> of <k> selected online files are missing ...` | `downloads.iss` | the downloads are over and checked |
   | `Install step: the game folder is changed from here on` | `setup_is6.iss` | the point of no return: the first statement of `CurStepChanged(ssInstall)` of the product script, before it deletes `install.ini` and `files.sha256`, checks and moves the downloads and replaces the shipped random maps; from here on the setup cannot be stopped without leaving a game without install state or half installed (before, it only downloaded into its own `%TEMP%` and changed nothing in the game folder) |
   | `Starting the installation process.` | Inno Setup | Inno Setup starts to write the game files, after the line above; the suite takes it as the point of no return only for a product setup that logs no line above |
   | `Dest filename: ...` | Inno Setup | one file entry of the installation, counted against an estimate per product |
   | `Installation process succeeded.` | Inno Setup | the files are installed; the rest are the entries of `[Run]`, the registry and the CD keys |
   | `Register NeoEE CD Keys ...` and `CD Keys generation result: <n>` | `setup_is6.iss` (NeoEE) | the CD key step; the number is read as text, as above |
   | `Checking <n> recorded destinations ...` and `Manifest: <n> files, ...` | `installstate.iss`, `utils.iss` | the setup records the installed files for the launcher |
   | `Log closed.` | Inno Setup | the log ends (this says nothing about success) |
6. **Products installed for one user only**: if a product is installed only in the mode `user` (its
   uninstall key in HKCU, none in HKLM64), the suite does not run its setup, reports that it is
   installed for one user only and has to be removed through Windows "Apps" first, and does not list
   it in `Products` ([1.6](#16-suite-record-optional)).
7. **Old shortcuts**: in a suite run the product setups create no shortcuts (`/NOICONS`,
   `!desktopicon`). After a product setup succeeded, still in the installation step and therefore
   before the suite creates its own shortcuts, the suite deletes the shortcuts that earlier standalone
   runs of that product created, if they exist, exactly these paths (`<AppName>` as in
   [Products](#products)): `{autodesktop}\<AppName>.lnk`, `{autodesktop}\<AppName> - AoC.lnk`,
   `{autoprograms}\Empire Earth\<AppName>.lnk`, `{autoprograms}\Empire Earth\<AppName> - AoC.lnk` and
   `{autoprograms}\Empire Earth\<AppName> Diagnostic.lnk`, then the folder `{autoprograms}\Empire Earth`
   if it is empty.
8. **Suite shortcut** (table below): since revision 6 the suite creates one shortcut, `Empire Earth
   Community`, on the desktop and in its start menu folder, after that cleanup and by every run of the
   suite, so a repair restores it. It starts the launcher without an argument, so the launcher opens with
   the product and the game the user chose last ([1.4](#14-discovery-by-the-launcher), default selection).
   Without .NET Framework 4.8 the launcher is not installed; the shortcut of the same name then starts the
   game program of NeoEE if NeoEE is installed, else that of EE (the product the default selection of 1.4
   prefers), and it is not created if neither is installed. Every run of the suite, before it creates its
   shortcut, and its uninstaller delete the shortcuts of suite 1.0.0 (revisions 4 and 5) that exist,
   exactly these paths: `{autodesktop}\Neo Empire Earth.lnk` and, in `{autoprograms}\Empire Earth
   Community`, `Empire Earth.lnk`, `Neo Empire Earth.lnk`, `Empire Earth Diagnostic.lnk`, `Neo Empire Earth
   Diagnostic.lnk` and `Empire Earth Launcher.lnk`; `{autodesktop}\Empire Earth.lnk` only if it starts the
   launcher, because the EE setup's own desktop shortcut has that name (point 7 deletes that one after a run
   of the EE setup). Up to revision 5 the suite created the game shortcuts `Empire Earth` and `Neo Empire
   Earth` with `--product=EE` and `--product=NeoEE`; the launcher still accepts the argument
   ([1.4](#14-discovery-by-the-launcher)).
9. **Launcher outside the product roots** (former **O10**): the suite installs the launcher into the
   suite root. Its files are therefore in no manifest ([2.3](#23-which-files) unchanged), the suite
   closes it before it runs through its `AppMutex` ([Suite and launcher](#suite-and-launcher)), and the
   product setups keep their `AppMutex`. The launcher's own folder (source 5 of
   [1.4](#14-discovery-by-the-launcher)) is neither an EE folder nor an install root, so it finds nothing
   and stays the source of the lowest preference.

| Shortcut | Product | Places | Target | Parameters | Without .NET Framework 4.8 |
|---|---|---|---|---|---|
| `Empire Earth Community` | `NeoEE`, else `EE` | `{autodesktop}`, `{autoprograms}\Empire Earth Community` | `{app}\Empire Earth Launcher.exe` | none | `<product root>\Empire Earth\Empire Earth.exe` |

`{app}` is the suite root, `<product root>` the install root of the first product of the column Product
that is installed. The suite's start menu folder also holds the Mod Creator (only with .NET Framework 4.8)
and the suite's uninstaller; they are no game shortcuts. Since revision 6 the suite creates no Diagnostic
shortcut; the program stays in `<product root>\Tools\Diagnostic` if the product setup installed it.

## 2. Integrity manifest

### 2.1 Location and lifetime

`<root>\_setupdata_<Product>\files.sha256`, written by setups since v2 in every install mode.

- **Deleted** at the start of the installation step (`ssInstall`), so that an aborted run leaves no
  manifest that looks valid. If the deletion fails (a program holds the file open without
  `FILE_SHARE_DELETE`, see [4.2](#42-running-setup)), the setup logs it.
- **Written** at the end of `ssPostInstall`, after every other step (random maps, NeoEE CD keys),
  together with `install.ini`: first as `files.sha256.tmp`, then renamed (the rename does not
  overwrite, so the old file must be gone). After it the Regular variants write
  `Empire Earth Community: ContractVersion` into the uninstall key
  ([1.3](#13-uninstall-key-informative)), **only if** this run deleted the old `install.ini` and
  manifest and wrote and renamed both new ones; otherwise the value is missing and the launcher reports
  Unknown ([2.5](#25-verification-by-the-launcher)).
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
  hash of every `code` file. Neither check runs while a setup runs ([4.2](#42-running-setup)).
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
- **Manifest not replaced** (install modes `admin` and `user`): if the last run of a setup since v2
  could not delete `install.ini` or `files.sha256` at `ssInstall` or could not rename the new ones (a
  program held them open without `FILE_SHARE_DELETE`, [4.2](#42-running-setup)), the files may still be
  those of an earlier run. That run writes no `Empire Earth Community: ContractVersion`
  ([2.1](#21-location-and-lifetime)), so the rule above gives Unknown as well. Portable installations:
  not detectable, the setup only logs it.
- **Damaged** or **Incomplete**: a localized message (English, German, French) that names the files, says
  that antivirus programs often delete or quarantine game files (t=11045 p=48037, t=41147 p=80317),
  suggests an exception for the install root, and offers the repair ([4](#4-repair-hand-off)).
- **Modified**: no message and no repair offer; the state may be shown, the files are listed only in
  the diagnostics.
- **Unknown**: kind `community` whose uninstall key lacks the value (see above): "an older setup ran
  after the current one, or the last setup could not replace its records; run the current setup";
  other kind `community` (the last setup run did not
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
| S | overwrite | at the launcher start: create if both values are missing ([3.6](#36-launcher-procedures)) | overwrite if different | overwrite |
| D | overwrite (`deletevalue`) | create if missing; offer to overwrite values that differ, until the user answers | | overwrite |
| P | create if missing (`createvalueifdoesntexist`) | create if missing | | overwrite |
| GPU preference ([3.4](#34-gpu-preference)) | overwrite | create if missing | | overwrite |

- **Defaults applied once**: D, P and the GPU preference at the first run, only where values are missing;
  existing D values only with the user's consent.
- **Reset only**: overwriting existing D and P values (and the GPU preference) happens only on an
  explicit reset by the user, with one exception: the user's explicit choice of the game window size.
- **Explicit choice of the game window size** (revision 6, compatible clarification): the user's own
  choice in the launcher is the consent that the table above asks for. A launcher that offers such a
  choice (the graphics page of launcher 1.1.0) MAY overwrite `Game Window Width` and `Game Window Height`,
  and only these two values, of every game of the installation for the account that runs it, after the
  user picked a size and clicked the button that applies it: with the `.reg` backup of
  [3.6](#36-launcher-procedures) first, only while no setup and no game runs, and within the limits of
  [3.3](#33-computed-values) (the launcher MUST NOT write a size outside them; revision 6 raised the
  height limit from 1080 to 1200, a launcher that offers more than 1920 x 1200 needs a new revision).
  The marker ([3.5](#35-defaults-marker)) is not touched. The setup overwrites both values on its next
  run (class D, `deletevalue`), so the choice does not survive a repair or an update; the launcher SHOULD
  say so.
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
  **`Game Window Height`**: its height, limited to 768 to 1200; on a screen wider than 1920 pixels the height is in
  addition at most the larger of 1080 and the height of the screen scaled to the width 1920 (`height x 1920 / width`,
  rounded down), so that the window keeps the shape of a screen larger than 1920 x 1200 (revision 6; up to revision 5 the
  height limit was 1080). Otherwise each dimension is limited on its own (setup: `GetSystemMetrics(SM_CXSCREEN/SM_CYSCREEN)`,
  `MinGameWindowWidth` ... `MaxGameWindowHeight` and `WideScreenGameWindowHeight`), with the limits of the tables below. The
  launcher MUST measure physical pixels (DPI-aware process or `EnumDisplaySettings`), **O4**. A screen lower than 768 pixels
  gets a warning in the setup and in the launcher (t=3863).

| Value | Screen size | Minimum | Maximum |
|---|---|---|---|
| `Game Window Width` | width of the primary screen (`SM_CXSCREEN`) | `1024` | `1920` |
| `Game Window Height` | height of the primary screen (`SM_CYSCREEN`) | `768` | `1200` |

| Value | Screen | Maximum |
|---|---|---|
| `Game Window Height` | wider than `1920` | the larger of `1080` and `height x 1920 / width` |

A screen size below the minimum gives the minimum, one above the maximum the maximum, e.g. 1366 x 768 stays 1366 x 768,
1920 x 1200 and 1600 x 1200 stay as they are, 2560 x 1600 gives 1920 x 1200, 2560 x 1440, 3840 x 2160 and 3440 x 1440 give
1920 x 1080 (as in revision 5) and 800 x 600 gives 1024 x 768.

The limits of the first table are also those of the size the user chooses in the launcher ([3.2](#32-values),
revision 6); the wide-screen limit applies only to the computed value (a user may choose 1920 x 1200 on a 2560 x 1440
screen).

### 3.4 GPU preference

Windows 10 and later, only if the task `compatibility_windows` was selected (`Tasks` of `install.ini` or
of the uninstall key): `HKCU\Software\Microsoft\DirectX\UserGpuPreferences`, one REG_SZ value per
program of the table below, whose game is installed (component), value name = full path of the
program, data `GpuPreference=2;` (high-performance graphics card). The uninstaller removes it for the
account that uninstalls (`uninsdeletevalue`).

| Value name | Component | Data | Windows versions | Task |
|---|---|---|---|---|
| `<root>\Empire Earth\Empire Earth.exe` | `game` | `GpuPreference=2;` | 10 and later | `compatibility_windows` |
| `<root>\Empire Earth - The Art of Conquest\EE-AOC.exe` | `gameaoc` | `GpuPreference=2;` | 10 and later | `compatibility_windows` |

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
- **Launcher**: writes the marker after the first run of [3.6](#36-launcher-procedures) (with the display
  question: once the user answered it, whatever the answer) and after a reset.
- **Marker present**: only class S is kept in sync; D and P values are not touched (a value the player
  deleted stays deleted). If the marker is lower than the launcher's contract version, the launcher
  creates the values added since that version if they are missing, and raises the marker. A higher
  contract version never overwrites existing values by itself.

### 3.6 Launcher procedures

- **Launcher start** (and every new search of the installations, e.g. after the user chose a folder):
  only for an installation that is unambiguous for its game settings key (the user chose it, or no
  other installation found uses that key; community EE, retail and GOG installations all use
  `Software\SSSI\Empire Earth`): class S of each game is created if both values are missing and is not
  changed there; then the first run of each game whose marker is missing. For any other installation
  the first run waits for the first start of that game from the launcher or a reset, so that the
  launcher never points the values of a shared key at another installation by itself.
- **First run**, per account, product and game (marker missing):
  1. class S (at the launcher start only created as above, before a game start synchronized);
  2. P and the GPU preference: create if missing;
  3. D: create if missing; if an existing D value differs from its default, ask without blocking the
     start, until the user answers: "Apply the recommended display settings?" Yes: `.reg` backup, then
     overwrite D;
  4. write the marker; with the question only once the user answered it.
- **Before every game start while no other game runs**: class S for the game that is started, then the
  first run if its marker is missing; old values that change are logged. While the other game runs the
  launcher changes no game settings (logged); the next start without it does.
- **Reset**, after the user confirmed it: a `.reg` backup of the game settings key with its
  subkeys (e.g. `reg.exe export`) into `%LOCALAPPDATA%\Empire Earth Launcher\Backups\`, file name with
  date and time, product and game; then overwrite S, D, P and the GPU preference; then the marker. If
  the backup fails, nothing is changed.
- **Game window size chosen by the user** (revision 6): the launcher asks the guard of
  [4.2](#42-running-setup) (no setup and no game running), writes a `.reg` backup of the game settings key
  with its subkeys of every game of the installation as for the reset, and only then overwrites `Game Window
  Width` and `Game Window Height` with the chosen size. If a backup fails, nothing is changed. Nothing else
  is written.
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
| `everyoneadminstart` | `RUNASADMIN` | all (opt-in) | HKLM / - / - |
| `compatibility` | `DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation` | 8 and later | HKLM / HKCU / HKCU |
| `compatibility_legacy` | `DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation` | 7 only (opt-in) | HKLM / HKCU / HKCU |
| `compatibility_windows` | `WIN7RTM` | 8 and later | HKLM / HKCU / HKCU |

- **Windows versions**: `all` = every Windows the setup runs on (Windows 7 SP1 and later); `8 and later`
  = Windows 8 (NT 6.2) and later, below it the setup neither shows nor selects the task (`MinVersion`);
  `7 only` = below Windows 8, i.e. Windows 7 SP1, from Windows 8 on the setup neither shows nor selects
  the task (`OnlyBelowVersion`). `(opt-in)`: the task is not selected by default (`Flags: unchecked`).
  **Root**: the root in the install modes `admin` / `user` / `portable`; `-` = the task does not exist
  in that mode.
- `everyoneadminstart` and `compatibility_legacy` are opt-in; `compatibility` and `compatibility_windows`
  are selected by default (their page is only shown with the custom settings). Under Wine the setup
  offers none of the four tasks.
- `compatibility` and `compatibility_legacy` add the same values and never apply together, because no
  Windows version has both tasks. `compatibility_legacy` never adds a Windows version layer (no
  `WINXPSP3`, setup ADR 0010).
- Outside `admin` mode `compatibility_windows` applies only together with `compatibility`.
- Examples: Windows 10, `admin`, default tasks:
  `~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WIN7RTM`; Windows 7, `admin`, with
  `everyoneadminstart`: `~ RUNASADMIN`; Windows 7, `admin`, with `everyoneadminstart` and
  `compatibility_legacy`: `~ RUNASADMIN DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation`;
  Windows 7, `user`, with `compatibility_legacy`: `~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation`;
  Windows 7, `user`, default tasks: no value.
- **Windows Vista and 7: no compatibility values by default** (**O7**); only the opt-in rows
  `everyoneadminstart` and `compatibility_legacy` write one. Earlier setups wrote values there (official
  1.7.2: `EE-AOC.exe` `~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3`,
  `Empire Earth.exe` none). On Windows Vista and 7 every run of the setup removes, in the root of its
  install mode and for both programs, a value that is exactly one of `~ WINXPSP3`,
  `~ RUNASADMIN WINXPSP3`, `~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation`,
  `~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3`,
  `~ RUNASADMIN DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation` and
  `~ RUNASADMIN DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3` (setup:
  `IsLegacyVistaCompatValue`), **except** for a program whose value this run writes with
  `compatibility_legacy` (the task and the component of the program selected; setup:
  `ShouldRemoveLegacyVistaCompatValue`): that value is the run's own and stays. Every other value stays,
  e.g. one the player set.

The launcher:

- MAY offer the values of the rows `compatibility`, `compatibility_legacy` and `compatibility_windows`
  as options, and SHOULD offer them only on the Windows versions of the table; it writes them into HKCU
  only, keeps every other entry of the value, and shows the HKLM value read-only;
- MUST NOT offer `RUNASADMIN`: running the game elevated is opt-in through the setup only, the online
  lobby should not run elevated;
- MAY remove the HKCU value if it is exactly `~ RUNASADMIN` (the default of setups up to 1.7.2), like
  the setup's `RemoveLegacyRunAsAdmin`;
- MUST NOT describe one of the six old values above as a leftover of an earlier setup if `Tasks` of the
  installation (`install.ini`, else the uninstall key) contains `compatibility_legacy`: then it is the
  value of the setup's opt-in task, which a run of the current setup keeps (the setup preselects the
  tasks of the previous run);
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
without `Global\`), from its first window on. Since revision 4 the suite
([Suite and launcher](#suite-and-launcher)) holds `EmpireEarthCommunity_Suite` for its whole run, also
between two product setups, and while its uninstaller runs; for the launcher it is a setup mutex like
the other two. While one of them exists the launcher:

- MUST NOT start a game and SHOULD show that a setup is running;
- MUST NOT read `install.ini` or `files.sha256` of any installation and MUST NOT run the quick or the
  full check ([2.5](#25-verification-by-the-launcher)); a check that is running when a setup mutex
  appears is cancelled without findings. The discovery ([1.4](#14-discovery-by-the-launcher)) reads
  `install.ini` only after the mutex is gone.

When it is gone, the launcher runs the discovery and the quick check again.

The launcher opens `install.ini`, `files.sha256` and every file it hashes with sharing that includes
at least `FILE_SHARE_READ | FILE_SHARE_DELETE` (.NET: `FileShare.Read | FileShare.Delete`), so that a
setup that starts while a file is open can still delete and rename it. A file that is open cannot be
created again under the same name until the launcher closes it, which is why the launcher does not read
these files at all while a setup runs. A setup that could not replace `install.ini` or the manifest
leaves the installation in the state Unknown ([2.5](#25-verification-by-the-launcher)); for portable
installations that is not detectable.

The other way round, the setup does not install while a game mutex
([Folders, programs and mutexes](#folders-programs-and-mutexes)) exists.

### 4.3 Where the user gets the setup

Since revision 6 the launcher and the setup send the user to the download page of the product on the
community website, chosen by this table alone:

| Installation | Page |
|---|---|
| product `EE`, kind `community` or `community-legacy` | `https://empireearth.eu/download/ee/` |
| product `NeoEE`, kind `community` or `community-legacy` | `https://empireearth.eu/download/neo/` |
| kind `foreign`, or the product is not known | `https://empireearth.eu/download/` |

1. No request is made to choose the page. The setup uses the row of its own product (`InstallType`).
2. The page opens in the default browser with the rights of the launcher (not elevated); the setup opens
   it as the original user, not with its administrator rights.
3. The website answers the two product pages with a redirect to the current setup of the product
   (informative; in October 2026 `https://r2.empireearth.eu/setup/game/EE_Setup.exe` and
   `https://r2.empireearth.eu/setup/game/neo/NeoEE_Setup.exe`), so the browser may download it right
   away. The launcher and the setup follow no redirect themselves.
4. Up to revision 5 the launcher and the setup asked `GET https://api.empireearth.eu/setup/?product=<AppId>`
   for a download URL, accepted it only if it passed the setup's `IsAllowedUpdateUrl` and otherwise opened
   `https://empireearth.eu/download`. In October 2026 that answer named `cdn.empireearth.eu`, which does not
   resolve. Setups up to 1.7.2, setups v2 before this revision and launcher 1.0.0 still ask it
   (informative: the server operators keep that answer pointing to a page that works, setup
   `docs/SERVER-OPERATIONS.md`).

### 4.4 What the launcher tells the user

Localized (English, German, French; other languages fall back to English):

- close the game, then run the downloaded setup; it detects the installation and offers to update or
  repair it;
- since revision 4, if the suite record ([1.6](#16-suite-record-optional)) lists the product of the
  installation in `Products` and the folder `SourceDir` exists: first to run `Empire Earth Community
  Setup.exe` again from that folder, which repairs or updates the products it installed. The launcher
  MAY open that folder in Explorer and MUST NOT start a program from it ([4.1](#41-principle)); the
  download of [4.3](#43-where-the-user-gets-the-setup) stays the second option, e.g. if the folder is
  gone;
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

Requests use HTTPS with certificate validation (TLS 1.2 or newer), never fall back to HTTP, have timeouts
and send nothing but the queries of this table (no telemetry). Since revision 6 these are the only
requests to the update API: the launcher and the setup no longer send the query without `&type=` (the
download URL of the latest setup, [4.3](#43-where-the-user-gets-the-setup) point 4). The launcher's network
check, which asks the update API to see whether the internet works, sends a request of this table too
(`&type=game`).

A request without an answer of HTTP 200 (no connection, timeout, certificate error, another status) is
no statement about the version: the launcher reports that it could not ask, never that the version is
current (the setup's `CheckUpdate` then asks no update question). An available update uses the
hand-off of [4.3](#43-where-the-user-gets-the-setup): the download page of the product.

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
| 1 (draft) | 2026-10-02 | revision 2 (second review of the setup v2 plan): tables of the window size limits (3.3) and of the GPU preference values (3.4), checked against the script like 3.2 and 3.7; opt-in row `compatibility_legacy` (Windows 7 only, the flags without a Windows version layer) and its exception from the cleanup of the old values, `(opt-in)` in the table, such a value is no leftover for the launcher (3.7, O4, O7, setup ADR 0010); while a setup runs the launcher reads neither `install.ini` nor `files.sha256` and runs no check, and opens them with `FILE_SHARE_READ` and `FILE_SHARE_DELETE` (4.2, 2.5); `Empire Earth Community: ContractVersion` only if the run replaced `install.ini` and the manifest, Unknown otherwise, not detectable for portable installations (1.3, 2.1, 2.5) | v2 (planned) | v2 (planned) |
| 1 (draft) | 2026-10-02 | revision 3 (compatible clarifications after the reviews of setup v2 and launcher v2, which already behave so): source 4 reads key before hive, the EE and AoC folders of `foreign` installations are the real folders (the AoC folder from the same hive and view), the user choice may be the AoC folder, a registry record without `install.ini` also means `community` (1.4); Modified gets no message and no repair offer, the state may be shown (2.5); at the launcher start class S is only created, and the first run only for an installation that is unambiguous for its game settings key; class S before every game start while no other game runs; the display question until the user answers (3.2, 3.5, 3.6); a request without an answer of HTTP 200 is no statement about the version (4.5); O11 also names the `<AppId>` setup data folder of setups up to 1.7.2 | v2 (planned) | v2 (planned) |
| 1 (draft) | 2026-10-05 | revision 4 (suite installer "Empire Earth Community", setup decision record 0013; optional additions only, no MUST or MUST NOT relaxed, 4.1 and 4.3 unchanged): names and mutexes of the suite and the launcher (0); `--product=EE` or `--product=NeoEE` selects for one session (1.4); suite record (1.6); how the suite runs a product setup, the log line `CD Keys generation result: <n>` as an interface, the guard for products installed for one user only, the removal of old product shortcuts before the suite shortcuts `Empire Earth` and `Neo Empire Earth`, the launcher outside the product roots (1.7, O10 answered); the suite mutex is a setup mutex (4.2); advice with `SourceDir` (4.4); checklist of the additions (7) | suite 1.0.0 (planned) | 1.0.0 (planned) |
| 1 (draft) | 2026-10-06 | revision 5 (laptop test TP-93: the launcher listed the suite's own uninstall key, whose `Publisher` is that of EE, as a damaged installation of EE; optional additions only, no MUST or MUST NOT relaxed): the marker `Empire Earth Community: Suite` in the suite's uninstall key (0, 1.3); source 3 skips a key with that value, and for a suite built before revision 5 a key in HKLM with the root `InstallPath` of the suite record and no AppId the record embeds (1.4, 1.6); checklist of the additions (7) | suite 1.0.0 (planned) | 1.0.0 (planned) |
| 1 (draft) | 2026-10-07 | revision 6 (suite 1.1.0, one window during the installation, one shortcut, and launcher 1.1.0, graphics page, one launcher for the four games; compatible, `ContractVersion` stays 1, optional additions and compatible clarifications only, no MUST or MUST NOT relaxed): the suite starts the product setups with `/VERYSILENT` instead of `/SILENT`, so that they show no progress window of their own (1.7 point 3); the lines of the product logs the suite reads for its progress display are an interface like the CD key line, among them Inno Setup's line of a file of the validated TLS transport and the new line `Install step: the game folder is changed from here on` that the product setups log as the first statement of their installation step (1.7 point 5); the suite waits for a product setup with a process handle: Cancel until that line (a product that finished before stays installed and keeps the suite's launcher, shortcut and record), a stall and a time limit that stops a product setup only before that line, every stop decided on a frozen product setup whose log was read to its end (1.7 point 2); one suite shortcut `Empire Earth Community` that starts the launcher without an argument (without .NET Framework 4.8 the game program of NeoEE, else EE), no game and no Diagnostic shortcuts, the shortcuts of suite 1.0.0 deleted (1.7 point 8); the launcher MAY save one chosen folder per product and the chosen product, and a second launcher without an argument MAY bring the running one to the front (1.4); the setup and the launcher open the download page of the product (`https://empireearth.eu/download/ee/`, `.../neo/`, `.../download/` for foreign installations) and no longer ask the update API for a download URL (4.3, 4.5, 1.1 `AppId`); the user's explicit choice of the game window size in the launcher is the consent of 3.2 to overwrite the class D values `Game Window Width` and `Game Window Height`, within the limits of 3.3, after the guard and the `.reg` backup of 3.6, without touching the marker (3.2, 3.3, 3.6); checklist of the additions (7); the height limit of the game window is 1200 instead of 1080, with a limit for screens wider than 1920 that keeps 1920 x 1080 on screens of the shape 16:9 and wider, also the limit of the user's choice (3.2, 3.3); the optional record value `ComponentDefaults`, with which the setup selects the intro movies once on the first update of a custom installation (1.1) | suite 1.1.0 (planned) | 1.1.0 (planned) |

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
  Windows 7 the setup writes it only with the opt-in task `compatibility_legacy`).
- **O5 Portable**: portable setups write no registry record; the launcher finds them through the user
  choice, its own folder or the HKCU "Installed From" values. Should they write an HKCU record anyway?
  Proposal: no.
- **O6 Mutable files**: does the game rewrite installed files other than `cfg ini conf config log` (e.g.
  in `Data\WONLobby Resources`)? Test on Windows: play, then run the full check and list the
  differences.
- **O7 Defaults under review** (decided, setup ADR 0005, amended by ADR 0010): no compatibility values
  on Windows Vista and 7 by default, only the opt-in `~ RUNASADMIN` and the opt-in flags of
  `compatibility_legacy` (without `WINXPSP3`), unchanged values on Windows 8 and later, see the table in
  [3.7](#37-compatibility-flags). Official 1.7.2 wrote no value for `Empire Earth.exe` on Windows 7,
  and the forum evidence is weak (t=4280 p=30477, p=30479, p=30480, p=30485; t=1827 p=12147). The
  DirectX wrapper preselection stays, so the `Rasterizer Name` rule of [3.3](#33-computed-values) does
  not change. A later change of these defaults changes [3](#3-per-user-default-game-settings) in the
  same commit, in both copies.
- **O8 CD keys**: which values `authtools.dll` writes below `Sierra\CDKeys` is unknown (closed source).
  Until it is known the launcher only checks that the key exists.
- **O9 Launcher mods**: the mod manager needs its own record of the files it changed, so that
  [2.5](#25-verification-by-the-launcher) can attribute them.
- **O10 Launcher in the setup** (answered in revision 4): the product setups do not install the
  launcher; the suite installs it outside every product root, so its files are in no manifest, the
  suite's `AppMutex` closes it, and its folder stays the source of the lowest preference, see
  [1.7](#17-how-the-suite-runs-a-product-setup-informative) point 9.
- **O11 One folder for EE and NeoEE** (decided): the setup allows it (separate setup data folders), but
  the integrity check of the product installed first becomes useless. The setup asks a Yes/No question
  when the user leaves the folder page and the folder already holds the other product
  (`_setupdata_<other product>` exists, or `<AppId of the other product>`, the setup data folder of
  setups up to 1.7.2, or the other product's uninstall key in HKLM (both views) or HKCU has this folder
  as `Inno Setup: App Path`, which also finds installations of 1.7.2): it names the consequences (the
  integrity check of the other product; uninstalling one removes files and firewall rules of the
  other) and recommends another folder. "Yes" (default) stays on the folder page, "No" continues.
  Silent installations only log it. The launcher rule of [1.4](#14-discovery-by-the-launcher) stays.
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
  ([1.3](#13-uninstall-key-informative)), Regular variants only, and only if the run replaced
  `install.ini` and the manifest ([2.1](#21-location-and-lifetime));
- recording of the processed files (`AfterInstall`), deleting `install.ini` and `files.sha256` at
  `ssInstall`, writing both as ASCII at the end of `ssPostInstall` (temporary file, then rename), with
  `[MissingAfterInstall]` and the localized antivirus hint;
- compatibility values as in the table of [3.7](#37-compatibility-flags), including the opt-in row
  `compatibility_legacy` and the removal of the old values on Windows Vista and 7 with its exception;
- unit tests (`ci/tests/unit_tests.iss`) for the manifest line, the path conversion, the INI values, the
  ASCII check, the old compatibility values and the exception of their removal;
- `GameSettings`, the window size limits, the GPU preference entries, `BuildCompatibilityFlags`, the
  compatibility entries, `CodeFileExtensions` and this document changed together.

Launcher v2, in the UI-free core library with unit tests (fake registry and file system, no network):

- discovery ([1.4](#14-discovery-by-the-launcher)) with all five sources, setups up to 1.7.2, foreign and
  damaged installations, merging;
- manifest reader and checks ([2](#2-integrity-manifest)): BOM, CRLF, invalid lines, paths outside the
  root, classes, states, the uninstall key rule of [2.5](#25-verification-by-the-launcher);
- defaults, marker, consistency checks and reset with backup ([3](#3-per-user-default-game-settings));
- repair hand-off with the download pages of [4.3](#43-where-the-user-gets-the-setup) and update check
  ([4.5](#45-update-check-optional));
- setup and game mutexes ([4.2](#42-running-setup)): no game start, no reading of `install.ini` and
  `files.sha256` and no integrity check while a setup mutex exists, a running check cancelled, the
  share modes; starting the games with shell execute.

### Additions of revision 4 (suite)

Suite 1.0.0 (setup repository, folder `suite/`, setup decision record 0013):

- names, `SetupMutex` and `AppMutex` of [Suite and launcher](#suite-and-launcher), install mode `admin`
  only, 64-bit install mode;
- suite record ([1.6](#16-suite-record-optional)) with the values of its table, `Products` only with
  products whose setup succeeded, removed by the uninstaller;
- the product setups at `ssInstall` with the parameters, the pinned SHA-256 and size, the legal texts,
  the result, the CD-key log line and the guard for products installed for one user only
  ([1.7](#17-how-the-suite-runs-a-product-setup-informative));
- the removal of the old product shortcuts before the suite shortcuts; the suite shortcuts of the table
  of 1.7, the game shortcuts to the launcher with `--product`, without .NET Framework 4.8 to the game
  program;
- `ci/check_contract.py` reads `suite/suite.iss`: `SetupMutex`, `AppMutex`, the value names and types of
  the suite record and the game shortcuts.

Launcher 1.0.0 (optional additions; the launcher works without the suite):

- `--product=EE` and `--product=NeoEE` for one session, handed to a running launcher
  ([1.4](#14-discovery-by-the-launcher), default selection);
- `EmpireEarthCommunity_Suite` as a setup mutex ([4.2](#42-running-setup));
- the suite record read-only and the advice with `SourceDir` ([1.6](#16-suite-record-optional),
  [4.4](#44-what-the-launcher-tells-the-user)).

### Additions of revision 5 (suite uninstall key)

Suite 1.0.0 (revision 5):

- the value `Empire Earth Community: Suite` (REG_DWORD `1`) in its own uninstall key, written at
  `ssPostInstall` of every run and only if the key exists ([1.3](#13-uninstall-key-informative));
- `ci/check_contract.py` reads the name from the row "Suite uninstall key marker" of
  [Suite and launcher](#suite-and-launcher), checks the write in `suite/suite_record.iss` and that no
  product script names the value.

Launcher 1.0.0 (revision 5):

- source 3 skips the uninstall key of the suite: a key with the value `Empire Earth Community: Suite`,
  and a key in HKLM with the root `InstallPath` of the suite record and an AppId that is neither
  `EEAppId` nor `NeoEEAppId` ([1.4](#14-discovery-by-the-launcher), [1.6](#16-suite-record-optional));
  every other key counts as before.

### Additions of revision 6 (suite 1.1.0, launcher 1.1.0)

Suite 1.1.0 (revision 6; no registry value of the suite or of the product setups changes, its shortcuts do):

- the product setups run with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` in the default mode instead of
  `/SILENT`, so no product setup shows a window or a taskbar button of its own
  ([1.7](#17-how-the-suite-runs-a-product-setup-informative) point 3); the advanced mode passes no silent
  switch as before;
- the log lines of the product setups that the suite reads while they run (phase, downloads, start of the
  installation, CD key number, manifest) are listed in
  [1.7](#17-how-the-suite-runs-a-product-setup-informative) point 5; the product scripts mark each of them
  with the comment "suite parses this line", and `ci/check_suite.py` checks that they are still written;
- the suite waits for a product setup with a process handle: Cancel of the suite works until the product setup
  has logged `Install step: the game folder is changed from here on` (the first statement of its installation
  step, `setup_is6.iss`) and stops the product setup with everything it started (exit code 3 of the suite if
  no product succeeded in the run; a product that finished before stays installed and the suite finishes its
  launcher, shortcuts and record for it), a stall of 10 minutes is reported once, 90 minutes stop the product
  setup as a failure, but only before that line ([1.7](#17-how-the-suite-runs-a-product-setup-informative) point 2);
- `ci/check_suite.py`, the unit tests of `SuiteProductArguments` and the suite scenarios of CI expect
  `/VERYSILENT` and no `/SILENT`;
- one shortcut `Empire Earth Community` on the desktop and in the start menu folder `Empire Earth
  Community`, to the launcher without an argument, without .NET Framework 4.8 to the game program of
  NeoEE, else EE; the Mod Creator and the uninstaller in the start menu folder; no game and no Diagnostic
  shortcut; the shortcuts of suite 1.0.0 deleted by every run and by the uninstaller,
  `{autodesktop}\Empire Earth.lnk` only if it starts the launcher
  ([1.7](#17-how-the-suite-runs-a-product-setup-informative) point 8);
- `ci/check_contract.py` reads the table of 1.7 with a shortcut without parameters and the products of
  its fallback in order, `ci/check_suite.py` checks that only the paths of suite 1.0.0 of 1.7 point 8 are
  deleted, and the suite scenarios of CI expect the one shortcut and none of suite 1.0.0.

Product setups (revision 6): the update question opens the download page of the setup's own product
([4.3](#43-where-the-user-gets-the-setup)), and the update API gets only the requests of
[4.5](#45-update-check-optional); they log the line `Install step: the game folder is changed from here on` as
the first statement of their installation step ([1.7](#17-how-the-suite-runs-a-product-setup-informative)
point 5); they compute `Game Window Height` with the limit 1200 and the wide-screen limit of [3.3](#33-computed-values);
they overwrite `Game Window Width` and `Game Window Height` at every run as before (class D,
`deletevalue`, [3.2](#32-values)), which ends a size the user chose in the launcher; they write the optional record
value `ComponentDefaults` ([1.1](#11-registry-record)) and select the intro movies once on the first update of a custom
installation made by an older setup.

Launcher 1.1.0 (revision 6; the launcher does not start or watch the product setups of the suite, so the
suite part asks nothing of it):

- the graphics page offers game window sizes within the limits of [3.3](#33-computed-values), from 1024 x 768 up to the
  physical size of the primary screen and at most 1920 x 1200 (1920 x 1200 only on a screen at least that tall),
  recommends the computed size of 3.3 with its wide-screen limit, and refuses a size outside them ([3.2](#32-values));
- after the user picked a size and clicked the button that applies it, it overwrites `Game Window Width`
  and `Game Window Height`, and only these two values, of every game of the installation in HKCU of the
  account that runs it; the marker ([3.5](#35-defaults-marker)) and the other values stay as they are
  ([3.2](#32-values));
- before it writes: the guard of [4.2](#42-running-setup) (no setup and no game running) and the `.reg`
  backup of [3.6](#36-launcher-procedures); if a backup fails, nothing is changed;
- the page says that the next run of a setup (repair, update) writes the recommended size again
  ([3.2](#32-values));
- the Play page lists the four games (Empire Earth and The Art of Conquest of each product); choosing one
  selects the installation of its product for every page, a game that is not installed is shown disabled,
  and the choice is saved with one chosen folder per product ([1.4](#14-discovery-by-the-launcher), default
  selection); `--product=` selects for one session as before;
- a second launcher started without an argument brings the running one to the front instead of showing a
  message ([1.4](#14-discovery-by-the-launcher));
- the repair advice and an available update open the download page of the product of the installation,
  `https://empireearth.eu/download/` for a foreign one, without a request
  ([4.3](#43-where-the-user-gets-the-setup)); the update API gets only the requests of
  [4.5](#45-update-check-optional), also from the network check.

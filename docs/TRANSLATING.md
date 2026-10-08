# Translating the launcher

The launcher speaks English, German and French ([ADR 0009](adr/0009-localization-with-resx-en-de-fr.md)).
Translations are welcome, but please only contribute languages you speak natively or fluently: texts are not
machine translated, a text without a translation stays English until a speaker of the language translates it.

Contents: [Languages](#languages) · [Where the texts are](#where-the-texts-are) · [Rules](#rules) ·
[Changing or adding a text](#changing-or-adding-a-text) · [Adding a language](#adding-a-language) ·
[Testing a translation](#testing-a-translation) · [Status](#status)

## Languages

| Language | File | State |
|---|---|---|
| English | `Empire Earth Launcher/Properties/Resources.resx` | neutral language, the source of every text |
| German (`de`) | `Empire Earth Launcher/Properties/Resources.de.resx` | complete; proof-reading in the laptop test (see [Status](#status)) |
| French (`fr`) | `Empire Earth Launcher/Properties/Resources.fr.resx` | complete; **review open** (see [Status](#status)) |

- The launcher shows the language of the Windows display language, or the one chosen on its *Launcher* page
  (Windows language, English, Deutsch, Français; saved as `UiCulture` in
  `%LOCALAPPDATA%\Empire Earth Launcher\settings.json`, used from the next start on).
- Every other language falls back to English. A regional Windows language uses its parent language: German
  (Austria) shows German, French (Canada) shows French.
- Portuguese (Brazil) and Chinese, which the setup offers as game languages, are not machine translated; they
  show English until a native speaker translates the launcher (see [Adding a language](#adding-a-language)).
- The mod creator (`Empire-Earth-Mod/Empire-Earth-Mod`) stays English and French in v2: its texts are in its own
  `Properties/Resources.resx` and `Resources.fr.resx`, and v2 adds none.
- Log messages (`log.txt`) are English on purpose (they are read in support threads, ADR 0013) and are not
  resources.

## Where the texts are

- All texts of the launcher's windows are string entries of `Properties/Resources.resx` (English, with a
  `<comment>` for translators: where the text is shown, what fills the placeholders, how long it may be) and the
  same keys in `Resources.de.resx` and `Resources.fr.resx`, in the same order.
- The designer files (`*.Designer.cs`, `MainForm.resx`) hold only placeholders for the Visual Studio designer.
  Each window and page sets its real texts in its `ApplyTexts()` method, and `Texts.cs` picks the text for a
  result (player state, lobby profiles, where the game folder comes from, the state of the game settings, the
  hints of the consistency checks, the result of a reset or a compatibility change). Do not translate designer texts; an
  architecture test fails if a designer text is not set again in `ApplyTexts()`.
- Images, icons and other files are entries of the neutral `Resources.resx` only; the language files inherit
  them and must not contain any.
- The names of the languages in the language list ("English", "Deutsch", "Français") are written in their own
  language and are not resources (`Texts.UiLanguageName`); only "Windows language" is translated.

## Rules

- **Placeholders**: `{0}`, `{1}`, ... are filled in by the launcher (the comment says with what: a number, a path,
  a file name). Every translation must contain exactly the placeholders of the English text; their order in the
  sentence may change. Do not add other curly braces.
- **Line breaks**: a text with line breaks (a message) has real line breaks inside `<value>`; keep the empty lines
  between paragraphs.
- **File dialog filters** (`ThemeFileFilter`): `description|pattern|description|pattern` - translate the
  descriptions, keep every `|` and the patterns (`*.xml`, `*.*`).
- **Not translated**: the names of the games (Empire Earth, The Art of Conquest), NeoEE, file names
  (`Empire Earth.exe`), and "Launcher" where German and French use the English word as well. The game settings
  texts also keep the registry names the player may look up: value names (`Game Bit Depth`, `Rasterizer Name`,
  "Installed From"), renderer names (`Direct3D Hardware TnL`), `.reg`, and the compatibility entries in parentheses
  (`HIGHDPIAWARE`, `~ RUNASADMIN`); translate the words around them.
- **Length**: buttons and labels have a fixed size. Where a comment gives a limit ("at most about 12 characters"),
  stay below it; otherwise stay close to the length of the English text. The screenshots of the laptop test show
  texts that do not fit.
- **Form of address**: German uses "Sie", like the setup; French uses "vous".
- **Encoding**: the `.resx` files are UTF-8 with BOM and CRLF line endings (`.gitattributes`). Edit them with the
  resource editor of Visual Studio or a text editor that keeps both; write `&`, `<` and `>` as `&amp;`, `&lt;`
  and `&gt;` in a text editor.

## Changing or adding a text

For developers:

1. Add the key to `Properties/Resources.resx` with the English text and a `<comment>`.
2. Add the German and the French text to `Resources.de.resx` and `Resources.fr.resx` (same key, same position).
   Without them the tests fail. Add the key to the French review list in [Status](#status) unless a French speaker
   wrote it.
3. Regenerate `Properties/Resources.Designer.cs`: Visual Studio does it when `Resources.resx` is saved
   (otherwise *Run Custom Tool* on the file). A test checks that every text has its property.
4. Use it from `ApplyTexts()` of the window, or from `Texts` for a text that depends on a result. Never assign a
   literal text in code (an architecture test looks for it). Format with
   `string.Format(CultureInfo.CurrentCulture, ...)`; names, paths and numbers are arguments, never pieces glued
   to a translated sentence.
5. Add a case to the German test plan (`docs/TEST-PLAN.de.md`) for the page that shows the text.

Changing the English text of an existing key means checking the German and French texts as well.

## Adding a language

1. Copy `Resources.fr.resx` to `Resources.<culture>.resx` (the culture name, e.g. `es` or `pt-BR`) and translate
   every value.
2. Add `<EmbeddedResource Include="Properties\Resources.<culture>.resx" />` to
   `Empire Earth Launcher/Empire Earth Launcher.csproj`. The build then creates
   `<culture>\Empire Earth Launcher.resources.dll` next to the program; the folder belongs into every package.
3. Add the culture to `UiLanguage.Choices` (`Empire-Earth-Launcher-Core/Settings/UiLanguage.cs`), its name in its
   own language to `Texts.UiLanguageName`, and the culture to `Cultures` in
   `Empire-Earth-Launcher.Tests/Architecture/ResourceParityTests.cs`.
4. Update the tables of this file and the README.

## Testing a translation

- Automatic (run the test program, see the README): `ResourceParityTests` compares the keys of every language with
  the English file, finds empty texts, different placeholders, images in a language file and a satellite assembly
  that is missing or older than its `.resx`; `ApplyTextsTests` finds designer texts that no `ApplyTexts()` sets;
  `PlaceholderControlsTests` keeps removed placeholder controls out.
- By hand on Windows: choose the language on the *Launcher* page, restart the launcher and look at every page:
  the navigation, *Play* (game choice, player list, profile), *Settings* (compatibility warning and its dialog),
  *Launcher* (labels, theme list and its file dialog, game folder dialogs and the question when the folder does
  not contain `Empire Earth.exe`, the list of installations with its column headers, types, states and tooltips,
  and the hints below it). The *Settings* page holds the game settings: scroll to its end, it has the defaults
  state, the display question, the buttons with their confirmations in place, the hints with their checkbox
  "Play page", the compatibility options and the result lines; the *Play* page shows the display question or a
  hint in an info bar. Long texts there wrap and push the following controls down; the buttons do not wrap, so
  keep their limits. The *Play* page shows the file versions and a state line below the game choice; its messages
  (a running game with the Task Manager hint, the question before starting the second game, start errors) are
  Windows message boxes, and the window "Repair the installation" (a missing program) wraps its text and grows
  with it. Below the versions the *Play* page shows the result of "Check version" and the integrity state
  ("Files: ...", at most about 40 characters, one line) with its button ("Details" or "Repair...", at most about 14
  characters). The *Tools* page (since L-WP7) holds the integrity check: its explanation, the list of files with what
  is wrong with each (`IntegrityFile*Format`), the progress of the full check, three buttons in one row (at most about
  26 characters each) and the version check; it scrolls like the *Settings* page. The repair window now also shows
  the files of the check (since 1.1.0 it shows the address of the download page at once and asks nobody; the texts
  "Asking the update server ..." and the note of the general download page are gone). Since L-WP8 the *Tools* page continues with
  the maintenance tools, one section each with a heading, an explanation that wraps, a state line and a result line:
  old registry entries (a list with check boxes, a read-only text box with one line of advice per key, the button
  "Delete selected..."), WON login, VirtualStore, saved games and scenarios (three buttons in one row, at most about
  35 characters each: export, import into Empire Earth, import into The Art of Conquest), player names and backups
  ("Open backup folder"). The confirmations of the cleanup (`CleanupConfirmFormat`) and of an import that replaces
  files (`ImportConfirmFormat`) are message boxes; `ExportFolderDescription` is the text of the Windows folder dialog,
  `ImportDialogTitle` and `ImportFileFilter` the title and the file type line of the open dialog (keep `*.ees;*.scn`
  and the `|` of the filter exactly). Keep file names, registry keys and error codes untranslated (`_wonkver.pub`,
  `_wonlogin.ks`, `Software\Sierra`, `WS_GetCert_InvalidPubKeyBlock`, `Program Files`, `.ees`, `.scn`); the advice for
  keys of all users (`CleanupAdviceExportFormat`) quotes the context menu of the Registry Editor ("Export" in English,
  „Exportieren“ in German, « Exporter » in French). Some texts quote what another program
  shows in the same language: the Task Manager of Windows (`StartHangingHintFormat`), the install mode options of
  Inno Setup (`RepairStepKeepFolderAllUsersFormat`, `RepairStepKeepFolderCurrentUserFormat`) and the CD-key task of
  the setup (`RepairStepCdKeys`); keep them equal to those programs. `IntegrityCheckInfo` quotes `FullCheckButton`;
  keep both equal. Since L-WP9 the *Tools* page ends with two more sections. "Network" has an explanation, the button
  "Check network" (at most about 26 characters), a result line (one of the verdicts `NetworkVerdict*`), the hints
  (`NetworkHint*`) and a read-only text box with one detail line per adapter, name lookup, server and file
  (`NetworkAdapterFormat`, `NetworkDns*`, `NetworkApi*`, `NetworkStatus*`, `NetworkNeoEeCfgFormat`,
  `NetworkWonLobbyFormat`, `NetworkUpnp*`, `NetworkPortsFormat`); the short texts `NetworkKind*`, `NetworkClass*`,
  `NetworkIPv6*`, `NetworkOn`, `NetworkOff`, `NetworkNone` and `NetworkInvalid` are inserted into those lines, so keep
  them short and without a full stop. "Diagnostics report" has an explanation, "Copy report" and "Save report..." in
  one row (at most about 26 characters each), a result line and a text box with the report. The report itself is
  English on purpose, like `log.txt` (it is read in the forum), and is not a resource. `ReportSaveDialogTitle` and
  `ReportFileFilter` are the title and the file type line of the save dialog (keep `|*.txt`); `ReportFileNameFormat`
  is the proposed file name, so it must not contain `\ / : * ? " < > |`. On the *Play* page the link
  `PlayerListCheckNetworkLink` (at most about 30 characters) appears below a player list that is "not available"; to
  see it, follow WP9-05 of the test plan. Keep untranslated: `NeoEE.cfg`, `WONLobby.cfg`, `upnp_info.txt`,
  `Empire Earth Launcher.exe.config`, `Active: false`, `CDKeyCheck`, `true`, TCP, UDP, IPv4, IPv6, HTTP, DNS, NAT,
  CGNAT, DS-Lite, Hamachi and the address ranges (`100.64.0.0/10`, `0.0.0.0`). The verdict `NetworkVerdictOutage`
  ("Probably a server outage, not your computer") must say clearly that the player's computer is not the cause.
  `log.txt` names the language in use (`UI language: de (launcher setting)`).
  The German test plan has these cases (sections L-WP3 to L-WP10, WP4-17, WP5-17, WP6-14, WP7-14, WP8-15, WP9-14 and WP10-05;
  the repair window with the suite step and its button "Open setup folder" is WP10-05, the player list heading "NeoEE only" WP10-07).

## Status

**In short (launcher 1.1.1):** English, German and French have the same 451 texts (`ResourceParityTests` checks that
the keys and placeholders match). English is the source. **No German text has been proof-read by a native speaker yet,
and the French texts, apart from two, were written without a native speaker.** Both reviews are open; help is welcome,
see [Help wanted](#help-wanted).

| Language | Texts | Review |
|---|---|---|
| English | 451 | source |
| German `de` | 451 | **not proof-read yet**: a native speaker is to read them in the laptop test ([TEST-PLAN.de.md](TEST-PLAN.de.md), cases WP3-*, WP4-17, WP5-17, WP6-14, WP7-14, WP8-15, WP9-14, WP10-05, WP11-01, WP12-07, WP13-08 and WP14-04); session 1 of the test of 1.1.0 recorded no result for them |
| French `fr` | 451 | **not reviewed**: only `NavigationPlay` ("Jouer") and `NavigationSettings` ("Paramètres") come from the original French authors; all other French texts were written during the review fixes, v2 and the later versions without a native speaker |

How the count grew: 378 texts in 1.0.0; 1.1.0 added 72 (the *Graphics* and *Mods* pages, the list of games of the *Play*
page, the VirtualStore copy of `dgVoodoo.conf`) and removed 3 (447); 1.1.1 added 4 (451). The paragraphs below say what
each version changed.

Launcher 1.1.1 sends an installation of the suite to the release page of the package "Empire Earth Community" instead of the
download page of the product setup, also when the unpacked folder of the package is gone. The repair window got three texts,
`RepairStepDownloadPackage` (the step that downloads the package again, unpacks it and runs "Empire Earth Community Setup",
with the warning not to use the setup from empireearth.eu), `RepairStepUpdateWithNewPackage` (the step of an available
update: the version comes from the update server of empireearth.eu, the package updates only with a new release) and
`RepairPackagePageLabel` (above the address of the release page), and `RepairDownloadPageSuiteLabel` changed in all three
languages (the second option below the suite step is now the package). The *Tools* page got the button
`PackageReleasePageButton` ("Open release page", next to "Check for updates", at most about 30 characters), and
`VersionCheckInfo` (what the version check covers and what not) and the two setup lines of its result
(`VersionSetupUpToDateFormat`, `VersionSetupUpdateFormat`, now with the product in `{1}`, the latest version in `{2}`)
changed in all three languages, and so did `IntegrityUnknownOlderSetup`, which now refers to the steps of the repair advice
instead of "the current setup". The French texts call the package « paquet » and its release page « page des versions »;
they belong to the French review (test plan WP7-09, WP7-10, WP7-11, WP10-06, WP14-04).

Launcher 1.1.0 (contract revision 6, one launcher for four games) removed three texts of the repair window (`FailureUrlRejected`,
`RepairLocating`, `RepairFallbackFormat`: the update API no longer chooses the download page) and added five for the *Play* page:
the four entries of the list of games (`PlayEntryEmpireEarth`, `PlayEntryEmpireEarthAoc`, `PlayEntryNeoEE`, `PlayEntryNeoEEAoc`:
the names of the games, not translated, joined by an en dash with "The Art of Conquest") and the hint below the list
(`PlayEntriesNotInstalledHint`, about 200 characters: it explains greyed out games and where another installation is chosen).
The radio buttons of the list wrap onto a second line when a name is longer than the page is wide. The text `NetworkInfo` (the
intro of the network check on the *Tools* page) changed in all three languages: besides the AppId, the update server gets the
question for the latest game version (`&type=game`).

After 1.0.0 the hint about a differing VirtualStore copy of `dgVoodoo.conf` added three texts (`VirtualStoreWrapperConfigFormat`,
`OpenVirtualStoreFolderButton`, `OpenVirtualStoreFolderFailedFormat`, A5; test plan WP8-19); they came with 1.1.0.

Launcher 1.0.0 had 378 texts (the suite additions of contract revision 4 added 5: `RepairStepRunSuiteFormat`,
`RepairDownloadPageSuiteLabel`, `RepairOpenSuiteFolderButton`, `RepairSuiteFolderNotOpenedFormat` of the repair advice that names the
folder of the suite setup, and `OnlinePlayersNeoOnly` of the player list; state before that, of the fixes of the laptop test: `ThemeBuiltIn` and `ThemeBuiltInNextStart` of the theme list; the review
fixes after L-WP9 added `ImportCheckManifestUnusable` and `CleanupKeepFolderUnknownFormat` of the
maintenance tools, and changed `SavesInfo` to the port table of the network check; L-WP9, the last work package of v2: 76 texts of the network check and the diagnostics report on the
*Tools* page and the link of the *Play* page were added; L-WP8 added 80 for the maintenance tools, L-WP7 59 for the
integrity check, the *Tools* page, the version check and the download of the update API, L-WP6 29 for Play, the repair
advice, a running setup and a second launcher, L-WP5 51 for the game settings, L-WP4 21 for the list of installations).

### Help wanted

- **French**: a native speaker who reads the 449 French texts other than the two navigation texts in
  `Resources.fr.resx`, ideally while looking at each page. The newest are the texts of 1.1.1 (the repair advice and the
  release page of the package, listed above, with « paquet » and « page des versions »), then those of 1.1.0 (`Graphics*`,
  `Mods*`, `PlayEntry*`, `OpenVirtualStoreFolder*` and `VirtualStoreWrapperConfigFormat`). Before them: the two texts of
  the theme list (`ThemeBuiltIn`, `ThemeBuiltInNextStart`, next to `ThemeCustom`), then the 76 texts of the network check and the diagnostics
  report (keys from `ToolsNetworkHeading` to `PlayerListCheckNetworkLink`) and the two texts of the review fixes after them
  (`ImportCheckManifestUnusable`, `CleanupKeepFolderUnknownFormat`); before them come the 80 texts of the maintenance tools (from `ToolsCleanupHeading` to `ToolsChecking`), the 59 texts of
  the integrity check and the update API (from `NavigationTools`, without the three texts removed in 1.1.0) and the 29 texts of Play
  and the repair advice (from `LauncherAlreadyRunning` to `RepairPageNotOpenedFormat`); the 51 texts of the game
  settings (from `GameSettingsDefaultsHeading` to `FindingFolderNotAnsiFormat`) and the explanations of the
  maintenance tools are the longest.
- **German**: a native speaker who reads the German texts on the pages of the launcher, best during the cases of the laptop
  test named in the table above, and notes what sounds wrong.
- **Other languages**: Portuguese (Brazil), Chinese, Spanish, Italian, Polish, Russian and Korean are game
  languages of the setup; the launcher shows English for them. See [Adding a language](#adding-a-language).

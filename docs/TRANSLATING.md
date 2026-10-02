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
  keep their limits. `log.txt` names the language in use (`UI language: de (launcher setting)`).
  The German test plan has these cases (sections L-WP3 to L-WP5, WP4-17 and WP5-17).

## Status

125 texts (state of L-WP5: 51 texts of the game settings were added; L-WP4 added 21 for the list of installations).

| Language | Translated | Review |
|---|---|---|
| English | 125 | source |
| German `de` | 125 | proof-reading by a native speaker in the laptop test ([TEST-PLAN.de.md](TEST-PLAN.de.md), cases WP3-*, WP4-17 and WP5-17); open until that test |
| French `fr` | 125 | **open**: only `NavigationPlay` ("Jouer") and `NavigationSettings` ("Paramètres") come from the original French authors; all other French texts were written during the review fixes and v2 without a native speaker |

### Help wanted

- **French**: a native speaker who reads the 123 French texts other than the two navigation texts in
  `Resources.fr.resx`, ideally while looking at each page. The 51 texts of the game settings (keys from
  `GameSettingsDefaultsHeading` to `FindingFolderNotAnsiFormat`, at the end of the file) are the newest and
  the longest.
- **Other languages**: Portuguese (Brazil), Chinese, Spanish, Italian, Polish, Russian and Korean are game
  languages of the setup; the launcher shows English for them. See [Adding a language](#adding-a-language).

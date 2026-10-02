# 0009 Localization with resx: English, German, French

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review, see the Amendment section)

## Context

The launcher has English texts in `Properties/Resources.resx` (30 string keys), French in
`Resources.fr.resx` (the same 30 keys) and French navigation texts in `MainForm.fr.resx`; about 50 designer
texts are English only (corrected, see the amendment). R17 requires every new user-visible text in English, German and French, other languages
falling back to English, no machine translation of Portuguese (Brazil) or Chinese. The setup uses the same
three languages for its own messages.

## Decision

- **resx with satellite assemblies**: neutral English in `Properties/Resources.resx` of the launcher
  (`NeutralResourcesLanguage("en")`), complete `Resources.de.resx` and `Resources.fr.resx`.
- **Texts from code only**: each form and page sets all its texts in one `ApplyTexts()` method from
  `Resources`; designer texts are placeholders. `MainForm.fr.resx` is merged into `Resources.fr.resx`.
  Formatting uses `string.Format(CultureInfo.CurrentCulture, ...)`; names, paths and values are arguments,
  never concatenated into translated sentences.
- **Core returns codes**: messages are chosen by the UI (`Texts` class) from enums and data of the core
  results (ADR 0003); log messages stay English.
- **Parity test** (ADR 0012): reads the launcher's `.resx` files from the source tree and fails on a key missing
  in `de` or `fr`, an empty value, or different `{n}` placeholders.
- **UI language**: Windows UI language (`CultureInfo.CurrentUICulture`), unless the setting `UiCulture` names
  `en`, `de` or `fr` (applied at start, before the first form).
- **Mod creator**: stays English and French in v2 (it gets no new texts).
- **Translator guide**: `docs/TRANSLATING.md` (languages, files, placeholder rules, how to test with the
  language setting).

## Evidence

- String keys of `Resources.resx` and `Resources.fr.resx` on `v2`: 30 each, same set (corrected, see the
  amendment); the designer texts are not in any resx and are invisible to translators today; one place per
  text and a parity test make gaps visible.
- R17 of the v2 requirements; the setup's message files and `TRANSLATING.md` follow the same rule (English,
  German, French, English fallback).
- Satellite assemblies are produced by both MSBuild and xbuild for `*.de.resx`/`*.fr.resx` (existing
  `fr/Empire Earth Launcher.resources.dll` in `bin/Debug`).

## Consequences

- One source of truth per text; a new text without translation fails the build's tests.
- Every page needs an `ApplyTexts()`; the designer view keeps showing placeholder texts.
- German becomes a full UI language of the launcher.

## Alternatives considered

- **`Localizable=true` designer forms** (per-form resx per culture): texts and layout mixed, parity hard to
  check, merge conflicts in designer resx. Rejected.
- **Own string tables (JSON/INI)**: no satellite assembly fallback, own loader. Rejected.

## Amendment 2026-10-02 (design review)

**Correction of the evidence.** The counts "79 vs 60" counted every line with `name="`, including the resx
schema, the example comment (`Name1`, `Color1`, `Bitmap1`, `Icon1`) and 18 image entries
(`type="System.Resources.ResXFileRef..."`). Counted without comments and without entries that have a `type`
or `mimetype`, `Properties/Resources.resx` and `Resources.fr.resx` have **30 string keys each, with the same
key set** (checked with a small Python script on `v2` at `cab4d44`). The real gap is elsewhere: about 50
texts are set only in the designer files (`grep 'Text = "' *.Designer.cs`: 52 lines) and exist in English
only, plus three French navigation texts in `MainForm.fr.resx`.

Consequences for the decision (unchanged otherwise):

- The parity test compares **string entries only** (comments removed, entries with `type` or `mimetype`
  skipped). A second test checks that image and other file entries exist only in the neutral
  `Resources.resx` (satellites inherit them).
- Placeholder controls are removed **before** their texts would be moved and translated (ADR 0014
  amendment), so no text of a removed control is translated.
- Quality of translations is not machine-checkable: German is proof-read by the user during the laptop test
  (a test-plan case per page); French translations of new texts are marked "review open" in
  `docs/TRANSLATING.md` until a French speaker has read them.

# 0009 Localization with resx: English, German, French

Status: **Accepted** (2026-10-02)

## Context

The launcher has English texts in `Properties/Resources.resx` (79 entries), French in
`Resources.fr.resx` (60 entries) and French navigation texts in `MainForm.fr.resx`; many designer texts are
English only. R17 requires every new user-visible text in English, German and French, other languages
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

- `grep -c 'name="' Resources.resx` 79 vs `Resources.fr.resx` 60 on `v2`: missing translations are invisible
  today; a parity test makes them visible.
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

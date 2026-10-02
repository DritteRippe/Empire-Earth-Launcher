# Architecture decision records

Each file records one decision of launcher v2: the context, the decision, the evidence it rests on, its
consequences and the alternatives that were rejected. The overview is [ARCHITECTURE.md](../ARCHITECTURE.md).

Rules:

- File name `NNNN-short-title.md`, numbered in the order the decisions were taken; numbers are never reused.
- Status: **Proposed**, **Accepted**, **Superseded by NNNN** or **Rejected**. An accepted record is not
  rewritten: a reversed decision gets a new record that supersedes the old one (the old one only gets its
  status changed). A refinement that keeps the decision is added at the end as a dated **Amendment**
  section, and the status line names it; a factual error in the evidence is corrected in place and the
  correction is noted in that section.
- Evidence is concrete: a file and line, a command and its output, a vendor document, a forum post
  (`t=` topic, `p=` post of the save-ee.com support forum) or a section of [CONTRACT.md](../CONTRACT.md).

| No. | Decision | Status |
|---|---|---|
| [0001](0001-target-dotnet-framework-4-8.md) | Target .NET Framework 4.8 | Accepted, amended 2026-10-02 |
| [0002](0002-keep-classic-project-files-and-packages-config.md) | Keep classic project files and packages.config | Accepted |
| [0003](0003-ui-free-core-library.md) | One UI-free core library, thin WinForms UI | Accepted |
| [0004](0004-async-await-threading-model.md) | async/await threading model | Accepted, amended 2026-10-02 (three times) |
| [0005](0005-own-settings-file-instead-of-user-config.md) | Own settings file instead of user.config | Accepted, amended 2026-10-02 (twice) |
| [0006](0006-platform-abstractions-and-windows-path-logic.md) | Platform abstractions and Windows path logic | Accepted, amended 2026-10-02 |
| [0007](0007-registry-write-scope-and-reg-backups.md) | Registry write scope, protected keys and .reg backups | Accepted, amended 2026-10-02 (four times) |
| [0008](0008-https-policy-and-update-api.md) | HTTPS policy and use of the update API | Accepted, amended 2026-10-02 (twice) |
| [0009](0009-localization-with-resx-en-de-fr.md) | Localization with resx: English, German, French | Accepted, amended 2026-10-02 |
| [0010](0010-game-start-and-mutex-probing.md) | Game start, mutex probing and single instance | Accepted, amended 2026-10-02 (twice) |
| [0011](0011-screen-size-in-physical-pixels.md) | Screen size in physical pixels | Accepted, amended 2026-10-02 (twice) |
| [0012](0012-test-strategy.md) | Test strategy | Accepted, amended 2026-10-02 (four times) |
| [0013](0013-error-handling-and-logging.md) | Error handling and logging | Accepted, amended 2026-10-02 |
| [0014](0014-only-working-features-in-the-ui.md) | Only working features in the UI | Accepted, amended 2026-10-02 (twice) |
| [0015](0015-game-settings-target-folders-and-write-timing.md) | Game settings: target folders and when the launcher writes | Accepted, amended 2026-10-02 (three times) |
| [0016](0016-mutation-guard-and-effective-game-paths.md) | Mutation guard and effective game paths | Accepted, amended 2026-10-02 (five times) |

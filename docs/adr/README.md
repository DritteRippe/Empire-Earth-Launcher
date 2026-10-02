# Architecture decision records

Each file records one decision of launcher v2: the context, the decision, the evidence it rests on, its
consequences and the alternatives that were rejected. The overview is [ARCHITECTURE.md](../ARCHITECTURE.md).

Rules:

- File name `NNNN-short-title.md`, numbered in the order the decisions were taken; numbers are never reused.
- Status: **Proposed**, **Accepted**, **Superseded by NNNN** or **Rejected**. An accepted record is not
  rewritten: a changed decision gets a new record that supersedes the old one (the old one only gets its
  status changed).
- Evidence is concrete: a file and line, a command and its output, a vendor document, a forum post
  (`t=` topic, `p=` post of the save-ee.com support forum) or a section of [CONTRACT.md](../CONTRACT.md).

| No. | Decision | Status |
|---|---|---|
| [0001](0001-target-dotnet-framework-4-8.md) | Target .NET Framework 4.8 | Accepted |
| [0002](0002-keep-classic-project-files-and-packages-config.md) | Keep classic project files and packages.config | Accepted |
| [0003](0003-ui-free-core-library.md) | One UI-free core library, thin WinForms UI | Accepted |
| [0004](0004-async-await-threading-model.md) | async/await threading model | Accepted |
| [0005](0005-own-settings-file-instead-of-user-config.md) | Own settings file instead of user.config | Accepted |
| [0006](0006-platform-abstractions-and-windows-path-logic.md) | Platform abstractions and Windows path logic | Accepted |
| [0007](0007-registry-write-scope-and-reg-backups.md) | Registry write scope, protected keys and .reg backups | Accepted |
| [0008](0008-https-policy-and-update-api.md) | HTTPS policy and use of the update API | Accepted |
| [0009](0009-localization-with-resx-en-de-fr.md) | Localization with resx: English, German, French | Accepted |
| [0010](0010-game-start-and-mutex-probing.md) | Game start, mutex probing and single instance | Accepted |
| [0011](0011-screen-size-in-physical-pixels.md) | Screen size in physical pixels | Accepted |
| [0012](0012-test-strategy.md) | Test strategy | Accepted |
| [0013](0013-error-handling-and-logging.md) | Error handling and logging | Accepted |
| [0014](0014-only-working-features-in-the-ui.md) | Only working features in the UI | Accepted |

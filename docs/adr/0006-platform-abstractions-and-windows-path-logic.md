# 0006 Platform abstractions and Windows path logic

Status: **Accepted** (2026-10-02), amended 2026-10-02 (L-WP4; implementation in L-WP9), see the Amendment sections

## Context

Almost everything v2 does touches Windows: registry views and hives, files below the install root, the
VirtualStore, processes, named mutexes, the display, the Windows version, HTTPS. The tests run under Mono on
Linux (local verify) and on Windows (CI); they must never touch the real registry, `%LOCALAPPDATA%` or the
network. On `v2` four tests are skipped under Mono because they use `System.IO.Path` with Windows paths
(`GameDirectoryLocatorTests`: `CombineInstallLocation_*`, `Locate_RegisteredFolder_*`), and
`GameDirectoryLocator` is faked by overriding a `protected virtual` method.

## Decision

- **Interfaces in `Core.Platform`**, implemented for Windows in the same namespace and faked in the tests:
  - `IRegistry`: open/read/enumerate/write/delete with an explicit `RegistryLocation` (hive, view, path);
    values carry their kind (`String`, `ExpandString`, `MultiString`, `DWord`, `QWord`, `Binary`); errors are
    returned as a status (missing, access denied, I/O error), never thrown for normal cases.
    `WindowsRegistry` uses `RegistryKey.OpenBaseKey(hive, view)` for every access (contract 0, "Registry
    views"). HKCU is opened with `RegistryView.Default`.
  - `IFileSystem`: exists, attributes, size, last write time, open for reading, enumerate, create directory,
    write via temporary file, replace, move, delete - only what the core needs.
  - `IProcessStarter` (shell execute, open URL), `IMutexProbe` (ADR 0010), `ISystemInfo` (Windows version,
    Wine, primary screen size in physical pixels, local application data folder), `IClock`,
    `IHttpsClient` (ADR 0008), `INetworkInfo` (adapters, DNS).
- **`WinPath`**: Windows path rules as pure string logic (drive, root, combine, normalize separators and
  doubled backslashes, trailing backslash, case-insensitive comparison, "is below", relative manifest path to
  absolute path, rejection of `..`, `:`, drive letters and absolute paths). The core never calls
  `System.IO.Path` for install paths, so its tests behave the same on Linux and Windows.
- **Fakes** in the test project: `InMemoryRegistry` (both views of HKLM, HKCU shared between views,
  value kinds, injectable access errors), `InMemoryFileSystem` (case-insensitive Windows paths, read errors,
  read-only files), `FakeHttpsClient`, `FakeProcessStarter`, `FakeMutexProbe`, `FakeClock`,
  `FakeSystemInfo`, `FakeNetworkInfo`.
- Windows implementations stay thin (no logic beyond translating calls and exceptions) and are covered by the
  German test plan on real Windows.

## Evidence

- `verify_launcher.sh` output on `v2`: `Skipped: 4`, all `Only supported on Win` in `GameDirectoryLocatorTests`.
- CONTRACT.md 0 "Registry views": HKLM entries of the setup are in the 64-bit view; the launcher MUST open
  every HKLM key with an explicit view and not depend on its bitness. CONTRACT.md 1.4: every registry source is
  read from HKCU, HKLM64 and HKLM32.
- CONTRACT.md 2.2: manifest paths with `/`, rejection of absolute paths, drives, `:`, `\` and `..`; CONTRACT.md
  1.4 and 3.3: roots compared case-insensitively after normalizing separators and trailing backslashes.
- CONTRACT.md 7: "in the UI-free core library with unit tests (fake registry and file system, no network)".

## Consequences

- All new path and registry logic is tested on every platform; the `[Platform("Win")]` exceptions disappear
  when `GameDirectoryLocator` is replaced.
- The core takes its dependencies through constructors; `Program` wires the Windows implementations.
- The abstractions grow only with the needs of the core (no general-purpose file system library).

## Alternatives considered

- **System.IO.Abstractions** (NuGet): large surface, uses `System.IO.Path` semantics of the host OS, so the
  Windows path tests would still fail on Linux. Rejected.
- **`protected virtual` hooks** as in `GameDirectoryLocator`: workable for one value, unmanageable for the
  contract's many sources. Rejected.
- **Tests only on Windows**: the local verify runs on Linux. Rejected.

## Amendment 2026-10-02 (L-WP4)

The consequence above is reached: `GameDirectoryLocator` was replaced by the discovery of the core, which reads the
registry through `IRegistry` and the files through `IFileSystem`. Its tests, among them the four that ran on Windows
only, are ported with their names to `Core/Installations/GameDirectoryLocatorPortTests` and run on the in-memory
registry and file system; the test run no longer skips a test. The discovery tests use the 32-bit mode of
`InMemoryRegistry` (one HKLM for both views) to show that no installation appears twice.

## Amendment 2026-10-02 (implementation, L-WP9)

The network diagnostics get one more abstraction, grown with the need of the core: `Platform.INetworkInfo` with the
adapters (`GetAdapters`: type, description, state, addresses with prefix, gateways) and the name lookups
(`ResolveAsync`: resolved with the number of IPv4 and IPv6 addresses, not found, timeout or failed, with the duration).
`WindowsNetworkInfo` implements it with `NetworkInterface.GetAllNetworkInterfaces` (loopback skipped, every
`NetworkInformationException` a result) and `Dns.GetHostAddressesAsync` limited to 5 seconds (`Task.WhenAny`; a lookup
that outlives the limit is observed so that its late exception is not an unobserved task exception). An adapter also
carries its id, user-chosen name, MAC address and DNS suffix as Windows reports them, only so that the tests can feed
them and prove that no report and no log line shows them (ADR 0013). `FakeNetworkInfo` answers from a table and records
every lookup; the tests never resolve a real name (`TestIsolationTests`, `NetworkDestinationTests`). `ISystemInfo` also
names the display adapter of the primary screen (`PrimaryDisplayAdapter`: the device string of `EnumDisplayDevices`,
null if unknown) for the diagnostics report.

Evidence: `Core/Platform/NetworkInfoTests`, `Core/Diagnostics/NetworkDiagnosticsTests`, `Core/Diagnostics/DiagnosticsReportTests`.

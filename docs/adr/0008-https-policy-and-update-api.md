# 0008 HTTPS policy and use of the update API

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review; plan review; implementation in L-WP7 and L-WP9),
2026-10-07 (launcher 1.1.0: the download pages) and 2026-10-08 (launcher 1.1.1: the release page of the package; the text
of an answer), see the Amendment sections

## Context

The launcher contacts the update API of the community (`https://api.empireearth.eu/setup/`) to find the
current setup for a repair and, optionally, to tell whether an update exists (contract 4.3, 4.5). The setup
uses strict TLS and an allow-list of download URLs (`IsAllowedUpdateUrl`, `utils.iss`). The file server
`files.empireearth.eu` once served a certificate for another name (`CN=cluster131.hosting.ovh.net`, R16):
certificate problems are real and must not be "fixed" by turning validation off.

## Decision

- One HTTPS client (`Platform.HttpsClient` over `System.Net.Http.HttpClient` with `HttpClientHandler`):
  - **certificate validation always on**: no `ServerCertificateValidationCallback`, no custom validation;
    an architecture test fails if the sources assign one;
  - **TLS**: `SecurityProtocolType.SystemDefault` (the OS chooses, TLS 1.2/1.3) on Windows 8 and later; on
    Windows 7 (6.1) the launcher adds `Tls12` explicitly at start, because TLS 1.2 is disabled by default for
    SChannel clients there and is used only when the program asks for it;
  - **`https://` only**, `AllowAutoRedirect = false` (a redirect is an error), timeout 10 s per request,
    response size limit 4 KiB, no cookies, no proxy credentials beyond the system default;
  - **only these requests**: `GET https://api.empireearth.eu/setup/?product=<AppId>` (+ `&type=...&version=...`
    for the update check); nothing else is sent (no telemetry, no identifiers beyond the AppId).
- **`Repair.UpdateUrlPolicy`** is a port of the setup's `IsAllowedUpdateUrl`: `https://`; no user
  information, port, backslash, space, control or non-ASCII character; host `empireearth.eu`, `neoee.net`
  or a subdomain, or `github.com` with a path starting with `/EE-modders/` (ignoring case) without `..` or
  `%`. Its tests contain every case of the setup's `ci/tests/unit_tests.iss` (`TestIsAllowedUpdateUrl`) plus
  the launcher's own.
- Any failure (no AppId, DNS, TLS, timeout, status other than 200, URL refused) -> fallback
  `https://empireearth.eu/download`, logged with the reason. The launcher never downloads or starts the setup.
- The update check runs only when the user asks (Tools page), never at start.

## Evidence

- CONTRACT.md 4.3: request, allow-list, fallback, "Requests use HTTPS with certificate validation (TLS 1.2 or
  newer), never fall back to HTTP, have timeouts and send nothing but the query above (no telemetry)".
- Setup repo `ci/tests/unit_tests.iss` lines 149-163 (`TestIsAllowedUpdateUrl`): website, subdomain,
  NeoEE, GitHub project/other/dot segment/escape/root, mirror, http, look-alike, user info, empty.
- Microsoft, "Transport Layer Security (TLS) best practices with the .NET Framework": target 4.7+ and let the
  OS choose (`SystemDefault`); on Windows 7 TLS 1.1/1.2 are not enabled by default and must be requested.
- ADR 0001 spike: `SecurityProtocolType.Tls12`/`Tls13` and `HttpClient` compile against the 4.8 reference
  assemblies.

## Consequences

- With a broken certificate on the API host the user still gets the fixed download page; the log says why.
- Tests use `FakeHttpsClient`; no test opens a socket.

## Alternatives considered

- **`WebClient`/`HttpWebRequest`**: older API, harder to fake. Rejected.
- **Following redirects within the allow-list**: more code, no need (the API answers with the URL in the body).
  Rejected.
- **Pinning the API certificate**: breaks on every certificate renewal of the hosting. Rejected.

## Amendment 2026-10-02 (design review)

- **The certificate test is broader.** The architecture test fails on any of
  `ServerCertificateValidationCallback`, `ServerCertificateCustomValidationCallback` (the `HttpClientHandler`
  property since .NET 4.7.1, the path this launcher uses), `RemoteCertificateValidationCallback` and
  `DangerousAcceptAnyServerCertificateValidator` in a `.cs` file outside the test project, and on
  `CheckCertificateRevocationList = false`.
- **Bounded answer.** The 4 KiB limit is set as `HttpClient.MaxResponseContentBufferSize = 4096` (a property of
  the client, not of the handler) and the body is read through that buffered path; a test checks the handler
  settings (`AllowAutoRedirect = false`, `UseCookies = false`) and the client limit.
- **TLS handshake failures** (`HttpRequestException` with an inner `AuthenticationException`, e.g. no common
  cipher suite on Windows 7 or a wrong certificate as in R16) are one of the tested fallback reasons; the
  log names the inner exception type.

## Amendment 2026-10-02 (plan review)

- **One place for the TLS setting.** `ServicePointManager.SecurityProtocol` is process-wide. `Program` sets it
  exactly once, before the first request: on NT 6.1 to exactly `Tls12`; on every other Windows it stays
  `SystemDefault`. Naming `Tls13` explicitly (as the ADR 0001 spike did, not committed) makes handshakes fail
  where SChannel has no TLS 1.3 (Windows 7 to older Windows 10); `Tls`/`Tls11`/`Ssl3` would lower the security.
  An architecture test fails if a source file outside the test project names `SecurityProtocolType.Tls13`,
  `Tls11`, `Tls` or `Ssl3`, or assigns `SecurityProtocol` anywhere but in `Program`.
- **The query is the setup's.** `product=<AppId>` carries the AppId as read (contract 1.1: without braces, case
  kept; the setup appends `{#AppID}` as is, `utils.iss` line 17, `setup_is6.iss` line 74), escaped with
  `Uri.EscapeDataString` (a no-op for a GUID); `type` and `version` the same way. A test compares the built URLs
  with the setup's form for the hand-off and both update checks.
- **Game version check is not optional.** The check `&type=game&version=<GameVersion>` (contract 4.5) answers the
  version conflicts of the forum (forum report section 8 row 1, forum 4.12) for every installation with an AppId,
  including `community-legacy` ones (AppId from the uninstall key name, version from the key). It runs on request
  (Tools page and the version line of the Play page), like the rest of the update check. Only the setup version
  check (`&type=setup`) may be dropped if time runs out.

## Amendment 2026-10-02 (implementation, L-WP7)

The decision is implemented as planned. Details decided while implementing, keeping the decision:

- **One client for the launcher's lifetime**: `Program` creates one `Platform.HttpsClient` (an `HttpClient` over an
  `HttpClientHandler` with `AllowAutoRedirect = false`, `UseCookies = false`, `Timeout` 10 s,
  `MaxResponseContentBufferSize` 4096) and disposes it when the main window has closed. Errors are `HttpsResponse`
  results, never exceptions: `Timeout`, `TlsError` (an `AuthenticationException` anywhere in the inner exceptions; the
  log names the chain of types, e.g. `HttpRequestException/WebException/AuthenticationException`) and `NetworkError`
  (also an answer larger than 4 KiB); the log line of a request has the status or the error and the duration, never the
  body. Only absolute `https` URLs are accepted (a programming error otherwise).
- **TLS**: `Program.TlsProtocolsFor` gives exactly `Tls12` for NT 6.1 and nothing else (the system default stays) and
  `ConfigureTls` makes the only assignment of `ServicePointManager.SecurityProtocol`, before the first request.
  `TlsSettingTests` checks both rules on the sources, `NoCertificateOverrideTests` the broad list of the design review
  plus `ICertificatePolicy`/`CertificatePolicy` (the .NET 1.x way) and `CheckCertificateRevocationList = false`, each
  with self-tests on forbidden and allowed samples.
- **`UpdateUrlPolicy`** ports `IsAllowedUpdateUrl`, `SplitHttpsUrl` and `IsDomainOrSubdomain` of the setup's `utils.iss`
  character by character; its tests hold the 13 cases of `TestIsAllowedUpdateUrl` (setup `ci/tests/unit_tests.iss`
  lines 149 to 163) with the same names, URLs and expectations, compared by a script outside the repository, plus the
  launcher's own (ports, capitals in scheme and host, spaces, non-ASCII, percent escapes on GitHub and on the website,
  fragments, look-alike hosts).
- **The download** (`SetupDownloadLocator`): the query is built by `QueryUrl` exactly like the setup's
  (`product=<AppId as read>`, `&type=`, `&version=`, every value through `Uri.EscapeDataString`); the trimmed answer of
  an HTTP 200 is used only if the policy allows it; every other outcome gives `https://empireearth.eu/download` with a
  `FallbackReason` (`NoAppId`, `Timeout`, `TlsError`, `NetworkError`, `StatusNotOk` - also a redirect -, `UrlRejected`)
  that is logged and shown below the address in the repair window ("No address from the update server (...)"; nothing
  for `NoAppId`, whose page the fixed page is). The window asks when it opens; "Open download page" waits for the
  answer (it is the trigger of `UiOperation`), and closing the window cancels the request.
- **The version check** (`UpdateChecker`, `UpdateModel`): "Check version" on the *Play* page asks the game version,
  "Check for updates" on the *Tools* page the game and the setup version, only when the player clicks. As in the
  setup, `false` means outdated, any other answer of HTTP 200 up to date, and the latest version is asked and shown only
  with at most 32 allowed characters, else `?`; unlike the setup (`CheckUpdate` treats no answer as no update), a missing
  answer is `Failed` and shown as "could not be asked", so the player is never told something unchecked. An outdated
  version opens the repair window with the hand-off of contract 4.3 (`RepairAdvice.ForUpdate`). The setup version check,
  which this ADR allowed to drop, is implemented.

Evidence: `Core/Platform/HttpsClientTests`, `Architecture/TlsSettingTests`, `Architecture/NoCertificateOverrideTests`,
`Core/Repair/UpdateUrlPolicyTests`, `Core/Repair/SetupDownloadLocatorTests`, `Core/Repair/UpdateCheckerTests`,
`Launcher/UpdateModelTests`; test plan WP7-10 to WP7-12 and W7-05.

## Amendment 2026-10-02 (implementation, L-WP9)

The network diagnostics of L-WP9 add no destination to the three of ARCHITECTURE 10:

- **Only on request**: "Check network" on the *Tools* page or the link "Why? Check the network" below an unavailable
  player list (`DiagnosticsModel`); nothing is asked at start or in the background.
- **The update API as the reference for "the internet works"**: the check sends the query of contract 4.3
  (`SetupDownloadLocator.QueryUrl`, since 2026-10-07 `UpdateApi.QueryUrl`, only the AppId: of the selected installation, else of the first one that has one;
  without one the API is not asked) through the same `HttpsClient` with the rules of this ADR. Any HTTP answer, also a
  status other than 200, counts as an answer, because it proves the connection and TLS; a timeout, TLS or network error
  does not. With the name lookup of the status host and the answer of the status server this gives the verdict, and
  "probably a server outage, not your computer" only when the name resolves and the update API answers while the
  status server does not (forum report section 8 row 9; `OutageHintTests` covers every combination).
- **DNS** for the status host and the `Server` of every `NeoEE.cfg` (only host names, never an address or a URL), at
  most 5 seconds each; the status server gets the request of the player list (`NeoApiClient`, the configured timeout).
- **Not done**: no "what is my IP" service (the external address comes only from `upnp_info.txt` and is shown as its
  class), no connection to the auth and firewall ports 10002 and 10003 of NeoEE, no port check from outside (needs
  server support, ARCHITECTURE 16).
- **Kept by a test**: `NetworkDestinationTests` (category `SourceTree`) reads every source outside the test project and
  fails on a name lookup outside `WindowsNetworkInfo`, a socket outside the WON library's `NeoApiClient`, an HTTP client
  outside `HttpsClient`, the number 10002 or 10003, and a URL literal other than the update API and the three download pages
  of contract 4.3 (amendment of 2026-10-07; before it the fixed download page and the `https://` prefix of the URL policy);
  self-tests show that each rule finds a forbidden sample.

Evidence: `Architecture/NetworkDestinationTests`, `Core/Diagnostics/NetworkDiagnosticsTests`
(`ItAsks_OnlyDnsTheUpdateApiAndTheStatusServer`, `WithoutAnAppId_TheUpdateApiIsNotAsked`), `Core/Diagnostics/OutageHintTests`,
`Launcher/DiagnosticsModelTests`; test plan WP9-01, WP9-05 to WP9-07 and W7-06.

## Amendment 2026-10-07 (launcher 1.1.0, contract revision 6: the download pages)

The update API named `https://cdn.empireearth.eu/setup/game/EE_Setup.exe` (NeoEE `.../neo/NeoEE_Setup.exe`) as the
download; `cdn.empireearth.eu` no longer resolves (a CNAME to `empireearth-cdn.trafficmanager.net`, NXDOMAIN), and the
URL check let it pass because it is a subdomain of `empireearth.eu`. The website's buttons work. Since launcher 1.1.0:

- **The download page is fixed per product** (contract 4.3): `https://empireearth.eu/download/ee/` for EE,
  `https://empireearth.eu/download/neo/` for NeoEE (community and community-legacy installations),
  `https://empireearth.eu/download/` for foreign installations and an unknown product (`Repair.SetupDownloadPage`). No
  request is made to choose it; the site redirects the browser to the setup file. The repair window shows the address
  at once.
- **Removed**: `SetupDownloadLocator`, `SetupDownloadLocation`, `FallbackReason`, `UpdateUrlPolicy` and their tests;
  the setup removed `IsAllowedUpdateUrl` in the same revision. The failure reasons of the version check are
  `UpdateApiFailure` (`Timeout`, `TlsError`, `NetworkError`, `StatusNotOk`).
- **The update API** gets only the requests of contract 4.5 (`UpdateApi.QueryUrl` with `&type=`), when the player
  asks: the version check and, as the reference of the network check, `&type=game` (the latest game version) instead
  of the query without `&type=`. The HTTPS rules of this ADR are unchanged.
- **`NetworkDestinationTests`** allow the update API and the three pages as URL literals and nothing else.

Evidence: `Core/Repair/SetupDownloadPageTests`, `Core/Repair/UpdateApiTests`, `Core/Repair/UpdateCheckerTests`,
`Launcher/UpdateModelTests`, `Architecture/NetworkDestinationTests`; test plan WP7-12, WP14-01 to WP14-03, W7-05.

## Amendment 2026-10-08 (launcher 1.1.1: the release page of the package)

The review after the release of 1.1.0 found that the repair advice sent an installation of the suite "Empire Earth
Community" to the download page of its product. The website redirects that page to the official setup (in October 2026
version 1.7.2, contract 4.3 point 3), another build with the AppId of the setup the suite embeds: it updates the
installation in place, undoes fixes of the package and leaves the installation Unknown (`OlderSetupRanAfter`, contract
1.5), for which the advice named the same page again. Since launcher 1.1.1:

- **One more page, opened in the browser only**: the release page of the package,
  `https://github.com/DritteRippe/Empire-Earth-Community/releases/latest` (`Repair.SetupDownloadPage.PackageRelease`). It is
  the download for an installation the suite installed (its record lists the product, contract 1.6;
  `SuiteRepairLocator.PackageFor`, `SuitePackage`): the second option below the step that runs the suite from its folder
  again, and the only download when that folder is gone. An installation the suite did not install keeps the page of its
  product.
- **An available update of an installation of the suite** (contract 4.5) leads to the release page as well, with its own
  step (`RepairStep.UpdateWithNewPackage`) instead of the run of the suite from its folder: the update API answers for the
  setups of the community website, and the suite of the folder installs only the versions it embeds. The package gets
  newer versions only as a new release; the step says so and names the release page, and the window offers no
  "Open setup folder" for it.
- **The version check does not cover the package**: it asks about the game and the product setup, which a new release of
  the package may keep (1.7.2), so it can say "up to date" while a newer package exists. The *Tools* page says so and has
  a button "Open release page" next to "Check for updates" (`UpdateModel.OpenPackageReleasePage`); the setup line of the
  result names the product ("Version 1.7.2 of the Empire Earth setup: up to date."). The launcher does not ask for the
  newest release: that would be a new destination.
- **No new destination**: the page opens like the download pages, after a click, through the shell and not elevated
  (`SetupDownloadPage.Open`, also used for the pages of contract 4.3). The launcher sends no request to GitHub (neither to
  the page nor to the GitHub API) and follows no redirect; GitHub answers `/releases/latest` with the release marked
  "Latest", so the address never has to change with a version.
- **Chosen by the suite record, not by the installation alone**: `SetupDownloadPage.For` keeps the table of contract 4.3,
  and `RepairAdvice.DownloadUrl` takes the release page when `InstalledBySuite`. The contract has no row for it yet (a
  proposal for its next revision, made in both repositories at once); until then the launcher departs from the wording
  of 4.3 and 4.4 ("the download of 4.3 stays the second option") in this one case. No MUST or MUST NOT of 4.1 changes:
  the launcher still downloads, starts and elevates nothing.
- **`NetworkDestinationTests`** allow the update API, the three pages of contract 4.3 and the release page as URL literals
  and nothing else; new self-tests show that a download of the ZIP file and the GitHub API are found.

Evidence: `Core/Repair/SuiteRepairTests`, `Core/Repair/SetupDownloadPageTests`, `Launcher/TextsTests`,
`Launcher/UpdateModelTests`, `Architecture/NetworkDestinationTests`; test plan WP7-10, WP7-11, WP10-05, WP10-06, WP10-11,
WP14-04.

## Amendment 2026-10-08 (launcher 1.1.1: the text of an answer)

The review after the release of 1.1.0 found that `HttpsClient.GetAsync` read the body with
`HttpContent.ReadAsStringAsync`, which throws an `InvalidOperationException` when the `Content-Type` names a character set
Windows does not know (`charset=foo`; on the .NET Framework also a quoted `charset="utf-8"`, whose quotes it passes on).
Only cancellations and `HttpRequestException` were caught, so such an answer of the update API, or of a proxy in between,
ended the version check and the whole network diagnostics (`Task.WhenAll`) as an unexpected error, against the rule of
the L-WP7 amendment that errors are results, never exceptions. Since launcher 1.1.1:

- **The client decodes the answer itself** (`HttpsClient.DecodeBody`), from the bytes the buffer of the client holds (the
  4 KiB limit is unchanged): with the character set of the `Content-Type` if Windows knows it (quotes removed), else with
  the one a byte order mark names, else as UTF-8; the mark is not part of the text. This is what `ReadAsStringAsync` does
  for every answer it could read, so no such answer changes; an unknown character set counts as none, and invalid bytes
  become U+FFFD. Nothing in the decoding throws. The update API answers in ASCII.
- **The tests send requests without a network**: `HttpsClient.GetAsync(HttpClient, ...)` (internal) runs the request of
  the launcher on a client of `CreateClient` over a handler that answers from memory; the launcher's own client is still
  never created in a test (`TestIsolationTests`).

Evidence: `Core/Platform/HttpsClientTests` (`GetAsync_WithAnyCharacterSet_GivesTheAnswer`,
`GetAsync_AnAnswerOfFourKiB_IsRead_ALargerOneIsANetworkError` and the `DecodeBody` cases).

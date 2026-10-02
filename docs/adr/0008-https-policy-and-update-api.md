# 0008 HTTPS policy and use of the update API

Status: **Accepted** (2026-10-02)

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

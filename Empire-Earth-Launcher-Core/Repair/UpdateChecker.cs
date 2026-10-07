using System;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Repair
{
    /// <summary>What the version check asks about (contract 4.5).</summary>
    public enum VersionKind
    {
        /// <summary><c>&amp;type=game</c> with <c>GameVersion</c>: not optional (ADR 0008 plan review).</summary>
        Game,

        /// <summary><c>&amp;type=setup</c> with <c>SetupVersion</c>: the part of the package that may be dropped.</summary>
        Setup
    }

    /// <summary>The answer of a version check.</summary>
    public enum VersionCheckOutcome
    {
        /// <summary>The update API does not call the version outdated.</summary>
        UpToDate,

        /// <summary>The update API answered <c>false</c>: the version is outdated, the hand-off of contract 4.3 applies.</summary>
        UpdateAvailable,

        /// <summary>Not asked: the installation has no AppId (foreign) or no version of that kind.</summary>
        NotPossible,

        /// <summary>No usable answer (timeout, TLS or network error, a status other than 200); see <see cref="VersionCheckResult.Failure"/>.</summary>
        Failed
    }

    /// <summary>The result of <see cref="UpdateChecker.CheckAsync"/>.</summary>
    public sealed class VersionCheckResult
    {
        internal VersionCheckResult(Installation installation, VersionKind kind, string installedVersion,
            VersionCheckOutcome outcome, string latestVersion, UpdateApiFailure failure)
        {
            Installation = installation;
            Kind = kind;
            InstalledVersion = installedVersion;
            Outcome = outcome;
            LatestVersion = latestVersion;
            Failure = failure;
        }

        public Installation Installation { get; }

        public VersionKind Kind { get; }

        /// <summary>The version of the installation that was asked about; null if it has none.</summary>
        public string InstalledVersion { get; }

        public VersionCheckOutcome Outcome { get; }

        /// <summary>
        /// For <see cref="VersionCheckOutcome.UpdateAvailable"/>: the latest version as the API names it if it has at most 32
        /// characters of <c>0-9 . - _ space A-Z a-z</c>, else <c>?</c> (contract 4.5); null for the other outcomes.
        /// </summary>
        public string LatestVersion { get; }

        /// <summary>For <see cref="VersionCheckOutcome.Failed"/>: why there is no answer; else <see cref="UpdateApiFailure.None"/>.</summary>
        public UpdateApiFailure Failure { get; }

        public override string ToString()
        {
            return Kind + " version " + (InstalledVersion ?? "unknown") + " of " + Installation.Root + ": " + Outcome +
                   (LatestVersion == null ? string.Empty : ", latest " + LatestVersion) +
                   (Failure == UpdateApiFailure.None ? string.Empty : " (" + Failure + ")");
        }
    }

    /// <summary>
    /// The version check of contract 4.5 with the API of the setup's <c>CheckUpdate</c>: <c>&amp;type=game&amp;version=</c>
    /// with the game version and <c>&amp;type=setup&amp;version=</c> with the setup version, for every installation with an
    /// AppId (also <c>community-legacy</c> ones: AppId from the uninstall key name, versions from the key). Runs only when the
    /// player asks (Tools page, version line of the Play page), never at start (ADR 0008).
    /// </summary>
    /// <remarks>
    /// As in the setup (<c>IsUpdateAvailable</c>, <c>GetLatestVersionText</c>): an answer <c>false</c> (HTTP 200, trimmed)
    /// means outdated, any other answer of HTTP 200 up to date; for an outdated version the latest one is asked with
    /// <c>&amp;type=&lt;kind&gt;</c> and shown only if it looks like a version. Unlike the setup, a missing answer is reported
    /// as <see cref="VersionCheckOutcome.Failed"/> instead of "up to date", so the player is never told something unchecked.
    /// </remarks>
    public sealed class UpdateChecker
    {
        /// <summary>The longest answer shown as the latest version (<c>MaxVersionTextLength</c> of the setup).</summary>
        public const int MaxVersionTextLength = 32;

        /// <summary>The answer of the API for an outdated version.</summary>
        public const string OutdatedAnswer = "false";

        private const string AllowedVersionCharacters = "0123456789.-_ abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private readonly IHttpsClient client;
        private readonly ILogger logger;

        public UpdateChecker(IHttpsClient client, ILogger logger)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>The name of <paramref name="kind"/> in the query: <c>game</c> or <c>setup</c>.</summary>
        public static string TypeName(VersionKind kind)
        {
            return kind == VersionKind.Game ? "game" : "setup";
        }

        /// <summary>
        /// The latest version as it may be shown (contract 4.5): the trimmed answer if it is not empty, has at most 32
        /// characters and only <c>0-9 . - _ space A-Z a-z</c>; else <c>?</c>.
        /// </summary>
        public static string DisplayableVersion(string answer)
        {
            string text = answer?.Trim();
            if (string.IsNullOrEmpty(text) || text.Length > MaxVersionTextLength)
                return "?";
            foreach (char c in text)
            {
                if (AllowedVersionCharacters.IndexOf(c) < 0)
                    return "?";
            }
            return text;
        }

        /// <summary>Asks the update API whether the <paramref name="kind"/> version of <paramref name="installation"/> is outdated.</summary>
        public async Task<VersionCheckResult> CheckAsync(Installation installation, VersionKind kind,
            CancellationToken cancellationToken = default)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            string version = kind == VersionKind.Game ? installation.GameVersion : installation.SetupVersion;
            if (string.IsNullOrWhiteSpace(installation.AppId) || string.IsNullOrWhiteSpace(version))
                return Done(new VersionCheckResult(installation, kind, version, VersionCheckOutcome.NotPossible, null,
                    UpdateApiFailure.None));

            string type = TypeName(kind);
            HttpsResponse answer = await GetAsync(UpdateApi.QueryUrl(installation.AppId, type, version),
                cancellationToken).ConfigureAwait(false);
            UpdateApiFailure? failure = UpdateApi.FailureOf(answer);
            if (failure != null)
                return Done(new VersionCheckResult(installation, kind, version, VersionCheckOutcome.Failed, null, failure.Value));
            if (!string.Equals(answer.Body.Trim(), OutdatedAnswer, StringComparison.Ordinal))
                return Done(new VersionCheckResult(installation, kind, version, VersionCheckOutcome.UpToDate, null, UpdateApiFailure.None));

            HttpsResponse latest = await GetAsync(UpdateApi.QueryUrl(installation.AppId, type), cancellationToken)
                .ConfigureAwait(false);
            string latestVersion = UpdateApi.FailureOf(latest) == null ? DisplayableVersion(latest.Body) : "?";
            return Done(new VersionCheckResult(installation, kind, version, VersionCheckOutcome.UpdateAvailable, latestVersion,
                UpdateApiFailure.None));
        }

        private async Task<HttpsResponse> GetAsync(string url, CancellationToken cancellationToken)
        {
            HttpsResponse response = await client.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
            logger.Info("Update API: GET " + url + ": " + response + ".");
            return response;
        }

        private VersionCheckResult Done(VersionCheckResult result)
        {
            logger.Info("Version check: " + result + ".");
            return result;
        }
    }
}

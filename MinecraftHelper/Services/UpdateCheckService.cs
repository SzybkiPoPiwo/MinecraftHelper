using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftHelper.Services
{
    internal sealed record AppUpdateInfo(
        string CurrentVersion,
        string LatestVersion,
        string ReleaseName,
        Uri DownloadUri,
        bool IsDirectInstaller);

    internal static class UpdateCheckService
    {
        private const string LatestReleaseEndpoint =
            "https://api.github.com/repos/SzybkiPoPiwo/MinecraftHelper/releases/latest";

        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        public static async Task<AppUpdateInfo?> CheckForUpdateAsync(
            string currentVersion,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseEndpoint);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                request.Headers.UserAgent.ParseAdd($"MinecraftHelper/{NormalizeVersion(currentVersion)}");

                using HttpResponseMessage response = await HttpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return null;

                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using JsonDocument document = await JsonDocument.ParseAsync(
                    responseStream,
                    cancellationToken: cancellationToken);

                JsonElement root = document.RootElement;
                string latestVersion = NormalizeVersion(GetString(root, "tag_name"));
                string normalizedCurrentVersion = NormalizeVersion(currentVersion);

                if (string.IsNullOrWhiteSpace(latestVersion)
                    || ReleaseNotesCatalog.CompareVersions(latestVersion, normalizedCurrentVersion) <= 0)
                {
                    return null;
                }

                string releaseName = GetString(root, "name");
                if (string.IsNullOrWhiteSpace(releaseName))
                    releaseName = $"Minecraft Helper {latestVersion}";

                Uri? installerUri = FindInstallerUri(root);
                if (installerUri != null)
                {
                    return new AppUpdateInfo(
                        normalizedCurrentVersion,
                        latestVersion,
                        releaseName,
                        installerUri,
                        IsDirectInstaller: true);
                }

                Uri? releaseUri = GetSafeGitHubUri(GetString(root, "html_url"));
                if (releaseUri == null)
                    return null;

                return new AppUpdateInfo(
                    normalizedCurrentVersion,
                    latestVersion,
                    releaseName,
                    releaseUri,
                    IsDirectInstaller: false);
            }
            catch
            {
                // Sprawdzenie jest tylko dodatkiem: żaden błąd sieci ani odpowiedzi
                // nie może blokować uruchomienia aplikacji.
                return null;
            }
        }

        private static Uri? FindInstallerUri(JsonElement root)
        {
            if (!root.TryGetProperty("assets", out JsonElement assets)
                || assets.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var candidates = new List<(string Name, string Url)>();
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                string name = GetString(asset, "name");
                string url = GetString(asset, "browser_download_url");
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(url))
                {
                    candidates.Add((name, url));
                }
            }

            foreach ((string _, string url) in candidates
                         .OrderByDescending(candidate =>
                             candidate.Name.Contains("MinecraftHelper", StringComparison.OrdinalIgnoreCase)
                             && candidate.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                         .ThenByDescending(candidate =>
                             candidate.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase)))
            {
                Uri? uri = GetSafeGitHubUri(url);
                if (uri != null)
                    return uri;
            }

            return null;
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement property)
                   && property.ValueKind == JsonValueKind.String
                ? property.GetString()?.Trim() ?? string.Empty
                : string.Empty;
        }

        private static string NormalizeVersion(string? version)
        {
            string normalized = version?.Trim() ?? string.Empty;
            return normalized.StartsWith('v') || normalized.StartsWith('V')
                ? normalized[1..]
                : normalized;
        }

        private static Uri? GetSafeGitHubUri(string? value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                   || uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)
                ? uri
                : null;
        }
    }
}

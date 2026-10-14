using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GkmStatus.src
{ 
    public class UpdateCheckResult
    {
        public bool IsSuccess { get; set; }
        public bool HasUpdate { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseUrl { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string DownloadFileName { get; set; } = string.Empty;
        public string DownloadSha256 { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public bool IsRateLimited { get; set; }
    }

    public class UpdateService:IDisposable
    {
        private static readonly HttpClient _httpClient = CreateHttpClient();
        private static readonly HttpClient _downloadClient = CreateDownloadClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConstants.HTTP_USER_AGENT);
            return client;
        }

        private static HttpClient CreateDownloadClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConstants.HTTP_USER_AGENT);
            return client;
        }

        private bool _disposed;

        internal static bool ShouldCheckAutomatically(bool enabled, DateTime lastCheckUtc, DateTime nowUtc) =>
            enabled && (nowUtc - lastCheckUtc) >= TimeSpan.FromHours(24);


        public async Task<UpdateCheckResult> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default)
        {
            var result = new UpdateCheckResult();

            try
            {
#if GKMSTATUS_UPDATER_TEST_MODE
                if (TryCheckLocalTestRelease(currentVersion, result))
                    return result;
#endif
                using var response  = await _httpClient.GetAsync(AppConstants.GITHUB_REPO_URL, cancellationToken);

                if(response.StatusCode == HttpStatusCode.Forbidden)
                {
                    result.IsRateLimited = true;
                    return result;
                }

                if(response.StatusCode == HttpStatusCode.NotFound)
                    return result;

                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                using var doc = JsonDocument.Parse(content);

                if(doc.RootElement.TryGetProperty("tag_name", out var tag))
                {
                    string latestStr = NormalizeVersion(tag.GetString() ?? "");
                    string currentStr = NormalizeVersion(currentVersion);
                    string htmlUrl = doc.RootElement.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                    var downloadAsset = SelectWindowsReleaseAsset(doc.RootElement, latestStr);

                    if(Version.TryParse(latestStr, out var latest) && Version.TryParse(currentStr, out var current)) { 
                        result.IsSuccess = true;
                        result.LatestVersion = latestStr;
                        result.ReleaseUrl = htmlUrl;
                        if (downloadAsset.HasValue)
                        {
                            result.DownloadFileName = downloadAsset.Value.Name;
                            result.DownloadUrl = downloadAsset.Value.Url;
                            result.DownloadSha256 = downloadAsset.Value.Sha256;
                        }
                        result.HasUpdate = latest > current;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "The update check timed out.";
            }
            catch(Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        internal static (string Name, string Url, string Sha256)? SelectWindowsReleaseAsset(JsonElement release, string version)
        {
            if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return null;

            string expectedName = $"GkmStatus_win-x64_v{version}.zip";
            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameProperty) || nameProperty.GetString() != expectedName ||
                    !asset.TryGetProperty("browser_download_url", out var urlProperty) ||
                    !asset.TryGetProperty("digest", out var digestProperty))
                    continue;

                string url = urlProperty.GetString() ?? string.Empty;
                string digest = digestProperty.GetString() ?? string.Empty;
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    uri.Scheme == Uri.UriSchemeHttps &&
                    uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
                    uri.AbsolutePath.StartsWith("/Wea017net/GkmStatus/releases/download/", StringComparison.Ordinal) &&
                    TryParseSha256(digest, out string sha256))
                    return (expectedName, uri.AbsoluteUri, sha256);
            }

            return null;
        }

        public async Task<string> DownloadReleaseAsync(UpdateCheckResult release, string destinationDirectory, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(release.DownloadUrl) || string.IsNullOrWhiteSpace(release.DownloadFileName) ||
                !TryParseSha256(release.DownloadSha256, out string expectedSha256) ||
                !Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("The release does not contain a supported Windows download.");

            string fileName = Path.GetFileName(release.DownloadFileName);
            string expectedFileName = $"GkmStatus_win-x64_v{NormalizeVersion(release.LatestVersion)}.zip";
            if (!string.Equals(fileName, release.DownloadFileName, StringComparison.Ordinal) ||
                !string.Equals(fileName, expectedFileName, StringComparison.Ordinal))
                throw new InvalidOperationException("The release download file name is invalid.");

#if GKMSTATUS_UPDATER_TEST_MODE
            bool isLocalTestAsset = uri.IsFile && IsAllowedLocalTestPackage(uri.LocalPath);
#else
            bool isLocalTestAsset = false;
#endif
            if (!isLocalTestAsset &&
                (uri.Scheme != Uri.UriSchemeHttps ||
                 !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                 !uri.AbsolutePath.StartsWith("/Wea017net/GkmStatus/releases/download/", StringComparison.Ordinal)))
                throw new InvalidOperationException("The release does not contain a supported Windows download.");

            Directory.CreateDirectory(destinationDirectory);
            string destinationPath = Path.Combine(destinationDirectory, fileName);
            string temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
#if GKMSTATUS_UPDATER_TEST_MODE
                if (isLocalTestAsset)
                {
                    File.Copy(uri.LocalPath, temporaryPath, overwrite: true);
                }
                else
#endif
                {
                    using var response = await _downloadClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var target = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                    await source.CopyToAsync(target, cancellationToken);
                }

                await using (var downloadedFile = File.OpenRead(temporaryPath))
                {
                    byte[] actualHash = await SHA256.HashDataAsync(downloadedFile, cancellationToken);
                    string actualSha256 = Convert.ToHexString(actualHash);
                    if (!actualSha256.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The downloaded update failed its SHA-256 verification.");
                }

                File.Move(temporaryPath, destinationPath, overwrite: true);
                return destinationPath;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        internal static bool TryParseSha256(string digest, out string sha256)
        {
            const string prefix = "sha256:";
            sha256 = string.Empty;
            if (!digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || digest.Length != prefix.Length + 64)
                return false;

            string value = digest[prefix.Length..];
            if (!value.All(Uri.IsHexDigit))
                return false;

            sha256 = value;
            return true;
        }

#if GKMSTATUS_UPDATER_TEST_MODE
        private static bool TryCheckLocalTestRelease(string currentVersion, UpdateCheckResult result)
        {
            string? packagePath = Environment.GetEnvironmentVariable("GKMSTATUS_TEST_PACKAGE");
            string? latestVersion = Environment.GetEnvironmentVariable("GKMSTATUS_TEST_LATEST_VERSION");
            if (string.IsNullOrWhiteSpace(packagePath) && string.IsNullOrWhiteSpace(latestVersion))
                return false;
            if (string.IsNullOrWhiteSpace(packagePath) || string.IsNullOrWhiteSpace(latestVersion))
                throw new InvalidOperationException("The local updater test package settings are incomplete.");

            string fullPackagePath = Path.GetFullPath(packagePath);
            string expectedName = $"GkmStatus_win-x64_v{NormalizeVersion(latestVersion)}.zip";
            if (!IsAllowedLocalTestPackage(fullPackagePath) ||
                !Path.GetFileName(fullPackagePath).Equals(expectedName, StringComparison.Ordinal))
                throw new InvalidOperationException("The local updater test package is outside the test folder or has an unexpected name.");

            string current = NormalizeVersion(currentVersion);
            if (!Version.TryParse(current, out var currentParsed) || !Version.TryParse(NormalizeVersion(latestVersion), out var latestParsed))
                throw new InvalidOperationException("The local updater test version is invalid.");

            string digest = "sha256:" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPackagePath)));
            result.IsSuccess = true;
            result.LatestVersion = NormalizeVersion(latestVersion);
            result.DownloadFileName = expectedName;
            result.DownloadUrl = new Uri(fullPackagePath).AbsoluteUri;
            result.DownloadSha256 = digest;
            result.HasUpdate = latestParsed > currentParsed;
            return true;
        }

        private static bool IsAllowedLocalTestPackage(string path)
        {
            string? testRoot = Environment.GetEnvironmentVariable("GKMSTATUS_TEST_ROOT");
            if (string.IsNullOrWhiteSpace(testRoot))
                return false;

            string fullRoot = Path.GetFullPath(testRoot) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath);
        }
#endif

        internal static string NormalizeVersion(string version)
        {
            if(string.IsNullOrEmpty(version))
                return string.Empty;

            version = version.Trim();
            if (version.StartsWith("Ver.", StringComparison.OrdinalIgnoreCase))
                version = version[4..];
            else if(version.StartsWith("Ver",StringComparison.OrdinalIgnoreCase))
                version = version[3..];
            else if(version.StartsWith("v",StringComparison.OrdinalIgnoreCase))
                version = version[1..];

            return version.Trim();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            // HTTP clients are static and shared for the process lifetime; they are intentionally not disposed here.

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net;
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
        public string? ErrorMessage { get; set; }
        public bool IsRateLimited { get; set; }
    }

    public class UpdateService:IDisposable
    {
        private readonly HttpClient _httpClient;
        private bool _disposed;


        public UpdateService()
        {
            _httpClient= new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppConstants.HTTP_USER_AGENT);
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(string currentVersion)
        {
            var result = new UpdateCheckResult();

            try
            {
                using var response  = await _httpClient.GetAsync(AppConstants.GITHUB_REPO_URL);

                if(response.StatusCode == HttpStatusCode.Forbidden)
                {
                    result.IsRateLimited = true;
                    return result;
                }

                if(response.StatusCode == HttpStatusCode.NotFound)
                    return result;

                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(content);

                if(doc.RootElement.TryGetProperty("tag_name", out var tag))
                {
                    string latestStr = tag.GetString()?.TrimStart('v') ?? "";
                    string currentStr = NormalizeVersion(currentVersion);
                    string htmlUrl = doc.RootElement.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";

                    if(Version.TryParse(latestStr, out var latest) && Version.TryParse(currentStr, out var current)) { 
                        result.IsSuccess = true;
                        result.LatestVersion = latestStr;
                        result.ReleaseUrl = htmlUrl;
                        result.HasUpdate = latest > current;
                    }
                }
            } catch(Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private static string NormalizeVersion(string version)
        {
            if(string.IsNullOrEmpty(version))
                return version;

            if(version.StartsWith("v",StringComparison.OrdinalIgnoreCase))
                version = version[1..];
            else if(version.StartsWith("Ver",StringComparison.OrdinalIgnoreCase))
                version = version[3..];
            else if (version.StartsWith("Ver.", StringComparison.OrdinalIgnoreCase))
                version = version[4..];

            return version.Trim();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _httpClient.Dispose();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GkmStatus.src;

public sealed class UsageTelemetryClient
{
    public const string AppLaunchedEvent = "App.launched";
    public const string ManualDiscordConnectStartedEvent = "Discord.connectionStartedManually";
    public const string MonitoredDiscordConnectStartedEvent = "Discord.connectionStartedByGameMonitor";

    private const string IngestBaseUrl = "https://nom.telemetrydeck.com/v2/namespace/";
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    private readonly HttpClient _httpClient;
    private readonly string _namespace;
    private readonly string _appId;

    public UsageTelemetryClient()
        : this(SharedHttpClient, AppConstants.TELEMETRYDECK_NAMESPACE, AppConstants.TELEMETRYDECK_APP_ID)
    {
    }

    public UsageTelemetryClient(HttpClient httpClient, string telemetryNamespace, string appId)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _namespace = telemetryNamespace ?? string.Empty;
        _appId = appId ?? string.Empty;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_namespace) &&
        !string.IsNullOrWhiteSpace(_appId);

    public static string CreateInstallationId() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static string HashInstallationId(string installationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installationId);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(installationId))).ToLowerInvariant();
    }

    public async Task<bool> SendAppStartedAsync(
        string installationId,
        string appVersion,
        CancellationToken cancellationToken = default) =>
        await SendEventAsync(AppLaunchedEvent, installationId, appVersion, cancellationToken).ConfigureAwait(false);

    public async Task<bool> SendManualDiscordConnectStartedAsync(
        string installationId,
        string appVersion,
        CancellationToken cancellationToken = default) =>
        await SendEventAsync(ManualDiscordConnectStartedEvent, installationId, appVersion, cancellationToken).ConfigureAwait(false);

    public async Task<bool> SendMonitoredDiscordConnectStartedAsync(
        string installationId,
        string appVersion,
        CancellationToken cancellationToken = default) =>
        await SendEventAsync(MonitoredDiscordConnectStartedEvent, installationId, appVersion, cancellationToken).ConfigureAwait(false);

    private async Task<bool> SendEventAsync(
        string eventType,
        string installationId,
        string appVersion,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(installationId))
            return false;

        var signal = new Signal(
            _appId,
            HashInstallationId(installationId),
            eventType,
            new Dictionary<string, string> { ["TelemetryDeck.AppInfo.version"] = appVersion });

        string url = IngestBaseUrl + Uri.EscapeDataString(_namespace) + "/";
        using var content = new StringContent(
            JsonSerializer.Serialize(new[] { signal }),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private sealed record Signal(
        [property: JsonPropertyName("appID")] string AppId,
        [property: JsonPropertyName("clientUser")] string ClientUser,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("payload")] Dictionary<string, string> Payload);
}

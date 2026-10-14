using System.Net;
using System.Text.Json;
using GkmStatus.src;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GkmStatus.Tests;

[TestClass]
public sealed class UsageTelemetryTests
{
    [TestMethod]
    public void TelemetryConsentDefaultsToUndecidedAndPersistsExplicitChoices()
    {
        string path = Path.Combine(Path.GetTempPath(), $"GkmStatus-{Guid.NewGuid():N}.json");
        try
        {
            var manager = new ConfigManager(path);
            manager.Load();
            Assert.IsNull(manager.Config.Settings.UsageTelemetryAllowed);
            Assert.IsNull(manager.Config.Settings.TelemetryInstallationId);

            manager.Config.Settings.UsageTelemetryAllowed = true;
            manager.Config.Settings.TelemetryInstallationId = UsageTelemetryClient.CreateInstallationId();
            Assert.IsTrue(manager.Save());

            var reloaded = new ConfigManager(path);
            reloaded.Load();
            Assert.IsTrue(reloaded.Config.Settings.UsageTelemetryAllowed);
            Assert.AreEqual(manager.Config.Settings.TelemetryInstallationId, reloaded.Config.Settings.TelemetryInstallationId);

            reloaded.Config.Settings.UsageTelemetryAllowed = false;
            reloaded.Config.Settings.TelemetryInstallationId = null;
            Assert.IsTrue(reloaded.Save());

            var denied = new ConfigManager(path);
            denied.Load();
            Assert.IsFalse(denied.Config.Settings.UsageTelemetryAllowed);
            Assert.IsNull(denied.Config.Settings.TelemetryInstallationId);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void LegacyConfigWithoutConsentRemainsUndecided()
    {
        string path = Path.Combine(Path.GetTempPath(), $"GkmStatus-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"ConfigVersion\":1,\"Settings\":{\"ConnectOnStart\":false}}");
            var manager = new ConfigManager(path);

            manager.Load();

            Assert.IsNull(manager.Config.Settings.UsageTelemetryAllowed);
            Assert.IsNull(manager.Config.Settings.TelemetryInstallationId);
            Assert.IsFalse(manager.Config.Settings.ConnectOnStart);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public async Task StartupSignalContainsOnlyAppVersionAndHashedInstallId()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var http = new HttpClient(handler);
        var client = new UsageTelemetryClient(http, "test-namespace", "test-app-id");
        string installationId = UsageTelemetryClient.CreateInstallationId();

        bool sent = await client.SendAppStartedAsync(installationId, "1.2.0");

        Assert.IsTrue(sent);
        Assert.AreEqual("https://nom.telemetrydeck.com/v2/namespace/test-namespace/", handler.RequestUri?.ToString());
        using var document = JsonDocument.Parse(handler.Body!);
        var signal = document.RootElement[0];
        Assert.AreEqual("test-app-id", signal.GetProperty("appID").GetString());
        Assert.AreEqual("App.launched", signal.GetProperty("type").GetString());
        Assert.AreEqual(UsageTelemetryClient.HashInstallationId(installationId), signal.GetProperty("clientUser").GetString());
        Assert.AreEqual("1.2.0", signal.GetProperty("payload").GetProperty("App.version").GetString());
        Assert.AreEqual(4, signal.EnumerateObject().Count());
    }

    [TestMethod]
    public async Task ManualAndGameMonitorConnectionsUseDistinctEvents()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var http = new HttpClient(handler);
        var client = new UsageTelemetryClient(http, "test-namespace", "test-app-id");
        string installationId = UsageTelemetryClient.CreateInstallationId();

        bool manualSent = await client.SendManualDiscordConnectStartedAsync(installationId, "1.2.0");
        using var manualDocument = JsonDocument.Parse(handler.Body!);
        var manualSignal = manualDocument.RootElement[0];
        string? manualType = manualSignal.GetProperty("type").GetString();
        Assert.AreEqual(UsageTelemetryClient.HashInstallationId(installationId), manualSignal.GetProperty("clientUser").GetString());
        Assert.AreEqual("1.2.0", manualSignal.GetProperty("payload").GetProperty("App.version").GetString());
        Assert.AreEqual(4, manualSignal.EnumerateObject().Count());

        bool monitoredSent = await client.SendMonitoredDiscordConnectStartedAsync(installationId, "1.2.0");
        using var monitoredDocument = JsonDocument.Parse(handler.Body!);
        var monitoredSignal = monitoredDocument.RootElement[0];
        string? monitoredType = monitoredSignal.GetProperty("type").GetString();
        Assert.AreEqual(UsageTelemetryClient.HashInstallationId(installationId), monitoredSignal.GetProperty("clientUser").GetString());
        Assert.AreEqual("1.2.0", monitoredSignal.GetProperty("payload").GetProperty("App.version").GetString());
        Assert.AreEqual(4, monitoredSignal.EnumerateObject().Count());

        Assert.IsTrue(manualSent);
        Assert.IsTrue(monitoredSent);
        Assert.AreEqual(UsageTelemetryClient.ManualDiscordConnectStartedEvent, manualType);
        Assert.AreEqual(UsageTelemetryClient.MonitoredDiscordConnectStartedEvent, monitoredType);
        Assert.AreNotEqual(manualType, monitoredType);
    }

    [TestMethod]
    public async Task StartupSignalIsSkippedWhenTelemetryIsNotConfigured()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var http = new HttpClient(handler);
        var client = new UsageTelemetryClient(http, string.Empty, string.Empty);

        bool sent = await client.SendAppStartedAsync(UsageTelemetryClient.CreateInstallationId(), "1.2.0");

        Assert.IsFalse(sent);
        Assert.IsNull(handler.RequestUri);
    }

    [TestMethod]
    public async Task NetworkFailureDoesNotEscapeTheTelemetryClient()
    {
        using var http = new HttpClient(new ThrowingHandler());
        var client = new UsageTelemetryClient(http, "test-namespace", "test-app-id");

        bool sent = await client.SendAppStartedAsync(UsageTelemetryClient.CreateInstallationId(), "1.2.0");

        Assert.IsFalse(sent);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("test failure");
    }
}

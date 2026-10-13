using System.Text;
using GkmStatus.src;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using static GkmStatus.src.AppSettingsHelper;

namespace GkmStatus.Tests;

[TestClass]
public sealed class RegressionTests
{
    [TestMethod]
    public void RpcButtonLabelTrimsAtUtf8ScalarBoundaries()
    {
        string input = new string('a', 27) + "😀b";

        string result = TrimRpcButtonLabel(input);

        Assert.AreEqual(new string('a', 27) + "😀", result);
        Assert.IsTrue(new UTF8Encoding(false, true).GetByteCount(result) <= 31);
        Assert.AreEqual(result, InvokeStatic<string>(typeof(DiscordRpcService), "SafeTrimUtf8", input, 31));
    }

    [TestMethod]
    public void RpcButtonUrlRequiresAnAbsoluteHttpAddress()
    {
        Assert.IsTrue(IsValidRpcButtonUrl("HTTPS://example.com/path"));
        Assert.IsFalse(IsValidRpcButtonUrl("https://"));
        Assert.IsFalse(IsValidRpcButtonUrl("https://exa mple.com"));
        Assert.IsFalse(IsValidRpcButtonUrl("https://example.com/アイドル"));
        Assert.IsFalse(IsValidRpcButtonUrl("javascript:alert(1)"));
    }

    [TestMethod]
    public void EnumParsingRejectsUndefinedNumericValues()
    {
        Assert.AreEqual(PresenceStateType.Producing, GetStateType("999"));
        Assert.AreEqual(PresenceStateType.Idol, GetStateType("Idol"));
        Assert.AreEqual(AppTheme.Auto, GetTheme("999"));
    }

    [TestMethod]
    public void SavingEditorsCapturesCurrentValuesWithoutDroppingOtherHistory()
    {
        var presence = new PresenceSettings();
        presence.StateHistory[PresenceStateType.Custom.ToString()] = "older custom";
        presence.ButtonHistory[PresenceButtonMode.Store.ToString()] = new ButtonHistoryData { L1 = "store" };

        var historyType = typeof(PresenceSettings).Assembly.GetType("GkmStatus.src.PresenceEditorHistory");
        Assert.IsNotNull(historyType);
        var saveMethod = historyType.GetMethod("SaveEditorHistory", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(saveMethod);
        saveMethod.Invoke(null, [presence, PresenceStateType.PID, "P-ID 1234", PresenceButtonMode.Custom,
            "Primary", "https://example.com", "Secondary", "https://example.org"]);

        Assert.AreEqual("P-ID 1234", presence.StateHistory[PresenceStateType.PID.ToString()]);
        Assert.AreEqual("older custom", presence.StateHistory[PresenceStateType.Custom.ToString()]);
        Assert.AreEqual("Primary", presence.ButtonHistory[PresenceButtonMode.Custom.ToString()].L1);
        Assert.AreEqual("https://example.org", presence.ButtonHistory[PresenceButtonMode.Custom.ToString()].U2);
        Assert.AreEqual("store", presence.ButtonHistory[PresenceButtonMode.Store.ToString()].L1);
    }

    [TestMethod]
    public void AutomaticUpdateCheckUsesEnabledFlagAndTwentyFourHourWindow()
    {
        var now = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);

        Assert.IsTrue(InvokeStatic<bool>(typeof(UpdateService), "ShouldCheckAutomatically", true, now.AddHours(-24), now));
        Assert.IsFalse(InvokeStatic<bool>(typeof(UpdateService), "ShouldCheckAutomatically", true, now.AddHours(-23), now));
        Assert.IsFalse(InvokeStatic<bool>(typeof(UpdateService), "ShouldCheckAutomatically", false, DateTime.MinValue, now));
    }

    [TestMethod]
    public void UpdateVersionPrefixesNormalizeToTheSameVersion()
    {
        string[] inputs = ["1.1.0", "v1.1.0", "Ver1.1.0", "Ver.1.1.0"];

        CollectionAssert.AreEqual(
            Enumerable.Repeat("1.1.0", inputs.Length).ToArray(),
            inputs.Select(input => InvokeStatic<string>(typeof(UpdateService), "NormalizeVersion", input)).ToArray());
    }

    [TestMethod]
    public void DisconnectedRpcCannotBeFalselyResumed()
    {
        using var rpc = new DiscordRpcService();

        rpc.Clear();
        rpc.Resume();

        Assert.AreEqual(RpcStatus.Disconnected, rpc.Status);
        Assert.IsFalse(rpc.IsInitialized);
    }

    [TestMethod]
    public void ConfigLoadKeepsValidSiblingsAndRepairsBadFields()
    {
        string path = CreateTemporaryConfigPath();
        try
        {
            File.WriteAllText(path, """
                {
                  "Settings": { "StartMinimized": true, "ConnectOnStart": "broken", "SelectedTheme": "999" },
                  "Presence": { "ProducerName": "Producer", "ProducerLevel": "invalid", "StateType": "999", "ButtonMode": "App", "StateHistory": null }
                }
                """);

            var manager = new ConfigManager(path);
            manager.Load();

            Assert.IsTrue(manager.Config.Settings.StartMinimized);
            Assert.IsTrue(manager.Config.Settings.ConnectOnStart);
            Assert.AreEqual(AppTheme.Auto.ToString(), manager.Config.Settings.SelectedTheme);
            Assert.AreEqual("Producer", manager.Config.Presence.ProducerName);
            Assert.AreEqual(1, manager.Config.Presence.ProducerLevel);
            Assert.AreEqual(PresenceStateType.Producing.ToString(), manager.Config.Presence.StateType);
            Assert.AreEqual(PresenceButtonMode.App.ToString(), manager.Config.Presence.ButtonMode);
            Assert.AreEqual(0, manager.Config.Presence.StateHistory.Count);
        }
        finally
        {
            DeleteTemporaryConfigPath(path);
        }
    }

    [TestMethod]
    public void ConfigSaveRoundTripsThroughSameDirectoryReplacement()
    {
        string path = CreateTemporaryConfigPath();
        try
        {
            var manager = new ConfigManager(path);
            manager.Config.Presence.ProducerName = "Producer name";
            Assert.IsTrue(manager.Save(), manager.LastSaveError);

            manager.Config.Presence.ProducerName = "Updated";
            Assert.IsTrue(manager.Save(), manager.LastSaveError);

            var loaded = new ConfigManager(path);
            loaded.Load();
            Assert.AreEqual("Updated", loaded.Config.Presence.ProducerName);
            Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Length);
        }
        finally
        {
            DeleteTemporaryConfigPath(path);
        }
    }

    private static string CreateTemporaryConfigPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GkmStatus.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "config.json");
    }

    private static T InvokeStatic<T>(Type type, string methodName, params object?[] arguments)
    {
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Expected helper method {type.FullName}.{methodName}.");
        object? value = method.Invoke(null, arguments);
        Assert.IsNotNull(value);
        return (T)value;
    }

    private static void DeleteTemporaryConfigPath(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (directory != null && Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}

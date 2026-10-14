using System.Text;
using System.Text.Json;
using System.IO.Compression;
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
    public void UpdateAssetSelectionChoosesTheFullWindowsX64Package()
    {
        using var release = JsonDocument.Parse("""
            {
              "assets": [
                { "name": "GkmStatus_win-x64_v1.2.0_lite.zip", "browser_download_url": "https://github.com/Wea017net/GkmStatus/releases/download/v1.2.0/GkmStatus_win-x64_v1.2.0_lite.zip", "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" },
                { "name": "GkmStatus_win-x64_v1.2.0.zip", "browser_download_url": "https://github.com/Wea017net/GkmStatus/releases/download/v1.2.0/GkmStatus_win-x64_v1.2.0.zip", "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" },
                { "name": "GkmStatus_win-arm64_v1.2.0.zip", "browser_download_url": "https://github.com/Wea017net/GkmStatus/releases/download/v1.2.0/GkmStatus_win-arm64_v1.2.0.zip", "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }
              ]
            }
            """);

        var selected = InvokeStatic<(string Name, string Url, string Sha256)>(typeof(UpdateService), "SelectWindowsReleaseAsset", release.RootElement, "1.2.0");

        Assert.AreEqual("GkmStatus_win-x64_v1.2.0.zip", selected.Name);
        Assert.AreEqual("https://github.com/Wea017net/GkmStatus/releases/download/v1.2.0/GkmStatus_win-x64_v1.2.0.zip", selected.Url);
        Assert.AreEqual(new string('a', 64), selected.Sha256);
    }

    [TestMethod]
    public void UpdateAssetSelectionRejectsUnexpectedDownloadHosts()
    {
        using var release = JsonDocument.Parse("""
            {
              "assets": [
                { "name": "GkmStatus_win-x64_v1.2.0.zip", "browser_download_url": "https://example.com/GkmStatus_win-x64_v1.2.0.zip", "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }
              ]
            }
            """);

        var method = typeof(UpdateService).GetMethod("SelectWindowsReleaseAsset", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        Assert.IsNull(method.Invoke(null, [release.RootElement, "1.2.0"]));
    }

    [TestMethod]
    public void UpdateChecksumMustBeASha256Digest()
    {
        var method = typeof(UpdateService).GetMethod("TryParseSha256", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        object?[] validArguments = ["sha256:" + new string('a', 64), null];
        Assert.IsTrue((bool)method.Invoke(null, validArguments)!);
        Assert.AreEqual(new string('a', 64), validArguments[1]);

        object?[] invalidArguments = ["sha1:" + new string('a', 64), null];
        Assert.IsFalse((bool)method.Invoke(null, invalidArguments)!);
    }

    [TestMethod]
    public void UpdaterRejectsZipEntriesThatEscapeTheStagingFolder()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GkmStatus.Updater.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string archivePath = Path.Combine(directory, "update.zip");
        string stagePath = Path.Combine(directory, "stage");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("../escaped.exe").Open()))
                writer.Write("unexpected");

            var updaterType = typeof(UpdateService).Assembly.GetType("GkmStatus.src.UpdaterService");
            Assert.IsNotNull(updaterType);
            var extractMethod = updaterType.GetMethod("ExtractAndValidate", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(extractMethod);

            var exception = Assert.ThrowsException<TargetInvocationException>(() => extractMethod.Invoke(null, [archivePath, stagePath]));
            Assert.IsInstanceOfType<InvalidDataException>(exception.InnerException);
            Assert.IsFalse(File.Exists(Path.Combine(directory, "escaped.exe")));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void UpdaterRejectsArchivesWithTooManyEntries()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GkmStatus.Updater.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string archivePath = Path.Combine(directory, "update.zip");
        string stagePath = Path.Combine(directory, "stage");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                for (int index = 0; index < 257; index++)
                    archive.CreateEntry($"file-{index}.txt");
            }

            var updaterType = typeof(UpdateService).Assembly.GetType("GkmStatus.src.UpdaterService");
            Assert.IsNotNull(updaterType);
            var extractMethod = updaterType.GetMethod("ExtractAndValidate", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(extractMethod);

            var exception = Assert.ThrowsException<TargetInvocationException>(() => extractMethod.Invoke(null, [archivePath, stagePath]));
            Assert.IsInstanceOfType<InvalidDataException>(exception.InnerException);
            Assert.AreEqual(0, Directory.GetFiles(stagePath).Length);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task UpdaterRetriesReplacementWhenDestinationIsTemporarilyLocked()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GkmStatus.Updater.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string sourcePath = Path.Combine(directory, "new.exe");
        string destinationPath = Path.Combine(directory, "GkmStatus.exe");
        File.WriteAllText(sourcePath, "new version");
        File.WriteAllText(destinationPath, "old version");

        var destinationLock = new FileStream(destinationPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        try
        {
            Task unlockDestination = Task.Run(async () =>
            {
                await Task.Delay(750);
                destinationLock.Dispose();
            });

            var updaterType = typeof(UpdateService).Assembly.GetType("GkmStatus.src.UpdaterService");
            Assert.IsNotNull(updaterType);
            var copyMethod = updaterType.GetMethod("CopyFileWithRetry", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(copyMethod);
            copyMethod.Invoke(null, [sourcePath, destinationPath, true]);
            await unlockDestination;

            Assert.AreEqual("new version", File.ReadAllText(destinationPath));
        }
        finally
        {
            destinationLock.Dispose();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void UpdaterRollsBackEarlierReplacementsWhenALaterCopyFails()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "GkmStatus.Updater.Tests", Guid.NewGuid().ToString("N"));
        string workDirectory = Path.Combine(testRoot, "work");
        string stageDirectory = Path.Combine(workDirectory, "stage");
        string installDirectory = Path.Combine(testRoot, "install");
        string backupDirectory = Path.Combine(workDirectory, "backup");
        Directory.CreateDirectory(stageDirectory);
        Directory.CreateDirectory(installDirectory);

        File.WriteAllText(Path.Combine(stageDirectory, "GkmStatus.exe"), "new executable");
        File.WriteAllText(Path.Combine(stageDirectory, "a.dll"), "new library a");
        File.WriteAllText(Path.Combine(stageDirectory, "b.dll"), "new library b");
        File.WriteAllText(Path.Combine(stageDirectory, "c.dll"), "new library c");
        File.WriteAllText(Path.Combine(installDirectory, "GkmStatus.exe"), "old executable");
        File.WriteAllText(Path.Combine(installDirectory, "a.dll"), "old library a");
        File.WriteAllText(Path.Combine(installDirectory, "b.dll"), "old library b");

        var changedFiles = new List<(string Destination, string? Backup)>();
        string failedDestination = Path.Combine(installDirectory, "b.dll");
        Action<string, string, bool> failOnLaterTarget = (source, destination, overwrite) =>
        {
            if (destination.Equals(failedDestination, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Injected replacement failure.");

            File.Copy(source, destination, overwrite);
        };

        try
        {
            var updaterType = typeof(UpdateService).Assembly.GetType("GkmStatus.src.UpdaterService");
            Assert.IsNotNull(updaterType);
            var replaceMethod = updaterType.GetMethod("ReplaceFiles", BindingFlags.Static | BindingFlags.NonPublic);
            var rollbackMethod = updaterType.GetMethod("RollbackChangedFilesAndCleanup", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(replaceMethod);
            Assert.IsNotNull(rollbackMethod);

            var replacementFailure = Assert.ThrowsException<TargetInvocationException>(() =>
                replaceMethod.Invoke(null, [stageDirectory, installDirectory, backupDirectory, changedFiles, failOnLaterTarget]));
            Assert.IsInstanceOfType<IOException>(replacementFailure.InnerException);
            Assert.AreEqual(3, changedFiles.Count);

            Assert.IsNull(rollbackMethod.Invoke(null, [changedFiles, workDirectory]));

            Assert.AreEqual("old executable", File.ReadAllText(Path.Combine(installDirectory, "GkmStatus.exe")));
            Assert.AreEqual("old library a", File.ReadAllText(Path.Combine(installDirectory, "a.dll")));
            Assert.AreEqual("old library b", File.ReadAllText(Path.Combine(installDirectory, "b.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(installDirectory, "c.dll")));
            Assert.IsFalse(Directory.Exists(workDirectory), "Successful rollback should remove staged and backup files.");
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void UpdaterReplacesFilesAndCreatesBackupsOnSuccess()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "GkmStatus.Updater.Tests", Guid.NewGuid().ToString("N"));
        string workDirectory = Path.Combine(testRoot, "work");
        string stageDirectory = Path.Combine(workDirectory, "stage");
        string installDirectory = Path.Combine(testRoot, "install");
        string backupDirectory = Path.Combine(workDirectory, "backup");
        Directory.CreateDirectory(stageDirectory);
        Directory.CreateDirectory(installDirectory);
        File.WriteAllText(Path.Combine(stageDirectory, "GkmStatus.exe"), "new executable");
        File.WriteAllText(Path.Combine(stageDirectory, "settings.dll"), "new settings");
        File.WriteAllText(Path.Combine(installDirectory, "GkmStatus.exe"), "old executable");
        File.WriteAllText(Path.Combine(installDirectory, "settings.dll"), "old settings");

        try
        {
            var updaterType = typeof(UpdateService).Assembly.GetType("GkmStatus.src.UpdaterService");
            Assert.IsNotNull(updaterType);
            var replaceMethod = updaterType.GetMethod("ReplaceFiles", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(replaceMethod);
            var changedFiles = new List<(string Destination, string? Backup)>();

            replaceMethod.Invoke(null, [stageDirectory, installDirectory, backupDirectory, changedFiles, null]);

            Assert.AreEqual("new executable", File.ReadAllText(Path.Combine(installDirectory, "GkmStatus.exe")));
            Assert.AreEqual("new settings", File.ReadAllText(Path.Combine(installDirectory, "settings.dll")));
            Assert.AreEqual("old executable", File.ReadAllText(Path.Combine(backupDirectory, "GkmStatus.exe")));
            Assert.AreEqual("old settings", File.ReadAllText(Path.Combine(backupDirectory, "settings.dll")));
            Assert.AreEqual(2, changedFiles.Count);
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void UpdaterDoesNotChangeDestinationWhenBackupCreationFails()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "GkmStatus.Updater.Tests", Guid.NewGuid().ToString("N"));
        string workDirectory = Path.Combine(testRoot, "work");
        string stageDirectory = Path.Combine(workDirectory, "stage");
        string installDirectory = Path.Combine(testRoot, "install");
        string backupDirectory = Path.Combine(workDirectory, "backup");
        Directory.CreateDirectory(stageDirectory);
        Directory.CreateDirectory(installDirectory);
        File.WriteAllText(Path.Combine(stageDirectory, "GkmStatus.exe"), "new executable");
        string destinationPath = Path.Combine(installDirectory, "GkmStatus.exe");
        string backupPath = Path.Combine(backupDirectory, "GkmStatus.exe");
        File.WriteAllText(destinationPath, "old executable");
        Action<string, string, bool> failBackup = (_, destination, _) =>
        {
            if (destination.Equals(backupPath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Injected backup failure.");

            Assert.Fail("No install file should be copied before its backup is complete.");
        };
        var changedFiles = new List<(string Destination, string? Backup)>();

        try
        {
            var updaterType = typeof(UpdateService).Assembly.GetType("GkmStatus.src.UpdaterService");
            Assert.IsNotNull(updaterType);
            var replaceMethod = updaterType.GetMethod("ReplaceFiles", BindingFlags.Static | BindingFlags.NonPublic);
            var rollbackMethod = updaterType.GetMethod("RollbackChangedFilesAndCleanup", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(replaceMethod);
            Assert.IsNotNull(rollbackMethod);

            var failure = Assert.ThrowsException<TargetInvocationException>(() =>
                replaceMethod.Invoke(null, [stageDirectory, installDirectory, backupDirectory, changedFiles, failBackup]));
            Assert.IsInstanceOfType<IOException>(failure.InnerException);
            Assert.AreEqual(0, changedFiles.Count);
            Assert.AreEqual("old executable", File.ReadAllText(destinationPath));
            Assert.IsNull(rollbackMethod.Invoke(null, [changedFiles, workDirectory]));
            Assert.IsFalse(Directory.Exists(workDirectory));
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void TemporaryUpdaterCleanupOnlyAcceptsItsGeneratedDirectChildFolder()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "GkmStatus.Updater.Tests", Guid.NewGuid().ToString("N"));
        string validDirectory = Path.Combine(tempDirectory, "Updater-" + Guid.NewGuid().ToString("N"));
        string nestedDirectory = Path.Combine(validDirectory, "nested");
        string invalidName = Path.Combine(tempDirectory, "Updater-not-a-guid");

        var updaterType = typeof(UpdateService).Assembly.GetType("GkmStatus.src.UpdaterService");
        Assert.IsNotNull(updaterType);
        var validateMethod = updaterType.GetMethod("IsValidTemporaryUpdaterDirectory", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(validateMethod);

        Assert.IsTrue((bool)validateMethod.Invoke(null, [validDirectory, tempDirectory])!);
        Assert.IsFalse((bool)validateMethod.Invoke(null, [nestedDirectory, tempDirectory])!);
        Assert.IsFalse((bool)validateMethod.Invoke(null, [invalidName, tempDirectory])!);
        Assert.IsFalse((bool)validateMethod.Invoke(null, [Path.Combine(Path.GetTempPath(), Path.GetFileName(validDirectory)), tempDirectory])!);
    }


    [TestMethod]
    public void TemporaryUpdaterIsCreatedOnlyInsideTheRequestedTempFolder()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "GkmStatus.Updater.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var updaterType = typeof(UpdateService).Assembly.GetType("GkmStatus.src.UpdaterService");
            Assert.IsNotNull(updaterType);
            var createMethod = updaterType.GetMethod("CreateTemporaryUpdater", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(createMethod);

            var (executablePath, directoryPath) = ((string ExecutablePath, string DirectoryPath))createMethod.Invoke(null, [tempDirectory])!;

            Assert.IsTrue(File.Exists(executablePath));
            Assert.IsTrue(Directory.Exists(directoryPath));
            Assert.IsTrue(directoryPath.StartsWith(Path.GetFullPath(tempDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual("Updater.exe", Path.GetFileName(executablePath));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
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
            Assert.IsFalse(manager.Config.Settings.ConnectOnStart);
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
    public void DefaultConfigDoesNotConnectOnStartup()
    {
        Assert.IsFalse(ConfigManager.CreateDefaultConfig().Settings.ConnectOnStart);
    }

    [TestMethod]
    public void NotifyOnMinimizeDefaultsToEnabledForNewAndLegacyConfigs()
    {
        Assert.IsTrue(ConfigManager.CreateDefaultConfig().Settings.NotifyOnMinimize);

        string path = CreateTemporaryConfigPath();
        try
        {
            File.WriteAllText(path, "{\"ConfigVersion\":1,\"Settings\":{}}");
            var manager = new ConfigManager(path);
            manager.Load();

            Assert.IsTrue(manager.Config.Settings.NotifyOnMinimize);
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

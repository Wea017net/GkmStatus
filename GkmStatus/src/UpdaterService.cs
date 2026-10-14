using System.Diagnostics;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using GkmStatus.src.ui;

namespace GkmStatus.src;

internal static class UpdaterService
{
    private const string UpdaterFolderName = "Updater";
    private const string ApplyUpdateArgument = "--apply-update";
    private const string ShowAfterUpdateArgument = "--show-after-update";
    private const int MaxRequestArgumentLength = 16 * 1024;
    private const int MaxArchiveEntries = 256;
    private const long MaxArchiveUncompressedBytes = 512L * 1024 * 1024;

    private sealed class UpdateRequest
    {
        public int ParentProcessId { get; set; }
        public string InstallDirectory { get; set; } = string.Empty;
        public string ApplicationPath { get; set; } = string.Empty;
        public string DataDirectory { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Language { get; set; } = nameof(I18n.Language.Japanese);
        public string TestRoot { get; set; } = string.Empty;
        public string TestPackage { get; set; } = string.Empty;
        public string TestLatestVersion { get; set; } = string.Empty;
    }

    public static string TempDirectory => Path.Combine(AppConstants.APP_DATA_DIRECTORY, "Temp");

    public static string EnsureInstalled()
    {
        string updaterDirectory = Path.Combine(AppConstants.APP_DATA_DIRECTORY, UpdaterFolderName);
        Directory.CreateDirectory(updaterDirectory);

        string? currentExecutable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExecutable) || !File.Exists(currentExecutable))
            throw new InvalidOperationException("Could not locate the running application executable.");

        string updaterPath = Path.Combine(updaterDirectory, "Updater.exe");
        File.Copy(currentExecutable, updaterPath, overwrite: true);

        string sourceDirectory = AppContext.BaseDirectory;
        foreach (string sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            string extension = Path.GetExtension(sourceFile);
            if (!extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            string targetPath = Path.Combine(updaterDirectory, Path.GetFileName(sourceFile));
            if (!Path.GetFullPath(sourceFile).Equals(Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
                File.Copy(sourceFile, targetPath, overwrite: true);
        }

        return updaterPath;
    }

    public static Process StartUpdate(UpdateCheckResult release, string applicationPath)
    {
        string updaterPath = EnsureInstalled();
        string installDirectory = Path.GetDirectoryName(Path.GetFullPath(applicationPath))
            ?? throw new InvalidOperationException("Could not determine the application folder.");
        string fileName = Path.GetFileName(applicationPath);
        if (!fileName.Equals("GkmStatus.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Automatic update is only supported for GkmStatus.exe installations.");

        var request = new UpdateRequest
        {
            ParentProcessId = Environment.ProcessId,
            InstallDirectory = installDirectory,
            ApplicationPath = Path.GetFullPath(applicationPath),
            DataDirectory = AppConstants.APP_DATA_DIRECTORY,
            DownloadUrl = release.DownloadUrl,
            FileName = release.DownloadFileName,
            Sha256 = release.DownloadSha256,
            Version = release.LatestVersion,
            Language = I18n.CurrentLanguage.ToString(),
#if GKMSTATUS_UPDATER_TEST_MODE
            TestRoot = Environment.GetEnvironmentVariable("GKMSTATUS_TEST_ROOT") ?? string.Empty,
            TestPackage = Environment.GetEnvironmentVariable("GKMSTATUS_TEST_PACKAGE") ?? string.Empty,
            TestLatestVersion = Environment.GetEnvironmentVariable("GKMSTATUS_TEST_LATEST_VERSION") ?? string.Empty
#endif
        };
        string requestArgument = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(request));
        if (requestArgument.Length > MaxRequestArgumentLength)
            throw new InvalidOperationException("The update request is too large.");

        var startInfo = new ProcessStartInfo(updaterPath)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(updaterPath)!
        };
        startInfo.ArgumentList.Add(ApplyUpdateArgument);
        startInfo.ArgumentList.Add(requestArgument);

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the updater.");
    }

    public static bool TryRunUpdaterMode(string[] args)
    {
        if (args.Length == 0 || !args[0].Equals(ApplyUpdateArgument, StringComparison.Ordinal))
            return false;

        try
        {
            if (args.Length != 2 || args[1].Length > MaxRequestArgumentLength)
                throw new InvalidOperationException("The updater request is invalid.");

            byte[] requestBytes = Convert.FromBase64String(args[1]);
            var request = JsonSerializer.Deserialize<UpdateRequest>(requestBytes)
                ?? throw new InvalidOperationException("The updater request could not be read.");
            SetUpdaterEnvironment(request);

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            ApplicationConfiguration.Initialize();
            RunUpdateWindow(request);
        }
        catch (Exception ex)
        {
            MessageBox.Show(I18n.T(I18n.Text_List.Update_DownloadFailed, ex.Message), I18n.T(I18n.Text_List.App_Name), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return true;
    }

    private static void RunUpdateWindow(UpdateRequest request)
    {
        string? errorMessage = null;
        bool completed = false;
        UpdateRequest? completedRequest = null;
        using var form = new Form
        {
            Text = I18n.T(I18n.Text_List.App_Name),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ControlBox = false,
            ShowInTaskbar = true,
            ClientSize = new Size(390, 95)
        };
        var message = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 50,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = I18n.T(I18n.Text_List.Update_Installing)
        };
        var progress = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 25,
            Dock = DockStyle.Bottom,
            Height = 18
        };
        form.Controls.Add(progress);
        form.Controls.Add(message);
        form.Shown += async (_, _) =>
        {
            try
            {
                completedRequest = await Task.Run(() => ApplyUpdate(request));
                completed = true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                TryRestartOriginalApplication(request);
            }
            form.Close();
        };

        Application.Run(form);
        if (completed && completedRequest != null)
        {
            MessageBox.Show(I18n.T(I18n.Text_List.Update_DownloadComplete), I18n.T(I18n.Text_List.App_Name), MessageBoxButtons.OK, MessageBoxIcon.Information);
            try
            {
                var startInfo = new ProcessStartInfo(completedRequest.ApplicationPath)
                {
                    UseShellExecute = false,
                    WorkingDirectory = completedRequest.InstallDirectory
                };
                startInfo.ArgumentList.Add(ShowAfterUpdateArgument);
#if GKMSTATUS_UPDATER_TEST_MODE
                startInfo.Environment["GKMSTATUS_TEST_DATA_DIR"] = completedRequest.DataDirectory;
                startInfo.Environment["GKMSTATUS_TEST_ROOT"] = completedRequest.TestRoot;
                startInfo.Environment["GKMSTATUS_TEST_PACKAGE"] = completedRequest.TestPackage;
                startInfo.Environment["GKMSTATUS_TEST_LATEST_VERSION"] = completedRequest.TestLatestVersion;
#endif
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(I18n.T(I18n.Text_List.Update_DownloadFailed, ex.Message), I18n.T(I18n.Text_List.App_Name), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else if (errorMessage != null)
            MessageBox.Show(I18n.T(I18n.Text_List.Update_DownloadFailed, errorMessage), I18n.T(I18n.Text_List.App_Name), MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static UpdateRequest ApplyUpdate(UpdateRequest request)
    {
        SetUpdaterEnvironment(request);

        ValidateRequest(request);
        WaitForParentProcess(request.ParentProcessId);

        string tempDirectory = Path.GetFullPath(TempDirectory);
        string downloadedArchivePath = Path.Combine(tempDirectory, request.FileName);
        string workDirectory = CreateSecureWorkingDirectory();
        string archivePath = Path.Combine(workDirectory, request.FileName);
        string stageDirectory = Path.Combine(workDirectory, "stage");
        string backupDirectory = Path.Combine(workDirectory, "backup");
        List<(string Destination, string? Backup)> changedFiles = [];

        try
        {
            using var updateService = new UpdateService();
            updateService.DownloadReleaseAsync(new UpdateCheckResult
            {
                LatestVersion = request.Version,
                DownloadFileName = request.FileName,
                DownloadUrl = request.DownloadUrl,
                DownloadSha256 = request.Sha256
            }, tempDirectory).GetAwaiter().GetResult();

            CopyVerifiedArchive(downloadedArchivePath, archivePath, request.Sha256);
            ExtractAndValidate(archivePath, stageDirectory);
            changedFiles = ReplaceFiles(stageDirectory, request.InstallDirectory, backupDirectory);

        }
        catch
        {
            RestoreChangedFiles(changedFiles);
            throw;
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
            TryDeleteFile(downloadedArchivePath);
        }

        return request;
    }

    private static void ValidateRequest(UpdateRequest request)
    {
        if (!Path.GetFullPath(request.DataDirectory).Equals(Path.GetFullPath(AppConstants.APP_DATA_DIRECTORY), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The updater data folder is invalid.");

        string installDirectory = Path.GetFullPath(request.InstallDirectory);
        string applicationPath = Path.GetFullPath(request.ApplicationPath);
        if (!Path.GetDirectoryName(applicationPath)!.Equals(installDirectory, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(applicationPath).Equals("GkmStatus.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The application update target is invalid.");
        EnsureNoReparsePoints(applicationPath);

        string expectedFileName = $"GkmStatus_win-x64_v{UpdateService.NormalizeVersion(request.Version)}.zip";
        if (!request.FileName.Equals(expectedFileName, StringComparison.Ordinal))
            throw new InvalidOperationException("The update package name is invalid.");

        if (!Uri.TryCreate(request.DownloadUrl, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("The update download URL is invalid.");

#if GKMSTATUS_UPDATER_TEST_MODE
        bool localTestPackage = uri.IsFile &&
            Path.GetFullPath(uri.LocalPath).Equals(Path.GetFullPath(request.TestPackage), StringComparison.OrdinalIgnoreCase) &&
            Path.GetFullPath(request.TestPackage).StartsWith(Path.GetFullPath(request.TestRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
#else
        bool localTestPackage = false;
#endif
        if (!localTestPackage &&
            (uri.Scheme != Uri.UriSchemeHttps ||
             !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
             !uri.AbsolutePath.StartsWith("/Wea017net/GkmStatus/releases/download/", StringComparison.Ordinal)))
            throw new InvalidOperationException("The update download URL is invalid.");

        if (!UpdateService.TryParseSha256(request.Sha256, out _))
            throw new InvalidOperationException("The update checksum is invalid.");

#if GKMSTATUS_UPDATER_TEST_MODE
        if (!string.IsNullOrWhiteSpace(request.TestPackage) &&
            !Path.GetFullPath(request.TestPackage).Equals(Path.GetFullPath(Environment.GetEnvironmentVariable("GKMSTATUS_TEST_PACKAGE") ?? string.Empty), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The local test package does not match the updater request.");
#endif
    }

    private static void WaitForParentProcess(int processId)
    {
        try
        {
            using var parent = Process.GetProcessById(processId);
            if (!parent.WaitForExit((int)TimeSpan.FromMinutes(2).TotalMilliseconds))
                throw new TimeoutException("The application did not close in time. No files were changed.");
        }
        catch (ArgumentException)
        {
            // The main application may have exited before the elevated updater started.
        }
    }

    private static void TryRestartOriginalApplication(UpdateRequest request)
    {
        try
        {
            ValidateRequest(request);

            try
            {
                using var parent = Process.GetProcessById(request.ParentProcessId);
                if (!parent.HasExited)
                    return;
            }
            catch (ArgumentException) { }

            if (File.Exists(request.ApplicationPath))
            {
                Process.Start(new ProcessStartInfo(request.ApplicationPath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = request.InstallDirectory
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Could not restart GkmStatus after a failed update: " + ex.Message);
        }
    }

    private static void ExtractAndValidate(string archivePath, string stageDirectory)
    {
        Directory.CreateDirectory(stageDirectory);
        string stageRoot = Path.GetFullPath(stageDirectory) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count == 0 || archive.Entries.Count > MaxArchiveEntries)
            throw new InvalidDataException("The update package contains an unsupported number of files.");

        long declaredUncompressedBytes = 0;
        var entries = new List<(ZipArchiveEntry Entry, string TargetPath, bool IsDirectory)>();
        var targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            string targetPath = Path.GetFullPath(Path.Combine(stageDirectory, entry.FullName));
            if (Path.IsPathRooted(entry.FullName) || entry.FullName.Contains(':') ||
                !targetPath.StartsWith(stageRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The update package contains an invalid file path.");

            if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000)
                throw new InvalidDataException("The update package contains a symbolic link.");

            if (!targetPaths.Add(targetPath))
                throw new InvalidDataException("The update package contains duplicate file paths.");

            if (entry.Length < 0 || entry.Length > MaxArchiveUncompressedBytes - declaredUncompressedBytes)
                throw new InvalidDataException("The update package expands beyond the supported size limit.");
            declaredUncompressedBytes += entry.Length;

            bool isDirectory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            entries.Add((entry, targetPath, isDirectory));
        }

        long extractedBytes = 0;
        foreach (var (entry, targetPath, isDirectory) in entries)
        {
            if (isDirectory)
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            using var source = entry.Open();
            using var destination = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[81920];
            long entryBytes = 0;
            int bytesRead;
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (bytesRead > MaxArchiveUncompressedBytes - extractedBytes)
                    throw new InvalidDataException("The update package expands beyond the supported size limit.");

                extractedBytes += bytesRead;
                entryBytes += bytesRead;
                destination.Write(buffer, 0, bytesRead);
            }

            if (entryBytes != entry.Length)
                throw new InvalidDataException("The update package contains an invalid file size.");
        }

        if (!File.Exists(Path.Combine(stageDirectory, "GkmStatus.exe")))
            throw new InvalidDataException("The update package does not contain GkmStatus.exe at its root.");
    }

    private static string CreateSecureWorkingDirectory()
    {
        string workRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GkmStatus", "UpdaterWork");
        EnsureNoReparsePoints(workRoot);
        Directory.CreateDirectory(workRoot);

        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(administrators);
        const InheritanceFlags inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(workRoot).SetAccessControl(security);

        string workDirectory = Path.Combine(workRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        new DirectoryInfo(workDirectory).SetAccessControl(security);
        return workDirectory;
    }

    private static void EnsureNoReparsePoints(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? currentPath = Path.GetPathRoot(fullPath);
        if (currentPath == null)
            throw new InvalidOperationException("The update path is invalid.");

        foreach (string segment in fullPath[currentPath.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            try
            {
                if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Update paths cannot contain symbolic links or junctions.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private static void CopyVerifiedArchive(string sourcePath, string destinationPath, string expectedSha256)
    {
        if (!UpdateService.TryParseSha256(expectedSha256, out string expectedHash))
            throw new InvalidOperationException("The update checksum is invalid.");

        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] actualHash = SHA256.HashData(source);
        if (!Convert.ToHexString(actualHash).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded update failed its SHA-256 verification.");

        source.Position = 0;
        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        source.CopyTo(destination);
        destination.Flush(flushToDisk: true);

        destination.Position = 0;
        byte[] copiedHash = SHA256.HashData(destination);
        if (!Convert.ToHexString(copiedHash).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The staged update failed its SHA-256 verification.");
    }

    private static List<(string Destination, string? Backup)> ReplaceFiles(string stageDirectory, string installDirectory, string backupDirectory)
    {
        var files = Directory.GetFiles(stageDirectory, "*", SearchOption.AllDirectories);
        if (files.Length == 0)
            throw new InvalidDataException("The update package is empty.");

        Directory.CreateDirectory(backupDirectory);
        var changedFiles = new List<(string Destination, string? Backup)>();
        foreach (string sourcePath in files)
        {
            string relativePath = Path.GetRelativePath(stageDirectory, sourcePath);
            string destinationPath = Path.GetFullPath(Path.Combine(installDirectory, relativePath));
            string installRoot = Path.GetFullPath(installDirectory) + Path.DirectorySeparatorChar;
            if (!destinationPath.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The update package contains an invalid target path.");

            EnsureNoReparsePoints(destinationPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            string? backupPath = null;
            if (File.Exists(destinationPath))
            {
                backupPath = Path.Combine(backupDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                CopyFileWithRetry(destinationPath, backupPath, overwrite: true);
            }

            changedFiles.Add((destinationPath, backupPath));
            CopyFileWithRetry(sourcePath, destinationPath, overwrite: true);
        }
        return changedFiles;
    }

    private static void CopyFileWithRetry(string sourcePath, string destinationPath, bool overwrite)
    {
        const int maxAttempts = 60;
        const int retryDelayMilliseconds = 250;

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(sourcePath, destinationPath, overwrite);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(retryDelayMilliseconds);
            }
        }
    }

    private static void RestoreChangedFiles(IEnumerable<(string Destination, string? Backup)> changedFiles)
    {
        foreach (var changedFile in changedFiles.Reverse())
        {
            if (changedFile.Backup != null && File.Exists(changedFile.Backup))
                File.Copy(changedFile.Backup, changedFile.Destination, overwrite: true);
            else if (File.Exists(changedFile.Destination))
                File.Delete(changedFile.Destination);
        }
    }

    private static void SetUpdaterEnvironment(UpdateRequest request)
    {
        if (Enum.TryParse<I18n.Language>(request.Language, out var language))
            I18n.CurrentLanguage = language;
#if GKMSTATUS_UPDATER_TEST_MODE
        Environment.SetEnvironmentVariable("GKMSTATUS_TEST_DATA_DIR", request.DataDirectory);
        Environment.SetEnvironmentVariable("GKMSTATUS_TEST_ROOT", request.TestRoot);
        Environment.SetEnvironmentVariable("GKMSTATUS_TEST_PACKAGE", request.TestPackage);
        Environment.SetEnvironmentVariable("GKMSTATUS_TEST_LATEST_VERSION", request.TestLatestVersion);
#endif
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

using GkmStatus.src;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GkmStatus.Tests;

[TestClass]
public sealed class ErrorLogTests
{
    [TestMethod]
    public void WritesDailyFilesAndKeepsOnlyTheNewestThirty()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"GkmStatus-logs-{Guid.NewGuid():N}");
        try
        {
            var start = new DateTime(2026, 1, 1, 12, 0, 0);
            for (int day = 0; day < ErrorLog.MaxFiles + 1; day++)
                ErrorLog.WriteTo(directory, start.AddDays(day), "Test", $"day {day}");
            // 関係のないファイルは消さない
            File.WriteAllText(Path.Combine(directory, "note.txt"), "keep");

            var logs = Directory.GetFiles(directory, "*.log").Select(Path.GetFileName).Order().ToArray();
            Assert.AreEqual(ErrorLog.MaxFiles, logs.Length);
            Assert.AreEqual("2026-01-02.log", logs[0]);
            Assert.AreEqual("2026-01-31.log", logs[^1]);
            Assert.IsTrue(File.Exists(Path.Combine(directory, "note.txt")));

            // 同じ日の 2 件目は同じファイルに追記する
            ErrorLog.WriteTo(directory, start.AddDays(30).AddHours(1), "Test", "second");
            string[] lines = File.ReadAllLines(Path.Combine(directory, "2026-01-31.log"));
            Assert.AreEqual(2, lines.Length);
            StringAssert.StartsWith(lines[1], "[2026-01-31 13:00:00]");
            StringAssert.EndsWith(lines[1], "Test: second");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void MemoryUsageIsReadAndFormattedInTenthsOfMegabytes()
    {
        Assert.IsTrue(MemoryUsage.GetBytes() > 0);
        Assert.AreEqual(123, MemoryUsage.ToTenthsOfMegabyte((long)(12.3 * 1024 * 1024)));
        Assert.AreEqual("12.3 MB", MemoryUsage.Format(123));
        Assert.AreEqual("0.5 MB", MemoryUsage.Format(5));
    }

    [TestMethod]
    public void LogFolderIsNextToTheConfigFile()
    {
        string config = Path.Combine(Path.GetTempPath(), "GkmStatusConfigDir", "config.json");
        Assert.AreEqual(Path.Combine(Path.GetTempPath(), "GkmStatusConfigDir", "logs"), ErrorLog.DirectoryFor(config));
    }
}

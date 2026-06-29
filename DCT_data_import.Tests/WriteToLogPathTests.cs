using System;
using System.IO;
using DCT_data_import;
using DCT_data_import.ReadAndImport;
using Xunit;

namespace DCT_data_import.Tests
{
    public class WriteToLogPathTests : IDisposable
    {
        private readonly string _root;

        public WriteToLogPathTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "dct-write-log-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Fact]
        public void WriteLogs_UsesConfiguredRoot_ForDataCheckAndSuccessLogs()
        {
            var writeToLog = new WriteToLog("  " + _root + "  ");

            writeToLog.WriteToDataImportLog(LogLevel.Error, "R3 configurable log root");
            writeToLog.WriteToCheckLog("r3_check.csv", "sample.csv,1KB,2026/06/28,1,2");
            writeToLog.WriteImportSuccessLog("Type=RawData DbKey=DB001 File=test.csv ElapsedSec=1.25 Cleanup=Local Delete completed");

            string exeName = Path.GetFileNameWithoutExtension(typeof(WriteToLog).Assembly.Location);
            string dataLogDirectory = Path.Combine(_root, exeName, "data_import_logs");
            string checkLogDirectory = Path.Combine(_root, exeName, "check_logs");

            string dataLogPath = Assert.Single(Directory.GetFiles(dataLogDirectory, "DCT_data_import_Log_*.txt"));
            Assert.Contains("[ERROR] R3 configurable log root", File.ReadAllText(dataLogPath));

            string successLogPath = Assert.Single(Directory.GetFiles(dataLogDirectory, "DCT_data_import_Success_Log_*.txt"));
            string successLogContent = File.ReadAllText(successLogPath);
            Assert.Contains("[SUCCESS] Type=RawData DbKey=DB001 File=test.csv ElapsedSec=1.25 Cleanup=Local Delete completed", successLogContent);

            string checkLogPath = Path.Combine(checkLogDirectory, "r3_check.csv");
            Assert.True(File.Exists(checkLogPath));
            Assert.Contains("sample.csv,1KB,2026/06/28,1,2", File.ReadAllText(checkLogPath));
        }

        [Fact]
        public void GetMutexName_WhenPathIsLong_ReturnsBoundedStableName()
        {
            string longPath = Path.Combine(_root, new string('x', 512), "DCT_data_import_Log_2026_06_28.txt");

            string first = WriteToLog.GetMutexName("DCT_Log_", longPath);
            string second = WriteToLog.GetMutexName("DCT_Log_", longPath);

            Assert.Equal(first, second);
            Assert.Equal("DCT_Log_".Length + 64, first.Length);
        }

        [Fact]
        public void WriteInfoAndErrorLog_WriteExpectedLevelTags()
        {
            var writeToLog = new WriteToLog(_root);

            writeToLog.WriteInfoLog("info message");
            writeToLog.WriteErrorLog("error message");
            writeToLog.WriteToDataImportLog("compat message");

            string exeName = Path.GetFileNameWithoutExtension(typeof(WriteToLog).Assembly.Location);
            string dataLogDirectory = Path.Combine(_root, exeName, "data_import_logs");
            string dataLogPath = Assert.Single(Directory.GetFiles(dataLogDirectory, "DCT_data_import_Log_*.txt"));
            string content = File.ReadAllText(dataLogPath);

            Assert.Contains("[INFO] info message", content);
            Assert.Contains("[ERROR] error message", content);
            Assert.Contains("[INFO] compat message", content);
        }

        [Fact]
        public void LogImportSuccess_WhenLocalArchiveCleanupHasPath_LogsCleanupResultOnly()
        {
            var writeToLog = new WriteToLog(_root);
            var importData = new TestImportData();

            importData.LogSuccess(
                writeToLog,
                "RawData",
                "DB001",
                "test.csv",
                1.25,
                "Local Archive completed: C:\\secret\\Imported\\test.csv");

            string exeName = Path.GetFileNameWithoutExtension(typeof(WriteToLog).Assembly.Location);
            string dataLogDirectory = Path.Combine(_root, exeName, "data_import_logs");
            string successLogPath = Assert.Single(Directory.GetFiles(dataLogDirectory, "DCT_data_import_Success_Log_*.txt"));
            string successLogContent = File.ReadAllText(successLogPath);

            Assert.Contains("Cleanup=Local Archive completed", successLogContent);
            Assert.DoesNotContain("C:\\secret", successLogContent);
        }

        [Fact]
        public void WriteLogs_WhenRetentionEnabled_DeletesOnlyExpiredKnownLogFiles()
        {
            var writeToLog = new WriteToLog(_root, 30);
            string exeName = Path.GetFileNameWithoutExtension(typeof(WriteToLog).Assembly.Location);
            string dataLogDirectory = Path.Combine(_root, exeName, "data_import_logs");
            string checkLogDirectory = Path.Combine(_root, exeName, "check_logs");
            Directory.CreateDirectory(dataLogDirectory);
            Directory.CreateDirectory(checkLogDirectory);

            string expiredDataLog = Path.Combine(dataLogDirectory, "DCT_data_import_Log_2026_01_01.txt");
            string expiredSuccessLog = Path.Combine(dataLogDirectory, "DCT_data_import_Success_Log_2026_01_01.txt");
            string freshDataLog = Path.Combine(dataLogDirectory, "DCT_data_import_Log_2099_01_01.txt");
            string unrelatedDataFile = Path.Combine(dataLogDirectory, "operator_note.txt");
            string expiredCheckLog = Path.Combine(checkLogDirectory, "DCT_data_check_log_rawData_20260101.csv");
            foreach (string path in new[] { expiredDataLog, expiredSuccessLog, freshDataLog, unrelatedDataFile, expiredCheckLog })
            {
                File.WriteAllText(path, "sample");
            }
            File.SetLastWriteTime(expiredDataLog, DateTime.Now.AddDays(-31));
            File.SetLastWriteTime(expiredSuccessLog, DateTime.Now.AddDays(-31));
            File.SetLastWriteTime(expiredCheckLog, DateTime.Now.AddDays(-31));

            writeToLog.WriteInfoLog("trigger data cleanup");
            writeToLog.WriteToCheckLog("DCT_data_check_log_rawData_20990101.csv", "sample.csv,1KB,2026/06/28,1,2");

            Assert.False(File.Exists(expiredDataLog));
            Assert.False(File.Exists(expiredSuccessLog));
            Assert.False(File.Exists(expiredCheckLog));
            Assert.True(File.Exists(freshDataLog));
            Assert.True(File.Exists(unrelatedDataFile));
        }

        [Fact]
        public void WriteLogs_WhenRetentionDisabled_KeepsExpiredKnownLogFiles()
        {
            var writeToLog = new WriteToLog(_root, 0);
            string exeName = Path.GetFileNameWithoutExtension(typeof(WriteToLog).Assembly.Location);
            string dataLogDirectory = Path.Combine(_root, exeName, "data_import_logs");
            Directory.CreateDirectory(dataLogDirectory);
            string expiredDataLog = Path.Combine(dataLogDirectory, "DCT_data_import_Log_2026_01_01.txt");
            File.WriteAllText(expiredDataLog, "sample");
            File.SetLastWriteTime(expiredDataLog, DateTime.Now.AddDays(-365));

            writeToLog.WriteInfoLog("retention disabled");

            Assert.True(File.Exists(expiredDataLog));
        }

        private sealed class TestImportData : ImportData
        {
            public void LogSuccess(WriteToLog writeToLog, string importType, string dbKey, string fileName, double elapsedSec, string cleanupStatus)
            {
                LogImportSuccess(writeToLog, importType, dbKey, fileName, elapsedSec, cleanupStatus);
            }
        }
    }
}

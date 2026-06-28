using System;
using System.IO;
using DCT_data_import;
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
        public void WriteLogs_UsesConfiguredRoot_ForDataAndCheckLogs()
        {
            var writeToLog = new WriteToLog(_root);

            writeToLog.WriteToDataImportLog(LogLevel.Error, "R3 configurable log root");
            writeToLog.WriteToCheckLog("r3_check.csv", "sample.csv,1KB,2026/06/28,1,2");

            string exeName = Path.GetFileNameWithoutExtension(typeof(WriteToLog).Assembly.Location);
            string dataLogDirectory = Path.Combine(_root, exeName, "data_import_logs");
            string checkLogDirectory = Path.Combine(_root, exeName, "check_logs");

            string dataLogPath = Assert.Single(Directory.GetFiles(dataLogDirectory, "DCT_data_import_Log_*.txt"));
            Assert.Contains("[ERROR] R3 configurable log root", File.ReadAllText(dataLogPath));

            string checkLogPath = Path.Combine(checkLogDirectory, "r3_check.csv");
            Assert.True(File.Exists(checkLogPath));
            Assert.Contains("sample.csv,1KB,2026/06/28,1,2", File.ReadAllText(checkLogPath));
        }
    }
}

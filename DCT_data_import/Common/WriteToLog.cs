using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
namespace DCT_data_import.Common
{
    /// <summary>
    /// 日誌層級列舉
    /// </summary>
    public enum LogLevel
    {
        Info = 0,    // 一般資訊
        Error = 1    // 錯誤訊息
    }
    public class WriteToLog
    {
        private const string LogRootAppSettingKey = "DataImportLogRoot";
        private const string LogRetentionDaysAppSettingKey = "DataImportLogRetentionDays";
        private const string DefaultLogRoot = @"C:\temp";
        private const int DefaultLogRetentionDays = 90;
        private readonly string _logRoot;
        private readonly int _logRetentionDays;

        public WriteToLog()
            : this(
                ConfigurationManager.AppSettings[LogRootAppSettingKey],
                ParseLogRetentionDays(ConfigurationManager.AppSettings[LogRetentionDaysAppSettingKey]))
        {
        }

        internal WriteToLog(string logRoot)
            : this(logRoot, DefaultLogRetentionDays)
        {
        }

        internal WriteToLog(string logRoot, int logRetentionDays)
        {
            _logRoot = string.IsNullOrWhiteSpace(logRoot) ? DefaultLogRoot : logRoot.Trim();
            _logRetentionDays = logRetentionDays;
        }

        /// <summary>
        /// 寫入資料匯入日誌，包含層級資訊
        /// </summary>
        /// <param name="level">日誌層級</param>
        /// <param name="message">日誌訊息</param>
        public void WriteToDataImportLog(LogLevel level, string message)
        {
            string logDirectory = GetLogDirectory("data_import_logs");
            // 確保資料夾存在
            Directory.CreateDirectory(logDirectory);
            DeleteExpiredLogFiles(logDirectory, "DCT_data_import_Log_*.txt", "DCT_data_import_Success_Log_*.txt");
            //string logDirectory = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + "\\data_import_logs").LocalPath;
            string log_path = Path.Combine(logDirectory, $"DCT_data_import_Log_{DateTime.Now:yyyy_MM_dd}.txt");
            // 使用檔案路徑建立唯一的 Mutex 名稱
            string mutexName = GetMutexName("DCT_Log_", log_path);
            // 根據層級產生標籤
            string levelTag = level == LogLevel.Error ? "[ERROR]" : "[INFO]";
            string logMessage = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {levelTag} {message}";
            // 使用 UTF-8 with BOM 確保相容性
            Encoding utf8WithBom = new UTF8Encoding(true);
            using (var mutex = new Mutex(false, mutexName))
            {
                bool hasHandle = false;
                try
                {
                    hasHandle = mutex.WaitOne(TimeSpan.FromSeconds(30), false);
                    if (!hasHandle)
                    {
                        throw new TimeoutException("無法取得檔案鎖定權限");
                    }
                    // 檢查資料夾是否存在，若不存在則建立
                    if (!Directory.Exists(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                    }
                    if (!File.Exists(log_path))
                    {
                        string swVersion = @"DCT_data_import v." + FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
                        using (StreamWriter writer = new StreamWriter(log_path, false, utf8WithBom))
                        {
                            writer.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} [INFO] {swVersion}");
                            writer.WriteLine(logMessage);
                        }
                        return;
                    }
                    // 改用同步寫入，避免 WriteLineAsync 的 fire-and-forget 問題
                    using (StreamWriter writer = new StreamWriter(log_path, true, utf8WithBom))
                    {
                        writer.WriteLine(logMessage);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Log write failed: {ex.Message}");
                }
                finally
                {
                    if (hasHandle)
                        mutex.ReleaseMutex();
                }
            }
        }
        /// <summary>
        /// 寫入資料匯入日誌（向後相容方法，預設為 Info 層級）
        /// </summary>
        /// <param name="message">日誌訊息</param>
        public void WriteToDataImportLog(string message)
        {
            // 呼叫新方法，預設為 Info 層級以保持向後相容性
            WriteToDataImportLog(LogLevel.Info, message);
        }
        /// <summary>
        /// 寫入資訊層級日誌的便利方法
        /// </summary>
        /// <param name="message">日誌訊息</param>
        public void WriteInfoLog(string message)
        {
            WriteToDataImportLog(LogLevel.Info, message);
        }
        /// <summary>
        /// 寫入錯誤層級日誌的便利方法
        /// </summary>
        /// <param name="message">日誌訊息</param>
        public void WriteErrorLog(string message)
        {
            WriteToDataImportLog(LogLevel.Error, message);
        }

        public void WriteImportSuccessLog(string message)
        {
            string logDirectory = GetLogDirectory("data_import_logs");
            Directory.CreateDirectory(logDirectory);
            DeleteExpiredLogFiles(logDirectory, "DCT_data_import_Log_*.txt", "DCT_data_import_Success_Log_*.txt");
            string log_path = Path.Combine(logDirectory, $"DCT_data_import_Success_Log_{DateTime.Now:yyyy_MM_dd}.txt");
            string mutexName = GetMutexName("DCT_SuccessLog_", log_path);
            string logMessage = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} [SUCCESS] {message}";
            Encoding utf8WithBom = new UTF8Encoding(true);
            using (var mutex = new Mutex(false, mutexName))
            {
                bool hasHandle = false;
                try
                {
                    hasHandle = mutex.WaitOne(TimeSpan.FromSeconds(30), false);
                    if (!hasHandle)
                    {
                        throw new TimeoutException("無法取得檔案鎖定權限");
                    }
                    if (!Directory.Exists(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                    }
                    using (StreamWriter writer = new StreamWriter(log_path, true, utf8WithBom))
                    {
                        writer.WriteLine(logMessage);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Success log write failed: {ex.Message}");
                }
                finally
                {
                    if (hasHandle)
                        mutex.ReleaseMutex();
                }
            }
        }

        public string WriteToMailTemp(string message)
        {
            string log_path = Path.Combine(AppContext.BaseDirectory, "mail_temp.txt");
            string mutexName = GetMutexName("DCT_MailTemp_", log_path);
            using (var mutex = new Mutex(false, mutexName))
            {
                bool hasHandle = false;
                try
                {
                    hasHandle = mutex.WaitOne(TimeSpan.FromSeconds(30), false);
                    if (!hasHandle)
                    {
                        throw new TimeoutException("無法取得檔案鎖定權限");
                    }
                    if (!File.Exists(log_path))
                    {
                        using (StreamWriter writer = File.CreateText(log_path))
                        {
                            writer.WriteLine(message);
                        }
                        return string.Empty;
                    }
                    using (StreamWriter writer = new StreamWriter(log_path, true, Encoding.UTF8))
                    {
                        writer.WriteLine(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Mail temp write failed: {ex.Message}");
                }
                finally
                {
                    if (hasHandle)
                        mutex.ReleaseMutex();
                }
            }
            return string.Empty;
        }
        public void WriteToCheckLog(string logFilename, string content)
        {
            string checkLogFolder = GetLogDirectory("check_logs");
            // 確保資料夾存在
            Directory.CreateDirectory(checkLogFolder);
            DeleteExpiredLogFiles(checkLogFolder, "DCT_data_check_log_*.csv");
            //修正路徑處理，避免 Substring(6) 的風險
            //string assemblyPath = Assembly.GetExecutingAssembly().CodeBase;
            //if (assemblyPath.StartsWith("file:///"))
            //{
            //    assemblyPath = assemblyPath.Substring(8); // 移除 "file:///"
            //}
            //else if (assemblyPath.StartsWith("file://"))
            //{
            //    assemblyPath = assemblyPath.Substring(7); // 移除 "file://"
            //}
            //string checkLogFolder = Path.Combine(Path.GetDirectoryName(assemblyPath), "check_logs");
            string log_path = Path.Combine(checkLogFolder, logFilename);
            string mutexName = GetMutexName("DCT_CheckLog_", log_path);
            using (var mutex = new Mutex(false, mutexName))
            {
                bool hasHandle = false;
                try
                {
                    hasHandle = mutex.WaitOne(TimeSpan.FromSeconds(30), false);
                    if (!hasHandle)
                    {
                        throw new TimeoutException("無法取得檔案鎖定權限");
                    }
                    if (!Directory.Exists(checkLogFolder))
                    {
                        Directory.CreateDirectory(checkLogFolder);
                    }
                    // 使用 UTF-8 with BOM 編碼
                    Encoding utf8WithBom = new UTF8Encoding(true);
                    if (!File.Exists(log_path))
                    {
                        using (StreamWriter writer = new StreamWriter(log_path, false, utf8WithBom))
                        {
                            writer.WriteLine("File Name, File Size, Time, Read File Takes Time(unit:s), Import Takes Time(unit:s)");
                            writer.WriteLine(content);
                        }
                        return;
                    }
                    using (StreamWriter writer = new StreamWriter(log_path, true, utf8WithBom))
                    {
                        writer.WriteLine(content);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Check log write failed: {ex.Message}");
                }
                finally
                {
                    if (hasHandle)
                        mutex.ReleaseMutex();
                }
            }
        }

        private string GetLogDirectory(string folderName)
        {
            string exeName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(_logRoot, exeName, folderName);
        }

        private static int ParseLogRetentionDays(string value)
        {
            return int.TryParse(value, out int days) ? days : DefaultLogRetentionDays;
        }

        // ponytail: retention cleanup only; add size-based rotation if daily files become too large.
        private void DeleteExpiredLogFiles(string logDirectory, params string[] searchPatterns)
        {
            if (_logRetentionDays <= 0)
            {
                return;
            }

            DateTime cutoff = DateTime.Now.AddDays(-_logRetentionDays);
            foreach (string searchPattern in searchPatterns)
            {
                try
                {
                    foreach (string path in Directory.EnumerateFiles(logDirectory, searchPattern))
                    {
                        if (File.GetLastWriteTime(path) < cutoff)
                        {
                            File.Delete(path);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Log cleanup failed: {ex.Message}");
                }
            }
        }

        internal static string GetMutexName(string prefix, string path)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
            return prefix + Convert.ToHexString(hash);
        }
    }
}

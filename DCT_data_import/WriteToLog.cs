using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
namespace DCT_data_import
{
    public class WriteToLog
    {
        public void WriteToDataImportLog(string message)
        {
            string logDirectory = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + "\\data_import_logs").LocalPath;
            string log_path = Path.Combine(logDirectory, $"DCT_data_import_Log_{DateTime.Now:yyyy_MM_dd}.txt");
            // 使用檔案路徑建立唯一的 Mutex 名稱
            string mutexName = "DCT_Log_" + log_path.Replace("\\", "_").Replace(":", "_").Replace("/", "_");
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
                        using (StreamWriter writer = new StreamWriter(log_path, false, Encoding.UTF8))
                        {
                            writer.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {swVersion}");
                            writer.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {message}");
                        }
                        return;
                    }
                    // 改用同步寫入，避免 WriteLineAsync 的 fire-and-forget 問題
                    using (StreamWriter writer = new StreamWriter(log_path, true, Encoding.UTF8))
                    {
                        writer.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {message}");
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
        public string WriteToMailTemp(string message)
        {
            string log_path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + @"\mail_temp.txt").LocalPath;
            string mutexName = "DCT_MailTemp_" + log_path.Replace("\\", "_").Replace(":", "_").Replace("/", "_");
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
                        return "";
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
            return "";
        }
        public void WriteToCheckLog(string logFilename, string content)
        {
            // 修正路徑處理，避免 Substring(6) 的風險
            string assemblyPath = Assembly.GetExecutingAssembly().CodeBase;
            if (assemblyPath.StartsWith("file:///"))
            {
                assemblyPath = assemblyPath.Substring(8); // 移除 "file:///"
            }
            else if (assemblyPath.StartsWith("file://"))
            {
                assemblyPath = assemblyPath.Substring(7); // 移除 "file://"
            }
            string checkLogFolder = Path.Combine(Path.GetDirectoryName(assemblyPath), "check_logs");
            string log_path = Path.Combine(checkLogFolder, logFilename);
            string mutexName = "DCT_CheckLog_" + logFilename.Replace("\\", "_").Replace(":", "_").Replace("/", "_");
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
                    if (!File.Exists(log_path))
                    {
                        using (StreamWriter writer = File.CreateText(log_path))
                        {
                            writer.WriteLine("File Name, File Size, Time, Read File Takes Time, Import Takes Time");
                            writer.WriteLine(content);
                        }
                        return;
                    }
                    using (StreamWriter writer = File.AppendText(log_path))
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
    }
    //public class WriteToLog
    //{
    //    int FailedCount = 0;
    //    public void WriteToDataImportLog(string message)
    //    {
    //        string logDirectory = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + "\\data_import_logs").LocalPath;
    //        string log_path = Path.Combine(logDirectory, $"DCT_data_import_Log_{DateTime.Now:yyyy_MM_dd}.txt");
    //        //string log_path = @"C:\temp\HL_System_WEB_Log.txt";
    //        //string log_path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + @"\DCT_data_import_Log.txt").LocalPath;
    //        // string log_path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + $"\\data_import_logs\\DCT_data_import_Log_{DateTime.Now.ToString("yyyy_MM_dd")}.txt").LocalPath;
    //        //using (var mutex = new Mutex(false, log_path.Replace("\\", "")))
    //        //{
    //        //    var hasHandle = false;
    //        try
    //        {
    //            // 檢查資料夾是否存在，若不存在則建立
    //            if (!Directory.Exists(logDirectory))
    //            {
    //                Directory.CreateDirectory(logDirectory);
    //            }
    //            //hasHandle = mutex.WaitOne(Timeout.Infinite, false);
    //            if (!File.Exists(log_path))
    //            {
    //                string swVersion = @"DCT_data_import v." + FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
    //                //File.Create(log_path);
    //                using (StreamWriter writer = new StreamWriter(log_path, false, Encoding.UTF8))
    //                {
    //                    writer.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {swVersion}");
    //                    writer.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {message}");
    //                }
    //                //using (StreamWriter writer = File.CreateText(log_path))
    //                //{
    //                //    writer.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " " + swVersion);
    //                //    writer.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " " + message);
    //                //}
    //                return;
    //            }
    //            //File.AppendAllText(log_path, DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + " " + message);
    //            // Write file using StreamWriter
    //            using (StreamWriter writer = new StreamWriter(log_path, true, Encoding.UTF8))
    //            {
    //                writer.WriteLineAsync($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {message}");
    //            }
    //            //using (StreamWriter writer = File.AppendText(log_path))
    //            //{
    //            //    //writer.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " " + message);
    //            //    writer.WriteLineAsync(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " " + message);
    //            //}
    //        }
    //        catch (Exception ex)
    //        {
    //            FailedCount++;
    //            Console.WriteLine(ex.Message);
    //        }
    //        //    finally
    //        //    {
    //        //        if (hasHandle)
    //        //            mutex.ReleaseMutex();
    //        //    }
    //        //}
    //    }
    //    public string WriteToMailTemp(string message)
    //    {
    //        string log_path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + @"\mail_temp.txt").LocalPath;
    //        if (!File.Exists(log_path))
    //        {
    //            //File.Create(log_path);
    //            using (StreamWriter writer = File.CreateText(log_path))
    //            {
    //                writer.WriteLine(message);
    //            }
    //            return "";
    //        }
    //        // Write file using StreamWriter
    //        using (StreamWriter writer = new StreamWriter(log_path, true, System.Text.Encoding.UTF8))
    //        {
    //            writer.WriteLine(message);
    //        }
    //        return "";
    //    }
    //    public void WriteToCheckLog(string logFilename, string content)
    //    {
    //        string checkLogFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + "\\" + "check_logs";
    //        checkLogFolder = checkLogFolder.Substring(6);
    //        if (!Directory.Exists(checkLogFolder))
    //        {
    //            Directory.CreateDirectory(checkLogFolder);
    //        }
    //        string log_path = new Uri(checkLogFolder + "\\" + logFilename).LocalPath;
    //        try
    //        {
    //            if (!File.Exists(log_path))
    //            {
    //                using (StreamWriter writer = File.CreateText(log_path))
    //                {
    //                    writer.WriteLine("File Name, File Size, Time, Read File Takes Time, Import Takes Time");
    //                    writer.WriteLine(content);
    //                }
    //                return;
    //            }
    //            using (StreamWriter writer = File.AppendText(log_path))
    //            {
    //                writer.WriteLine(content);
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            FailedCount++;
    //            Console.WriteLine(ex.Message);
    //        }
    //    }
    //}
}
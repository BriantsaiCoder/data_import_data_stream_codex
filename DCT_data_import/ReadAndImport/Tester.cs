using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using static DCT_data_import.DbObject;
namespace DCT_data_import.ReadAndImport
{
    public class Tester : ImportData
    {
        public async Task<ImportResult> ReadAndImportTesterStatus(FileProcess fileAccess, DatabaseService DatabaseService, string dbKey)
        {
            bool isDBKeyExist = false, import_result = false;
            WriteToLog writeToLog = new WriteToLog();
            string deleteStatus;
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts2 = stopWatch.Elapsed;
            double readTakeTime = 0, importTakeTime = 0;
            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();
            // 檢查FTP是否有此檔案
            string filename = "tester_" + dbKey + ".csv";
            string ftpFilePath = GetFilePath("tester", dbKey);
            string errorPath = GetErrorPath("tester", dbKey);
            bool isFileExist = FileExists(ftpFilePath);
            if (!isFileExist)
            {
                Console.WriteLine("Tester Status File not found:  " + filename);
                writeToLog.WriteErrorLog("Tester Status File not found: " + ftpFilePath);
                return new ImportResult(0, "File not found.");
            }
            try
            {
                long fileSize = GetFileLength(ftpFilePath);
                stopWatch.Reset();
                stopWatch.Start();
                TestStatusContentFormat testStatusContentFormat = ReadBig5File(ftpFilePath, FileReadTesterStatus);
                stopWatch.Stop();
                ts2 = stopWatch.Elapsed;
                readTakeTime = Math.Round(Convert.ToDouble(ts2.TotalMilliseconds / 1000), 3);
                if (!string.IsNullOrEmpty(testStatusContentFormat.ErrMsg))
                {
                    return new ImportResult(2, testStatusContentFormat.ErrMsg);
                }
                if (testStatusContentFormat == null || testStatusContentFormat.Tester_device_info.Rows.Count < 1)
                {
                    Console.WriteLine("Tester Status 讀檔失敗:  " + filename);
                    writeToLog.WriteErrorLog("Tester Status  讀檔失敗: " + ftpFilePath);
                    MoveToError(ftpFilePath, errorPath);
                    return new ImportResult(2, "File content is missing. " + testStatusContentFormat.ErrMsg);
                }
                if (!testStatusContentFormat.CompareInfo())
                {
                    Console.WriteLine("Tester Status 之 information 欄位名稱不符:  " + filename);
                    writeToLog.WriteErrorLog("Tester Status 之 information 欄位名稱不符: " + ftpFilePath);
                    MoveToError(ftpFilePath, errorPath);
                    return new ImportResult(2, "Information field name not match.");
                }
                if (!testStatusContentFormat.CompareStatus())
                {
                    Console.WriteLine("Tester Status 之 tester_status 欄位名稱不符:  " + filename);
                    writeToLog.WriteErrorLog("Tester Status 之 tester_status 欄位名稱不符: " + ftpFilePath);
                    MoveToError(ftpFilePath, errorPath);
                    return new ImportResult(2, "tester_status field name not match.");
                }
                if (!dbKey.Equals(testStatusContentFormat.Tester_device_info.Rows[0]["DB_Key"].ToString()))
                {
                    writeToLog.WriteErrorLog("檔名與內容的DB_Key不相符: " + ftpFilePath);
                    MoveToError(ftpFilePath, errorPath);
                    return new ImportResult(2, "The filename does not match the DB_Key in the content.");
                }
                isDBKeyExist = fileAccess.IsDBKeyExistInDB("tester_device_info", testStatusContentFormat.Tester_device_info.Rows[0]["DB_Key"].ToString(), DatabaseService);
                if (isDBKeyExist)
                {
                    Console.WriteLine("資料庫已存在此資料: Tester Status     檔名:" + filename);
                    writeToLog.WriteToDataImportLog("資料庫已存在此資料: " + ftpFilePath);
                    MoveToError(ftpFilePath, errorPath);
                    return new ImportResult(3, "The same DB_Key exists in the database.");
                }
                else
                {
                    stopWatch.Reset();
                    stopWatch.Start();
                    await Task.Run(() =>
                    {
                        import_result = fileAccess.ImportTesterStatus(testStatusContentFormat, DatabaseService);
                    }).ConfigureAwait(false);
                    stopWatch.Stop();
                    ts2 = stopWatch.Elapsed;
                    importTakeTime = Math.Round(Convert.ToDouble(ts2.TotalMilliseconds / 1000), 3);
                    string dateStr = DateTime.Now.ToString("yyyyMMdd");
                    string checkLogFileName = "DCT_data_check_log_tester_" + dateStr + ".csv";
                    // 寫入 file name, file size, import time, read file take time, import take time
                    writeToLog.WriteToCheckLog(checkLogFileName, filename + "," + FormatFileSize(fileSize) + "," + DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + "," + readTakeTime.ToString() + "," + importTakeTime.ToString());
                    if (import_result)
                    {
                        Console.WriteLine("匯入完成! Tester Status   檔名: " + filename + "    耗時: " + Convert.ToInt32(ts2.TotalMilliseconds / 1000).ToString() + " 秒");
                        // 刪除已存在的的CSV檔案
                        deleteStatus = CompleteSuccess(ftpFilePath);
                        LogImportSuccess(writeToLog, "TesterStatus", dbKey, filename, importTakeTime, deleteStatus);
                    }
                    else
                    {
                        Console.WriteLine("匯入失敗: Tester Status " + filename);
                        writeToLog.WriteErrorLog("匯入失敗: " + ftpFilePath);
                        MoveToError(ftpFilePath, errorPath);
                        return new ImportResult(3, "Import failed.");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog writeToLogService = new WriteToLog();
                writeToLogService.WriteErrorLog($"[Tester 匯入] 處理異常: {filename}, 錯誤: {ex.Message}");
                Console.WriteLine(ex.Message);
                Console.WriteLine(MoveToError(ftpFilePath, errorPath));
                return new ImportResult(3, "Exception error occurred during reading and import. " + ex.Message);
            }
            GC.Collect();
            return new ImportResult(1, string.Empty);
        }
        public TestStatusContentFormat FileReadTesterStatus(StreamReader reader)
        {
            TestStatusContentFormat testStatusContentFormat = new TestStatusContentFormat();
            try
            {
                int content_part = 1;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (!string.IsNullOrEmpty(line) && IsChinese(line))
                    {
                        testStatusContentFormat.ErrMsg = "Chinese word exists.";
                    }
                    var values = EraseSpecificChar(line);
                    if (values.Length < 1) continue;
                    if (values[0] == "Device information")
                    {
                        content_part = 1;
                        continue;
                    }
                    else if (values[0] == "Tester status")
                    {
                        content_part = 2;
                        continue;
                    }
                    else if (values[0] == "SW version")
                    {
                        content_part = 3;
                        continue;
                    }
                    else if (values[0] == "Production analysis")
                    {
                        content_part = 4;
                        continue;
                    }
                    switch (content_part)
                    {
                        case 1:
                            if (values[0] == "DB_Key")
                            {
                                for (int i = 0; i < values.Length; i++)
                                {
                                    testStatusContentFormat.Tester_device_info.Columns.Add(values[i], typeof(string));
                                }
                            }
                            else if (testStatusContentFormat.Tester_device_info.Columns.Count < 1)
                            {
                                return null;
                            }
                            else
                            {
                                DataRow dr_tester_device_info = testStatusContentFormat.Tester_device_info.NewRow();
                                for (int i = 0; i < testStatusContentFormat.Tester_device_info.Columns.Count; i++)
                                {
                                    dr_tester_device_info[i] = values[i];
                                }
                                testStatusContentFormat.Tester_device_info.Rows.Add(dr_tester_device_info);
                            }
                            break;
                        case 2:
                            if (values[0] == "DPW")
                            {
                                for (int i = 0; i < values.Length; i++)
                                {
                                    testStatusContentFormat.Tester_status.Columns.Add(values[i], typeof(string));
                                }
                            }
                            else
                            {
                                DataRow dr_tester_status = testStatusContentFormat.Tester_status.NewRow();
                                for (int i = 0; i < values.Length; i++)
                                {
                                    dr_tester_status[i] = values[i];
                                }
                                testStatusContentFormat.Tester_status.Rows.Add(dr_tester_status);
                            }
                            break;
                        case 3:
                            if (values[0] == "PUI version")
                            {
                                for (int i = 0; i < values.Length; i++)
                                {
                                    testStatusContentFormat.Tester_sw_version.Columns.Add(values[i], typeof(string));
                                }
                            }
                            else
                            {
                                DataRow dr_tester_sw_version = testStatusContentFormat.Tester_sw_version.NewRow();
                                for (int i = 0; i < values.Length; i++)
                                {
                                    dr_tester_sw_version[i] = values[i];
                                }
                                testStatusContentFormat.Tester_sw_version.Rows.Add(dr_tester_sw_version);
                            }
                            break;
                        case 4:
                            if (values[0] == "site1_yield")
                            {
                                for (int i = 0; i < values.Length; i++)
                                {
                                    testStatusContentFormat.Tester_production_analysis.Columns.Add(values[i], typeof(string));
                                }
                            }
                            else
                            {
                                DataRow dr_tester_production_analysis = testStatusContentFormat.Tester_production_analysis.NewRow();
                                for (int i = 0; i < values.Length; i++)
                                {
                                    dr_tester_production_analysis[i] = values[i];
                                }
                                testStatusContentFormat.Tester_production_analysis.Rows.Add(dr_tester_production_analysis);
                                return testStatusContentFormat;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog writeToLogService = new WriteToLog();
                writeToLogService.WriteErrorLog($"[FileReadTesterStatus] 讀檔內容錯誤, 錯誤: {ex.Message}");
                Console.WriteLine(ex.Message);
                testStatusContentFormat.ErrMsg = "讀檔內容錯誤, Eroror:" + ex.Message;
                return null;
            }
            return testStatusContentFormat;
        }
    }
}

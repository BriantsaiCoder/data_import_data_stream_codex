using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using static DCT_data_import.DbObject;
namespace DCT_data_import.ReadAndImport
{
    public class UiStatus : ImportData
    {
        public ImportResult ReadAndImportUIStatus(FileProcess fileAccess, DatabaseService DatabaseService, string dbKeyUiStatus)
        {
            bool import_result = false;
            WriteToLog writeToLog = new WriteToLog();
            string deleteStatus;
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts2 = stopWatch.Elapsed;
            double importTakeTime = 0;
            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();
            // 檢查FTP是否有此檔案
            string filename = "ui_status_" + dbKeyUiStatus + ".csv";
            string ftpFilePath = GetFilePath("uistatus", dbKeyUiStatus);
            string errorPath = GetErrorPath("uistatus", dbKeyUiStatus);
            bool isFileExist = FileExists(ftpFilePath);
            if (!isFileExist)
            {
                Console.WriteLine("UI Status File not found:  " + filename);
                writeToLog.WriteToDataImportLog("UI Status File not found: " + ftpFilePath);
                return new ImportResult(0, "File not found.");
            }
            try
            {
                import_result = false;
                UIStatusContentFormat uiStatusContentFormat = ReadBig5File(ftpFilePath, FileReadUIStatus);
                if (!string.IsNullOrEmpty(uiStatusContentFormat.ErrMsg))
                {
                    return new ImportResult(2, uiStatusContentFormat.ErrMsg);
                }
                if (uiStatusContentFormat == null)
                {
                    Console.WriteLine("UI Status 讀取失敗: " + filename);
                    writeToLog.WriteToDataImportLog("UI Status 讀取失敗:" + ftpFilePath);
                    MoveToError(ftpFilePath, errorPath);
                    return new ImportResult(2, "File content is missing.");
                }
                if (!uiStatusContentFormat.CompareUiStatus())
                {
                    Console.WriteLine("UI Status 之 ui_status 欄位名稱不符: " + filename);
                    writeToLog.WriteToDataImportLog("UI Status 之 ui_status 欄位名稱不符:" + ftpFilePath);
                    MoveToError(ftpFilePath, errorPath);
                    return new ImportResult(2, "ui_status field name not match.");
                }
                stopWatch.Reset();
                stopWatch.Start();
                import_result = fileAccess.ImportUIStatus(uiStatusContentFormat, DatabaseService);
                stopWatch.Stop();
                ts2 = stopWatch.Elapsed;
                importTakeTime = Math.Round(Convert.ToDouble(ts2.TotalMilliseconds / 1000), 3);
                if (import_result)
                {
                    Console.WriteLine("匯入完成! UI Status " + filename + "    耗時: " + Convert.ToInt32(ts2.TotalMilliseconds / 1000).ToString() + " 秒");
                    // 刪除已存在的的CSV檔案
                    deleteStatus = CompleteSuccess(ftpFilePath);
                    LogImportSuccess(writeToLog, "UiStatus", dbKeyUiStatus, filename, importTakeTime, deleteStatus);
                }
                else
                {
                    Console.WriteLine("匯入失敗: UI Status " + filename);
                    writeToLog.WriteToDataImportLog("匯入失敗:" + ftpFilePath);
                    MoveToError(ftpFilePath, errorPath);
                    return new ImportResult(3, "Import failed.");
                }
                Thread.Sleep(500);
            }
            catch (Exception ex)
            {
                WriteToLog writeToLogService = new WriteToLog();
                writeToLogService.WriteErrorLog($"[UI Status匯入] 處理異常: {filename}, 錯誤: {ex.Message}");
                Console.WriteLine(ex.Message);
                MoveToError(ftpFilePath, errorPath);
                return new ImportResult(3, "Exception error occurred during import. " + ex.Message);
            }
            GC.Collect();
            return new ImportResult(1, string.Empty);
        }
        public UIStatusContentFormat FileReadUIStatus(StreamReader reader)
        {
            UIStatusContentFormat uiStatusContentFormat = new UIStatusContentFormat();
            try
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = EraseSpecificChar(line);
                    if (values.Length < 1) continue;
                    if (values[0] == "Mac_Address")
                    {
                        for (int i = 0; i < values.Length; i++)
                        {
                            uiStatusContentFormat.UI_status.Columns.Add(values[i], typeof(string));
                        }
                    }
                    else
                    {
                        DataRow dr_UI_status = uiStatusContentFormat.UI_status.NewRow();
                        for (int i = 0; i < values.Length; i++)
                        {
                            dr_UI_status[i] = values[i];
                        }
                        uiStatusContentFormat.UI_status.Rows.Add(dr_UI_status);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog writeToLogService = new WriteToLog();
                writeToLogService.WriteErrorLog($"[FileReadUIStatus] 檔案讀取失敗, 錯誤: {ex.Message}");
                uiStatusContentFormat.ErrMsg = ex.Message;
                Console.WriteLine(ex.Message);
                return null;
            }
            return uiStatusContentFormat;
        }
    }
}

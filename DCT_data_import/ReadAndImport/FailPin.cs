using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using DCT_data_import.Common;
using DCT_data_import.DbApi;
using DCT_data_import.FileAccess;
using static DCT_data_import.DbApi.DbObject;
namespace DCT_data_import.ReadAndImport
{
    public class FailPin : ImportData
    {
        public ImportResult ReadAndImportFailPinLog(FileProcess fileAccess, DatabaseService DatabaseService, string dbKey)
        {
            bool import_result = false, isDBKeyExist = false;
            WriteToLog writeToLog = new WriteToLog();
            string deleteStatus;
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts2 = stopWatch.Elapsed;
            double readTakeTime = 0, importTakeTime = 0;
            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();
            string filename = "fail_pin_" + dbKey + ".csv";
            string ftpFilePath = GetFilePath("failpin", dbKey);
            string errorPath = GetErrorPath("failpin", dbKey);
            try
            {
                // 檢查FTP是否有此檔案
                bool isFileExist = FileExists(ftpFilePath);
                if (!isFileExist)
                {
                    Console.WriteLine("Fail Pin Log File not found:  " + filename);
                    writeToLog.WriteErrorLog("Fail Pin Log File not found: " + ftpFilePath);
                    return new ImportResult(0, "File not found.");
                }
                long fileSize = GetFileLength(ftpFilePath);
                stopWatch.Reset();
                stopWatch.Start();
                FailPinLogContentFormat failPinLogContent = ReadBig5File(ftpFilePath, FileReadFailPinLog);
                stopWatch.Stop();
                ts2 = stopWatch.Elapsed;
                readTakeTime = Math.Round(Convert.ToDouble(ts2.TotalMilliseconds / 1000), 3);
                if (!string.IsNullOrEmpty(failPinLogContent.ErrMsg))
                {
                    return new ImportResult(2, failPinLogContent.ErrMsg);
                }
                // 讀取失敗或沒有資料
                if (failPinLogContent == null || failPinLogContent.Fail_pin_rate_info.Rows.Count < 1)
                {
                    Console.WriteLine("Fail Pin Log 讀取失敗:  " + filename);
                    writeToLog.WriteErrorLog("Fail Pin Log 讀取失敗: " + ftpFilePath);
                    MoveToError(ftpFilePath, errorPath);
                    return new ImportResult(2, "File content is missing. ");
                }
                if (!failPinLogContent.CompareInfo())
                {
                    Console.WriteLine("Fail Pin Log 之 information 欄位名稱不符:  " + filename);
                    writeToLog.WriteErrorLog("Fail Pin Log 之 information 欄位名稱不符: " + ftpFilePath);
                    MoveToError(ftpFilePath, errorPath);
                    return new ImportResult(2, "Information field name not match.");
                }
                isDBKeyExist = fileAccess.IsDBKeyExistInDB("fail_pin_rate_info", failPinLogContent.Fail_pin_rate_info.Rows[0][CsvColumnNames.DbKeyWithSpace].ToString(), DatabaseService);
                if (isDBKeyExist)
                {
                    Console.WriteLine("資料庫已存在此資料: Fail Pin   檔名:" + filename);
                    MoveToError(ftpFilePath, errorPath);
                    return new ImportResult(3, "The same DB_Key exists in the database.");
                }
                else
                {
                    stopWatch.Reset();
                    stopWatch.Start();
                    import_result = fileAccess.ImportFailPinLog(failPinLogContent, DatabaseService);
                    stopWatch.Stop();
                    ts2 = stopWatch.Elapsed;
                    importTakeTime = Math.Round(Convert.ToDouble(ts2.TotalMilliseconds / 1000), 3);
                    string dateStr = DateTime.Now.ToString("yyyyMMdd");
                    string checkLogFileName = "DCT_data_check_log_failPin_" + dateStr + ".csv";
                    // 寫入 file name, file size, import time, read file take time, import take time
                    writeToLog.WriteToCheckLog(checkLogFileName, filename + "," + FormatFileSize(fileSize) + "," + DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + "," + readTakeTime.ToString() + "," + importTakeTime.ToString());
                    if (import_result)
                    {
                        Console.WriteLine("匯入完成! Fail Pin      檔名:" + filename + "    耗時: " + Convert.ToInt32(ts2.TotalMilliseconds / 1000).ToString() + " 秒");
                        // 刪除完成的CSV檔案
                        deleteStatus = CompleteSuccess(ftpFilePath);
                        LogImportSuccess(writeToLog, "FailPin", dbKey, filename, importTakeTime, deleteStatus);
                    }
                    else
                    {
                        Console.WriteLine("匯入失敗: Fail Pin " + filename);
                        writeToLog.WriteErrorLog("匯入失敗:" + ftpFilePath);
                        MoveToError(ftpFilePath, errorPath);
                        return new ImportResult(3, "Import failed.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                writeToLog.WriteErrorLog(ex.Message);
                MoveToError(ftpFilePath, errorPath);
                return new ImportResult(3, "Exception error occurred during import.");
            }
            //Console.WriteLine("Fail pin log end~");
            return new ImportResult(1, string.Empty);
        }
        public FailPinLogContentFormat FileReadFailPinLog(StreamReader reader)
        {
            FailPinLogContentFormat failPinLogContentFormat = new FailPinLogContentFormat();
            try
            {
                string data_format = string.Empty;
                int content_part = 1;
                int fail_pin_list_id = 0;
                bool hasSnNum = false;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = EraseSpecificChar(line);
                    if (values == null)
                    {
                        continue;
                    }
                    if (values.Length < 1) continue;
                    // 看到關鍵字 "Data format" ，取得pin/ball類型
                    if (values[0] == "Data format")
                    {
                        data_format = values[1];
                    }
                    // 看到關鍵字 "DUT"
                    if (values[0] == "DUT")
                    {
                        content_part = 2;
                        // 判斷第二欄是否為 "SN Num" 來確定格式
                        if (values.Length > 1 && values[1] == "SN Num")
                        {
                            hasSnNum = true;
                            failPinLogContentFormat.HasSnNum = true;
                        }
                        else
                        {
                            hasSnNum = false;
                            failPinLogContentFormat.HasSnNum = false;
                        }
                        continue;
                    }
                    // fail pin rate的上半部分
                    if (content_part == 1)
                    {
                        failPinLogContentFormat.Fail_pin_rate_info.Columns.Add(values[0], typeof(string));
                        failPinLogContentFormat.Fail_pin_rate_info.Rows[0][values[0]] = (values.Length > 1) ? values[1] : string.Empty;
                    }
                    // fail pin rate的下半部分
                    else if (content_part == 2)
                    {
                        // 舊格式至少需要 3 欄 (DUT, Site, Fail Type)
                        // 新格式至少需要 4 欄 (DUT, SN Num, Site, Fail Type)
                        int minColumns = hasSnNum ? 4 : 3;
                        if (values.Length >= minColumns)
                        {
                            DataRow dr_fail_pin_rate_list = failPinLogContentFormat.Fail_pin_rate_list.NewRow();
                            int dataStartIndex; // fail pin 資料開始的索引位置
                            if (hasSnNum)
                            {
                                // 新格式:  DUT, SN Num, Site, Fail Type, ...
                                dr_fail_pin_rate_list["dut"] = values[0];
                                dr_fail_pin_rate_list["sn_num"] = values[1];
                                dr_fail_pin_rate_list["site"] = values[2];
                                dr_fail_pin_rate_list["fail_type"] = values[3];
                                dataStartIndex = 4;
                            }
                            else
                            {
                                // 舊格式: DUT, Site, Fail Type, ...
                                dr_fail_pin_rate_list["dut"] = values[0];
                                dr_fail_pin_rate_list["sn_num"] = string.Empty; // 舊格式無 SN Num，設為空字串
                                dr_fail_pin_rate_list["site"] = values[1];
                                dr_fail_pin_rate_list["fail_type"] = values[2];
                                dataStartIndex = 3;
                            }
                            failPinLogContentFormat.Fail_pin_rate_list.Rows.Add(dr_fail_pin_rate_list);
                            // 讀取 fail pin 與 log 存到 List
                            int fail_pin_part = 1;
                            List<string> fail_pin_list = new List<string>();
                            List<string> fail_pin_log = new List<string>();
                            DataTable test_result_dt = InitDtFailPinTestResult();
                            int row_index = -1, column_index = 0;
                            for (int i = dataStartIndex; i < values.Length; i++)
                            {
                                // 以 ';' 分開fail pin log 與 remark  ，2024/3/1 新增以@分開test result
                                if (values[i] == ";")
                                {
                                    fail_pin_part = 2;
                                    continue;
                                }
                                else if (values[i] == "@")
                                {
                                    fail_pin_part = 3;
                                    test_result_dt.Rows.Add(test_result_dt.NewRow());
                                    row_index++;
                                    column_index = 0;
                                    continue;
                                }
                                if (fail_pin_part == 1)
                                {
                                    fail_pin_list.Add(values[i]);
                                }
                                else if (fail_pin_part == 2)
                                {
                                    fail_pin_log.Add(values[i]);
                                }
                                else if (fail_pin_part == 3)
                                {
                                    if (column_index > 0) // column_index:     0-item_name    1-open    2-short    3-vmeas
                                    {
                                        double tmp_val = 0;
                                        if (!double.TryParse(values[i], out tmp_val))
                                        {
                                            values[i] = null;
                                        }
                                    }
                                    test_result_dt.Rows[row_index][column_index] = values[i];
                                    column_index++;
                                }
                            }
                            failPinLogContentFormat.Fail_pin_rate_list_test_result.Tables.Add(test_result_dt);
                            fail_pin_list_id++;
                            //  將 fail pin 與 log 存到 DataTable
                            for (int i = 0; i < fail_pin_list.Count; i++)
                            {
                                DataRow dr_fail_pin_rate_list_pin_ball = failPinLogContentFormat.Fail_pin_rate_list_pin_ball.NewRow();
                                string[] value_split = fail_pin_list[i].Split('(', ')');
                                if (data_format == "Pin")
                                {
                                    dr_fail_pin_rate_list_pin_ball["pin"] = value_split[0];
                                    dr_fail_pin_rate_list_pin_ball["ball"] = value_split[1];
                                }
                                else if (data_format == "Ball")
                                {
                                    dr_fail_pin_rate_list_pin_ball["ball"] = value_split[0];
                                    dr_fail_pin_rate_list_pin_ball["pin"] = value_split[1];
                                }
                                dr_fail_pin_rate_list_pin_ball["fail_pin_rate_list_id"] = fail_pin_list_id;
                                dr_fail_pin_rate_list_pin_ball["remark"] = String.Join(",", fail_pin_log.ToArray());
                                failPinLogContentFormat.Fail_pin_rate_list_pin_ball.Rows.Add(dr_fail_pin_rate_list_pin_ball);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog writeToLogService = new WriteToLog();
                writeToLogService.WriteErrorLog($"[FileReadFailPinLog] 讀檔內容錯誤, 錯誤: {ex.Message}");
                failPinLogContentFormat.ErrMsg = ex.Message;
                Console.WriteLine(ex.Message);
                return null;
            }
            return failPinLogContentFormat;
        }
        private DataTable InitDtFailPinTestResult()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("item_name", typeof(string));
            dt.Columns.Add("open", typeof(string));
            dt.Columns.Add("short", typeof(string));
            dt.Columns.Add("vmeas", typeof(string));
            return dt;
        }
    }
}

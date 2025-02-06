using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static DCT_data_import.ApiObject;
namespace DCT_data_import.ReadAndImport
{
    public class RecoveryRate : ImportData
    {        //public async Task<ImportResult> TestCallAsync()
        //{
        //    await Task.Run(() =>
        //    {
        //        Thread.Sleep(1000);
        //        //return new ImportResult(0, "test msg");
        //    }).ConfigureAwait(false);
        //    return new ImportResult(0, "test msg");
        //}
        public async Task<ImportResult> ReadAndImportRecoveryRateData(FileProcess fileAccess, WebApiClient webApiClient, string dbKey)
        {
            string ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            WriteToLog writeToLog = new WriteToLog();
            string deleteStatus;
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts2 = stopWatch.Elapsed;
            double readTakeTime = 0, importTakeTime = 0;
            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();
            //// 檢查FTP連線狀態
            //ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/";
            //bool isFtpConnected = isValidFtpConnection(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
            //if (!isFtpConnected)
            //    return new ImportResult(0, "FTP server connection failed.");
            // 檢查FTP是否有此檔案
            string filename = "Recovery_rate_" + dbKey + ".csv";
            string errorDir = string.Empty;
            ftpserver = "ftp://" + Program.FTP_IP;
            if (Program.Environment == "Dev")
            {
                ftpserver += "/DCT_Log/DCT_DB_DATA_Dev/Recovery_rate_data/" + filename;
                errorDir = "/DCT_Log/DCT_DB_DATA_Dev/Recovery_rate_data_Error/";
            }
            else if (Program.Environment == "Prod")
            {
                ftpserver += "/DCT_Log/DCT_DB_DATA/Recovery_rate_data/" + filename;
                errorDir = "/DCT_Log/DCT_DB_DATA/Recovery_rate_data_Error/";
            }
            //ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/Recovery_rate_data/" + filename;
            bool isFileExist = CheckIfFileExistsOnServer(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
            if (!isFileExist)
                return new ImportResult(0, "File not found.");
            //// 確認 pool 連線狀態
            //bool isConnect = webApiClient.checkDBConnect(Program.POOL_NAME);
            //if (!isConnect) // 沒有pool連線資訊，則建立一個新的連線。如果建立pool失敗就中斷程式
            //    if (!createPool(webApiClient, writeToLog))
            //        return new ImportResult(0, "MySQL database connection failed.");
            // 開始讀檔與匯入
            try
            {
                bool import_result = false;
                bool isDBKeyExist = false;
                // 取得編碼格式
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
                response = (FtpWebResponse)reqFTP.GetResponse();
                responseStream = response.GetResponseStream();
                reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));
                long fileSize = GetFileSize(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
                stopWatch.Reset();
                stopWatch.Start();
                RecoveryRateDataContentFormat recoveryRateDataContentFormat = FileReadRecoveryRateData(reader);
                reader.Close();
                stopWatch.Stop();
                ts2 = stopWatch.Elapsed;
                readTakeTime = Math.Round(Convert.ToDouble(ts2.TotalMilliseconds / 1000), 3);
                //if (rawDataContentFormat.lotInfo.Rows[0]["Customer"].ToString() != "TSMC") return null;
                if (!string.IsNullOrEmpty(recoveryRateDataContentFormat.ErrMsg))
                {
                    return new ImportResult(2, recoveryRateDataContentFormat.ErrMsg);
                }
                if (recoveryRateDataContentFormat == null || recoveryRateDataContentFormat.LotInfo.Rows.Count < 1)
                {
                    Console.WriteLine("Recovery Rate 讀檔失敗:  " + filename);
                    writeToLog.WriteToDataImportLog("Recovery Rate  讀檔失敗: " + ftpserver);
                    RenameFile(ftpserver, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    //RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV_/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                    return new ImportResult(2, "File content is missing. " + recoveryRateDataContentFormat.ErrMsg);
                }
                if (!recoveryRateDataContentFormat.CompareInfo())
                {
                    Console.WriteLine("Recovery Rate 之 information 欄位名稱不符:  " + filename);
                    writeToLog.WriteToDataImportLog("Recovery Rate 之 information 欄位名稱不符:" + ftpserver);
                    RenameFile(ftpserver, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    //RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV_/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                    return new ImportResult(2, "Information field name not match.");
                }
                if (!recoveryRateDataContentFormat.CompareRecoveryRate())
                {
                    Console.WriteLine("Recovery Rate 之 data 欄位名稱不符:  " + filename);
                    writeToLog.WriteToDataImportLog("Recovery Rate 之 data 欄位名稱不符:" + ftpserver);
                    RenameFile(ftpserver, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    //RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV_/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                    return new ImportResult(2, "Recovery data field name not match.");
                }
                string queryDbKey = recoveryRateDataContentFormat.LotInfo.Rows[0]["DB Key"].ToString();
                Console.WriteLine("dbkey =" + queryDbKey);
                if (!dbKey.Equals(recoveryRateDataContentFormat.LotInfo.Rows[0]["DB Key"].ToString()))
                {
                    writeToLog.WriteToDataImportLog("檔名與內容的DB_Key不相符: " + ftpserver);
                    RenameFile(ftpserver, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(2, "The filename does not match the DB_Key in the content.");
                }
                //  DB_Key是否已存在於資料庫
                isDBKeyExist = fileAccess.IsDBKeyExistInDB("recovery_rate", recoveryRateDataContentFormat.LotInfo.Rows[0]["DB Key"].ToString(), webApiClient);
                if (isDBKeyExist)
                {
                    Console.WriteLine("資料庫已存在此資料:  " + "   檔名:" + filename);
                    writeToLog.WriteToDataImportLog("資料庫已存在此資料:  " + "   檔名:" + filename);
                    RenameFile(ftpserver, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    //// Kerwin 的電腦
                    //if (macid == "94C6913F94BD")
                    //{
                    //    // 下載檔案到本地端
                    //    //downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\raw_data_temp\" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    //}
                    ////// 刪除已存在的的CSV檔案
                    ////deleteStatus = DeleteFile(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(3, "The same DB_Key exists in the database.");
                }
                else
                {
                    stopWatch.Reset();
                    stopWatch.Start();
                    // 開始匯入
                    await Task.Run(() =>
                    {
                        import_result = fileAccess.ImportRecoveryData(recoveryRateDataContentFormat, webApiClient);
                    }).ConfigureAwait(false);
                    stopWatch.Stop();
                    ts2 = stopWatch.Elapsed;
                    importTakeTime = Math.Round(Convert.ToDouble(ts2.TotalMilliseconds / 1000), 3);
                    string dateStr = DateTime.Now.ToString("yyyyMMdd");
                    string checkLogFileName = "DCT_data_check_log_recoveryRate_" + dateStr + ".csv";
                    // 寫入 file name, file size, import time, read file take time, import take time
                    writeToLog.WriteToCheckLog(checkLogFileName, filename + "," + FormatFileSize(fileSize) + "," + DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + "," + readTakeTime.ToString() + "," + importTakeTime.ToString());
                    if (import_result)
                    {
                        //Console.WriteLine("匯入完成! Raw data    比對: " + compare_result + "   檔名:" + list_filename[i]);
                        Console.WriteLine("匯入完成! Raw data    檔名:" + filename + "    耗時: " + Convert.ToInt32(ts2.TotalMilliseconds / 1000).ToString() + " 秒");
                        //// Kerwin 的電腦
                        //if (macid == "94C6913F94BD")
                        //{
                        //    // 下載檔案到本地端
                        //    downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\raw_data_temp\" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                        //}
                        // 刪除已存在的的CSV檔案
                        deleteStatus = DeleteFile(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
                        reader.Close();
                        response.Close();
                        //return new ImportResult(1, "");
                    }
                    else
                    {
                        Console.WriteLine("匯入失敗: Raw data " + filename);
                        writeToLog.WriteToDataImportLog("匯入失敗:" + ftpserver);
                        RenameFile(ftpserver, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                        reader.Close();
                        response.Close();
                        return new ImportResult(3, "Import failed.");
                    }
                }
            }
            catch (Exception)
            {
                //Console.WriteLine(ftpserver + ": " + ex.ToString());
                //writeToLog.writeToDataImportLog(ftpserver + ": " + ex.ToString());
                RenameFile(ftpserver, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                return new ImportResult(3, "Exception error occurred during import.");
            }
            GC.Collect();
            //Console.WriteLine("Raw data end~");
            return new ImportResult(1, "");
        }
        private RecoveryRateDataContentFormat FileReadRecoveryRateData(StreamReader reader)
        {
            RecoveryRateDataContentFormat fileContentFormat = new RecoveryRateDataContentFormat();
            try
            {
                bool isBasicInfo = true;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim(); // 去除行首尾的空白
                    if (string.IsNullOrEmpty(line)) continue; // 跳過空行
                    string[] values = line.Split(',').Select(v => v.Trim()).ToArray(); // 分割並去除每個值的首尾空白
                    if (values[0] == "Test_Item")
                    {
                        isBasicInfo = false;
                        // 使用 CSV 中的標題作為 TestResults 的列名
                        foreach (string columnName in values)
                        {
                            fileContentFormat.LotRecoveryRate.Columns.Add(columnName, typeof(string));
                        }
                        continue;
                    }
                    if (isBasicInfo)
                    {
                        fileContentFormat.LotInfo.Columns.Add(values[0], typeof(string));
                        fileContentFormat.LotInfo.Rows[0][values[0]] = values[1];
                    }
                    else
                    {
                        fileContentFormat.LotRecoveryRate.Rows.Add(values);
                    }
                }
                // 添加 datatable1 的所有列
                foreach (DataColumn col in fileContentFormat.LotInfo.Columns)
                {
                    fileContentFormat.FinalRecoveryRateTable.Columns.Add(col.ColumnName, col.DataType);
                }
                // 添加 datatable2 的所有列
                foreach (DataColumn col in fileContentFormat.LotRecoveryRate.Columns)
                {
                    fileContentFormat.FinalRecoveryRateTable.Columns.Add(col.ColumnName, col.DataType);
                }
                // 合併資料
                foreach (DataRow row1 in fileContentFormat.LotInfo.Rows)
                {
                    foreach (DataRow row2 in fileContentFormat.LotRecoveryRate.Rows)
                    {
                        DataRow newRow = fileContentFormat.FinalRecoveryRateTable.NewRow();
                        foreach (DataColumn col in fileContentFormat.LotInfo.Columns)
                        {
                            newRow[col.ColumnName] = row1[col.ColumnName];
                        }
                        foreach (DataColumn col in fileContentFormat.LotRecoveryRate.Columns)
                        {
                            newRow[col.ColumnName] = row2[col.ColumnName];
                        }
                        fileContentFormat.FinalRecoveryRateTable.Rows.Add(newRow);
                        //// 印出剛剛添加的行
                        //Console.WriteLine("新添加的行資料：");
                        //foreach (DataColumn column in fileContentFormat.FinalRecoveryRateTable.Columns)
                        //{
                        //    Console.WriteLine($"{column.ColumnName}: {newRow[column]}");
                        //}
                        //Console.WriteLine(); // 添加空行以區分不同的行
                        //Console.ReadLine(); // 暫停程式以便查看結果
                    }
                }
                //// 印出 LotInfo 的內容
                //Console.WriteLine("LotInfo 內容:");
                //foreach (DataColumn column in fileContentFormat.LotInfo.Columns)
                //{
                //    Console.Write($"{column.ColumnName}\t");
                //}
                //Console.WriteLine();
                //foreach (DataRow row in fileContentFormat.LotInfo.Rows)
                //{
                //    foreach (var item in row.ItemArray)
                //    {
                //        Console.Write($"{item}\t");
                //    }
                //    Console.WriteLine();
                //}
                //Console.ReadLine();
                //// 印出 LotRecoveryRate 的內容
                //Console.WriteLine("\nLotRecoveryRate 內容:");
                //foreach (DataColumn column in fileContentFormat.LotRecoveryRate.Columns)
                //{
                //    Console.Write($"{column.ColumnName}\t");
                //}
                //Console.WriteLine();
                //foreach (DataRow row in fileContentFormat.LotRecoveryRate.Rows)
                //{
                //    foreach (var item in row.ItemArray)
                //    {
                //        Console.Write($"{item}\t");
                //    }
                //    Console.WriteLine();
                //}
                //Console.ReadLine();
                //// 顯示 FinalRecoveryRateTable 的內容
                //Console.WriteLine("FinalRecoveryRateTable 的內容如下：");
                //foreach (DataColumn col in fileContentFormat.FinalRecoveryRateTable.Columns)
                //{
                //    Console.Write($"{col.ColumnName}\t");
                //}
                //Console.WriteLine();
                //foreach (DataRow row in fileContentFormat.FinalRecoveryRateTable.Rows)
                //{
                //    foreach (var item in row.ItemArray)
                //    {
                //        Console.Write($"{item}\t");
                //    }
                //    Console.WriteLine();
                //    Console.ReadLine();
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                fileContentFormat.ErrMsg = "讀檔內容錯誤";
                return null;
            }
            return fileContentFormat;
        }
    }
}
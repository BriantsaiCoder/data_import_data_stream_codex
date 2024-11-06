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
            //String ftpserver;
            //FtpWebRequest reqFTP;
            //FtpWebResponse response;
            //Stream responseStream;
            //StreamReader reader;
            //WriteToLog writeToLog = new WriteToLog();
            //Stopwatch stopWatch = new Stopwatch();
            //TimeSpan ts2 = stopWatch.Elapsed;
            //double readTakeTime = 0;
            ////抓mac id
            //NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            //string macid = nics[0].GetPhysicalAddress().ToString();
            ////// 檢查FTP連線狀態
            ////ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/";
            ////bool isFtpConnected = isValidFtpConnection(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
            ////if (!isFtpConnected)
            ////    return new ImportResult(0, "FTP server connection failed.");
            //// 檢查FTP是否有此檔案
            //string filename = "Recovery_rate_" + dbKey + ".csv";
            ////ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/Recovery_rate_data/" + filename;
            //ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/Recovery_rate_data_bk/" + filename;
            //bool isFileExist = CheckIfFileExistsOnServer(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
            //if (!isFileExist)
            //    return new ImportResult(0, "File not found.");
            ////// 確認 pool 連線狀態
            ////bool isConnect = webApiClient.checkDBConnect(Program.POOL_NAME);
            ////if (!isConnect) // 沒有pool連線資訊，則建立一個新的連線。如果建立pool失敗就中斷程式
            ////    if (!createPool(webApiClient, writeToLog))
            ////        return new ImportResult(0, "MySQL database connection failed.");
            //// 開始讀檔與匯入
            //try
            //{
            //    //ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/" + filename;
            //    // 取得編碼格式
            //    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            //    reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
            //    response = (FtpWebResponse)reqFTP.GetResponse();
            //    responseStream = response.GetResponseStream();
            //    reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));
            //    long fileSize = GetFileSize(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
            //    stopWatch.Reset();
            //    stopWatch.Start();
            //    RecoveryRateDataContentFormat recoveryRateDataContentFormat = FileReadRecoveryRateData(reader);
            //    reader.Close();
            //    stopWatch.Stop();
            //    ts2 = stopWatch.Elapsed;
            //    readTakeTime = Math.Round(Convert.ToDouble(ts2.TotalMilliseconds / 1000), 3);
            //    //if (rawDataContentFormat.lotInfo.Rows[0]["Customer"].ToString() != "TSMC") return null;
            //    if (!string.IsNullOrEmpty(recoveryRateDataContentFormat.ErrMsg))
            //    {
            //        return new ImportResult(2, recoveryRateDataContentFormat.ErrMsg);
            //    }
            //    if (recoveryRateDataContentFormat == null || recoveryRateDataContentFormat.LotInfo.Rows.Count < 1)
            //    {
            //        Console.WriteLine("Raw data 讀檔失敗:  " + filename);
            //        writeToLog.WriteToDataImportLog("Raw data  讀檔失敗: " + ftpserver);
            //        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Recovery_rate_data_Error/" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
            //        //RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV_/" + list_filename[i], FTP_USER, FTP_PASSWORD);
            //        return new ImportResult(2, "File content is missing. " + recoveryRateDataContentFormat.ErrMsg);
            //    }
            //    if (!recoveryRateDataContentFormat.CompareInfo())
            //    {
            //        Console.WriteLine("Raw data 之 information 欄位名稱不符:  " + filename);
            //        writeToLog.WriteToDataImportLog("Raw data 之 information 欄位名稱不符:" + ftpserver);
            //        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Recovery_rate_data_Error/" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
            //        //RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV_/" + list_filename[i], FTP_USER, FTP_PASSWORD);
            //        return new ImportResult(2, "Information field name not match.");
            //    }
            //    if (!rawDataContentFormat.CompareStatistic())
            //    {
            //        Console.WriteLine("Raw data 之 statistic 欄位名稱不符:  " + filename);
            //        writeToLog.WriteToDataImportLog("Raw data 之 statistic 欄位名稱不符:" + ftpserver);
            //        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Recovery_rate_data_Error/" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
            //        //RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV_/" + list_filename[i], FTP_USER, FTP_PASSWORD);
            //        return new ImportResult(2, "Statistic field name not match.");
            //    }
            //    //if (!dbKey.Equals(recoveryRateDataContentFormat.LotInfo.Rows[0]["DB_Key"].ToString()))
            //    //{
            //    //    writeToLog.WriteToDataImportLog("檔名與內容的DB_Key不相符: " + ftpserver);
            //    //    RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
            //    //    return new ImportResult(2, "The filename does not match the DB_Key in the content.");
            //    //}
            //    ////  DB_Key是否已存在於資料庫
            //    //isDBKeyExist = fileAccess.IsDBKeyExistInDB("lots_info", recoveryRateDataContentFormat.LotInfo.Rows[0]["DB_Key"].ToString(), webApiClient);
            //    //if (isDBKeyExist)
            //    //{
            //    //    //compare_result = compareTool.compareRawData(rawDataContentFormat, webApiClient);
            //    //    Console.WriteLine("資料庫已存在此資料: Raw data 比對: " + compareResult + "   檔名:" + filename);
            //    //    ////writeToLog.writeToDataImportLog("資料庫已存在此資料:" + ftpserver);
            //    //    RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
            //    //    // Kerwin 的電腦
            //    //    if (macid == "94C6913F94BD")
            //    //    {
            //    //        // 下載檔案到本地端
            //    //        //downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\raw_data_temp\" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
            //    //    }
            //    //    //// 刪除已存在的的CSV檔案
            //    //    //deleteStatus = DeleteFile(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
            //    //    return new ImportResult(3, "The same DB_Key exists in the database.");
            //    //}
            //    //else
            //    //{
            //    //    //// 計算平方和
            //    //    //list_square_sum = calculateSPC.SquareSum(rawDataContentFormat);
            //    //    // 計算均方和
            //    //    avg2 = calculateSPC.AverageOfSumSquare(rawDataContentFormat);
            //    //    fileAccess.AddColumnForDataset(rawDataContentFormat.LotStatistic, "avg_2", avg2);
            //    //    stopWatch.Reset();
            //    //    stopWatch.Start();
            //    //    // 開始匯入
            //    //    await Task.Run(() =>
            //    //    {
            //    //        import_result = fileAccess.ImportRawData(rawDataContentFormat, webApiClient);
            //    //    }).ConfigureAwait(false);
            //    //    stopWatch.Stop();
            //    //    ts2 = stopWatch.Elapsed;
            //    //    importTakeTime = Math.Round(Convert.ToDouble(ts2.TotalMilliseconds / 1000), 3);
            //    //    //compare_result = compareTool.compareRawData(rawDataContentFormat, webApiClient);
            //    //    string dateStr = DateTime.Now.ToString("yyyyMMdd");
            //    //    string checkLogFileName = "DCT_data_check_log_rawData_" + dateStr + ".csv";
            //    //    // 寫入 file name, file size, import time, read file take time, import take time
            //    //    writeToLog.WriteToCheckLog(checkLogFileName, filename + "," + FormatFileSize(fileSize) + "," + DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + "," + readTakeTime.ToString() + "," + importTakeTime.ToString());
            //    //    if (import_result)
            //    //    {
            //    //        //Console.WriteLine("匯入完成! Raw data    比對: " + compare_result + "   檔名:" + list_filename[i]);
            //    //        Console.WriteLine("匯入完成! Raw data    檔名:" + filename + "    耗時: " + Convert.ToInt32(ts2.TotalMilliseconds / 1000).ToString() + " 秒");
            //    //        // Kerwin 的電腦
            //    //        if (macid == "94C6913F94BD")
            //    //        {
            //    //            // 下載檔案到本地端
            //    //            downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\raw_data_temp\" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
            //    //        }
            //    //        // 刪除已存在的的CSV檔案
            //    //        deleteStatus = DeleteFile(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
            //    //        reader.Close();
            //    //        response.Close();
            //    //        //return new ImportResult(1, "");
            //    //    }
            //    //    else
            //    //    {
            //    //        Console.WriteLine("匯入失敗: Raw data " + filename);
            //    //        writeToLog.WriteToDataImportLog("匯入失敗:" + ftpserver);
            //    //        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
            //    //        reader.Close();
            //    //        response.Close();
            //    //        return new ImportResult(3, "Import failed.");
            //    //    }
            //    //}
            //}
            //catch (Exception)
            //{
            //    //Console.WriteLine(ftpserver + ": " + ex.ToString());
            //    //writeToLog.writeToDataImportLog(ftpserver + ": " + ex.ToString());
            //    RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
            //    return new ImportResult(3, "Exception error occurred during import.");
            //}
            //GC.Collect();
            ////Console.WriteLine("Raw data end~");
            return new ImportResult(1, "");
        }
        private RecoveryRateDataContentFormat FileReadRecoveryRateData(StreamReader reader)
        {
            RecoveryRateDataContentFormat fileContentFormat = new RecoveryRateDataContentFormat();
            //try
            //{
            //    // 存放第二部分統計值的 Dictionary
            //    Dictionary<string, List<string>> statistic_dict = new Dictionary<string, List<string>>();
            //    // 存放第三部分  Serial, SN Num, SiteID, X, Y, HBIN, P/F
            //    List<List<string>> rawData_list = new List<List<string>>();
            //    // item 數量
            //    int item_count = 0;
            //    //FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            //    //StreamReader reader = new StreamReader(stream);
            //    int content_part = 1;
            //    // 第三部分  raw data的起始index
            //    int rawData_part_index = 0;
            //    string lines = reader.ReadToEnd();
            //    reader.Close();
            //    List<string> split_lines = lines.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            //    for (int r = 0; r < split_lines.Count; r++)
            //    {
            //        var line = split_lines[r].Trim();
            //        if (!string.IsNullOrEmpty(line) && IsChinese(line))
            //        {
            //            fileContentFormat.ErrMsg = "Chinese word exists.";
            //            //return new RawDataContentFormat("Chinese word exists.");
            //        }
            //        var values = line.Split(',', '\0', '\r', '\n');
            //        var values_tmp = values;
            //        // 去除空白值
            //        if (content_part != 3)
            //        {
            //            values_tmp = values.Where(x => !string.IsNullOrEmpty(x)).ToArray();
            //        }
            //        if (values_tmp.Length < 1) continue;
            //        //Console.WriteLine("values : " + values.Length);
            //        //Console.WriteLine(values[0]);
            //        // 看到關鍵字 "Serial"
            //        if (values[0] == "Serial")
            //        {
            //            content_part = 3;
            //        }
            //        // 第一部分  info
            //        if (content_part == 1)
            //        {
            //            // "Open fail"與"Short fail"為TSMC 客戶中兩個非必要欄位，故排除不存進資料庫
            //            if (values[0].Split(':')[0] == "Open fail" || values[0].Split(':')[0] == "Short fail") continue;
            //            fileContentFormat.LotInfo.Columns.Add(values[0].Split(':')[0], typeof(string));
            //            fileContentFormat.LotInfo.Rows[0][values[0].Split(':')[0]] = values[1];
            //            //fileContentFormat.setLotInfo(lotInfo, values[0], values[1]);
            //        }
            //        // 第二部分  statistic
            //        else if (content_part == 2)
            //        {
            //            // 找到第一個非空值的index
            //            for (int i = 0; i < values.Length; i++)
            //            {
            //                if (!string.IsNullOrEmpty(values[i]))
            //                {
            //                    // 將第一個非空值之前的所有空值砍掉
            //                    values = values.Where(s => values.ToList().IndexOf(s) >= i).ToArray();
            //                    // 前有7格空白表示在統計值區塊的 Item_NO 或 Item_name
            //                    if (i == 7)
            //                    {
            //                        if (values[0] == "1001")
            //                        {
            //                            statistic_dict.Add("Item No", values.ToList<string>());
            //                            item_count = values.Length;
            //                        }
            //                        else
            //                        {
            //                            // 只取到item數量為止，在後方的則是註記欄位，如 test time、index time
            //                            values = values.Where(s => values.ToList().IndexOf(s) < item_count).ToArray();
            //                            statistic_dict.Add("Item Name", values.ToList<string>());
            //                        }
            //                    }
            //                    else
            //                    {
            //                        // 屬於欄位: Force, Wait time, Spec MAX, Spec MIN, # of PASS, # of FAIL, MIN, MAX, AVG, STDEV, Cp, Cpk
            //                        var values_except_head = values.Where(x => x != values[0]).ToArray();
            //                        values_except_head = values_except_head.Where(s => values_except_head.ToList().IndexOf(s) < item_count).ToArray();
            //                        statistic_dict.Add(values[0], values_except_head.ToList<string>());
            //                    }
            //                    break;
            //                }
            //            }
            //        }
            //        // 第三部分  raw data
            //        else if (content_part == 3)
            //        {
            //            // unit 值填入 Dictionary
            //            if (values[0] == "Serial")
            //            {
            //                var values_unit = values.Where(s => values.ToList().IndexOf(s) >= 7).ToArray();
            //                values_unit = values_unit.Where(s => values_unit.ToList().IndexOf(s) < item_count).ToArray();
            //                statistic_dict.Add("unit", values_unit.ToList<string>());
            //            }
            //            int result_part = 1;  // 1.表示Serial,SN Num,SiteID,	X,Y,HBIN,P/F   2.表示 raw data值的部分含單位(V, uA,...)
            //            DataRow dr_lotResult = fileContentFormat.LotResult.NewRow();
            //            for (int i = 0; i < values.Length; i++)
            //            {
            //                // 欄位 Serial, SN Num, SiteID,	 X, Y, HBIN, P/F
            //                if (result_part == 1 && values[0] == "Serial")
            //                {
            //                    fileContentFormat.LotResult.Columns.Add(values[i], typeof(string));
            //                }
            //                // 欄位 Serial 為空的直接跳過
            //                else if (string.IsNullOrEmpty(values[0].Trim()))
            //                {
            //                    continue;
            //                }
            //                // 欄位 Serial, SN Num, SiteID,	 X, Y, HBIN, P/F 的 values
            //                else if (result_part == 1)
            //                {
            //                    // 解析'*'字號
            //                    if (i == 0 && values[i].Contains("*"))
            //                    {
            //                        dr_lotResult["retest_loc"] = "Y";
            //                        dr_lotResult[i] = values[i].Remove(0, 1);
            //                    }
            //                    else
            //                    {
            //                        dr_lotResult["retest_loc"] = "N";
            //                        dr_lotResult[i] = values[i];
            //                    }
            //                }
            //                // unit
            //                if (result_part == 2 && values[0] == "Serial")
            //                {
            //                    // 第三部分最右方 test time, index time, real time
            //                    if (i == rawData_part_index + item_count)
            //                    {
            //                        fileContentFormat.LotResult.Columns.Add("test time", typeof(string));
            //                        fileContentFormat.LotResult.Columns.Add("index time", typeof(string));
            //                        fileContentFormat.LotResult.Columns.Add("real time", typeof(string));
            //                        fileContentFormat.LotResult.Columns.Add("retest_loc", typeof(string));
            //                        break;
            //                    }
            //                    rawData_list.Add(new List<string>());
            //                }
            //                // raw data value的部分 先存入rawData_list
            //                else if (result_part == 2 && i < rawData_part_index + rawData_list.Count)
            //                {
            //                    // 讀值若含有小數點"."而沒有小數位，則移除小數點
            //                    if (values[i].Substring(values[i].Length - 1) == ".") values[i] = values[i].Substring(0, values[i].Length - 1);
            //                    // 去除數值包含(O)(S)的括號
            //                    if (values[i].Contains("(O)"))
            //                    {
            //                        values[i] = values[i].Replace("(O)", "");
            //                    }
            //                    if (values[i].Contains("(S)"))
            //                    {
            //                        values[i] = values[i].Replace("(S)", "");
            //                    }
            //                    rawData_list[i - rawData_part_index].Add(values[i]);
            //                }
            //                else if (result_part == 2 && i >= rawData_part_index + rawData_list.Count)
            //                {
            //                    if (i - rawData_list.Count >= fileContentFormat.LotResult.Columns.Count)
            //                    {
            //                        //break;
            //                        throw new ArgumentException("Read value column count greater than expected.");
            //                    }
            //                    dr_lotResult[i - rawData_list.Count] = values[i];
            //                }
            //                // 以"P/F"為分界
            //                if (values[i] == "P/F" || i == rawData_part_index - 1)
            //                {
            //                    result_part = 2;
            //                    rawData_part_index = i + 1;
            //                }
            //            }
            //            if (values[0] != "Serial")
            //            {
            //                fileContentFormat.LotResult.Rows.Add(dr_lotResult);
            //            }
            //        }
            //        // 看到關鍵字 "Stop"
            //        if (values[0] == "Stop:")
            //        {
            //            content_part = 2;
            //        }
            //    }
            //    // 將raw data values 填入統計值的表
            //    DataTable item_table = new DataTable();
            //    for (int i = 0; i < statistic_dict.Keys.Count; i++)
            //    {
            //        item_table.Columns.Add(statistic_dict.ElementAt(i).Key, typeof(string));
            //    }
            //    item_table.Columns.Add("net_name", typeof(string));
            //    item_table.Columns.Add("value", typeof(string));
            //    for (int i = 0; i < item_count; i++)
            //    {
            //        // Clone 方法建立的新 DataTable 不會包含任何 DataRows.，只有相同的Schema。
            //        DataTable item_table_tmp = item_table.Clone();
            //        DataRow dataRow = item_table_tmp.NewRow();
            //        // 對 dataRow 填入 統計值
            //        for (int j = 0; j < statistic_dict.Keys.Count; j++)
            //        {
            //            dataRow[statistic_dict.ElementAt(j).Key] = statistic_dict[statistic_dict.ElementAt(j).Key][i];
            //        }
            //        // 對 dataRow 填入 raw data list
            //        if (rawData_list.Count > 0)
            //        {
            //            string strJoin = String.Join(", ", rawData_list[i].ToArray());
            //            if (string.IsNullOrEmpty(strJoin.Trim()))
            //            {
            //                dataRow["value"] = "[]";
            //            }
            //            else
            //            {
            //                dataRow["value"] = "[" + String.Join(", ", rawData_list[i].ToArray()) + "]";
            //            }
            //            //dataRow["value"] = "[";
            //            //for (int k = 0; k < rawData_list[i].Count; k++)
            //            //{
            //            //    if (string.IsNullOrEmpty(rawData_list[i][k]))
            //            //    {
            //            //        dataRow["value"] += "null";
            //            //    }
            //            //    else
            //            //    {
            //            //        dataRow["value"] += rawData_list[i][k];
            //            //    }
            //            //    if(k!= rawData_list[i].Count-1)
            //            //    {
            //            //        dataRow["value"] += ",";
            //            //    }
            //            //    else
            //            //    {
            //            //        dataRow["value"] += "]";
            //            //    }
            //            //}
            //        }
            //        else
            //        {
            //            dataRow["value"] = "[]";
            //        }
            //        if (dataRow["value"].ToString().Substring(dataRow["value"].ToString().Length - 1) != "]")
            //        {
            //            dataRow["value"] += "]";
            //        }
            //        if (netnameList.Count > i)
            //        {
            //            dataRow["net_name"] = netnameList[i];
            //        }
            //        item_table_tmp.Rows.Add(dataRow);
            //        fileContentFormat.LotStatistic.Tables.Add(item_table_tmp);
            //    }
            //    //stream.Close();
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex.ToString());
            //    fileContentFormat.ErrMsg = "讀檔內容錯誤";
            //    return null;
            //}
            return fileContentFormat;
        }
    }
}
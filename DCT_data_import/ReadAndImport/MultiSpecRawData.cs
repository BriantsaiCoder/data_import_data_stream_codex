using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using static DCT_data_import.DbObject;
namespace DCT_data_import.ReadAndImport
{/// <summary>
 /// 檔案檢查結果類別 - 支援多個檔案及對應錯誤路徑
 /// </summary>
    public class FileCheckResult
    {
        public bool Exists { get; set; }
        public List<string> FilePaths { get; set; }
        public List<string> FileNames { get; set; }
        public List<string> ErrorPaths { get; set; }
        public List<int> SiteNumbers { get; set; }
        public FileCheckResult(bool exists)
        {
            Exists = exists;
            FilePaths = new List<string>();
            FileNames = new List<string>();
            ErrorPaths = new List<string>();
            SiteNumbers = new List<int>();
        }
        public FileCheckResult(bool exists, List<string> filePaths, List<string> fileNames, List<string> errorPaths, List<int> siteNumbers)
        {
            Exists = exists;
            FilePaths = filePaths ?? new List<string>();
            FileNames = fileNames ?? new List<string>();
            ErrorPaths = errorPaths ?? new List<string>();
            SiteNumbers = siteNumbers ?? new List<int>();
        }
        // 為了向後相容，提供單一檔案的屬性
        public string FilePath => FilePaths.Count > 0 ? FilePaths[0] : "";
        public string FileName => FileNames.Count > 0 ? FileNames[0] : "";
        public string ErrorPath => ErrorPaths.Count > 0 ? ErrorPaths[0] : "";
        public int SiteNumber => SiteNumbers.Count > 0 ? SiteNumbers[0] : 0;
    }
    public class MultiSpecRawData : ImportData
    {
        /// <summary>
        /// 從檔案名稱中提取 site 數值
        /// </summary>
        /// <param name="fileName">檔案名稱，例如 "test_result_site1_ABC123.csv"</param>
        /// <returns>site 數值，如果無法解析則返回 0</returns>
        protected int ExtractSiteNumberFromFileName(string fileName)
        {
            try
            {
                // 使用正規表示式提取 site 後面的數字
                var match = System.Text.RegularExpressions.Regex.Match(fileName, @"test_result_site(\d+)_", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    int siteNumber;
                    if (int.TryParse(match.Groups[1].Value, out siteNumber))
                    {
                        return siteNumber;
                    }
                }
            }
            catch (Exception ex)
            {
                var writeToLog = new WriteToLog();
                writeToLog.WriteErrorLog(string.Format("[ExtractSiteNumberFromFileName] 提取 site 數值失敗: {0}, 錯誤: {1}", fileName, ex.Message));
            }
            return 0; // 如果無法提取，返回 0
        }
        /// <summary>
        /// 搜尋並檢查 multiSpecRawdata 檔案是否存在 - 修改版本
        /// </summary>
        /// <param name="dbKey">資料庫鍵值</param>
        /// <param name="ftpUser">FTP使用者</param>
        /// <param name="ftpPassword">FTP密碼</param>
        /// <returns>檔案存在結果，包含所有符合的檔案路徑、對應的錯誤路徑和 site 數值</returns>
        protected FileCheckResult CheckMultiSpecRawDataFile(string dbKey, string ftpUser, string ftpPassword)
        {
            string directoryPath = GetFilePath("multiSpecRawdata", dbKey);
            string filePattern = string.Format("test_result_site*_{0}.csv", dbKey);
            var matchingFiles = SearchFilesInFtpDirectory(directoryPath, filePattern, ftpUser, ftpPassword);
            if (matchingFiles.Count > 0)
            {
                var filePaths = new List<string>();
                var fileNames = new List<string>();
                var errorPaths = new List<string>();
                var siteNumbers = new List<int>();
                foreach (string fileName in matchingFiles)
                {
                    string fullPath = directoryPath + fileName;
                    string errorPath = GetErrorPathForSpecificFile("multiSpecRawdata", fileName, dbKey);
                    int siteNumber = ExtractSiteNumberFromFileName(fileName); // 提取 site 數值
                    filePaths.Add(fullPath);
                    fileNames.Add(fileName);
                    errorPaths.Add(errorPath);
                    siteNumbers.Add(siteNumber);
                }
                return new FileCheckResult(true, filePaths, fileNames, errorPaths, siteNumbers);
            }
            return new FileCheckResult(false);
        }
        public async Task<ImportResult> ReadAndImportMultiSpecRawData(FileProcess fileAccess, DatabaseService DatabaseService, string dbKey)
        {
            WriteToLog writeToLog = new WriteToLog();
            bool compareResult = false;
            CalculateSPC calculateSPC = new CalculateSPC();
            List<StatisticItem> list_statistic_item;
            string deleteStatus;
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts2 = stopWatch.Elapsed;
            double readTakeTime = 0, importTakeTime = 0;
            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();
            fileAccess.MultiSiteRawDataLotInfoInsertId = string.Empty;
            fileAccess.MultiSiteRawDataLotInfoIsImported = false;
            // 檢查FTP是否有此檔案
            FileCheckResult result = CheckMultiSpecRawDataFile(dbKey, Program.FTP_USER, Program.FTP_PASSWORD);
            if (!result.Exists)
            {
                string errorMessage = string.Format("MultiSpec Raw data Files not found for dbKey: {0}", dbKey);
                Console.WriteLine(errorMessage);
                writeToLog.WriteErrorLog(errorMessage);
                return new ImportResult(0, "Files not found.");
            }
            bool isDBKeyExist = false;
            //  DB_Key是否已存在於資料庫
            isDBKeyExist = fileAccess.IsDBKeyExistInDB("lots_info", dbKey, DatabaseService);
            if (isDBKeyExist)
            {
                for (int i = 0; i < result.FileNames.Count; i++)
                {
                    string ftpFilePath = result.FilePaths[i];
                    string filename = result.FileNames[i];
                    string errorPath = result.ErrorPaths[i];
                    Console.WriteLine("資料庫已存在此資料: MultiSpec Raw data 比對: " + compareResult + "   檔名:" + filename);
                    MoveToError(ftpFilePath, errorPath);
                }
                return new ImportResult(3, "The same DB_Key exists in the database.");
            }
            // 顯示所有找到的檔案
            Console.WriteLine(string.Format("Found {0} matching multiSpecRawdata files for dbKey: {1}", result.FileNames.Count, dbKey));
            writeToLog.WriteInfoLog(string.Format("Found {0} matching multiSpecRawdata files for dbKey: {1}", result.FileNames.Count, dbKey));
            // 處理每個找到的檔案
            for (int i = 0; i < result.FileNames.Count; i++)
            {
                string ftpFilePath = result.FilePaths[i];
                string filename = result.FileNames[i];
                string errorPath = result.ErrorPaths[i];
                int siteNumber = result.SiteNumbers[i];
                Console.WriteLine(string.Format("Processing file [{0}/{1}]: {2}", i + 1, result.FileNames.Count, filename));
                //Console.WriteLine(string.Format("  File Path: {0}", currentFilePath));
                //Console.WriteLine(string.Format("  Error Path: {0}", currentErrorPath));
                // 開始讀檔與匯入
                try
                {
                    bool import_result = false;
                    long fileSize = GetFileLength(ftpFilePath);
                    stopWatch.Reset();
                    stopWatch.Start();
                    RawDataContentFormat rawDataContentFormat = ReadBig5File(ftpFilePath, FileReadRawData);
                    stopWatch.Stop();
                    ts2 = stopWatch.Elapsed;
                    readTakeTime = Math.Round(Convert.ToDouble(ts2.TotalMilliseconds / 1000), 3);
                    if (!string.IsNullOrEmpty(rawDataContentFormat.ErrMsg))
                    {
                        Console.WriteLine("MultiSpec Raw data Error:  " + rawDataContentFormat.ErrMsg);
                        writeToLog.WriteErrorLog("MultiSpec Raw data Error: " + rawDataContentFormat.ErrMsg);
                        MoveToError(ftpFilePath, errorPath);
                        return new ImportResult(2, rawDataContentFormat.ErrMsg);
                    }
                    if (rawDataContentFormat == null || rawDataContentFormat.LotInfo.Rows.Count < 1)
                    {
                        Console.WriteLine("MultiSpec Raw data 讀檔失敗:  " + filename);
                        writeToLog.WriteErrorLog("MultiSpec Raw data 讀檔失敗: " + ftpFilePath);
                        MoveToError(ftpFilePath, errorPath);
                        return new ImportResult(2, "File content is missing. " + rawDataContentFormat.ErrMsg);
                    }
                    if (!rawDataContentFormat.CompareInfo())
                    {
                        Console.WriteLine("MultiSpec Raw data 之 information 欄位名稱不符:  " + filename);
                        writeToLog.WriteErrorLog("MultiSpec Raw data 之 information 欄位名稱不符:" + ftpFilePath);
                        MoveToError(ftpFilePath, errorPath);
                        return new ImportResult(2, "Information field name not match.");
                    }
                    if (!rawDataContentFormat.CompareStatistic())
                    {
                        Console.WriteLine("MultiSpec Raw data 之 statistic 欄位名稱不符:  " + filename);
                        writeToLog.WriteErrorLog("MultiSpec Raw data 之 statistic 欄位名稱不符:" + ftpFilePath);
                        MoveToError(ftpFilePath, errorPath);
                        return new ImportResult(2, "Statistic field name not match.");
                    }
                    if (!dbKey.Equals(rawDataContentFormat.LotInfo.Rows[0]["DB_Key"].ToString()))
                    {
                        writeToLog.WriteErrorLog("檔名與內容的DB_Key不相符: " + ftpFilePath);
                        MoveToError(ftpFilePath, errorPath);
                        return new ImportResult(2, "The filename does not match the DB_Key in the content.");
                    }
                    // 計算均方和
                    list_statistic_item = calculateSPC.AverageOfSumSquare(rawDataContentFormat);
                    fileAccess.AddColumnForDataset(rawDataContentFormat.LotStatistic, list_statistic_item);
                    stopWatch.Reset();
                    stopWatch.Start();
                    // 開始匯入
                    await Task.Run(() =>
                    {
                        import_result = fileAccess.ImportMultiSpecRawData(rawDataContentFormat, DatabaseService, siteNumber);
                    }).ConfigureAwait(false);
                    stopWatch.Stop();
                    ts2 = stopWatch.Elapsed;
                    importTakeTime = Math.Round(Convert.ToDouble(ts2.TotalMilliseconds / 1000), 3);
                    string dateStr = DateTime.Now.ToString("yyyyMMdd");
                    string checkLogFileName = "DCT_data_check_log_multiSpecRawdata_" + dateStr + ".csv";
                    // 寫入 file name, file size, import time, read file take time, import take time
                    writeToLog.WriteToCheckLog(checkLogFileName, filename + "," + FormatFileSize(fileSize) + "," + DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + "," + readTakeTime.ToString() + "," + importTakeTime.ToString());
                    if (import_result)
                    {
                        Console.WriteLine("匯入完成! MultiSpec Raw data    檔名:" + filename + "    耗時: " + Convert.ToInt32(ts2.TotalMilliseconds / 1000).ToString() + " 秒");
                        // 刪除已存在的的CSV檔案
                        deleteStatus = CompleteSuccess(ftpFilePath);
                    }
                    else
                    {
                        Console.WriteLine("匯入失敗: MultiSpec Raw data " + filename);
                        writeToLog.WriteErrorLog("匯入失敗:" + ftpFilePath);
                        MoveToError(ftpFilePath, errorPath);
                        return new ImportResult(3, "Import failed.");
                    }
                }
                catch (Exception ex)
                {
                    writeToLog.WriteErrorLog($"RawData MultiSpec 匯入處理發生例外錯誤: {ftpFilePath}, 檔案: {filename}, 錯誤: {ex.Message}");
                    Console.WriteLine($"RawData MultiSpec 匯入處理發生例外錯誤: {ftpFilePath}, 檔案: {filename}, 錯誤: {ex.Message}");
                    MoveToError(ftpFilePath, errorPath);
                    return new ImportResult(3, "Exception error occurred during import.");
                }
                finally
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect(); // 二次回收，確保徹底清理
                }
            }
            return new ImportResult(1, string.Empty);
        }
        internal RawDataContentFormat FileReadRawData(StreamReader reader)
        {
            RawDataContentFormat fileContentFormat = new RawDataContentFormat();
            try
            {
                // 存放第二部分統計值的 Dictionary
                Dictionary<string, List<string>> statistic_dict = new Dictionary<string, List<string>>();
                // 存放第三部分  Serial, SN Num, SiteID, X, Y, HBIN, P/F
                List<List<string>> rawData_list = new List<List<string>>();
                // item 數量
                int item_count = 0;
                int content_part = 1;
                // 第三部分  raw data的起始index
                int rawData_part_index = 0;
                string lines = reader.ReadToEnd();
                reader.Close();
                List<string> split_lines = lines.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                for (int r = 0; r < split_lines.Count; r++)
                {
                    var line = split_lines[r].Trim();
                    if (!string.IsNullOrEmpty(line) && IsChinese(line))
                    {
                        fileContentFormat.ErrMsg = "Chinese word exists.";
                    }
                    var values = line.Split(',', '\0', '\r', '\n');
                    var values_tmp = values;
                    // 去除空白值
                    if (content_part != 3)
                    {
                        values_tmp = values.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                    }
                    if (values_tmp.Length < 1) continue;
                    // 看到關鍵字 "Serial"
                    if (values[0] == "Serial")
                    {
                        content_part = 3;
                    }
                    // 第一部分  info
                    if (content_part == 1)
                    {
                        // Safe string splitting with bounds checking
                        var firstValueParts = values[0]?.Split(':');
                        if (firstValueParts == null || firstValueParts.Length == 0)
                        {
                            Console.WriteLine($"RawData invalid first value format: {values[0]}");
                            continue;
                        }
                        string firstValueKey = firstValueParts[0];
                        // "Open fail"與"Short fail"為TSMC 客戶中兩個非必要欄位，故排除不存進資料庫
                        if (firstValueKey == "Open fail" || firstValueKey == "Short fail") continue;
                        fileContentFormat.LotInfo.Columns.Add(firstValueKey, typeof(string));
                        fileContentFormat.LotInfo.Rows[0][firstValueKey] = values[1];
                    }
                    // 第二部分  statistic
                    else if (content_part == 2)
                    {
                        // 找到第一個非空值的index
                        for (int i = 0; i < values.Length; i++)
                        {
                            if (!string.IsNullOrEmpty(values[i]))
                            {
                                // 將第一個非空值之前的所有空值砍掉
                                values = values.Where(s => values.ToList().IndexOf(s) >= i).ToArray();
                                // 前有7格空白表示在統計值區塊的 Item_NO 或 Item_name
                                if (i == 7)
                                {
                                    if (values[0] == "1001")
                                    {
                                        statistic_dict.Add("Item No", values.ToList<string>());
                                        item_count = values.Length;
                                    }
                                    else
                                    {
                                        // 只取到item數量為止，在後方的則是註記欄位，如 test time、index time
                                        values = values.Where(s => values.ToList().IndexOf(s) < item_count).ToArray();
                                        statistic_dict.Add("Item Name", values.ToList<string>());
                                    }
                                }
                                else
                                {
                                    // 屬於欄位: Force, Wait time, Spec MAX, Spec MIN, # of PASS, # of FAIL, MIN, MAX, AVG, STDEV, Cp, Cpk
                                    var values_except_head = values.Where(x => x != values[0]).ToArray();
                                    values_except_head = values_except_head.Where(s => values_except_head.ToList().IndexOf(s) < item_count).ToArray();
                                    statistic_dict.Add(values[0], values_except_head.ToList<string>());
                                }
                                break;
                            }
                        }
                    }
                    // 第三部分  raw data
                    else if (content_part == 3)
                    {
                        // unit 值填入 Dictionary
                        if (values[0] == "Serial")
                        {
                            var values_unit = values.Where(s => values.ToList().IndexOf(s) >= 7).ToArray();
                            values_unit = values_unit.Where(s => values_unit.ToList().IndexOf(s) < item_count).ToArray();
                            statistic_dict.Add("unit", values_unit.ToList<string>());
                        }
                        int result_part = 1;  // 1.表示Serial,SN Num,SiteID,	X,Y,HBIN,P/F   2.表示 raw data值的部分含單位(V, uA,...)
                        DataRow dr_lotResult = fileContentFormat.LotResult.NewRow();
                        for (int i = 0; i < values.Length; i++)
                        {
                            // 欄位 Serial, SN Num, SiteID,	 X, Y, HBIN, P/F
                            if (result_part == 1 && values[0] == "Serial")
                            {
                                fileContentFormat.LotResult.Columns.Add(values[i], typeof(string));
                            }
                            // 欄位 Serial 為空的直接跳過
                            else if (string.IsNullOrEmpty(values[0].Trim()))
                            {
                                continue;
                            }
                            // 欄位 Serial, SN Num, SiteID,	 X, Y, HBIN, P/F 的 values
                            else if (result_part == 1)
                            {
                                // 解析'*'字號
                                if (i == 0 && values[i].Contains("*"))
                                {
                                    dr_lotResult["retest_loc"] = "Y";
                                    dr_lotResult[i] = values[i].Remove(0, 1);
                                }
                                else
                                {
                                    dr_lotResult["retest_loc"] = "N";
                                    dr_lotResult[i] = values[i];
                                }
                            }
                            // unit
                            if (result_part == 2 && values[0] == "Serial")
                            {
                                // 第三部分最右方 test time, index time, real time
                                if (i == rawData_part_index + item_count)
                                {
                                    fileContentFormat.LotResult.Columns.Add("test time", typeof(string));
                                    fileContentFormat.LotResult.Columns.Add("index time", typeof(string));
                                    fileContentFormat.LotResult.Columns.Add("real time", typeof(string));
                                    fileContentFormat.LotResult.Columns.Add("retest_loc", typeof(string));
                                    break;
                                }
                                rawData_list.Add(new List<string>());
                            }
                            // raw data value的部分 先存入rawData_list
                            else if (result_part == 2 && i < rawData_part_index + rawData_list.Count)
                            {
                                // 讀值若含有小數點"."而沒有小數位，則移除小數點
                                if (values[i].Substring(values[i].Length - 1) == ".") values[i] = values[i].Substring(0, values[i].Length - 1);
                                // 去除數值包含(O)(S)的括號
                                if (values[i].Contains("(O)"))
                                {
                                    values[i] = values[i].Replace("(O)", string.Empty);
                                }
                                if (values[i].Contains("(S)"))
                                {
                                    values[i] = values[i].Replace("(S)", string.Empty);
                                }
                                rawData_list[i - rawData_part_index].Add(values[i]);
                            }
                            else if (result_part == 2 && i >= rawData_part_index + rawData_list.Count)
                            {
                                if (i - rawData_list.Count >= fileContentFormat.LotResult.Columns.Count)
                                {
                                    //break;
                                    throw new ArgumentException("Read value column count greater than expected.");
                                }
                                dr_lotResult[i - rawData_list.Count] = values[i];
                            }
                            // 以"P/F"為分界
                            if (values[i] == "P/F" || i == rawData_part_index - 1)
                            {
                                result_part = 2;
                                rawData_part_index = i + 1;
                            }
                        }
                        if (values[0] != "Serial")
                        {
                            fileContentFormat.LotResult.Rows.Add(dr_lotResult);
                        }
                    }
                    // 看到關鍵字 "Stop"
                    if (values[0] == "Stop:")
                    {
                        content_part = 2;
                    }
                }
                // 讀取TSMC 的 CSV net name欄位加入到此表
                TsmcIeda tsmcIeda = new TsmcIeda();
                List<string> netnameList = new List<string>();
                if (fileContentFormat.LotInfo.Rows[0]["Customer"].ToString() == "TSMC")
                {
                    netnameList = tsmcIeda.GetNetNameList(fileContentFormat.LotInfo.Rows[0]["AO_lot"].ToString());
                }
                // 將raw data values 填入統計值的表
                DataTable item_table = new DataTable();
                for (int i = 0; i < statistic_dict.Keys.Count; i++)
                {
                    item_table.Columns.Add(statistic_dict.ElementAt(i).Key, typeof(string));
                }
                item_table.Columns.Add("net_name", typeof(string));
                item_table.Columns.Add("value", typeof(string));
                for (int i = 0; i < item_count; i++)
                {
                    // Clone 方法建立的新 DataTable 不會包含任何 DataRows.，只有相同的Schema。
                    DataTable item_table_tmp = item_table.Clone();
                    DataRow dataRow = item_table_tmp.NewRow();
                    // 對 dataRow 填入 統計值
                    for (int j = 0; j < statistic_dict.Keys.Count; j++)
                    {
                        dataRow[statistic_dict.ElementAt(j).Key] = statistic_dict[statistic_dict.ElementAt(j).Key][i];
                    }
                    // 對 dataRow 填入 raw data list
                    if (rawData_list.Count > 0)
                    {
                        string strJoin = String.Join(", ", rawData_list[i].ToArray());
                        if (string.IsNullOrEmpty(strJoin.Trim()))
                        {
                            dataRow["value"] = "[]";
                        }
                        else
                        {
                            dataRow["value"] = "[" + String.Join(", ", rawData_list[i].ToArray()) + "]";
                        }
                    }
                    else
                    {
                        dataRow["value"] = "[]";
                    }
                    if (dataRow["value"].ToString().Substring(dataRow["value"].ToString().Length - 1) != "]")
                    {
                        dataRow["value"] += "]";
                    }
                    if (netnameList.Count > i)
                    {
                        dataRow["net_name"] = netnameList[i];
                    }
                    item_table_tmp.Rows.Add(dataRow);
                    fileContentFormat.LotStatistic.Tables.Add(item_table_tmp);
                }
            }
            catch (Exception ex)
            {
                WriteToLog writeToLogService = new WriteToLog();
                writeToLogService.WriteErrorLog($"[FileReadRawData] 讀檔內容錯誤, 錯誤: {ex.Message}");
                Console.WriteLine(ex.Message);
                fileContentFormat.ErrMsg = "讀檔內容錯誤, Error:" + ex.Message;
                return null;
            }
            return fileContentFormat;
        }
    }
}

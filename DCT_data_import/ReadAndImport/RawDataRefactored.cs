using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DCT_data_import.Common;
using static DCT_data_import.DbObject;

namespace DCT_data_import.ReadAndImport
{
    /// <summary>
    /// RawData 重構版本
    /// 使用共用模組並拆分複雜邏輯以提升維護性
    /// </summary>
    public class RawDataRefactored : ImportData
    {
        private readonly FtpService _ftpService;
        private readonly FileValidator _fileValidator;
        private readonly DatabaseHelper _databaseHelper;
        private readonly WriteToLog _writeToLog;
        private readonly CalculateSPC _calculateSPC;

        public RawDataRefactored()
        {
            _ftpService = new FtpService();
            _fileValidator = new FileValidator();
            _databaseHelper = new DatabaseHelper();
            _writeToLog = new WriteToLog();
            _calculateSPC = new CalculateSPC();
        }

        /// <summary>
        /// 驗證結果類別
        /// </summary>
        public class ValidationResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }

            public ValidationResult(bool success, string message)
            {
                Success = success;
                Message = message;
            }
        }

        /// <summary>
        /// 讀取並匯入 Raw Data - 重構版本
        /// </summary>
        public async Task<ImportResult> ReadAndImportRawData(FileProcess fileAccess, DatabaseService databaseService, string dbKey)
        {
            var stopWatch = new Stopwatch();
            double readTakeTime = 0, importTakeTime = 0;

            try
            {
                // 1. 驗證輸入參數
                if (string.IsNullOrEmpty(dbKey))
                {
                    return new ImportResult(0, "DB Key 不能為空");
                }

                // 2. 建立檔案路徑
                string filename = string.Format("test_result_{0}.csv", dbKey);
                string ftpFilePath = BuildFtpFilePath(filename);
                string errorDir = BuildErrorDirectory();

                // 3. 檢查檔案存在性
                if (!_ftpService.CheckIfFileExists(ftpFilePath, Program.FTP_USER, Program.FTP_PASSWORD))
                {
                    _writeToLog.WriteToDataImportLog(string.Format("Raw data File not found: {0}", ftpFilePath));
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(0, "File not found.");
                }

                // 4. 讀取檔案內容
                stopWatch.Start();
                var rawDataContent = await ReadRawDataFileAsync(ftpFilePath);
                stopWatch.Stop();
                readTakeTime = Math.Round(stopWatch.Elapsed.TotalSeconds, 3);

                if (rawDataContent == null)
                {
                    return new ImportResult(2, "檔案讀取失敗");
                }

                // 5. 驗證檔案內容
                var validationResult = ValidateRawDataContent(rawDataContent, dbKey);
                if (!validationResult.Success)
                {
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(2, validationResult.Message);
                }

                // 6. 檢查 DB Key 是否已存在
                bool isDBKeyExist = fileAccess.IsDBKeyExistInDB("lots_info", dbKey, databaseService);
                if (isDBKeyExist)
                {
                    _writeToLog.WriteToDataImportLog(string.Format("資料庫已存在此資料: {0}", filename));
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(3, "The same DB_Key exists in the database.");
                }

                // 7. 計算 SPC 統計值
                var spcResults = await CalculateSPCStatisticsAsync(rawDataContent);
                fileAccess.AddColumnForDataset(rawDataContent.LotStatistic, "avg_2", spcResults);

                // 8. 匯入資料庫
                stopWatch.Restart();
                bool importResult = await ImportRawDataAsync(rawDataContent, databaseService, fileAccess);
                stopWatch.Stop();
                importTakeTime = Math.Round(stopWatch.Elapsed.TotalSeconds, 3);

                // 9. 記錄處理結果
                LogProcessingResult(filename, ftpFilePath, readTakeTime, importTakeTime);

                if (importResult)
                {
                    Console.WriteLine(string.Format("匯入完成! Raw data 檔名: {0} 耗時: {1} 秒", filename, (int)stopWatch.Elapsed.TotalSeconds));
                    _ftpService.DeleteFile(ftpFilePath, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(1, string.Empty);
                }
                else
                {
                    _writeToLog.WriteToDataImportLog(string.Format("匯入失敗: {0}", ftpFilePath));
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(3, "Import failed.");
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("ReadAndImportRawData 發生錯誤: {0}", ex.Message));
                return new ImportResult(3, "Exception error occurred during import.");
            }
            finally
            {
                GC.Collect();
            }
        }

        /// <summary>
        /// 建立 FTP 檔案路徑
        /// </summary>
        private string BuildFtpFilePath(string filename)
        {
            string basePath = string.Format("ftp://{0}", Program.FTP_IP);
            string subPath = Program.Environment == "Dev" 
                ? "/DCT_Log/DCT_DB_DATA_Dev/Data_Cloud_CSV/" 
                : "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/";
            
            return basePath + subPath + filename;
        }

        /// <summary>
        /// 建立錯誤目錄路徑
        /// </summary>
        private string BuildErrorDirectory()
        {
            string basePath = string.Format("ftp://{0}", Program.FTP_IP);
            return Program.Environment == "Dev" 
                ? basePath + "/DCT_Log/DCT_DB_DATA_Dev/Data_Cloud_CSV_Error/"
                : basePath + "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/";
        }

        /// <summary>
        /// 非同步讀取 Raw Data 檔案
        /// </summary>
        private async Task<RawDataContentFormat> ReadRawDataFileAsync(string ftpFilePath)
        {
            try
            {
                var reqFTP = (FtpWebRequest)WebRequest.Create(new Uri(ftpFilePath));
                reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
                reqFTP.Method = WebRequestMethods.Ftp.DownloadFile;

                using (var response = (FtpWebResponse)await reqFTP.GetResponseAsync())
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream, Encoding.GetEncoding("big5")))
                {
                    return await Task.Run(() => ParseRawDataFile(reader));
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("讀取檔案時發生錯誤: {0}", ex.Message));
                return null;
            }
        }

        /// <summary>
        /// 解析 Raw Data 檔案內容
        /// </summary>
        private RawDataContentFormat ParseRawDataFile(StreamReader reader)
        {
            var fileContentFormat = new RawDataContentFormat();
            
            try
            {
                var statistic_dict = new Dictionary<string, List<string>>();
                var rawData_list = new List<List<string>>();
                int item_count = 0;
                int content_part = 1;
                int rawData_part_index = 0;

                string lines = reader.ReadToEnd();
                reader.Close();
                List<string> split_lines = lines.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

                for (int r = 0; r < split_lines.Count; r++)
                {
                    var line = split_lines[r].Trim();
                    
                    // 檢查中文字符
                    if (!string.IsNullOrEmpty(line) && StringHelper.ContainsChinese(line))
                    {
                        fileContentFormat.ErrMsg = "Chinese word exists.";
                    }

                    var values = line.Split(',', '\0', '\r', '\n');
                    var values_tmp = values;

                    // 去除空白值（除了第三部分）
                    if (content_part != 3)
                    {
                        values_tmp = values.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                    }

                    if (values_tmp.Length < 1) continue;

                    // 檢查關鍵字段來決定處理部分
                    if (values[0] == "Serial")
                    {
                        content_part = 3;
                    }
                    else if (values[0] == "Stop:")
                    {
                        content_part = 2;
                    }

                    // 處理不同部分的內容
                    switch (content_part)
                    {
                        case 1: // 處理資訊部分
                            ProcessInfoSection(fileContentFormat, values);
                            break;
                        case 2: // 處理統計部分
                            ProcessStatisticSection(statistic_dict, values, ref item_count);
                            break;
                        case 3: // 處理原始資料部分
                            ProcessRawDataSection(fileContentFormat, values, statistic_dict, rawData_list, 
                                                item_count, ref rawData_part_index);
                            break;
                    }
                }

                // 處理 TSMC 客戶的 net name
                ProcessTsmcNetNames(fileContentFormat, statistic_dict, rawData_list, item_count);

                return fileContentFormat;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("解析檔案內容時發生錯誤: {0}", ex.Message));
                fileContentFormat.ErrMsg = string.Format("讀檔內容錯誤, Error: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 處理資訊區段
        /// </summary>
        private void ProcessInfoSection(RawDataContentFormat fileContentFormat, string[] values)
        {
            // 排除 TSMC 客戶的非必要欄位
            if (values[0].Split(':')[0] == "Open fail" || values[0].Split(':')[0] == "Short fail") 
                return;

            fileContentFormat.LotInfo.Columns.Add(values[0].Split(':')[0], typeof(string));
            fileContentFormat.LotInfo.Rows[0][values[0].Split(':')[0]] = values[1];
        }

        /// <summary>
        /// 處理統計區段
        /// </summary>
        private void ProcessStatisticSection(Dictionary<string, List<string>> statistic_dict, string[] values, ref int item_count)
        {
            // 找到第一個非空值的index
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                {
                    values = values.Where(s => values.ToList().IndexOf(s) >= i).ToArray();

                    if (i == 7) // Item_NO 或 Item_name
                    {
                        if (values[0] == "1001")
                        {
                            statistic_dict.Add("Item No", values.ToList<string>());
                            item_count = values.Length;
                        }
                        else
                        {
                            int currentItemCount = item_count; // 建立本地變數
                            values = values.Where(s => values.ToList().IndexOf(s) < currentItemCount).ToArray();
                            statistic_dict.Add("Item Name", values.ToList<string>());
                        }
                    }
                    else
                    {
                        var values_except_head = values.Where(x => x != values[0]).ToArray();
                        int currentItemCount = item_count; // 建立本地變數
                        values_except_head = values_except_head.Where(s => values_except_head.ToList().IndexOf(s) < currentItemCount).ToArray();
                        statistic_dict.Add(values[0], values_except_head.ToList<string>());
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 處理原始資料區段
        /// </summary>
        private void ProcessRawDataSection(RawDataContentFormat fileContentFormat, string[] values, 
            Dictionary<string, List<string>> statistic_dict, List<List<string>> rawData_list, 
            int item_count, ref int rawData_part_index)
        {
            // unit 值填入 Dictionary
            if (values[0] == "Serial")
            {
                var values_unit = values.Where(s => values.ToList().IndexOf(s) >= 7).ToArray();
                values_unit = values_unit.Where(s => values_unit.ToList().IndexOf(s) < item_count).ToArray();
                statistic_dict.Add("unit", values_unit.ToList<string>());
            }

            int result_part = 1;
            DataRow dr_lotResult = fileContentFormat.LotResult.NewRow();

            for (int i = 0; i < values.Length; i++)
            {
                if (result_part == 1 && values[0] == "Serial")
                {
                    fileContentFormat.LotResult.Columns.Add(values[i], typeof(string));
                }
                else if (string.IsNullOrEmpty(values[0].Trim()))
                {
                    continue;
                }
                else if (result_part == 1)
                {
                    // 處理重測標記 '*'
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

                // 處理原始資料值部分
                if (result_part == 2)
                {
                    ProcessRawDataValues(fileContentFormat, values, rawData_list, item_count, rawData_part_index, i, dr_lotResult);
                }

                // 以 "P/F" 為分界
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

        /// <summary>
        /// 處理原始資料值
        /// </summary>
        private void ProcessRawDataValues(RawDataContentFormat fileContentFormat, string[] values, 
            List<List<string>> rawData_list, int item_count, int rawData_part_index, int i, DataRow dr_lotResult)
        {
            if (values[0] == "Serial")
            {
                if (i == rawData_part_index + item_count)
                {
                    fileContentFormat.LotResult.Columns.Add("test time", typeof(string));
                    fileContentFormat.LotResult.Columns.Add("index time", typeof(string));
                    fileContentFormat.LotResult.Columns.Add("real time", typeof(string));
                    fileContentFormat.LotResult.Columns.Add("retest_loc", typeof(string));
                    return;
                }
                rawData_list.Add(new List<string>());
            }
            else if (i < rawData_part_index + rawData_list.Count)
            {
                // 清理數值格式
                string cleanedValue = CleanNumericValue(values[i]);
                rawData_list[i - rawData_part_index].Add(cleanedValue);
            }
            else if (i >= rawData_part_index + rawData_list.Count)
            {
                if (i - rawData_list.Count >= fileContentFormat.LotResult.Columns.Count)
                {
                    throw new ArgumentException("Read value column count greater than expected.");
                }
                dr_lotResult[i - rawData_list.Count] = values[i];
            }
        }

        /// <summary>
        /// 清理數值格式
        /// </summary>
        private string CleanNumericValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            // 移除末尾的小數點
            if (value.EndsWith("."))
            {
                value = value.Substring(0, value.Length - 1);
            }

            // 移除 (O) 和 (S) 標記
            value = value.Replace("(O)", "").Replace("(S)", "");

            return value;
        }

        /// <summary>
        /// 處理 TSMC 客戶的 net name
        /// </summary>
        private void ProcessTsmcNetNames(RawDataContentFormat fileContentFormat, 
            Dictionary<string, List<string>> statistic_dict, List<List<string>> rawData_list, int item_count)
        {
            List<string> netnameList = new List<string>();
            
            if (fileContentFormat.LotInfo.Rows[0]["Customer"].ToString() == "TSMC")
            {
                var tsmcIeda = new TsmcIeda();
                netnameList = tsmcIeda.GetNetNameList(fileContentFormat.LotInfo.Rows[0]["AO_lot"].ToString());
            }

            // 建立統計表
            DataTable item_table = new DataTable();
            foreach (var key in statistic_dict.Keys)
            {
                item_table.Columns.Add(key, typeof(string));
            }
            item_table.Columns.Add("net_name", typeof(string));
            item_table.Columns.Add("value", typeof(string));

            for (int i = 0; i < item_count; i++)
            {
                DataTable item_table_tmp = item_table.Clone();
                DataRow dataRow = item_table_tmp.NewRow();

                // 填入統計值
                foreach (var kvp in statistic_dict)
                {
                    dataRow[kvp.Key] = kvp.Value[i];
                }

                // 填入原始資料
                if (rawData_list.Count > 0 && i < rawData_list.Count)
                {
                    string strJoin = string.Join(", ", rawData_list[i].ToArray());
                    dataRow["value"] = string.IsNullOrEmpty(strJoin.Trim()) ? "[]" : string.Format("[{0}]", strJoin);
                }
                else
                {
                    dataRow["value"] = "[]";
                }

                // 確保值以 ] 結尾
                if (!dataRow["value"].ToString().EndsWith("]"))
                {
                    dataRow["value"] += "]";
                }

                // 填入 net name
                if (netnameList.Count > i)
                {
                    dataRow["net_name"] = netnameList[i];
                }

                item_table_tmp.Rows.Add(dataRow);
                fileContentFormat.LotStatistic.Tables.Add(item_table_tmp);
            }
        }

        /// <summary>
        /// 驗證 Raw Data 內容
        /// </summary>
        private ValidationResult ValidateRawDataContent(RawDataContentFormat data, string expectedDbKey)
        {
            if (!string.IsNullOrEmpty(data.ErrMsg))
            {
                return new ValidationResult(false, data.ErrMsg);
            }

            if (data.LotInfo.Rows.Count < 1)
            {
                return new ValidationResult(false, "File content is missing.");
            }

            if (!data.CompareInfo())
            {
                return new ValidationResult(false, "Information field name not match.");
            }

            if (!data.CompareStatistic())
            {
                return new ValidationResult(false, "Statistic field name not match.");
            }

            string fileDbKey = data.LotInfo.Rows[0]["DB_Key"]?.ToString();
            if (!expectedDbKey.Equals(fileDbKey))
            {
                return new ValidationResult(false, "The filename does not match the DB_Key in the content.");
            }

            return new ValidationResult(true, string.Empty);
        }

        /// <summary>
        /// 非同步計算 SPC 統計值
        /// </summary>
        private async Task<List<StatisticItem>> CalculateSPCStatisticsAsync(RawDataContentFormat data)
        {
            try
            {
                return await Task.Run(() => _calculateSPC.AverageOfSumSquare(data));
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("計算 SPC 統計值時發生錯誤: {0}", ex.Message));
                return new List<StatisticItem>();
            }
        }

        /// <summary>
        /// 非同步匯入 Raw Data
        /// </summary>
        private async Task<bool> ImportRawDataAsync(RawDataContentFormat data, DatabaseService databaseService, FileProcess fileAccess)
        {
            try
            {
                return await Task.Run(() => fileAccess.ImportRawData(data, databaseService));
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("匯入資料時發生錯誤: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 記錄處理結果
        /// </summary>
        private void LogProcessingResult(string filename, string ftpFilePath, double readTakeTime, double importTakeTime)
        {
            try
            {
                long fileSize = _ftpService.GetFileSize(ftpFilePath, Program.FTP_USER, Program.FTP_PASSWORD);
                string dateStr = DateTime.Now.ToString("yyyyMMdd");
                string checkLogFileName = string.Format("DCT_data_check_log_rawData_{0}.csv", dateStr);
                
                string logContent = string.Format("{0},{1},{2},{3},{4}",
                    filename,
                    _ftpService.FormatFileSize(fileSize),
                    DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
                    readTakeTime,
                    importTakeTime);

                _writeToLog.WriteToCheckLog(checkLogFileName, logContent);
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("記錄處理結果時發生錯誤: {0}", ex.Message));
            }
        }
    }
}
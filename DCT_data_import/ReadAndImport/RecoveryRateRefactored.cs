using System;
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
    /// Recovery Rate 重構版本
    /// 使用共用模組減少重複程式碼並提升維護性
    /// </summary>
    public class RecoveryRateRefactored : ImportData
    {
        private readonly FtpService _ftpService;
        private readonly FileValidator _fileValidator;
        private readonly DatabaseHelper _databaseHelper;
        private readonly WriteToLog _writeToLog;

        public RecoveryRateRefactored()
        {
            _ftpService = new FtpService();
            _fileValidator = new FileValidator();
            _databaseHelper = new DatabaseHelper();
            _writeToLog = new WriteToLog();
        }

        /// <summary>
        /// 驗證結果類別 (.NET 4.6.2 相容)
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
        /// 讀取並匯入 Recovery Rate 資料 - 重構版本
        /// </summary>
        public async Task<ImportResult> ReadAndImportRecoveryRateData(FileProcess fileAccess, DatabaseService databaseService, string dbKey)
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
                string filename = string.Format("Recovery_rate_{0}.csv", dbKey);
                string ftpFilePath = BuildFtpFilePath(filename);
                string errorDir = BuildErrorDirectory();

                // 3. 檢查檔案存在性
                if (!_ftpService.CheckIfFileExists(ftpFilePath, Program.FTP_USER, Program.FTP_PASSWORD))
                {
                    _writeToLog.WriteToDataImportLog(string.Format("Recovery Rate File not found: {0}", ftpFilePath));
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(0, "File not found.");
                }

                // 4. 讀取檔案內容
                stopWatch.Start();
                var recoveryRateData = ReadRecoveryRateFile(ftpFilePath);
                stopWatch.Stop();
                readTakeTime = Math.Round(stopWatch.Elapsed.TotalSeconds, 3);

                if (recoveryRateData == null)
                {
                    return new ImportResult(2, "檔案讀取失敗");
                }

                // 5. 驗證檔案內容
                var validationResult = ValidateRecoveryRateData(recoveryRateData, dbKey);
                if (!validationResult.Success)
                {
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(2, validationResult.Message);
                }

                // 6. 檢查 DB Key 是否已存在
                bool isDBKeyExist = fileAccess.IsDBKeyExistInDB("recovery_rate", dbKey, databaseService);
                if (isDBKeyExist)
                {
                    _writeToLog.WriteToDataImportLog(string.Format("資料庫已存在此資料: {0}", filename));
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(3, "The same DB_Key exists in the database.");
                }

                // 7. 匯入資料庫
                stopWatch.Restart();
                bool importResult = await ImportRecoveryDataAsync(recoveryRateData, databaseService);
                stopWatch.Stop();
                importTakeTime = Math.Round(stopWatch.Elapsed.TotalSeconds, 3);

                // 8. 記錄處理結果
                LogProcessingResult(filename, ftpFilePath, readTakeTime, importTakeTime);

                if (importResult)
                {
                    Console.WriteLine(string.Format("匯入完成! Recovery Rate 檔名: {0} 耗時: {1} 秒", filename, (int)stopWatch.Elapsed.TotalSeconds));
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
                _writeToLog.WriteToDataImportLog(string.Format("ReadAndImportRecoveryRateData 發生錯誤: {0}", ex.Message));
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
                ? "/DCT_Log/DCT_DB_DATA_Dev/Recovery_rate_data/" 
                : "/DCT_Log/DCT_DB_DATA/Recovery_rate_data/";
            
            return basePath + subPath + filename;
        }

        /// <summary>
        /// 建立錯誤目錄路徑
        /// </summary>
        private string BuildErrorDirectory()
        {
            string basePath = string.Format("ftp://{0}", Program.FTP_IP);
            return Program.Environment == "Dev" 
                ? basePath + "/DCT_Log/DCT_DB_DATA_Dev/Recovery_rate_data_Error/"
                : basePath + "/DCT_Log/DCT_DB_DATA/Recovery_rate_data_Error/";
        }

        /// <summary>
        /// 讀取 Recovery Rate 檔案
        /// </summary>
        private RecoveryRateDataContentFormat ReadRecoveryRateFile(string ftpFilePath)
        {
            try
            {
                var reqFTP = (FtpWebRequest)WebRequest.Create(new Uri(ftpFilePath));
                reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
                reqFTP.Method = WebRequestMethods.Ftp.DownloadFile;

                using (var response = (FtpWebResponse)reqFTP.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream, Encoding.GetEncoding("big5")))
                {
                    return ParseRecoveryRateFile(reader);
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("讀取檔案時發生錯誤: {0}", ex.Message));
                return null;
            }
        }

        /// <summary>
        /// 解析 Recovery Rate 檔案內容
        /// </summary>
        private RecoveryRateDataContentFormat ParseRecoveryRateFile(StreamReader reader)
        {
            var fileContentFormat = new RecoveryRateDataContentFormat();
            
            try
            {
                bool isBasicInfo = true;
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    line = StringHelper.ConvertEmptyToDefault(line.Trim());
                    if (string.IsNullOrEmpty(line)) continue;

                    string[] values = line.Split(',').Select(v => StringHelper.ConvertEmptyToDefault(v.Trim())).ToArray();

                    if (values[0] == "Test_Item")
                    {
                        isBasicInfo = false;
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

                // 合併資料表
                CombineDataTables(fileContentFormat);
                
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
        /// 合併資料表
        /// </summary>
        private void CombineDataTables(RecoveryRateDataContentFormat contentFormat)
        {
            // 添加 LotInfo 的所有列
            foreach (DataColumn col in contentFormat.LotInfo.Columns)
            {
                contentFormat.FinalRecoveryRateTable.Columns.Add(col.ColumnName, col.DataType);
            }

            // 添加 LotRecoveryRate 的所有列
            foreach (DataColumn col in contentFormat.LotRecoveryRate.Columns)
            {
                contentFormat.FinalRecoveryRateTable.Columns.Add(col.ColumnName, col.DataType);
            }

            // 合併資料
            foreach (DataRow row1 in contentFormat.LotInfo.Rows)
            {
                foreach (DataRow row2 in contentFormat.LotRecoveryRate.Rows)
                {
                    DataRow newRow = contentFormat.FinalRecoveryRateTable.NewRow();
                    
                    foreach (DataColumn col in contentFormat.LotInfo.Columns)
                    {
                        newRow[col.ColumnName] = row1[col.ColumnName];
                    }
                    
                    foreach (DataColumn col in contentFormat.LotRecoveryRate.Columns)
                    {
                        newRow[col.ColumnName] = row2[col.ColumnName];
                    }
                    
                    contentFormat.FinalRecoveryRateTable.Rows.Add(newRow);
                }
            }
        }

        /// <summary>
        /// 驗證 Recovery Rate 資料
        /// </summary>
        private ValidationResult ValidateRecoveryRateData(RecoveryRateDataContentFormat data, string expectedDbKey)
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

            if (!data.CompareRecoveryRate())
            {
                return new ValidationResult(false, "Recovery data field name not match.");
            }

            string fileDbKey = data.LotInfo.Rows[0]["DB Key"]?.ToString();
            if (!expectedDbKey.Equals(fileDbKey))
            {
                return new ValidationResult(false, "The filename does not match the DB_Key in the content.");
            }

            return new ValidationResult(true, string.Empty);
        }

        /// <summary>
        /// 非同步匯入 Recovery Rate 資料
        /// </summary>
        private async Task<bool> ImportRecoveryDataAsync(RecoveryRateDataContentFormat data, DatabaseService databaseService)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var fileProcess = new FileProcess();
                    return fileProcess.ImportRecoveryData(data, databaseService);
                });
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
                string checkLogFileName = string.Format("DCT_data_check_log_recoveryRate_{0}.csv", dateStr);
                
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
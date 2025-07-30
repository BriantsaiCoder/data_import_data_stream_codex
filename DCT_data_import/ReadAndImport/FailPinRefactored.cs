using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DCT_data_import.Common;
using static DCT_data_import.DbObject;

namespace DCT_data_import.ReadAndImport
{
    /// <summary>
    /// FailPin 重構版本
    /// 統一錯誤處理並使用共用模組
    /// </summary>
    public class FailPinRefactored : ImportData
    {
        private readonly FtpService _ftpService;
        private readonly FileValidator _fileValidator;
        private readonly DatabaseHelper _databaseHelper;
        private readonly WriteToLog _writeToLog;

        public FailPinRefactored()
        {
            _ftpService = new FtpService();
            _fileValidator = new FileValidator();
            _databaseHelper = new DatabaseHelper();
            _writeToLog = new WriteToLog();
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
        /// 讀取並匯入 Fail Pin Log - 重構版本
        /// </summary>
        public async Task<ImportResult> ReadAndImportFailPinLog(FileProcess fileAccess, DatabaseService databaseService, string dbKey)
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
                string filename = string.Format("fail_pin_{0}.csv", dbKey);
                string ftpFilePath = BuildFtpFilePath(filename);
                string errorDir = BuildErrorDirectory();

                // 3. 檢查檔案存在性
                if (!_ftpService.CheckIfFileExists(ftpFilePath, Program.FTP_USER, Program.FTP_PASSWORD))
                {
                    _writeToLog.WriteToDataImportLog(string.Format("Fail Pin Log File not found: {0}", ftpFilePath));
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(0, "File not found.");
                }

                // 4. 讀取檔案內容
                stopWatch.Start();
                var failPinData = await ReadFailPinFileAsync(ftpFilePath);
                stopWatch.Stop();
                readTakeTime = Math.Round(stopWatch.Elapsed.TotalSeconds, 3);

                // 5. 驗證檔案內容
                var validationResult = ValidateFailPinData(failPinData, dbKey);
                if (!validationResult.Success)
                {
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(2, validationResult.Message);
                }

                // 6. 檢查 DB Key 是否已存在
                bool isDBKeyExist = fileAccess.IsDBKeyExistInDB("fail_pin_rate_info", dbKey, databaseService);
                if (isDBKeyExist)
                {
                    _writeToLog.WriteToDataImportLog(string.Format("資料庫已存在此資料: {0}", filename));
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(3, "The same DB_Key exists in the database.");
                }

                // 7. 匯入資料庫
                stopWatch.Restart();
                bool importResult = await ImportFailPinAsync(failPinData, databaseService, fileAccess);
                stopWatch.Stop();
                importTakeTime = Math.Round(stopWatch.Elapsed.TotalSeconds, 3);

                // 8. 記錄處理結果
                LogProcessingResult(filename, ftpFilePath, readTakeTime, importTakeTime);

                if (importResult)
                {
                    Console.WriteLine(string.Format("匯入完成! Fail Pin 檔名: {0} 耗時: {1} 秒", filename, (int)stopWatch.Elapsed.TotalSeconds));
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
                _writeToLog.WriteToDataImportLog(string.Format("ReadAndImportFailPinLog 發生錯誤: {0}", ex.Message));
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
                ? "/DCT_Log/DCT_DB_DATA_Dev/Fail_Pin_Log/ST_RT_AT/" 
                : "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log/ST_RT_AT/";
            
            return basePath + subPath + filename;
        }

        /// <summary>
        /// 建立錯誤目錄路徑
        /// </summary>
        private string BuildErrorDirectory()
        {
            string basePath = string.Format("ftp://{0}", Program.FTP_IP);
            return Program.Environment == "Dev" 
                ? basePath + "/DCT_Log/DCT_DB_DATA_Dev/Fail_Pin_Log_Error/"
                : basePath + "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/";
        }

        /// <summary>
        /// 非同步讀取 Fail Pin 檔案
        /// </summary>
        private async Task<FailPinLogContentFormat> ReadFailPinFileAsync(string ftpFilePath)
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
                    return await Task.Run(() => ParseFailPinFile(reader));
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("讀取 Fail Pin 檔案時發生錯誤: {0}", ex.Message));
                return null;
            }
        }

        /// <summary>
        /// 解析 Fail Pin 檔案內容
        /// </summary>
        private FailPinLogContentFormat ParseFailPinFile(StreamReader reader)
        {
            var failPinLogContentFormat = new FailPinLogContentFormat();
            
            try
            {
                string data_format = string.Empty;
                int content_part = 1;
                int fail_pin_list_id = 0;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = EraseSpecificChar(line);
                    
                    if (values == null || values.Length < 1) continue;

                    // 處理數據格式標記
                    if (values[0] == "Data format")
                    {
                        data_format = values.Length > 1 ? values[1] : string.Empty;
                        continue;
                    }

                    // 處理區段轉換
                    if (values[0] == "DUT")
                    {
                        content_part = 2;
                        continue;
                    }

                    // 根據區段處理資料
                    switch (content_part)
                    {
                        case 1: // Fail pin rate info 區段
                            ProcessFailPinRateInfo(failPinLogContentFormat, values);
                            break;
                        case 2: // Fail pin rate list 區段
                            ProcessFailPinRateList(failPinLogContentFormat, values, data_format, ref fail_pin_list_id);
                            break;
                    }
                }

                return failPinLogContentFormat;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("解析 Fail Pin 檔案內容時發生錯誤: {0}", ex.Message));
                failPinLogContentFormat.ErrMsg = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// 處理 Fail Pin Rate Info 區段
        /// </summary>
        private void ProcessFailPinRateInfo(FailPinLogContentFormat contentFormat, string[] values)
        {
            if (values.Length >= 1)
            {
                contentFormat.Fail_pin_rate_info.Columns.Add(values[0], typeof(string));
                contentFormat.Fail_pin_rate_info.Rows[0][values[0]] = 
                    values.Length > 1 ? StringHelper.ConvertEmptyToDefault(values[1]) : string.Empty;
            }
        }

        /// <summary>
        /// 處理 Fail Pin Rate List 區段
        /// </summary>
        private void ProcessFailPinRateList(FailPinLogContentFormat contentFormat, string[] values, 
            string data_format, ref int fail_pin_list_id)
        {
            if (values.Length >= 3)
            {
                // 建立 fail pin rate list 記錄
                DataRow dr_fail_pin_rate_list = contentFormat.Fail_pin_rate_list.NewRow();
                for (int i = 0; i < 3; i++)
                {
                    dr_fail_pin_rate_list[i] = StringHelper.ConvertEmptyToDefault(values[i]);
                }
                contentFormat.Fail_pin_rate_list.Rows.Add(dr_fail_pin_rate_list);

                // 解析 fail pin 資料
                var parseResult = ParseFailPinData(values, fail_pin_list_id + 1, data_format);
                fail_pin_list_id++;

                // 添加測試結果
                contentFormat.Fail_pin_rate_list_test_result.Tables.Add(parseResult.TestResultTable);

                // 添加 pin/ball 資料
                foreach (DataRow row in parseResult.PinBallData.Rows)
                {
                    contentFormat.Fail_pin_rate_list_pin_ball.Rows.Add(row);
                }
            }
        }

        /// <summary>
        /// Fail Pin 資料解析結果
        /// </summary>
        private class FailPinParseResult
        {
            public DataTable TestResultTable { get; set; }
            public DataTable PinBallData { get; set; }

            public FailPinParseResult()
            {
                TestResultTable = InitTestResultTable();
                PinBallData = new DataTable();
                // 建立 PinBallData 結構
                PinBallData.Columns.Add("pin", typeof(string));
                PinBallData.Columns.Add("ball", typeof(string));
                PinBallData.Columns.Add("fail_pin_rate_list_id", typeof(int));
                PinBallData.Columns.Add("remark", typeof(string));
            }

            private DataTable InitTestResultTable()
            {
                DataTable dt = new DataTable();
                dt.Columns.Add("item_name", typeof(string));
                dt.Columns.Add("open", typeof(string));
                dt.Columns.Add("short", typeof(string));
                dt.Columns.Add("vmeas", typeof(string));
                return dt;
            }
        }

        /// <summary>
        /// 解析 Fail Pin 資料
        /// </summary>
        private FailPinParseResult ParseFailPinData(string[] values, int listId, string dataFormat)
        {
            var result = new FailPinParseResult();
            var fail_pin_list = new List<string>();
            var fail_pin_log = new List<string>();

            int fail_pin_part = 1;
            int row_index = -1, column_index = 0;

            for (int i = 3; i < values.Length; i++)
            {
                string value = values[i];

                // 處理分隔符號
                if (value == ";")
                {
                    fail_pin_part = 2;
                    continue;
                }
                else if (value == "@")
                {
                    fail_pin_part = 3;
                    result.TestResultTable.Rows.Add(result.TestResultTable.NewRow());
                    row_index++;
                    column_index = 0;
                    continue;
                }

                // 根據部分處理資料
                switch (fail_pin_part)
                {
                    case 1: // Fail pin list
                        fail_pin_list.Add(value);
                        break;
                    case 2: // Fail pin log
                        fail_pin_log.Add(value);
                        break;
                    case 3: // Test result
                        ProcessTestResultValue(result.TestResultTable, row_index, column_index, value);
                        column_index++;
                        break;
                }
            }

            // 處理 pin/ball 資料
            ProcessPinBallData(result, fail_pin_list, fail_pin_log, listId, dataFormat);

            return result;
        }

        /// <summary>
        /// 處理測試結果值
        /// </summary>
        private void ProcessTestResultValue(DataTable testResultTable, int rowIndex, int columnIndex, string value)
        {
            if (rowIndex >= 0 && rowIndex < testResultTable.Rows.Count && columnIndex < testResultTable.Columns.Count)
            {
                // 數值欄位進行格式驗證
                if (columnIndex > 0) // 非 item_name 欄位
                {
                    if (!double.TryParse(value, out double tmp_val))
                    {
                        value = null;
                    }
                }
                testResultTable.Rows[rowIndex][columnIndex] = StringHelper.ConvertEmptyToDefault(value);
            }
        }

        /// <summary>
        /// 處理 Pin/Ball 資料
        /// </summary>
        private void ProcessPinBallData(FailPinParseResult result, List<string> failPinList, 
            List<string> failPinLog, int listId, string dataFormat)
        {
            string remarkText = string.Join(",", failPinLog.ToArray());

            foreach (string failPin in failPinList)
            {
                DataRow dr_pin_ball = result.PinBallData.NewRow();
                string[] value_split = failPin.Split('(', ')');

                if (dataFormat == "Pin")
                {
                    dr_pin_ball["pin"] = value_split.Length > 0 ? StringHelper.ConvertEmptyToDefault(value_split[0]) : string.Empty;
                    dr_pin_ball["ball"] = value_split.Length > 1 ? StringHelper.ConvertEmptyToDefault(value_split[1]) : string.Empty;
                }
                else if (dataFormat == "Ball")
                {
                    dr_pin_ball["ball"] = value_split.Length > 0 ? StringHelper.ConvertEmptyToDefault(value_split[0]) : string.Empty;
                    dr_pin_ball["pin"] = value_split.Length > 1 ? StringHelper.ConvertEmptyToDefault(value_split[1]) : string.Empty;
                }

                dr_pin_ball["fail_pin_rate_list_id"] = listId;
                dr_pin_ball["remark"] = StringHelper.ConvertEmptyToDefault(remarkText);
                result.PinBallData.Rows.Add(dr_pin_ball);
            }
        }

        /// <summary>
        /// 驗證 Fail Pin 資料
        /// </summary>
        private ValidationResult ValidateFailPinData(FailPinLogContentFormat data, string expectedDbKey)
        {
            if (data == null)
            {
                return new ValidationResult(false, "檔案讀取失敗");
            }

            if (!string.IsNullOrEmpty(data.ErrMsg))
            {
                return new ValidationResult(false, data.ErrMsg);
            }

            if (data.Fail_pin_rate_info.Rows.Count < 1)
            {
                return new ValidationResult(false, "File content is missing.");
            }

            if (!data.CompareInfo())
            {
                return new ValidationResult(false, "Information field name not match.");
            }

            return new ValidationResult(true, string.Empty);
        }

        /// <summary>
        /// 非同步匯入 Fail Pin 資料
        /// </summary>
        private async Task<bool> ImportFailPinAsync(FailPinLogContentFormat data, DatabaseService databaseService, FileProcess fileAccess)
        {
            try
            {
                return await Task.Run(() => fileAccess.ImportFailPinLog(data, databaseService));
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("匯入 Fail Pin 資料時發生錯誤: {0}", ex.Message));
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
                string checkLogFileName = string.Format("DCT_data_check_log_failPin_{0}.csv", dateStr);
                
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
using System;
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
    /// Tester 重構版本
    /// 使用共用模組統一狀態處理邏輯並提升維護性
    /// </summary>
    public class TesterRefactored : ImportData
    {
        private readonly FtpService _ftpService;
        private readonly FileValidator _fileValidator;
        private readonly DatabaseHelper _databaseHelper;
        private readonly WriteToLog _writeToLog;

        public TesterRefactored()
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
        /// 讀取並匯入 Tester Status - 重構版本
        /// </summary>
        public async Task<ImportResult> ReadAndImportTesterStatus(FileProcess fileAccess, DatabaseService databaseService, string dbKey)
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
                string filename = string.Format("tester_{0}.csv", dbKey);
                string ftpFilePath = BuildFtpFilePath(filename);
                string errorDir = BuildErrorDirectory();

                // 3. 檢查檔案存在性
                if (!_ftpService.CheckIfFileExists(ftpFilePath, Program.FTP_USER, Program.FTP_PASSWORD))
                {
                    _writeToLog.WriteToDataImportLog(string.Format("Tester Status File not found: {0}", ftpFilePath));
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(0, "File not found.");
                }

                // 4. 讀取檔案內容
                stopWatch.Start();
                var testerStatusData = ReadTesterStatusFile(ftpFilePath);
                stopWatch.Stop();
                readTakeTime = Math.Round(stopWatch.Elapsed.TotalSeconds, 3);

                if (testerStatusData == null)
                {
                    return new ImportResult(2, "檔案讀取失敗");
                }

                // 5. 驗證檔案內容
                var validationResult = ValidateTesterStatusData(testerStatusData, dbKey);
                if (!validationResult.Success)
                {
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(2, validationResult.Message);
                }

                // 6. 檢查 DB Key 是否已存在
                bool isDBKeyExist = fileAccess.IsDBKeyExistInDB("tester_device_info", dbKey, databaseService);
                if (isDBKeyExist)
                {
                    _writeToLog.WriteToDataImportLog(string.Format("資料庫已存在此資料: {0}", filename));
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(3, "The same DB_Key exists in the database.");
                }

                // 7. 匯入資料庫
                stopWatch.Restart();
                bool importResult = await ImportTesterStatusAsync(testerStatusData, databaseService, fileAccess);
                stopWatch.Stop();
                importTakeTime = Math.Round(stopWatch.Elapsed.TotalSeconds, 3);

                // 8. 記錄處理結果
                LogProcessingResult(filename, ftpFilePath, readTakeTime, importTakeTime);

                if (importResult)
                {
                    Console.WriteLine(string.Format("匯入完成! Tester Status 檔名: {0} 耗時: {1} 秒", filename, (int)stopWatch.Elapsed.TotalSeconds));
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
                _writeToLog.WriteToDataImportLog(string.Format("ReadAndImportTesterStatus 發生錯誤: {0}", ex.Message));
                return new ImportResult(3, string.Format("Exception error occurred during reading and import. {0}", ex.Message));
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
                ? "/DCT_Log/DCT_DB_DATA_Dev/Tester_Status/" 
                : "/DCT_Log/DCT_DB_DATA/Tester_Status/";
            
            return basePath + subPath + filename;
        }

        /// <summary>
        /// 建立錯誤目錄路徑
        /// </summary>
        private string BuildErrorDirectory()
        {
            string basePath = string.Format("ftp://{0}", Program.FTP_IP);
            return Program.Environment == "Dev" 
                ? basePath + "/DCT_Log/DCT_DB_DATA_Dev/Tester_Status_Error/"
                : basePath + "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/";
        }

        /// <summary>
        /// 讀取 Tester Status 檔案
        /// </summary>
        private TestStatusContentFormat ReadTesterStatusFile(string ftpFilePath)
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
                    return ParseTesterStatusFile(reader);
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("讀取檔案時發生錯誤: {0}", ex.Message));
                return null;
            }
        }

        /// <summary>
        /// 解析 Tester Status 檔案內容
        /// </summary>
        private TestStatusContentFormat ParseTesterStatusFile(StreamReader reader)
        {
            var testStatusContentFormat = new TestStatusContentFormat();
            
            try
            {
                int content_part = 1;
                
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    
                    // 檢查中文字符
                    if (!string.IsNullOrEmpty(line) && StringHelper.ContainsChinese(line))
                    {
                        testStatusContentFormat.ErrMsg = "Chinese word exists.";
                    }

                    var values = EraseSpecificChar(line);
                    if (values.Length < 1) continue;

                    // 根據關鍵字決定處理區段
                    content_part = DetermineContentPart(values[0], content_part);
                    if (IsHeaderRow(values[0])) continue;

                    // 根據區段處理資料
                    switch (content_part)
                    {
                        case 1: // Device information
                            ProcessDeviceInformation(testStatusContentFormat, values);
                            break;
                        case 2: // Tester status
                            ProcessTesterStatus(testStatusContentFormat, values);
                            break;
                        case 3: // SW version
                            ProcessSoftwareVersion(testStatusContentFormat, values);
                            break;
                        case 4: // Production analysis
                            var result = ProcessProductionAnalysis(testStatusContentFormat, values);
                            if (result) return testStatusContentFormat;
                            break;
                    }
                }

                return testStatusContentFormat;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("解析檔案內容時發生錯誤: {0}", ex.Message));
                testStatusContentFormat.ErrMsg = string.Format("讀檔內容錯誤, Error: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 決定內容區段
        /// </summary>
        private int DetermineContentPart(string value, int currentPart)
        {
            switch (value)
            {
                case "Device information": return 1;
                case "Tester status": return 2;
                case "SW version": return 3;
                case "Production analysis": return 4;
                default: return currentPart;
            }
        }

        /// <summary>
        /// 檢查是否為標題行
        /// </summary>
        private bool IsHeaderRow(string value)
        {
            string[] headers = { "Device information", "Tester status", "SW version", "Production analysis" };
            return Array.IndexOf(headers, value) >= 0;
        }

        /// <summary>
        /// 處理設備資訊區段
        /// </summary>
        private void ProcessDeviceInformation(TestStatusContentFormat contentFormat, string[] values)
        {
            if (values[0] == "DB_Key")
            {
                for (int i = 0; i < values.Length; i++)
                {
                    contentFormat.Tester_device_info.Columns.Add(values[i], typeof(string));
                }
            }
            else if (contentFormat.Tester_device_info.Columns.Count > 0)
            {
                DataRow dr_tester_device_info = contentFormat.Tester_device_info.NewRow();
                for (int i = 0; i < Math.Min(contentFormat.Tester_device_info.Columns.Count, values.Length); i++)
                {
                    dr_tester_device_info[i] = StringHelper.ConvertEmptyToDefault(values[i]);
                }
                contentFormat.Tester_device_info.Rows.Add(dr_tester_device_info);
            }
        }

        /// <summary>
        /// 處理測試器狀態區段
        /// </summary>
        private void ProcessTesterStatus(TestStatusContentFormat contentFormat, string[] values)
        {
            if (values[0] == "DPW")
            {
                for (int i = 0; i < values.Length; i++)
                {
                    contentFormat.Tester_status.Columns.Add(values[i], typeof(string));
                }
            }
            else if (contentFormat.Tester_status.Columns.Count > 0)
            {
                DataRow dr_tester_status = contentFormat.Tester_status.NewRow();
                for (int i = 0; i < Math.Min(values.Length, contentFormat.Tester_status.Columns.Count); i++)
                {
                    dr_tester_status[i] = StringHelper.ConvertEmptyToDefault(values[i]);
                }
                contentFormat.Tester_status.Rows.Add(dr_tester_status);
            }
        }

        /// <summary>
        /// 處理軟體版本區段
        /// </summary>
        private void ProcessSoftwareVersion(TestStatusContentFormat contentFormat, string[] values)
        {
            if (values[0] == "PUI version")
            {
                for (int i = 0; i < values.Length; i++)
                {
                    contentFormat.Tester_sw_version.Columns.Add(values[i], typeof(string));
                }
            }
            else if (contentFormat.Tester_sw_version.Columns.Count > 0)
            {
                DataRow dr_tester_sw_version = contentFormat.Tester_sw_version.NewRow();
                for (int i = 0; i < Math.Min(values.Length, contentFormat.Tester_sw_version.Columns.Count); i++)
                {
                    dr_tester_sw_version[i] = StringHelper.ConvertEmptyToDefault(values[i]);
                }
                contentFormat.Tester_sw_version.Rows.Add(dr_tester_sw_version);
            }
        }

        /// <summary>
        /// 處理生產分析區段
        /// </summary>
        private bool ProcessProductionAnalysis(TestStatusContentFormat contentFormat, string[] values)
        {
            if (values[0] == "site1_yield")
            {
                for (int i = 0; i < values.Length; i++)
                {
                    contentFormat.Tester_production_analysis.Columns.Add(values[i], typeof(string));
                }
                return false;
            }
            else if (contentFormat.Tester_production_analysis.Columns.Count > 0)
            {
                DataRow dr_tester_production_analysis = contentFormat.Tester_production_analysis.NewRow();
                for (int i = 0; i < Math.Min(values.Length, contentFormat.Tester_production_analysis.Columns.Count); i++)
                {
                    dr_tester_production_analysis[i] = StringHelper.ConvertEmptyToDefault(values[i]);
                }
                contentFormat.Tester_production_analysis.Rows.Add(dr_tester_production_analysis);
                return true; // 處理完成，返回結果
            }
            return false;
        }

        /// <summary>
        /// 驗證 Tester Status 資料
        /// </summary>
        private ValidationResult ValidateTesterStatusData(TestStatusContentFormat data, string expectedDbKey)
        {
            if (!string.IsNullOrEmpty(data.ErrMsg))
            {
                return new ValidationResult(false, data.ErrMsg);
            }

            if (data.Tester_device_info.Rows.Count < 1)
            {
                return new ValidationResult(false, "File content is missing.");
            }

            if (!data.CompareInfo())
            {
                return new ValidationResult(false, "Information field name not match.");
            }

            if (!data.CompareStatus())
            {
                return new ValidationResult(false, "tester_status field name not match.");
            }

            string fileDbKey = data.Tester_device_info.Rows[0]["DB_Key"]?.ToString();
            if (!expectedDbKey.Equals(fileDbKey))
            {
                return new ValidationResult(false, "The filename does not match the DB_Key in the content.");
            }

            return new ValidationResult(true, string.Empty);
        }

        /// <summary>
        /// 非同步匯入 Tester Status 資料
        /// </summary>
        private async Task<bool> ImportTesterStatusAsync(TestStatusContentFormat data, DatabaseService databaseService, FileProcess fileAccess)
        {
            try
            {
                return await Task.Run(() => fileAccess.ImportTesterStatus(data, databaseService));
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
                string checkLogFileName = string.Format("DCT_data_check_log_tester_{0}.csv", dateStr);
                
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
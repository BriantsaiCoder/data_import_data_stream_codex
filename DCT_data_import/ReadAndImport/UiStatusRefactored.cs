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
    /// UiStatus 重構版本
    /// 統一錯誤處理並使用共用模組
    /// </summary>
    public class UiStatusRefactored : ImportData
    {
        private readonly FtpService _ftpService;
        private readonly FileValidator _fileValidator;
        private readonly DatabaseHelper _databaseHelper;
        private readonly WriteToLog _writeToLog;

        public UiStatusRefactored()
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
        /// 讀取並匯入 UI Status - 重構版本
        /// </summary>
        public async Task<ImportResult> ReadAndImportUIStatus(FileProcess fileAccess, DatabaseService databaseService, string dbKeyUiStatus)
        {
            var stopWatch = new Stopwatch();
            double importTakeTime = 0;

            try
            {
                // 1. 驗證輸入參數
                if (string.IsNullOrEmpty(dbKeyUiStatus))
                {
                    return new ImportResult(0, "DB Key 不能為空");
                }

                // 2. 建立檔案路徑
                string filename = string.Format("ui_status_{0}.csv", dbKeyUiStatus);
                string ftpFilePath = BuildFtpFilePath(filename);
                string errorDir = BuildErrorDirectory();

                // 3. 檢查檔案存在性
                if (!_ftpService.CheckIfFileExists(ftpFilePath, Program.FTP_USER, Program.FTP_PASSWORD))
                {
                    _writeToLog.WriteToDataImportLog(string.Format("UI Status File not found: {0}", ftpFilePath));
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(0, "File not found.");
                }

                // 4. 讀取檔案內容
                var uiStatusData = await ReadUiStatusFileAsync(ftpFilePath);

                // 5. 驗證檔案內容
                var validationResult = ValidateUiStatusData(uiStatusData);
                if (!validationResult.Success)
                {
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(2, validationResult.Message);
                }

                // 6. 匯入資料庫
                stopWatch.Start();
                bool importResult = await ImportUiStatusAsync(uiStatusData, databaseService, fileAccess);
                stopWatch.Stop();
                importTakeTime = Math.Round(stopWatch.Elapsed.TotalSeconds, 3);

                if (importResult)
                {
                    Console.WriteLine(string.Format("匯入完成! UI Status {0} 耗時: {1} 秒", filename, (int)stopWatch.Elapsed.TotalSeconds));
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
                _writeToLog.WriteToDataImportLog(string.Format("ReadAndImportUIStatus 發生錯誤: {0}", ex.Message));
                return new ImportResult(3, string.Format("Exception error occurred during import. {0}", ex.Message));
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
                ? "/DCT_Log/DCT_DB_DATA_Dev/UI_Status/" 
                : "/DCT_Log/DCT_DB_DATA/UI_Status/";
            
            return basePath + subPath + filename;
        }

        /// <summary>
        /// 建立錯誤目錄路徑
        /// </summary>
        private string BuildErrorDirectory()
        {
            string basePath = string.Format("ftp://{0}", Program.FTP_IP);
            return Program.Environment == "Dev" 
                ? basePath + "/DCT_Log/DCT_DB_DATA_Dev/UI_Status_Error/"
                : basePath + "/DCT_Log/DCT_DB_DATA/UI_Status_Error/";
        }

        /// <summary>
        /// 非同步讀取 UI Status 檔案
        /// </summary>
        private async Task<UIStatusContentFormat> ReadUiStatusFileAsync(string ftpFilePath)
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
                    return await Task.Run(() => ParseUiStatusFile(reader));
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("讀取 UI Status 檔案時發生錯誤: {0}", ex.Message));
                return null;
            }
        }

        /// <summary>
        /// 解析 UI Status 檔案內容
        /// </summary>
        private UIStatusContentFormat ParseUiStatusFile(StreamReader reader)
        {
            var uiStatusContentFormat = new UIStatusContentFormat();
            
            try
            {
                bool isHeaderProcessed = false;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = EraseSpecificChar(line);
                    
                    if (values == null || values.Length < 1) continue;

                    if (!isHeaderProcessed && values[0] == "Mac_Address")
                    {
                        // 處理標題行
                        ProcessHeaderRow(uiStatusContentFormat, values);
                        isHeaderProcessed = true;
                    }
                    else if (isHeaderProcessed)
                    {
                        // 處理資料行
                        ProcessDataRow(uiStatusContentFormat, values);
                    }
                }

                return uiStatusContentFormat;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("解析 UI Status 檔案內容時發生錯誤: {0}", ex.Message));
                uiStatusContentFormat.ErrMsg = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// 處理標題行
        /// </summary>
        private void ProcessHeaderRow(UIStatusContentFormat contentFormat, string[] values)
        {
            foreach (string value in values)
            {
                string normalizedColumnName = StringHelper.NormalizeColumnName(value);
                contentFormat.UI_status.Columns.Add(normalizedColumnName, typeof(string));
            }
        }

        /// <summary>
        /// 處理資料行
        /// </summary>
        private void ProcessDataRow(UIStatusContentFormat contentFormat, string[] values)
        {
            DataRow dr_UI_status = contentFormat.UI_status.NewRow();
            
            for (int i = 0; i < Math.Min(values.Length, contentFormat.UI_status.Columns.Count); i++)
            {
                dr_UI_status[i] = StringHelper.ConvertEmptyToDefault(values[i]);
            }
            
            contentFormat.UI_status.Rows.Add(dr_UI_status);
        }

        /// <summary>
        /// 驗證 UI Status 資料
        /// </summary>
        private ValidationResult ValidateUiStatusData(UIStatusContentFormat data)
        {
            if (data == null)
            {
                return new ValidationResult(false, "檔案讀取失敗");
            }

            if (!string.IsNullOrEmpty(data.ErrMsg))
            {
                return new ValidationResult(false, data.ErrMsg);
            }

            if (data.UI_status.Rows.Count < 1)
            {
                return new ValidationResult(false, "File content is missing.");
            }

            if (!data.CompareUiStatus())
            {
                return new ValidationResult(false, "ui_status field name not match.");
            }

            return new ValidationResult(true, string.Empty);
        }

        /// <summary>
        /// 非同步匯入 UI Status 資料
        /// </summary>
        private async Task<bool> ImportUiStatusAsync(UIStatusContentFormat data, DatabaseService databaseService, FileProcess fileAccess)
        {
            try
            {
                return await Task.Run(() => fileAccess.ImportUIStatus(data, databaseService));
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("匯入 UI Status 資料時發生錯誤: {0}", ex.Message));
                return false;
            }
        }
    }
}
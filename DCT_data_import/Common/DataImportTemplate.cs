using System;
using System.Threading.Tasks;
using DCT_data_import.Common;
using static DCT_data_import.DbObject;

namespace DCT_data_import.Common
{
    /// <summary>
    /// 資料匯入範本抽象基底類別
    /// 定義標準匯入流程並提供統一錯誤處理機制
    /// </summary>
    public abstract class DataImportTemplate
    {
        protected readonly WriteToLog _writeToLog;
        protected readonly DatabaseHelper _databaseHelper;
        protected readonly FileValidator _fileValidator;
        protected readonly FtpService _ftpService;

        protected DataImportTemplate()
        {
            _writeToLog = new WriteToLog();
            _databaseHelper = new DatabaseHelper();
            _fileValidator = new FileValidator();
            _ftpService = new FtpService();
        }

        /// <summary>
        /// 匯入結果類別
        /// </summary>
        public class DataImportResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public string DbKey { get; set; }
            public Exception Exception { get; set; }

            public DataImportResult(bool success, string message, string dbKey = "", Exception exception = null)
            {
                Success = success;
                Message = message ?? string.Empty;
                DbKey = dbKey ?? string.Empty;
                Exception = exception;
            }
        }

        /// <summary>
        /// 標準匯入流程範本方法
        /// </summary>
        public async Task<DataImportResult> ExecuteImportAsync(string dbKey, DatabaseService databaseService)
        {
            var result = new DataImportResult(false, string.Empty, dbKey);

            try
            {
                // 1. 驗證輸入參數
                if (!ValidateInputParameters(dbKey, databaseService, result))
                {
                    return result;
                }

                // 2. 準備匯入環境
                if (!await PrepareImportEnvironmentAsync(dbKey, databaseService, result))
                {
                    return result;
                }

                // 3. 下載並驗證檔案
                var fileData = await DownloadAndValidateFileAsync(dbKey, result);
                if (fileData == null)
                {
                    return result;
                }

                // 4. 處理和轉換資料
                var processedData = await ProcessDataAsync(fileData, result);
                if (processedData == null)
                {
                    return result;
                }

                // 5. 匯入資料庫
                if (!await ImportToDatabaseAsync(processedData, databaseService, result))
                {
                    await RollbackChangesAsync(dbKey, databaseService);
                    return result;
                }

                // 6. 清理暫存檔案
                await CleanupTempFilesAsync(dbKey);

                // 7. 記錄成功結果
                result.Success = true;
                result.Message = "匯入成功";
                
                _writeToLog.WriteToDataImportLog(string.Format("DB Key {0} 匯入成功", dbKey));
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = string.Format("匯入過程發生未預期錯誤: {0}", ex.Message);
                result.Exception = ex;
                
                _writeToLog.WriteToDataImportLog(result.Message);
                
                // 嘗試回滾變更
                try
                {
                    await RollbackChangesAsync(dbKey, databaseService);
                }
                catch (Exception rollbackEx)
                {
                    _writeToLog.WriteToDataImportLog(string.Format("回滾變更時發生錯誤: {0}", rollbackEx.Message));
                }

                return result;
            }
        }

        /// <summary>
        /// 驗證輸入參數
        /// </summary>
        protected virtual bool ValidateInputParameters(string dbKey, DatabaseService databaseService, DataImportResult result)
        {
            if (string.IsNullOrEmpty(dbKey))
            {
                result.Message = "DB Key 不能為空";
                return false;
            }

            if (databaseService == null)
            {
                result.Message = "DatabaseService 不能為 null";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 準備匯入環境 - 抽象方法，由子類實作
        /// </summary>
        protected abstract Task<bool> PrepareImportEnvironmentAsync(string dbKey, DatabaseService databaseService, DataImportResult result);

        /// <summary>
        /// 下載並驗證檔案 - 抽象方法，由子類實作
        /// </summary>
        protected abstract Task<object> DownloadAndValidateFileAsync(string dbKey, DataImportResult result);

        /// <summary>
        /// 處理和轉換資料 - 抽象方法，由子類實作
        /// </summary>
        protected abstract Task<object> ProcessDataAsync(object fileData, DataImportResult result);

        /// <summary>
        /// 匯入資料庫 - 抽象方法，由子類實作
        /// </summary>
        protected abstract Task<bool> ImportToDatabaseAsync(object processedData, DatabaseService databaseService, DataImportResult result);

        /// <summary>
        /// 回滾變更 - 虛擬方法，子類可以覆寫
        /// </summary>
        protected virtual async Task RollbackChangesAsync(string dbKey, DatabaseService databaseService)
        {
            _writeToLog.WriteToDataImportLog(string.Format("嘗試回滾 DB Key {0} 的變更", dbKey));
            await Task.CompletedTask;
        }

        /// <summary>
        /// 清理暫存檔案 - 虛擬方法，子類可以覆寫
        /// </summary>
        protected virtual async Task CleanupTempFilesAsync(string dbKey)
        {
            _writeToLog.WriteToDataImportLog(string.Format("清理 DB Key {0} 的暫存檔案", dbKey));
            await Task.CompletedTask;
        }
    }
}
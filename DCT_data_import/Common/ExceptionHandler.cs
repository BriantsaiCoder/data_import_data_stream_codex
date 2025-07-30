using System;
using System.Text;

namespace DCT_data_import.Common
{
    /// <summary>
    /// 異常處理器
    /// 統一異常處理策略和結構化錯誤回應
    /// </summary>
    public class ExceptionHandler
    {
        private readonly WriteToLog _writeToLog;

        public ExceptionHandler()
        {
            _writeToLog = new WriteToLog();
        }

        /// <summary>
        /// 異常處理結果
        /// </summary>
        public class ExceptionResult
        {
            public bool IsHandled { get; set; }
            public string ErrorCode { get; set; }
            public string ErrorMessage { get; set; }
            public string DetailMessage { get; set; }
            public Exception OriginalException { get; set; }

            public ExceptionResult(bool isHandled, string errorCode, string errorMessage, string detailMessage = "", Exception originalException = null)
            {
                IsHandled = isHandled;
                ErrorCode = errorCode;
                ErrorMessage = errorMessage;
                DetailMessage = detailMessage;
                OriginalException = originalException;
            }
        }

        /// <summary>
        /// 處理一般異常
        /// </summary>
        public ExceptionResult HandleException(Exception ex, string context = "")
        {
            try
            {
                string errorCode = GenerateErrorCode(ex);
                string errorMessage = ex.Message;
                string detailMessage = GenerateDetailMessage(ex, context);

                // 記錄詳細錯誤資訊
                _writeToLog.WriteToDataImportLog(string.Format("[錯誤] {0} - {1}: {2}", errorCode, context, detailMessage));

                return new ExceptionResult(true, errorCode, errorMessage, detailMessage, ex);
            }
            catch (Exception logEx)
            {
                // 避免在錯誤處理中產生新的錯誤
                Console.WriteLine(string.Format("異常處理器發生錯誤: {0}", logEx.Message));
                return new ExceptionResult(false, "EH001", "異常處理器內部錯誤", ex.Message, ex);
            }
        }

        /// <summary>
        /// 處理檔案操作異常
        /// </summary>
        public ExceptionResult HandleFileException(Exception ex, string filePath = "")
        {
            string context = string.Format("檔案操作 ({0})", filePath);
            var result = HandleException(ex, context);
            
            // 檔案操作特殊處理
            if (ex is System.IO.FileNotFoundException)
            {
                result.ErrorCode = "FILE001";
                result.ErrorMessage = "檔案不存在";
            }
            else if (ex is System.IO.DirectoryNotFoundException)
            {
                result.ErrorCode = "FILE002";
                result.ErrorMessage = "目錄不存在";
            }
            else if (ex is System.IO.IOException)
            {
                result.ErrorCode = "FILE003";
                result.ErrorMessage = "檔案 I/O 錯誤";
            }

            return result;
        }

        /// <summary>
        /// 處理網路操作異常
        /// </summary>
        public ExceptionResult HandleNetworkException(Exception ex, string operation = "")
        {
            string context = string.Format("網路操作 ({0})", operation);
            var result = HandleException(ex, context);

            // 網路操作特殊處理
            if (ex is System.Net.WebException)
            {
                result.ErrorCode = "NET001";
                result.ErrorMessage = "網路請求錯誤";
            }
            else if (ex is System.Net.Sockets.SocketException)
            {
                result.ErrorCode = "NET002";
                result.ErrorMessage = "網路連接錯誤";
            }
            else if (ex is TimeoutException)
            {
                result.ErrorCode = "NET003";
                result.ErrorMessage = "網路請求逾時";
            }

            return result;
        }

        /// <summary>
        /// 處理資料庫操作異常
        /// </summary>
        public ExceptionResult HandleDatabaseException(Exception ex, string operation = "")
        {
            string context = string.Format("資料庫操作 ({0})", operation);
            var result = HandleException(ex, context);

            // 資料庫操作特殊處理
            if (ex.Message.Contains("connection") || ex.Message.Contains("連接"))
            {
                result.ErrorCode = "DB001";
                result.ErrorMessage = "資料庫連接錯誤";
            }
            else if (ex.Message.Contains("timeout") || ex.Message.Contains("逾時"))
            {
                result.ErrorCode = "DB002";
                result.ErrorMessage = "資料庫查詢逾時";
            }
            else if (ex.Message.Contains("syntax") || ex.Message.Contains("語法"))
            {
                result.ErrorCode = "DB003";
                result.ErrorMessage = "SQL 語法錯誤";
            }
            else
            {
                result.ErrorCode = "DB999";
                result.ErrorMessage = "資料庫操作錯誤";
            }

            return result;
        }

        /// <summary>
        /// 產生錯誤代碼
        /// </summary>
        private string GenerateErrorCode(Exception ex)
        {
            string prefix = "GEN";
            
            if (ex is System.IO.IOException) prefix = "FILE";
            else if (ex is System.Net.WebException) prefix = "NET";
            else if (ex is System.Data.Common.DbException) prefix = "DB";
            else if (ex is ArgumentException) prefix = "ARG";
            else if (ex is InvalidOperationException) prefix = "OP";

            // 使用異常類型的 hash code 產生唯一編號
            int hashCode = Math.Abs(ex.GetType().GetHashCode()) % 1000;
            return string.Format("{0}{1:D3}", prefix, hashCode);
        }

        /// <summary>
        /// 產生詳細錯誤訊息
        /// </summary>
        private string GenerateDetailMessage(Exception ex, string context)
        {
            var sb = new StringBuilder();
            
            if (!string.IsNullOrEmpty(context))
            {
                sb.AppendLine(string.Format("Context: {0}", context));
            }
            
            sb.AppendLine(string.Format("Exception Type: {0}", ex.GetType().Name));
            sb.AppendLine(string.Format("Message: {0}", ex.Message));
            
            if (ex.InnerException != null)
            {
                sb.AppendLine(string.Format("Inner Exception: {0}", ex.InnerException.Message));
            }

            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(ex.StackTrace);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 檢查是否為關鍵錯誤
        /// </summary>
        public bool IsCriticalError(Exception ex)
        {
            return ex is OutOfMemoryException ||
                   ex is StackOverflowException ||
                   ex is System.Threading.ThreadAbortException ||
                   ex is AccessViolationException;
        }

        /// <summary>
        /// 記錄關鍵錯誤
        /// </summary>
        public void LogCriticalError(Exception ex, string context = "")
        {
            string criticalMessage = string.Format("[關鍵錯誤] {0} - {1}: {2}", 
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), context, ex.Message);
            
            _writeToLog.WriteToDataImportLog(criticalMessage);
            Console.WriteLine(criticalMessage);
        }
    }
}
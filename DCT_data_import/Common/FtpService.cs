using System;
using System.Net;

namespace DCT_data_import.Common
{
    /// <summary>
    /// FTP 服務共用模組
    /// 統一管理所有 FTP 相關操作
    /// </summary>
    public class FtpService
    {
        private readonly WriteToLog _writeToLog;
        
        public FtpService()
        {
            _writeToLog = new WriteToLog();
        }

        /// <summary>
        /// 檢查檔案是否存在於 FTP 伺服器
        /// </summary>
        /// <param name="requestUri">FTP 檔案完整路徑</param>
        /// <param name="ftpUser">FTP 使用者名稱</param>
        /// <param name="ftpPassword">FTP 密碼</param>
        /// <returns>檔案是否存在</returns>
        public bool CheckIfFileExists(string requestUri, string ftpUser, string ftpPassword)
        {
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(requestUri);
                request.Credentials = new NetworkCredential(ftpUser, ftpPassword);
                request.Method = WebRequestMethods.Ftp.GetFileSize;
                request.Timeout = 10000; // 10秒逾時

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    return true;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is FtpWebResponse response && 
                    response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    return false;
                }
                _writeToLog.WriteToDataImportLog($"FTP檔案存在檢查發生錯誤: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog($"FTP檔案存在檢查發生未預期錯誤: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 取得 FTP 檔案大小
        /// </summary>
        /// <param name="ftpUrl">FTP 檔案 URL</param>
        /// <param name="ftpUsername">FTP 使用者名稱</param>
        /// <param name="ftpPassword">FTP 密碼</param>
        /// <returns>檔案大小（位元組），失敗回傳 0</returns>
        public long GetFileSize(string ftpUrl, string ftpUsername, string ftpPassword)
        {
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.GetFileSize;
                request.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
                request.Timeout = 10000; // 10秒逾時

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    return response.ContentLength;
                }
            }
            catch (WebException ex)
            {
                _writeToLog.WriteToDataImportLog($"FTP取得檔案大小發生錯誤: {ex.Status}, {ex.Message}");
                return 0;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog($"FTP取得檔案大小發生未預期錯誤: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 刪除 FTP 檔案
        /// </summary>
        /// <param name="fileName">檔案完整路徑</param>
        /// <param name="user">FTP 使用者名稱</param>
        /// <param name="password">FTP 密碼</param>
        /// <returns>操作結果描述</returns>
        public string DeleteFile(string fileName, string user, string password)
        {
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fileName);
                request.Method = WebRequestMethods.Ftp.DeleteFile;
                request.Credentials = new NetworkCredential(user, password);
                request.Timeout = 10000; // 10秒逾時

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    return response.StatusDescription;
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog($"FTP刪除檔案失敗: {ex.Message}");
                return $"DeleteFile() Fail: {ex.Message}";
            }
        }

        /// <summary>
        /// 重新命名 FTP 檔案
        /// </summary>
        /// <param name="fileName">原檔案名稱</param>
        /// <param name="newFileName">新檔案名稱</param>
        /// <param name="user">FTP 使用者名稱</param>
        /// <param name="password">FTP 密碼</param>
        /// <returns>操作結果描述</returns>
        public string RenameFile(string fileName, string newFileName, string user, string password)
        {
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fileName);
                request.Method = WebRequestMethods.Ftp.Rename;
                request.Credentials = new NetworkCredential(user, password);
                request.RenameTo = newFileName;
                request.Timeout = 10000; // 10秒逾時

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    return response.StatusDescription;
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog($"FTP重新命名檔案失敗: {ex.Message}");
                return $"RenameFile() Fail: {ex.Message}";
            }
        }

        /// <summary>
        /// 格式化檔案大小顯示
        /// </summary>
        /// <param name="fileSize">檔案大小（位元組）</param>
        /// <returns>格式化後的檔案大小字串</returns>
        public string FormatFileSize(long fileSize)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = fileSize;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size = size / 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
namespace DCT_data_import.ReadAndImport
{
    public class ImportData
    {
        /// <summary>
        /// 在FTP目錄中搜尋符合模式的檔案
        /// </summary>
        /// <param name="ftpDirectoryPath">FTP目錄路徑</param>
        /// <param name="filePattern">檔案名稱模式</param>
        /// <param name="ftpUser">FTP使用者</param>
        /// <param name="ftpPassword">FTP密碼</param>
        /// <returns>符合模式的檔案名稱列表</returns>
        protected List<string> SearchFilesInFtpDirectory(string ftpDirectoryPath, string filePattern, string ftpUser, string ftpPassword)
        {
            var matchingFiles = new List<string>();
            var writeToLog = new WriteToLog();
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpDirectoryPath);
                request.Credentials = new NetworkCredential(ftpUser, ftpPassword);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        // 使用正規表示式檢查檔案名稱是否符合模式
                        if (IsFileMatchPattern(line, filePattern))
                        {
                            matchingFiles.Add(line);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog(string.Format("[SearchFilesInFtpDirectory] FTP目錄搜尋失敗: {0}, 錯誤: {1}", ftpDirectoryPath, ex.Message));
                Console.WriteLine(string.Format("[SearchFilesInFtpDirectory] FTP目錄搜尋失敗: {0}, 錯誤: {1}", ftpDirectoryPath, ex.Message));
            }
            return matchingFiles;
        }
        /// <summary>
        /// 檢查檔案名稱是否符合指定模式
        /// </summary>
        /// <param name="fileName">檔案名稱</param>
        /// <param name="pattern">模式字串，如 "test_result_site*_{dbKey}.csv"</param>
        /// <returns>是否符合模式</returns>
        protected bool IsFileMatchPattern(string fileName, string pattern)
        {
            // 將模式轉換為正規表示式
            // * 代表任意數字
            string regexPattern = pattern.Replace("*", @"\d+");
            regexPattern = "^" + regexPattern + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(fileName, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        /// <summary>
        /// 檢查FTP伺服器上是否存在指定檔案
        /// </summary>
        protected bool CheckIfFileExistsOnServer(string requestUri, string ftpUser, string ftpPassword)
        {
            FtpWebRequest request;
            FtpWebResponse response = null;
            try
            {
                request = (FtpWebRequest)WebRequest.Create(requestUri);
                request.Credentials = new NetworkCredential(ftpUser, ftpPassword);
                request.Method = WebRequestMethods.Ftp.GetFileSize;
                response = (FtpWebResponse)request.GetResponse();
                response.Close();
                return true;
            }
            catch (WebException ex)
            {
                var writeToLog = new WriteToLog();
                writeToLog.WriteErrorLog(string.Format("[CheckIfFileExistsOnServer] FTP檔案檢查失敗: {0}, 錯誤: {1}", requestUri, ex.Message));
                Console.WriteLine(string.Format("[CheckIfFileExistsOnServer] FTP檔案檢查失敗: {0}, 錯誤: {1}", requestUri, ex.Message));
                response?.Close();
                response = (FtpWebResponse)ex.Response;
                if (response != null && response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    response.Close();
                    return false;
                }
            }
            finally
            {
                response?.Close();
            }
            return false;
        }
        protected string[] EraseSpecificChar(string str_line)
        {
            string[] values = str_line.Split(',', '\0', '\r', '\n');
            int first_value_idx = -1, last_value_idx = -1;
            //去除頭尾空白
            for (int i = 0; i < values.Length; i++)
            {
                if (first_value_idx == -1 && !string.IsNullOrEmpty(values[i]))
                {
                    first_value_idx = i;
                    last_value_idx = i;
                }
                else if (!string.IsNullOrEmpty(values[i]))
                {
                    last_value_idx = i;
                }
            }
            if (first_value_idx == -1) return null;
            string[] values_tmp = new string[0] { };
            Array.Resize(ref values_tmp, last_value_idx - first_value_idx + 1);
            for (int i = 0; i <= last_value_idx - first_value_idx; i++)
            {
                values_tmp[i] = values[first_value_idx + i];
            }
            return values_tmp;
        }
        #region Comman tool
        public string DeleteFile(string fileName, string user, string password)
        {
            if (RuntimeMode.IsDryRun)
            {
                return "DryRun: FTP DeleteFile skipped";
            }
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fileName);
            request.Method = WebRequestMethods.Ftp.DeleteFile;
            request.Credentials = new NetworkCredential(user, password);
            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                return response.StatusDescription;
            }
        }
        public string RenameFile(string filePath, string newFilePath, string user, string password)
        {
            if (RuntimeMode.IsDryRun)
            {
                return "DryRun: FTP RenameFile skipped";
            }
            var writeToLog = new WriteToLog();
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(newFilePath))
                return $"RenameFile() Fail: path is empty. [filePath={filePath}, newFilePath={newFilePath}]";
            try
            {
                var srcUri = new Uri(filePath, UriKind.Absolute);
                var dstUri = new Uri(newFilePath, UriKind.Absolute);
                if (srcUri.Scheme != Uri.UriSchemeFtp || dstUri.Scheme != Uri.UriSchemeFtp || srcUri.Host != dstUri.Host)
                    return $"RenameFile() Fail: only same FTP host is supported. [filePath={filePath}, newFilePath={newFilePath}]";
                string renameTo = Uri.UnescapeDataString(dstUri.AbsolutePath);
                var request = (FtpWebRequest)WebRequest.Create(srcUri);
                request.Method = WebRequestMethods.Ftp.Rename;
                request.Credentials = new NetworkCredential(user, password);
                request.UsePassive = true;
                request.UseBinary = true;
                request.KeepAlive = false;
                request.Proxy = null;
                request.Timeout = 10000;
                request.RenameTo = renameTo;
                using (var response = (FtpWebResponse)request.GetResponse())
                {
                    return response.StatusDescription;
                }
            }
            catch (UriFormatException ex)
            {
                string msg = $"RenameFile() Fail: URI 格式錯誤. [filePath={filePath}, newFilePath={newFilePath}, user={user}] Msg: {ex.Message}";
                writeToLog.WriteErrorLog(msg);
                return msg;
            }
            catch (ArgumentException ex)
            {
                string msg = $"RenameFile() Fail: 參數錯誤. [filePath={filePath}, newFilePath={newFilePath}, user={user}] Msg: {ex.Message}";
                writeToLog.WriteErrorLog(msg);
                return msg;
            }
            catch (WebException ex)
            {
                string detail = string.Empty;
                FtpStatusCode statusCode = 0;
                if (ex.Response is FtpWebResponse ftpResp)
                {
                    statusCode = ftpResp.StatusCode;
                    detail = $"FTP Status: {statusCode}, Description: {ftpResp.StatusDescription}";
                }
                if (statusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    string msg = $"RenameFile() Fail: 檔案不存在或無法存取. [filePath={filePath}, newFilePath={newFilePath}, user={user}] Msg: {ex.Message}";
                    writeToLog.WriteErrorLog(msg);
                    return msg;
                }
                string msg2 = $"RenameFile() Fail: 網路/FTP 錯誤. [filePath={filePath}, newFilePath={newFilePath}, user={user}] Status: {statusCode}, {detail}, Msg: {ex.Message}";
                writeToLog.WriteErrorLog(msg2);
                return msg2;
            }
            catch (IOException ex)
            {
                string msg = $"RenameFile() Fail: IO 錯誤. [filePath={filePath}, newFilePath={newFilePath}, user={user}] Msg: {ex.Message}";
                writeToLog.WriteErrorLog(msg);
                return msg;
            }
            catch (NotSupportedException ex)
            {
                string msg = $"RenameFile() Fail: 不支援的作業. [filePath={filePath}, newFilePath={newFilePath}, user={user}] Msg: {ex.Message}";
                writeToLog.WriteErrorLog(msg);
                return msg;
            }
            catch (Exception ex)
            {
                string msg = $"RenameFile() Fail: 未預期的錯誤. [filePath={filePath}, newFilePath={newFilePath}, user={user}] Msg: {ex.Message}";
                writeToLog.WriteErrorLog(msg);
                return msg;
            }
        }
        public bool IsChinese(string input)
        {
            // 使用 Unicode 字節順序標記 (BOM) 判斷字符串是否為 Unicode 編碼
            if (input.StartsWith("\uFEFF"))
            {
                input = input.Substring(1);
            }
            // 判斷字符串中是否包含中文字符
            foreach (char c in input)
            {
                if (c >= 0x4E00 && c <= 0x9FFF)
                {
                    return true;
                }
            }
            return false;
        }
        public long GetFileSize(string ftpUrl, string ftpUsername, string ftpPassword)
        {
            long fileSize = 0;
            // 创建 FtpWebRequest 对象
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
            request.Method = WebRequestMethods.Ftp.GetFileSize; // 指定获取文件大小的方法
            // 提供 FTP 凭据
            request.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
            try
            {
                // 获取响应
                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    fileSize = response.ContentLength;  // 文件大小
                }
            }
            catch (WebException ex)
            {
                var writeToLog = new WriteToLog();
                writeToLog.WriteErrorLog($"[GetFileSize] FTP取得檔案大小失敗: {ftpUrl}, 狀態: {ex.Status}, 錯誤: {ex.Message}");
                Console.WriteLine($"FTP GetFileSize 錯誤: {ex.Status}, {ex.Message}");
                return 0;
            }
            return fileSize;
        }
        /// <summary>
        /// 轉換檔案大小
        /// </summary>
        /// <param name="fileSize"></param>
        /// <returns></returns>
        public string FormatFileSize(long fileSize)
        {
            string[] sizes = { "B", "KB", "MB" };
            int order = 0;
            while (fileSize >= 1024 && order < sizes.Length - 1)
            {
                order++;
                fileSize = fileSize / 1024;
            }
            return string.Format("{0:0.##} {1}", fileSize, sizes[order]);
        }
        /// <summary>
        /// 根據檔案類型和DB Key產生FTP檔案路徑
        /// </summary>
        /// <param name="fileType">檔案類型 (rawdata, failpin, recovery, tester, uistatus, multiSpecRawdata)</param>
        /// <param name="dbKey">資料庫鍵值</param>
        /// <returns>完整的FTP檔案路徑</returns>
        protected string GetFilePath(string fileType, string dbKey)
        {
            string basePath = Program.Environment == "Dev"
              ? "/DCT_Log/DCT_DB_DATA_Dev/"
              : "/DCT_Log/DCT_DB_DATA/";
            var pathMap = new Dictionary<string, string>
        {
            {"rawdata", "Data_Cloud_CSV/test_result_"},
            {"failpin", "Fail_Pin_Log/ST_RT_AT/fail_pin_"},
            {"recovery", "Recovery_rate_data/Recovery_rate_"},
            {"tester", "Tester_Status/tester_"},
            {"uistatus", "UI_Status/ui_status_"},
            {"multiSpecRawdata", "Data_Cloud_CSV_MultiSpec/"} // 新增：只提供目錄路徑
        };
            if (!pathMap.ContainsKey(fileType))
            {
                throw new ArgumentException(string.Format("不支援的檔案類型: {0}", fileType));
            }
            // 對於 multiSpecRawdata，返回目錄路徑用於搜尋
            if (fileType == "multiSpecRawdata")
            {
                return $"ftp://{Program.FTP_IP}{basePath}{pathMap[fileType]}";
            }
            return $"ftp://{Program.FTP_IP}{basePath}{pathMap[fileType]}{dbKey}.csv";
        }
        /// <summary>
        /// 根據檔案類型和DB Key產生FTP錯誤檔案路徑
        /// </summary>
        /// <param name="fileType">檔案類型 (rawdata, failpin, recovery, tester, uistatus, tsmc_ieda)</param>
        /// <param name="dbKey">資料庫鍵值</param>
        /// <returns>完整的FTP錯誤檔案路徑</returns>
        protected string GetErrorPath(string fileType, string dbKey)
        {
            string basePath = Program.Environment == "Dev"
              ? "/DCT_Log/DCT_DB_DATA_Dev/"
              : "/DCT_Log/DCT_DB_DATA/";
            var pathMap = new Dictionary<string, string>
        {
            {"rawdata", "Data_Cloud_CSV_Error/test_result_"},
            {"failpin", "Fail_Pin_Log_Error/fail_pin_"},
            {"recovery", "Recovery_rate_data_Error/Recovery_rate_"},
            {"tester", "Tester_Status_Error/tester_"},
            {"uistatus", "UI_Status_Error/ui_status_"},
            {"multiSpecRawdata", "Data_Cloud_CSV_MultiSpec_Error/"} // 新增錯誤路徑目錄
        };
            if (!pathMap.ContainsKey(fileType))
            {
                throw new ArgumentException($"不支援的檔案類型: {fileType}");
            }
            // 對於 multiSpecRawdata，返回目錄路徑，因為檔案名稱是動態的
            if (fileType == "multiSpecRawdata")
            {
                return $"ftp://{Program.FTP_IP}{basePath}{pathMap[fileType]}";
            }
            return $"ftp://{Program.FTP_IP}{basePath}{pathMap[fileType]}{dbKey}.csv";
        }
        /// <summary>
        /// 根據實際檔案名稱產生對應的錯誤檔案路徑
        /// </summary>
        /// <param name="fileType">檔案類型</param>
        /// <param name="fileName">實際找到的檔案名稱</param>
        /// <param name="dbKey">資料庫鍵值</param>
        /// <returns>對應的錯誤檔案完整路徑</returns>
        protected string GetErrorPathForSpecificFile(string fileType, string fileName, string dbKey)
        {
            if (fileType == "multiSpecRawdata")
            {
                string errorDirectory = GetErrorPath(fileType, dbKey);
                return errorDirectory + fileName;
            }
            else
            {
                return GetErrorPath(fileType, dbKey);
            }
        }
        #endregion Common tool
    }
}
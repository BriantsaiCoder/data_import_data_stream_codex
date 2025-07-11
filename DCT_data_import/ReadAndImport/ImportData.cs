using System;
using System.Linq;
using System.Net;
namespace DCT_data_import.ReadAndImport
{
    public class ImportData
    {
        protected bool CheckIfFileExistsOnServer(string requestUri, string ftpUser, string ftpPassword)
        {
            FtpWebRequest request;
            FtpWebResponse response;
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
                response = (FtpWebResponse)ex.Response;
                if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                    return false;
            }
            return false;
        }
        protected string[] EraseSpecificChar(string str_line)
        {
            string[] values = str_line.Split(',', '\0', '\r', '\n');
            // 去除空白值
            string[] values_tmp1 = values.Where(x => !string.IsNullOrEmpty(x)).ToArray();
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
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fileName);
            request.Method = WebRequestMethods.Ftp.DeleteFile;
            request.Credentials = new NetworkCredential(user, password);
            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                return response.StatusDescription;
            }
        }
        public string RenameFile(string fileName, string newFileName, string user, string password)
        {
            WriteToLog writeToLog = new WriteToLog();
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fileName);
            request.Method = WebRequestMethods.Ftp.Rename;
            request.Credentials = new NetworkCredential(user, password);
            request.RenameTo = newFileName;
            request.Timeout = 10000;
            try
            {
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                response.Close();
                return response.StatusDescription;
            }
            catch (Exception ex)
            {
                writeToLog.WriteToDataImportLog("RenameFile() Fail, Exception :" + ex.Message);
                return "RenameFile() Fail";
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
                // 处理可能出现的错误
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
        #endregion Common tool
    }
}
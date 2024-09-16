using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static DCT_data_import.ApiObject;

namespace DCT_data_import.ReadAndImport
{
    public class ImportData
    {
        protected bool isValidFtpConnection(string requestUri, string ftpUser, string ftpPassword)
        {
            FtpWebRequest request;
            FtpWebResponse response;
            try
            {
                request = (FtpWebRequest)WebRequest.Create(requestUri);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(ftpUser, ftpPassword);
                //request.KeepAlive = true;
                response = (FtpWebResponse)request.GetResponse();
                response.Close();
                return true;
            }
            catch (WebException ex)
            {
                return false;
            }
            finally
            {
                request = null;
            }
        }

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

        protected string[] eraseSpecificChar(string str_line)
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
                return "RenameFile() Fail";
            }

        }

        public string DownloadFile(string fileName, string newFileName, string user, string password)
        {
            try
            {
                FileStream outputStream = new FileStream(newFileName, FileMode.Create);


                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fileName);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new NetworkCredential(user, password);

                FtpWebResponse response = (FtpWebResponse)request.GetResponse();

                //建立物件接收從FTP回應的資料流
                Stream responseStream = response.GetResponseStream();
                //建立物件讀取資料流的字元
                StreamReader reader = new StreamReader(responseStream);

                long cl = response.ContentLength;

                int bufferSize = 2048;

                int readCount;

                byte[] buffer = new byte[bufferSize];

                readCount = responseStream.Read(buffer, 0, bufferSize);

                while (readCount > 0)
                {
                    outputStream.Write(buffer, 0, readCount);

                    readCount = responseStream.Read(buffer, 0, bufferSize);
                }
                //Console.WriteLine(reader.ReadToEnd());

                response.Close();
                responseStream.Close();
                outputStream.Close();
                response.Close();
                return response.StatusDescription;
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public bool createPool(WebApiClient webApiClient, WriteToLog writeToLog)
        {
            Pool pool = new Pool
            {
                pool_name = Program.POOL_NAME,
                host = Program.HOST,
                port = Program.PORT,
                user = Program.USER,
                password = Program.PASSWORD,
                database = Program.DATABASE
            };
            try
            {
                var create_response = webApiClient.CreatePoolAsync(pool).GetAwaiter().GetResult();
                if (create_response.error != null)
                {
                    throw new Exception("Pool 建立失敗: " + create_response.error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                writeToLog.writeToLog("Pool 建立失敗: " + ex.ToString());
                webApiClient.client.Dispose();
                return false;
            }

            return true;
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

        public long GetFileSize(string ftpUrl , string ftpUsername, string ftpPassword)
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

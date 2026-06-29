using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace DCT_data_import.FileAccess
{
    public class ReadWriteINIfile
    {
        // 修正 P/Invoke 宣告
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool WritePrivateProfileString(string lpAppName, string lpKeyName, string lpString, string lpFileName);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, uint nSize, string lpFileName);
        private readonly string _filePath;
        private const int DefaultBufferSize = 1024;  // 增加預設緩衝區大小
        public string FilePath
        {
            get { return _filePath; }
        }
        public ReadWriteINIfile(string iniPath)
        {
            if (string.IsNullOrWhiteSpace(iniPath))
            {
                throw new ArgumentException("INI 檔案路徑不能為空", "iniPath");
            }
            // 驗證路徑格式
            try
            {
                _filePath = Path.GetFullPath(iniPath);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(string.Format("無效的檔案路徑: {0}", ex.Message), "iniPath");
            }
            // 確保目錄存在
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(string.Format("無法建立目錄: {0}", ex.Message));
                }
            }
        }
        /// <summary>
        /// 寫入 INI 檔案
        /// </summary>
        /// <param name="section">區段名稱</param>
        /// <param name="key">鍵名</param>
        /// <param name="value">值</param>
        /// <returns>是否成功</returns>
        public bool WriteINI(string section, string key, string value)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException("區段名稱不能為空", "section");
            }
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("鍵名不能為空", "key");
            }
            try
            {
                // 檢查檔案權限
                CheckFilePermissions();
                bool result = WritePrivateProfileString(section, key, value, _filePath);
                if (!result)
                {
                    int lastError = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException(string.Format("寫入 INI 失敗，錯誤代碼: {0}", lastError));
                }
                return result;
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is InvalidOperationException))
            {
                throw new InvalidOperationException(string.Format("寫入 INI 檔案時發生錯誤: {0}", ex.Message), ex);
            }
        }
        /// <summary>
        /// 讀取 INI 檔案
        /// </summary>
        /// <param name="section">區段名稱</param>
        /// <param name="key">鍵名</param>
        /// <param name="defaultValue">預設值</param>
        /// <returns>讀取的值</returns>
        public string ReadINI(string section, string key, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException("區段名稱不能為空", "section");
            }
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("鍵名不能為空", "key");
            }
            try
            {
                // 檢查檔案是否存在
                if (!File.Exists(_filePath))
                {
                    return defaultValue;
                }
                // 動態調整緩衝區大小
                int bufferSize = DefaultBufferSize;
                StringBuilder buffer;
                uint bytesReturned;
                do
                {
                    buffer = new StringBuilder(bufferSize);
                    bytesReturned = GetPrivateProfileString(section, key, defaultValue, buffer, (uint)bufferSize, _filePath);
                    // 如果返回的長度等於緩衝區大小-1，可能被截斷，需要增加緩衝區
                    if (bytesReturned == bufferSize - 1)
                    {
                        bufferSize *= 2;
                        if (bufferSize > 32768) // 設定最大限制，避免無限增長
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                } while (true);
                return buffer.ToString();
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                throw new InvalidOperationException(string.Format("讀取 INI 檔案時發生錯誤: {0}", ex.Message), ex);
            }
        }
        /// <summary>
        /// 讀取整個區段的所有鍵值對
        /// </summary>
        /// <param name="section">區段名稱</param>
        /// <returns>鍵值對字典</returns>
        public System.Collections.Generic.Dictionary<string, string> ReadSection(string section)
        {
            var result = new System.Collections.Generic.Dictionary<string, string>();
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException("區段名稱不能為空", "section");
            }
            if (!File.Exists(_filePath))
            {
                return result;
            }
            try
            {
                int bufferSize = 32768;
                StringBuilder buffer = new StringBuilder(bufferSize);
                uint bytesReturned = GetPrivateProfileString(section, null, string.Empty, buffer, (uint)bufferSize, _filePath);
                if (bytesReturned > 0)
                {
                    string[] keys = buffer.ToString().Split('\0');
                    foreach (string key in keys)
                    {
                        if (!string.IsNullOrEmpty(key))
                        {
                            string value = ReadINI(section, key);
                            result[key] = value;
                        }
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("讀取區段時發生錯誤: {0}", ex.Message), ex);
            }
        }
        /// <summary>
        /// 刪除指定的鍵
        /// </summary>
        /// <param name="section">區段名稱</param>
        /// <param name="key">鍵名</param>
        /// <returns>是否成功</returns>
        public bool DeleteKey(string section, string key)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException("區段名稱不能為空", "section");
            }
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("鍵名不能為空", "key");
            }
            try
            {
                return WritePrivateProfileString(section, key, null, _filePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("刪除鍵時發生錯誤: {0}", ex.Message), ex);
            }
        }
        /// <summary>
        /// 刪除指定的區段
        /// </summary>
        /// <param name="section">區段名稱</param>
        /// <returns>是否成功</returns>
        public bool DeleteSection(string section)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException("區段名稱不能為空", "section");
            }
            try
            {
                return WritePrivateProfileString(section, null, null, _filePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("刪除區段時發生錯誤: {0}", ex.Message), ex);
            }
        }
        /// <summary>
        /// 檢查檔案是否存在
        /// </summary>
        /// <returns>檔案是否存在</returns>
        public bool FileExists()
        {
            return File.Exists(_filePath);
        }
        /// <summary>
        /// 檢查檔案權限
        /// </summary>
        private void CheckFilePermissions()
        {
            try
            {
                // 如果檔案存在，檢查寫入權限
                if (File.Exists(_filePath))
                {
                    var fileInfo = new FileInfo(_filePath);
                    if (fileInfo.IsReadOnly)
                    {
                        throw new UnauthorizedAccessException("檔案為唯讀，無法寫入");
                    }
                }
                // 檢查目錄權限
                string directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    var dirInfo = new DirectoryInfo(directory);
                    if (!dirInfo.Exists)
                    {
                        dirInfo.Create(); // 嘗試建立目錄
                    }
                }
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException(string.Format("檔案權限檢查失敗: {0}", ex.Message), ex);
            }
        }
        /// <summary>
        /// 檢查是否在 Windows 平台上運行
        /// </summary>
        /// <returns>是否為 Windows 平台</returns>
        public static bool IsWindowsPlatform()
        {
            // net8.0-windows 啟用 CA1416 平台相容性分析器,只認 OperatingSystem.IsWindows() 當 kernel32 守衛。
            return OperatingSystem.IsWindows();
        }
    }
}

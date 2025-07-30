using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DCT_data_import.Common;
using static DCT_data_import.DbObject;

namespace DCT_data_import.ReadAndImport
{
    /// <summary>
    /// TsmcIeda 重構版本
    /// 簡化IEDA資料處理並使用共用批次對映邏輯
    /// </summary>
    public class TsmcIedaRefactored : ImportData
    {
        private readonly FtpService _ftpService;
        private readonly FileValidator _fileValidator;
        private readonly DatabaseHelper _databaseHelper;
        private readonly WriteToLog _writeToLog;
        private readonly DataTable _lotMappingDt;

        public TsmcIedaRefactored()
        {
            _ftpService = new FtpService();
            _fileValidator = new FileValidator();
            _databaseHelper = new DatabaseHelper();
            _writeToLog = new WriteToLog();
            _lotMappingDt = new DataTable();
            
            // 初始化批次對映資料
            InitializeLotMapping();
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
        /// 讀取並匯入 IEDA 資料 - 重構版本
        /// </summary>
        public async Task<ImportResult> ReadAndImportIeda(FileProcess fileAccess, DatabaseService databaseService, string dbKey)
        {
            try
            {
                // 1. 取得檔案清單
                var fileList = await GetIedaFileListAsync();
                if (fileList == null || fileList.Count == 0)
                {
                    _writeToLog.WriteToDataImportLog("TSMC IEDA: 無檔案可處理");
                    return new ImportResult(1, "No files to process");
                }

                // 2. 批次處理所有檔案
                int processedCount = 0;
                int successCount = 0;

                foreach (string filename in fileList)
                {
                    var result = await ProcessSingleIedaFileAsync(filename, databaseService);
                    processedCount++;
                    
                    if (result.Success)
                    {
                        successCount++;
                        Console.WriteLine(string.Format("匯入完成! TSMC IEDA 檔名: {0}", filename));
                    }
                    else
                    {
                        _writeToLog.WriteToDataImportLog(string.Format("TSMC IEDA 處理失敗: {0}, 錯誤: {1}", filename, result.Message));
                    }
                }

                string summaryMessage = string.Format("TSMC IEDA 批次處理完成: {0}/{1} 檔案成功處理", successCount, processedCount);
                Console.WriteLine(summaryMessage);
                _writeToLog.WriteToDataImportLog(summaryMessage);

                return new ImportResult(1, summaryMessage);
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("ReadAndImportIeda 發生錯誤: {0}", ex.Message));
                return new ImportResult(3, string.Format("批次處理失敗: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 非同步取得 IEDA 檔案清單
        /// </summary>
        private async Task<List<string>> GetIedaFileListAsync()
        {
            try
            {
                string ftpBaseDir = BuildIedaFtpPath("");
                
                var reqFTP = (FtpWebRequest)WebRequest.Create(new Uri(ftpBaseDir));
                reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
                reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);

                using (var response = (FtpWebResponse)await reqFTP.GetResponseAsync())
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    string names = await reader.ReadToEndAsync();
                    return names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                              .Where(name => !string.IsNullOrWhiteSpace(name))
                              .ToList();
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("取得 IEDA 檔案清單時發生錯誤: {0}", ex.Message));
                return new List<string>();
            }
        }

        /// <summary>
        /// 處理單一 IEDA 檔案
        /// </summary>
        private async Task<ValidationResult> ProcessSingleIedaFileAsync(string filename, DatabaseService databaseService)
        {
            var stopWatch = new Stopwatch();
            string ftpFilePath = BuildIedaFtpPath(filename);
            string errorDir = BuildErrorDirectory();

            try
            {
                // 1. 讀取檔案內容
                stopWatch.Start();
                var iedaData = await ReadIedaFileAsync(ftpFilePath);
                stopWatch.Stop();

                if (iedaData == null)
                {
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ValidationResult(false, "檔案讀取失敗");
                }

                // 2. 驗證檔案內容
                var validationResult = ValidateIedaData(iedaData);
                if (!validationResult.Success)
                {
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return validationResult;
                }

                // 3. 匯入資料庫
                stopWatch.Restart();
                bool importResult = await ImportIedaAsync(iedaData, databaseService);
                stopWatch.Stop();

                if (importResult)
                {
                    // 刪除成功處理的檔案
                    _ftpService.DeleteFile(ftpFilePath, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ValidationResult(true, string.Format("處理成功，耗時: {0} 秒", (int)stopWatch.Elapsed.TotalSeconds));
                }
                else
                {
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ValidationResult(false, "資料庫匯入失敗");
                }
            }
            catch (Exception ex)
            {
                _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                return new ValidationResult(false, string.Format("處理檔案時發生錯誤: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 建立 IEDA FTP 路徑
        /// </summary>
        private string BuildIedaFtpPath(string filename)
        {
            string basePath = string.Format("ftp://{0}", Program.FTP_IP);
            string subPath = Program.Environment == "Dev" 
                ? "/DCT_Log/DCT_DB_DATA_Dev/TSMC_DATA/IEDA/" 
                : "/DCT_Log/DCT_DB_DATA/TSMC_DATA/IEDA/";
            
            return basePath + subPath + filename;
        }

        /// <summary>
        /// 建立錯誤目錄路徑
        /// </summary>
        private string BuildErrorDirectory()
        {
            string basePath = string.Format("ftp://{0}", Program.FTP_IP);
            return Program.Environment == "Dev" 
                ? basePath + "/DCT_Log/DCT_DB_DATA_Dev/TSMC_DATA/IEDA_error/"
                : basePath + "/DCT_Log/DCT_DB_DATA/TSMC_DATA/IEDA_error/";
        }

        /// <summary>
        /// 非同步讀取 IEDA 檔案
        /// </summary>
        private async Task<IedaDataFormat> ReadIedaFileAsync(string ftpFilePath)
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
                    return await Task.Run(() => ParseIedaFile(reader));
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("讀取 IEDA 檔案時發生錯誤: {0}", ex.Message));
                return null;
            }
        }

        /// <summary>
        /// 解析 IEDA 檔案內容
        /// </summary>
        private IedaDataFormat ParseIedaFile(StreamReader reader)
        {
            var iedaDataFormat = new IedaDataFormat();
            
            try
            {
                string lines = reader.ReadToEnd();
                List<string> split_lines = lines.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

                for (int r = 0; r < split_lines.Count; r++)
                {
                    if (r == 0)  // IEDA title part
                    {
                        ProcessIedaTitle(split_lines[r], iedaDataFormat);
                    }
                    else // IEDA content part
                    {
                        ProcessIedaContent(split_lines[r], iedaDataFormat);
                    }
                }

                return iedaDataFormat;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("解析 IEDA 檔案內容時發生錯誤: {0}", ex.Message));
                iedaDataFormat.ErrMsg = string.Format("讀檔內容錯誤, Error: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 處理 IEDA 標題部分
        /// </summary>
        private void ProcessIedaTitle(string line, IedaDataFormat iedaDataFormat)
        {
            int charIdx = 0;
            DataRow dr = iedaDataFormat.IedaTitle.NewRow();
            
            for (int j = 1; j < iedaDataFormat.IedaTitle.Columns.Count; j++)
            {
                int columnSize = iedaDataFormat.titleColumnsDataSize[j];
                if (charIdx + columnSize <= line.Length)
                {
                    dr[j] = StringHelper.ConvertEmptyToDefault(line.Substring(charIdx, columnSize).Trim());
                }
                else
                {
                    dr[j] = StringHelper.ConvertEmptyToDefault(string.Empty);
                }
                charIdx += columnSize;
            }

            // 使用批次對映獲取 ase_lot
            string lotId = dr["lot_id"]?.ToString();
            if (!string.IsNullOrEmpty(lotId))
            {
                string aseLot = GetAseLotFromMapping(lotId);
                if (!string.IsNullOrEmpty(aseLot))
                {
                    dr["ase_lot"] = aseLot;
                }
            }

            iedaDataFormat.IedaTitle.Rows.Add(dr);
        }

        /// <summary>
        /// 處理 IEDA 內容部分
        /// </summary>
        private void ProcessIedaContent(string line, IedaDataFormat iedaDataFormat)
        {
            int charIdx = 0;
            DataRow dr = iedaDataFormat.IedaContent.NewRow();
            
            for (int j = 1; j < iedaDataFormat.IedaContent.Columns.Count; j++)
            {
                int columnSize = iedaDataFormat.contentColumnsDataSize[j];
                if (charIdx + columnSize <= line.Length)
                {
                    dr[j] = StringHelper.ConvertEmptyToDefault(line.Substring(charIdx, columnSize).Trim());
                }
                else
                {
                    dr[j] = StringHelper.ConvertEmptyToDefault(string.Empty);
                }
                charIdx += columnSize;
            }

            iedaDataFormat.IedaContent.Rows.Add(dr);
        }

        /// <summary>
        /// 從對映表獲取 ASE Lot
        /// </summary>
        private string GetAseLotFromMapping(string tsmcLot)
        {
            try
            {
                DataRow[] mappingRows = _lotMappingDt.Select(string.Format("tsmc_lot='{0}'", tsmcLot));
                if (mappingRows.Length > 0)
                {
                    return mappingRows[0]["ase_lot"]?.ToString() ?? string.Empty;
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("查詢批次對映時發生錯誤: {0}", ex.Message));
                return string.Empty;
            }
        }

        /// <summary>
        /// 驗證 IEDA 資料
        /// </summary>
        private ValidationResult ValidateIedaData(IedaDataFormat data)
        {
            if (!string.IsNullOrEmpty(data.ErrMsg))
            {
                return new ValidationResult(false, data.ErrMsg);
            }

            if (data.IedaTitle.Rows.Count < 1 || data.IedaContent.Rows.Count < 1)
            {
                return new ValidationResult(false, "IEDA 資料內容不完整");
            }

            return new ValidationResult(true, string.Empty);
        }

        /// <summary>
        /// 非同步匯入 IEDA 資料
        /// </summary>
        private async Task<bool> ImportIedaAsync(IedaDataFormat content, DatabaseService databaseService)
        {
            try
            {
                return await Task.Run(() =>
                {
                    // 1. 匯入 IEDA Title
                    var titleResult = ImportIedaTitle(content, databaseService);
                    if (!titleResult.Success)
                    {
                        return false;
                    }

                    // 2. 匯入 IEDA Content
                    return ImportIedaContent(content, databaseService, titleResult.TitleId);
                });
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("匯入 IEDA 資料時發生錯誤: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// IEDA Title 匯入結果
        /// </summary>
        private class TitleImportResult
        {
            public bool Success { get; set; }
            public string TitleId { get; set; }
            public string Message { get; set; }
        }

        /// <summary>
        /// 匯入 IEDA Title
        /// </summary>
        private TitleImportResult ImportIedaTitle(IedaDataFormat content, DatabaseService databaseService)
        {
            try
            {
                var sqlResult = _databaseHelper.ConvertDataTableToSql(content.IedaTitle);
                var response = ExecuteInsertWithAPI(databaseService, "ieda_title", sqlResult.Columns, 
                    string.Join(",", sqlResult.ValuesList));

                if (!string.IsNullOrEmpty(response.Error))
                {
                    _writeToLog.WriteToDataImportLog(string.Format("INSERT INTO ieda_title error: {0}", response.Error));
                    return new TitleImportResult { Success = false, Message = response.Error };
                }

                string titleId = response.Data?[0]?["insertId"]?.ToString() ?? string.Empty;
                return new TitleImportResult { Success = true, TitleId = titleId };
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("ImportIedaTitle 發生錯誤: {0}", ex.Message));
                return new TitleImportResult { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// 匯入 IEDA Content
        /// </summary>
        private bool ImportIedaContent(IedaDataFormat content, DatabaseService databaseService, string titleId)
        {
            try
            {
                // 建立包含 title_id 的資料表
                DataTable contentWithTitleId = content.IedaContent.Copy();
                contentWithTitleId.Columns.Add("title_id", typeof(string));
                
                foreach (DataRow row in contentWithTitleId.Rows)
                {
                    row["title_id"] = StringHelper.ConvertEmptyToDefault(titleId);
                }

                var sqlResult = _databaseHelper.ConvertDataTableToSql(contentWithTitleId);
                var response = ExecuteInsertWithAPI(databaseService, "ieda_content", sqlResult.Columns, 
                    string.Join(",", sqlResult.ValuesList));

                if (!string.IsNullOrEmpty(response.Error))
                {
                    _writeToLog.WriteToDataImportLog(string.Format("INSERT INTO ieda_content error: {0}", response.Error));
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("ImportIedaContent 發生錯誤: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 執行 INSERT 操作 - 使用 API
        /// </summary>
        private Execute_query_response ExecuteInsertWithAPI(DatabaseService databaseService, string tableName, string columns, string values)
        {
            try
            {
                var fileProcess = new FileProcess();
                return fileProcess.ExecuteInsertWithAPI(databaseService, tableName, columns, values);
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("ExecuteInsertWithAPI 發生錯誤: {0}", ex.Message));
                return new Execute_query_response { Error = ex.Message };
            }
        }

        /// <summary>
        /// 取得 Net Name 清單 - 重構版本
        /// </summary>
        public List<string> GetNetNameList(string aseLot, int recursive = 0)
        {
            try
            {
                string ftpFilePath = BuildCsvFtpPath(aseLot);
                if (string.IsNullOrEmpty(ftpFilePath))
                {
                    return new List<string>();
                }

                return ReadNetNameFromCsv(ftpFilePath, recursive, aseLot);
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("GetNetNameList 發生錯誤: {0}", ex.Message));
                return new List<string>();
            }
        }

        /// <summary>
        /// 建立 CSV FTP 路徑
        /// </summary>
        private string BuildCsvFtpPath(string aseLot)
        {
            try
            {
                DataRow[] mappingRows = _lotMappingDt.Select(string.Format("ase_lot='{0}'", aseLot));
                if (mappingRows.Length == 0)
                {
                    return string.Empty;
                }

                string filename = mappingRows[0]["csv"]?.ToString();
                if (string.IsNullOrEmpty(filename))
                {
                    return string.Empty;
                }

                string basePath = string.Format("ftp://{0}", Program.FTP_IP);
                string subPath = Program.Environment == "Dev" 
                    ? "/DCT_Log/DCT_DB_DATA_Dev/TSMC_DATA/CSV/" 
                    : "/DCT_Log/DCT_DB_DATA/TSMC_DATA/CSV/";
                
                return basePath + subPath + filename;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("建立 CSV FTP 路徑時發生錯誤: {0}", ex.Message));
                return string.Empty;
            }
        }

        /// <summary>
        /// 從 CSV 讀取 Net Name
        /// </summary>
        private List<string> ReadNetNameFromCsv(string ftpFilePath, int recursive, string aseLot)
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
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("Net Name"))
                        {
                            var netNameList = line.Split(',').ToList();
                            if (netNameList.Count > 0) netNameList.RemoveAt(0);
                            
                            // 刪除已成功讀完的 TSMC CSV 檔案
                            _ftpService.DeleteFile(ftpFilePath, Program.FTP_USER, Program.FTP_PASSWORD);
                            return netNameList;
                        }
                    }
                }

                return new List<string>();
            }
            catch (Exception ex)
            {
                if (recursive == 0)
                {
                    return GetNetNameList(aseLot, 1);
                }
                else
                {
                    _writeToLog.WriteToDataImportLog(string.Format("TSMC CSV 讀檔錯誤: {0}, error: {1}", ftpFilePath, ex.Message));
                    string errorDir = BuildCsvErrorDirectory();
                    string filename = Path.GetFileName(ftpFilePath);
                    _ftpService.RenameFile(ftpFilePath, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new List<string>();
                }
            }
        }

        /// <summary>
        /// 建立 CSV 錯誤目錄路徑
        /// </summary>
        private string BuildCsvErrorDirectory()
        {
            string basePath = string.Format("ftp://{0}", Program.FTP_IP);
            return Program.Environment == "Dev" 
                ? basePath + "/DCT_Log/DCT_DB_DATA_Dev/TSMC_DATA/CSV_error/"
                : basePath + "/DCT_Log/DCT_DB_DATA/TSMC_DATA/CSV_error/";
        }

        /// <summary>
        /// 初始化批次對映資料
        /// </summary>
        private void InitializeLotMapping()
        {
            try
            {
                string ftpFilePath = BuildLotMappingFtpPath();
                
                var reqFTP = (FtpWebRequest)WebRequest.Create(new Uri(ftpFilePath));
                reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
                reqFTP.Method = WebRequestMethods.Ftp.DownloadFile;

                using (var response = (FtpWebResponse)reqFTP.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream, Encoding.GetEncoding("big5")))
                {
                    string lines = reader.ReadToEnd();
                    ProcessLotMappingData(lines);
                }

                _writeToLog.WriteToDataImportLog("批次對映資料初始化成功");
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("InitializeLotMapping 發生錯誤: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 建立批次對映 FTP 路徑
        /// </summary>
        private string BuildLotMappingFtpPath()
        {
            string basePath = string.Format("ftp://{0}", Program.FTP_IP);
            string subPath = Program.Environment == "Dev" 
                ? "/DCT_Log/DCT_DB_DATA_Dev/TSMC_DATA/LotID/lot_mapping.csv" 
                : "/DCT_Log/DCT_DB_DATA/TSMC_DATA/LotID/lot_mapping.csv";
            
            return basePath + subPath;
        }

        /// <summary>
        /// 處理批次對映資料
        /// </summary>
        private void ProcessLotMappingData(string csvContent)
        {
            var split_lines = csvContent.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            
            for (int i = 0; i < split_lines.Count; i++)
            {
                string[] values = split_lines[i].Trim().Split(',', '\0', '\r', '\n');
                
                if (i == 0) // 標題行
                {
                    foreach (string value in values)
                    {
                        _lotMappingDt.Columns.Add(StringHelper.ConvertEmptyToDefault(value.Trim()));
                    }
                }
                else // 資料行
                {
                    DataRow dr = _lotMappingDt.NewRow();
                    for (int j = 0; j < Math.Min(values.Length, _lotMappingDt.Columns.Count); j++)
                    {
                        dr[j] = StringHelper.ConvertEmptyToDefault(values[j].Trim());
                    }
                    _lotMappingDt.Rows.Add(dr);
                }
            }
        }
    }
}
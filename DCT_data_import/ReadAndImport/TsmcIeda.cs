using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using Dapper;
using static DCT_data_import.DbObject;
namespace DCT_data_import.ReadAndImport
{
    public class TsmcIeda : ImportData
    {
        private readonly DataTable _lotMappingDt = new DataTable();
        public TsmcIeda()
        {
            // 設定全域變數中的DataTable _lotMappingDt
            GetLotMapping();
        }
        public ImportResult ReadAndImportIeda(FileProcess fileAccess, DatabaseService DatabaseService, string dbKey)
        {
            string ftpserver = string.Empty;
            WriteToLog writeToLog = new WriteToLog();
            string deleteStatus;
            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();
            bool import_result;
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts2 = stopWatch.Elapsed;
            List<string> list_filename = new List<string>();
            ftpserver = GetSourcePath("TSMC_DATA/IEDA/");
            try
            {
                list_filename = FileSource.ListFiles(ftpserver, string.Empty);
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog($"TSMC 之 IEDA 讀取檔案清單錯誤: {ftpserver}, 詳細錯誤: {ex.Message}");
            }
            // 查詢所有檔案
            for (int i = list_filename.Count - 1; i >= 0; i--)
            {
                // 取得IEDA檔案名
                string filename = list_filename[i];
                try
                {
                    ftpserver = GetSourcePath("TSMC_DATA/IEDA/" + filename);
                    string errorPath = GetSourcePath("TSMC_DATA/IEDA_error/" + filename);
                    IedaDataFormat iedaDataFormat = ReadBig5File(ftpserver, FileReadIeda);
                    if (!string.IsNullOrEmpty(iedaDataFormat.ErrMsg))
                    {
                        MoveToError(ftpserver, errorPath);
                        return new ImportResult(2, iedaDataFormat.ErrMsg);
                    }
                    stopWatch.Reset();
                    stopWatch.Start();
                    // 開始匯入
                    import_result = ImportIeda(iedaDataFormat, DatabaseService);
                    stopWatch.Stop();
                    ts2 = stopWatch.Elapsed;
                    if (import_result)
                    {
                        Console.WriteLine("匯入完成! TSMC IEDA    檔名:" + filename + "    耗時: " + Convert.ToInt32(ts2.TotalMilliseconds / 1000).ToString() + " 秒");
                        // 刪除已存在的的CSV檔案
                        deleteStatus = CompleteSuccess(ftpserver);
                    }
                    else
                    {
                        Console.WriteLine("匯入失敗: TSMC IEDA " + filename);
                        writeToLog.WriteErrorLog("匯入失敗: TSMC IEDA " + ftpserver);
                        MoveToError(ftpserver, errorPath);
                    }
                }
                catch (Exception ex)
                {
                    MoveToError(ftpserver, GetSourcePath("TSMC_DATA/IEDA_error/" + filename));
                    writeToLog.WriteErrorLog($"TSMC 之 IEDA 讀檔失敗: {ftpserver}, 檔案: {filename}, 錯誤: {ex.Message}");
                    Console.WriteLine($"TSMC 之 IEDA 讀檔失敗: {ftpserver}, 檔案: {filename}, 錯誤: {ex.Message}");
                }
            }
            return new ImportResult(1, string.Empty);
        }
        public IedaDataFormat FileReadIeda(StreamReader reader)
        {
            IedaDataFormat iedaDataFormat = new IedaDataFormat();
            try
            {
                string lines;
                lines = reader.ReadToEnd();
                List<string> split_lines = lines.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                for (int r = 0; r < split_lines.Count; r++)
                {
                    if (r == 0)  // iEDA title part
                    {
                        int charIdx = 0;
                        DataRow dr = iedaDataFormat.IedaTitle.NewRow();
                        for (int j = 1; j < iedaDataFormat.IedaTitle.Columns.Count; j++)
                        {
                            dr[j] = split_lines[r].Substring(charIdx, iedaDataFormat.titleColumnsDataSize[j]).Trim();
                            charIdx += iedaDataFormat.titleColumnsDataSize[j];
                        }
                        // 取得ase_lot
                        DataRow[] mappingDtRows = _lotMappingDt.Select("tsmc_lot='" + EscapeDataTableFilterValue(dr["lot_id"].ToString()) + "'");
                        if (mappingDtRows.Length > 0)
                        {
                            dr["ase_lot"] = mappingDtRows[0]["ase_lot"];
                        }
                        iedaDataFormat.IedaTitle.Rows.Add(dr);
                    }
                    else // iEDA content part
                    {
                        int charIdx = 0;
                        DataRow dr = iedaDataFormat.IedaContent.NewRow();
                        for (int j = 1; j < iedaDataFormat.IedaContent.Columns.Count; j++)
                        {
                            dr[j] = split_lines[r].Substring(charIdx, iedaDataFormat.contentColumnsDataSize[j]).Trim();
                            charIdx += iedaDataFormat.contentColumnsDataSize[j];
                        }
                        iedaDataFormat.IedaContent.Rows.Add(dr);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog writeToLogService = new WriteToLog();
                writeToLogService.WriteErrorLog($"[TSMC IEDA檔案解析] 讀檔內容錯誤, 錯誤: {ex.Message}");
                iedaDataFormat.ErrMsg = "讀檔內容錯誤, Error:" + ex.Message;
                Console.WriteLine($"{ex.Message} ");
            }
            //Console.ReadLine();
            return iedaDataFormat;
        }
        #region GetAseLot()
        //public string GetAseLot(string DbKey)
        //{
        //    String ftpserver;
        //    FtpWebRequest reqFTP;
        //    FtpWebResponse response;
        //    Stream responseStream;
        //    StreamReader reader;
        //    try
        //    {
        //        string filename = DbKey+".txt";
        //        ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/TSMC_DATA/LotID/" + filename;
        //        reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
        //        reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
        //        response = (FtpWebResponse)reqFTP.GetResponse();
        //        responseStream = response.GetResponseStream();
        //        reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));
        //        string lines = reader.ReadToEnd();
        //        return lines.Split(',')[0];
        //    }
        //    catch(Exception ex)
        //    {
        //        return "";
        //    }
        //}
        #endregion GetAseLot() end
        public List<string> GetNetNameList(string aseLot, int recursive = 0)
        {
            string ftpserver = string.Empty;
            WriteToLog writeToLog = new WriteToLog();
            string filename = string.Empty;
            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();
            List<string> netNameList = new List<string>();
            string errorPath = string.Empty;
            ftpserver = GetSourcePath("TSMC_DATA/CSV/");
            try
            {
                // 取得 CSV 檔名
                DataRow[] mappingDtRows = _lotMappingDt.Select("ase_lot='" + EscapeDataTableFilterValue(aseLot) + "'");
                if (mappingDtRows.Length > 0)
                {
                    filename = mappingDtRows[0]["csv"].ToString();  //mappingDtRows[2] 是 "csv" 欄位值
                }
                else
                {
                    return netNameList;
                }
                ftpserver = GetSourcePath("TSMC_DATA/CSV/" + filename);
                errorPath = GetSourcePath("TSMC_DATA/CSV_error/" + filename);
                string netNameLine = string.Empty;
                using (StreamReader reader = OpenBig5Reader(ftpserver))
                {
                    string line = reader.ReadLine();
                    while (line != null)
                    {
                        // Safe string splitting with bounds checking
                        var lineParts = line.Split(',');
                        if (lineParts.Length > 0 && lineParts[0] == "Net Name")
                        {
                            netNameLine = line;
                            break;
                        }
                        line = reader.ReadLine();
                    }
                }
                netNameList = netNameLine.Split(',').ToList();
                if (netNameList.Count > 0) netNameList.RemoveAt(0);
                // 刪除已成功讀完的TSMC CSV檔案
                string deleteStatus = CompleteSuccess(ftpserver);
            }
            catch (Exception ex)
            {
                if (recursive == 0)
                {
                    return GetNetNameList(aseLot, 1);
                }
                else
                {
                    writeToLog.WriteErrorLog("TSMC 之 CSV 讀檔錯誤:" + ftpserver + "  error:" + ex.Message);
                    MoveToError(ftpserver, errorPath);
                    return new List<string>();
                }
            }
            return netNameList;
        }
        public string GetLotMapping()
        {
            WriteToLog writeToLog = new WriteToLog();
            string ftpserver;
            try
            {
                ftpserver = GetSourcePath("TSMC_DATA/LotID/lot_mapping.csv");
                string lines = ReadBig5File(ftpserver, reader => reader.ReadToEnd());
                List<string> split_lines = lines.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                for (int i = 0; i < split_lines.Count; i++)
                {
                    string[] values = split_lines[i].Trim().Split(',', '\0', '\r', '\n');
                    // assign DataTable 欄位
                    if (i == 0)
                    {
                        for (int j = 0; j < values.Length; j++)
                        {
                            _lotMappingDt.Columns.Add(values[j].Trim());
                        }
                    }
                    else
                    {
                        DataRow dr = _lotMappingDt.NewRow();
                        for (int j = 0; j < values.Length; j++)
                        {
                            dr[j] = values[j].Trim();
                        }
                        _lotMappingDt.Rows.Add(dr);
                    }
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("'GetLotMapping() error:" + ex.Message);
                return ex.Message;
            }
            return "Get lot mapping successfully.";
        }
        public bool ImportIeda(IedaDataFormat content, DatabaseService DatabaseService)
        {
            if (content.IedaTitle.Rows.Count < 1 || content.IedaContent.Rows.Count < 1) return false;
            Execute_query_response response2;
            FileProcess fileProcess = new FileProcess();
            WriteToLog writeToLog = new WriteToLog();
            #region insert ieda 的 title 表格
            try
            {
                response2 = fileProcess.ExecuteInsert(DatabaseService, "ieda_title", BuildIedaTitleInsertQuery(content.IedaTitle, fileProcess));
                if (!string.IsNullOrEmpty(response2.Error))
                {
                    writeToLog.WriteErrorLog("'INSERT INTO ieda_title' error:" + response2.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("'INSERT INTO ieda_title' error:" + ex.Message);
                return false;
            }
            #endregion
            #region  取得當前 title id 值
            string titleId = string.Empty;
            try
            {
                titleId = response2.Data[0]["insertId"].ToString();
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("'取得當前 lot id 值 error:" + ex.Message);
                Console.WriteLine(ex.Message);
                return false;
            }
            #endregion
            #region insert ieda 的 content 表格
            try
            {
                response2 = fileProcess.ExecuteInsert(DatabaseService, "ieda_content", BuildIedaContentInsertQuery(content.IedaContent, titleId, fileProcess));
                if (!string.IsNullOrEmpty(response2.Error))
                {
                    writeToLog.WriteErrorLog("'INSERT INTO ieda_content' error:" + response2.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("'INSERT INTO ieda_content' error:" + ex.Message);
                return false;
            }
            #endregion
            return true;
        }

        internal static string EscapeDataTableFilterValue(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        internal static Execute_query BuildIedaTitleInsertQuery(DataTable titleTable, FileProcess fileProcess)
        {
            string columns = string.Empty;
            string values = string.Empty;
            var parameters = new DynamicParameters();
            for (int i = 0; i < titleTable.Columns.Count; i++)
            {
                columns += "`" + titleTable.Columns[i].ColumnName.Trim() + "`";
                values += "@title_" + i;
                parameters.Add("title_" + i, fileProcess.ConvertEmptyToDefaultString(titleTable.Rows[0][i].ToString()));
                if (i != titleTable.Columns.Count - 1)
                {
                    columns += ",";
                    values += ",";
                }
            }

            return FileProcess.BuildInsertQuery("ieda_title", columns, values, parameters);
        }

        internal static Execute_query BuildIedaContentInsertQuery(DataTable contentTable, string titleId, FileProcess fileProcess)
        {
            string columns = string.Empty;
            for (int i = 0; i < contentTable.Columns.Count; i++)
            {
                columns += "`" + contentTable.Columns[i].ColumnName.Trim() + "`";
                if (i != contentTable.Columns.Count - 1)
                {
                    columns += ",";
                }
            }

            string values = string.Empty;
            var parameters = new DynamicParameters();
            for (int i = 0; i < contentTable.Rows.Count; i++)
            {
                values += "(@content_" + i + "_0,";
                parameters.Add("content_" + i + "_0", fileProcess.ConvertEmptyToDefaultString(titleId));
                for (int j = 1; j < contentTable.Columns.Count; j++)
                {
                    values += "@content_" + i + "_" + j;
                    parameters.Add("content_" + i + "_" + j, fileProcess.ConvertEmptyToDefaultString(contentTable.Rows[i][j].ToString()));
                    if (j != contentTable.Columns.Count - 1)
                    {
                        values += ",";
                    }
                }
                if (i != contentTable.Rows.Count - 1)
                {
                    values += "),";
                }
            }
            values = values.Substring(1, values.Length - 1);

            return FileProcess.BuildInsertQuery("ieda_content", columns, values, parameters);
        }
    }
}

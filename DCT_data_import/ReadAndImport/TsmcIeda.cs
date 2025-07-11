using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using static DCT_data_import.ApiObject;
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
        public ImportResult ReadAndImportIeda(FileProcess fileAccess, DatabaseService  DatabaseService , string dbKey)
        {
            string ftpserver = "";
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            WriteToLog writeToLog = new WriteToLog();
            string deleteStatus;
            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();
            bool import_result;
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts2 = stopWatch.Elapsed;
            string names;
            List<string> list_filename = new List<string>();
            string errorDir = string.Empty;
            ftpserver = "ftp://" + Program.FTP_IP;
            if (Program.Environment == "Dev")
            {
                ftpserver += "/DCT_Log/DCT_DB_DATA_Dev/TSMC_DATA/IEDA/";
            }
            else if (Program.Environment == "Prod")
            {
                ftpserver += "/DCT_Log/DCT_DB_DATA/TSMC_DATA/IEDA/";
            }
            //ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/TSMC_DATA/IEDA/";
            try
            {
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
                reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
                response = (FtpWebResponse)reqFTP.GetResponse();
                responseStream = response.GetResponseStream();
                reader = new StreamReader(responseStream);
                names = reader.ReadToEnd();
                list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            catch (Exception)
            {
                writeToLog.WriteToDataImportLog("TSMC 之 IEDA 讀取檔案清單錯誤:" + ftpserver);
            }
            // 查詢所有檔案
            for (int i = list_filename.Count - 1; i >= 0; i--)
            {
                // 取得IEDA檔案名
                string filename = list_filename[i];
                try
                {
                    ftpserver = "ftp://" + Program.FTP_IP;
                    if (Program.Environment == "Dev")
                    {
                        ftpserver += "/DCT_Log/DCT_DB_DATA_Dev/TSMC_DATA/IEDA/" + filename;
                        errorDir = "/DCT_Log/DCT_DB_DATA_Dev/TSMC_DATA/IEDA_error/";
                    }
                    else if (Program.Environment == "Prod")
                    {
                        ftpserver += "/DCT_Log/DCT_DB_DATA/TSMC_DATA/IEDA/" + filename;
                        errorDir = "/DCT_Log/DCT_DB_DATA/TSMC_DATA/IEDA_error/";
                    }
                    //ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/TSMC_DATA/IEDA/" + filename;
                    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
                    response = (FtpWebResponse)reqFTP.GetResponse();
                    responseStream = response.GetResponseStream();
                    reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));
                    IedaDataFormat iedaDataFormat = FileReadIeda(reader);
                    if (!string.IsNullOrEmpty(iedaDataFormat.ErrMsg))
                    {
                        RenameFile(ftpserver, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                        return new ImportResult(2, iedaDataFormat.ErrMsg);
                    }
                    stopWatch.Reset();
                    stopWatch.Start();
                    // 開始匯入
                    import_result = ImportIeda(iedaDataFormat, DatabaseService );
                    stopWatch.Stop();
                    ts2 = stopWatch.Elapsed;
                    if (import_result)
                    {
                        Console.WriteLine("匯入完成! TSMC IEDA    檔名:" + filename + "    耗時: " + Convert.ToInt32(ts2.TotalMilliseconds / 1000).ToString() + " 秒");
                        // 刪除已存在的的CSV檔案
                        deleteStatus = DeleteFile(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
                    }
                    else
                    {
                        RenameFile(ftpserver, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    }
                }
                catch (Exception)
                {
                    RenameFile(ftpserver, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    writeToLog.WriteToDataImportLog("TSMC 之 IEDA 讀檔失敗:" + ftpserver);
                }
            }
            return new ImportResult(1, "");
        }
        public IedaDataFormat FileReadIeda(StreamReader reader)
        {
            IedaDataFormat iedaDataFormat = new IedaDataFormat();
            try
            {
                string lines;
                lines = reader.ReadToEnd();
                //using (StreamReader reader = new StreamReader(path))
                //{
                //}
                List<string> split_lines = lines.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                for (int r = 0; r < split_lines.Count; r++)
                {
                    //Console.WriteLine(split_lines[r]);
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
                        DataRow[] mappingDtRows = _lotMappingDt.Select("tsmc_lot='" + dr["lot_id"].ToString() + "'");
                        //string aseLot = GetAseLot("ASE01-5070-075-10.10.53.169_AAH@A212990017-0_0315_N_20230904-213225");  // 輸入DB_KEY取得 ase_lot對應檔案的檔名
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
                iedaDataFormat.ErrMsg = "讀檔內容錯誤";
                Console.WriteLine($"{ex.ToString()} , {ex.Message}");
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
            string ftpserver = "";
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            WriteToLog writeToLog = new WriteToLog();
            string filename = "";
            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();
            List<string> netNameList = new List<string>();
            string errorDir = string.Empty;
            ftpserver = "ftp://" + Program.FTP_IP;
            if (Program.Environment == "Dev")
            {
                ftpserver += "/DCT_Log/DCT_DB_DATA_Dev/TSMC_DATA/CSV/";
                errorDir = "/DCT_Log/DCT_DB_DATA_Dev/TSMC_DATA/CSV_error/";
            }
            else if (Program.Environment == "Prod")
            {
                ftpserver += "/DCT_Log/DCT_DB_DATA/TSMC_DATA/CSV/";
                errorDir = "/DCT_Log/DCT_DB_DATA/TSMC_DATA/CSV_error/";
            }
            try
            {
                // 取得 CSV 檔名
                DataRow[] mappingDtRows = _lotMappingDt.Select("ase_lot='" + aseLot + "'");
                if (mappingDtRows.Length > 0)
                {
                    filename = mappingDtRows[0]["csv"].ToString();  //mappingDtRows[2] 是 "csv" 欄位值
                }
                else
                {
                    return netNameList;
                }
                ftpserver += filename;
                //ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/TSMC_DATA/CSV/" + filename;
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                //reqFTP.Timeout = 5000;//設定5秒超時
                //reqFTP.ReadWriteTimeout = 5000;
                reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
                response = (FtpWebResponse)reqFTP.GetResponse();
                responseStream = response.GetResponseStream();
                string netNameLine = "";
                StreamReader reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));
                string line = reader.ReadLine();
                while (line != null)
                {
                    if (line.Split(',')[0] == "Net Name")
                    {
                        netNameLine = line;
                        break;
                    }
                    line = reader.ReadLine();
                }
                reader.Close();
                netNameList = netNameLine.Split(',').ToList();
                if (netNameList.Count > 0) netNameList.RemoveAt(0);
                // 刪除已成功讀完的TSMC CSV檔案
                string deleteStatus = DeleteFile(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
            }
            catch (Exception ex)
            {
                if (recursive == 0)
                {
                    return GetNetNameList(aseLot, 1);
                }
                else
                {
                    writeToLog.WriteToDataImportLog("TSMC 之 CSV 讀檔錯誤:" + ftpserver + "  error:" + ex.ToString());
                    RenameFile(ftpserver, errorDir + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new List<string>();
                }
            }
            return netNameList;
        }
        public string GetLotMapping()
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            try
            {
                ftpserver = "ftp://" + Program.FTP_IP;
                if (Program.Environment == "Dev")
                {
                    ftpserver += "/DCT_Log/DCT_DB_DATA_Dev/TSMC_DATA/LotID/lot_mapping.csv";
                }
                else if (Program.Environment == "Prod")
                {
                    ftpserver += "/DCT_Log/DCT_DB_DATA/TSMC_DATA/LotID/lot_mapping.csv";
                }
                //ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/TSMC_DATA/LotID/lot_mapping.csv";
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
                response = (FtpWebResponse)reqFTP.GetResponse();
                responseStream = response.GetResponseStream();
                reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));
                string lines = reader.ReadToEnd();
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
                return ex.ToString();
            }
            return "Get lot mapping successfully.";
        }
        public bool ImportIeda(IedaDataFormat content, DatabaseService  DatabaseService )
        {
            if (content.IedaTitle.Rows.Count < 1 || content.IedaContent.Rows.Count < 1) return false;
            // assign 需要 insert 的 欄位名稱 與 values
            string columns = "", values = "";
            Pool_execute_response response2;
            FileProcess fileProcess = new FileProcess();
            WriteToLog writeToLog = new WriteToLog();
            #region insert ieda 的 title 表格
            try
            {
                for (int i = 0; i < content.IedaTitle.Columns.Count; i++)
                {
                    columns += "`" + content.IedaTitle.Columns[i].ColumnName.Trim() + "`";
                    values += "'" + fileProcess.ConvertEmptyToDefaultString(content.IedaTitle.Rows[0][i].ToString()) + "'";
                    //values += "'" + content.iedaTitle.Rows[0][i].ToString().Trim() + "'";
                    if (i != content.IedaTitle.Columns.Count - 1)
                    {
                        columns += ",";
                        values += ",";
                    }
                }
                response2 = fileProcess.ExecuteInsertWithAPI(DatabaseService , "ieda_title", columns, values);
                if (!string.IsNullOrEmpty(response2.Error))
                {
                    if (response2.Error.Contains("Please initiate connection pool first using the init function"))
                    {
                        writeToLog.WriteToDataImportLog("'INSERT INTO ieda_title' error:" + response2.Error);
                        return false;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                writeToLog.WriteToDataImportLog("'INSERT INTO ieda_title' error:" + ex.ToString());
                return false;
            }
            #endregion
            #region  取得當前 title id 值
            string titleId = "";
            try
            {
                titleId = response2.Data[0]["insertId"].ToString();
            }
            catch (Exception ex)
            {
                writeToLog.WriteToDataImportLog("'取得當前 lot id 值 error:" + ex.ToString());
                Console.WriteLine(ex.ToString());
                return false;
            }
            #endregion
            #region insert ieda 的 content 表格
            try
            {
                columns = "";
                for (int i = 0; i < content.IedaContent.Columns.Count; i++)
                {
                    columns += "`" + content.IedaContent.Columns[i].ColumnName.Trim() + "`";
                    values += "'" + fileProcess.ConvertEmptyToDefaultString(content.IedaContent.Rows[0][i].ToString()) + "'";
                    //values += "'" + content.iedaContent.Rows[0][i].ToString().Trim() + "'";
                    if (i != content.IedaContent.Columns.Count - 1)
                    {
                        columns += ",";
                    }
                }
                values = "";
                for (int i = 0; i < content.IedaContent.Rows.Count; i++)
                {
                    values += "('" + fileProcess.ConvertEmptyToDefaultString(titleId) + "',";
                    //values += "('" + titleId + "',";
                    for (int j = 1; j < content.IedaContent.Columns.Count; j++)
                    {
                        values += "'" + fileProcess.ConvertEmptyToDefaultString(content.IedaContent.Rows[i][j].ToString()) + "'";
                        //values += "'" + content.iedaContent.Rows[i][j].ToString() + "'";
                        if (j != content.IedaContent.Columns.Count - 1)
                        {
                            values += ",";
                        }
                    }
                    if (i != content.IedaContent.Rows.Count - 1)
                    {
                        values += "),";
                    }
                }
                values = values.Substring(1, values.Length - 1);
                response2 = fileProcess.ExecuteInsertWithAPI(DatabaseService , "ieda_content", columns, values);
                if (!string.IsNullOrEmpty(response2.Error))
                {
                    if (response2.Error.Contains("Please initiate connection pool first using the init function"))
                    {
                        writeToLog.WriteToDataImportLog("'INSERT INTO ieda_content' error:" + response2.Error);
                        return false;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                writeToLog.WriteToDataImportLog("'INSERT INTO ieda_content' error:" + ex.ToString());
                return false;
            }
            #endregion
            return true;
        }
    }
}
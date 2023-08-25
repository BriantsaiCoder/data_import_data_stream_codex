using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static DCT_data_import.ApiObject;

namespace DCT_data_import.ReadAndImport
{
    public class TsmcIeda : ImportData
    {

        public ImportResult readAndImportIeda(FileProcess fileAccess, WebApiClient webApiClient, string dbKey)
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;

            bool import_result;

            try
            {
                string filename = "N9HK53.LE-FT1-202208311814";
                ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/TSMC_DATA/IEDA/" + filename;
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
                response = (FtpWebResponse)reqFTP.GetResponse();
                responseStream = response.GetResponseStream();
                reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));

                IedaDataFormat iedaDataFormat =   FileReadIeda(reader);


                // 開始匯入
                import_result = ImportIeda(iedaDataFormat, webApiClient);


            }
            catch(Exception ex)
            {

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
                        DataRow dr = iedaDataFormat.iedaTitle.NewRow();

                        // 取得ase_lot
                        string aseLot = GetAseLot("DB_Key_exmaple");  // 輸入DB_KEY取得 ase_lot對應檔案的檔名
                        dr["ase_lot"] = aseLot;

                        for (int j = 1; j < iedaDataFormat.iedaTitle.Columns.Count; j++)
                        {
                            dr[j] = split_lines[r].Substring(charIdx, iedaDataFormat.titleColumnsDataSize[j]).Trim();
                            charIdx += iedaDataFormat.titleColumnsDataSize[j];
                        }
                        
                        iedaDataFormat.iedaTitle.Rows.Add(dr);

                    }
                    else // iEDA content part
                    {
                        int charIdx = 0;
                        DataRow dr = iedaDataFormat.iedaContent.NewRow();

                        for (int j = 1; j < iedaDataFormat.iedaContent.Columns.Count; j++)
                        {
                            dr[j] = split_lines[r].Substring(charIdx, iedaDataFormat.contentColumnsDataSize[j]).Trim();
                            charIdx += iedaDataFormat.contentColumnsDataSize[j];
                        }

                        iedaDataFormat.iedaContent.Rows.Add(dr);
                    }




                }

            }
            catch (Exception ex)
            {
                iedaDataFormat.errMsg = "讀檔內容錯誤";
                Console.WriteLine(ex.ToString());
            }


            //Console.ReadLine();

            return iedaDataFormat;

        }
        

        public string GetAseLot(string DbKey)
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;

            try
            {

                string filename = DbKey+".txt";
                ftpserver = "ftp://" + Program.FTP_IP + "/DCT_Log/DCT_DB_DATA/TSMC_DATA/LotID/" + filename;
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
                response = (FtpWebResponse)reqFTP.GetResponse();
                responseStream = response.GetResponseStream();
                reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));

                string lines = reader.ReadToEnd();

                return lines.Split(',')[0];

            }
            catch(Exception ex)
            {
                return "";
            }
            
        }

        public List<string> GetNetNameList(string path)
        {
            List<string> netNameList = new List<string>();

            try
            {
                string netNameLine = "";
                using (StreamReader reader = new StreamReader(path))
                {
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
                }

                netNameList = netNameLine.Split(',').ToList();
                netNameList.RemoveAt(0);

            }
            catch (Exception ex)
            {
                return new List<string>();
            }

            return netNameList;
        }

        public bool ImportIeda(IedaDataFormat content, WebApiClient webApiClient)
        {
            if (content.iedaTitle.Rows.Count < 1 || content.iedaContent.Rows.Count < 1) return false;
            // assign 需要 insert 的 欄位名稱 與 values
            string columns = "", values = "";
            Pool_excute pool_excute;
            Pool_excute_response response2;
            FileProcess fileProcess = new FileProcess();
            WriteToLog writeToLog = new WriteToLog();

            #region insert ieda 的 title 表格
            try
            {
                for (int i = 0; i < content.iedaTitle.Columns.Count; i++)
                {
                    columns += "`" + content.iedaTitle.Columns[i].ColumnName.Trim() + "`";
                    values += "'" + content.iedaTitle.Rows[0][i].ToString().Trim() + "'";
                    if (i != content.iedaTitle.Columns.Count - 1)
                    {
                        columns += ",";
                        values += ",";
                    }
                }

                response2 = fileProcess.executeInsertWithAPI(webApiClient, "ieda_title", columns, values);
                if (!string.IsNullOrEmpty(response2.error))
                {
                    if (response2.error.Contains("Please initiate connection pool first using the init function"))
                    {
                        writeToLog.writeToLog("'INSERT INTO ieda_title' error:" + response2.error);
                        return false;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                writeToLog.writeToLog("'INSERT INTO ieda_title' error:" + ex.ToString());
                return false;
            }

            #endregion

            #region  取得當前 title id 值
            string titleId = "";
            try
            {
                titleId = response2.data[0]["insertId"].ToString();
            }
            catch (Exception ex)
            {
                writeToLog.writeToLog("'取得當前 lot id 值 error:" + ex.ToString());
                Console.WriteLine(ex.ToString());
                return false;
            }
            #endregion

            #region insert ieda 的 content 表格

            try
            {
                columns = "";
                for (int i = 0; i < content.iedaContent.Columns.Count; i++)
                {
                    columns += "`" + content.iedaContent.Columns[i].ColumnName.Trim() + "`";
                    values += "'" + content.iedaContent.Rows[0][i].ToString().Trim() + "'";
                    if (i != content.iedaContent.Columns.Count - 1)
                    {
                        columns += ",";
                    }
                }

                values = "";
                for (int i = 0; i < content.iedaContent.Rows.Count; i++)
                {
                    values += "('" + titleId + "',";
                    for (int j = 1; j < content.iedaContent.Columns.Count; j++)
                    {
                        values += "'" + content.iedaContent.Rows[i][j].ToString() + "'";

                        if (j != content.iedaContent.Columns.Count - 1)
                        {
                            values += ",";
                        }
                    }

                    if (i != content.iedaContent.Rows.Count - 1)
                    {
                        values += "),";
                    }

                }
                
                values = values.Substring(1, values.Length - 1);

                response2 = fileProcess.executeInsertWithAPI(webApiClient, "ieda_content", columns, values);
                if (!string.IsNullOrEmpty(response2.error))
                {
                    if (response2.error.Contains("Please initiate connection pool first using the init function"))
                    {
                        writeToLog.writeToLog("'INSERT INTO ieda_content' error:" + response2.error);
                        return false;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                writeToLog.writeToLog("'INSERT INTO ieda_content' error:" + ex.ToString());
                return false;
            }

            #endregion


            return true;
        }



    }
}

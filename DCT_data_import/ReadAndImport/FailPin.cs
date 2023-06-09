using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DCT_data_import.ApiObject;

namespace DCT_data_import.ReadAndImport
{
    public class FailPin : ImportData
    {
        public async Task<ImportResult> readAndImportFailPinLog(FileProcess fileAccess, WebApiClient webApiClient, string dbKey)
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            bool import_result = false, isDBKeyExist = false;
            WriteToLog writeToLog = new WriteToLog();
            CompareTool compareTool = new CompareTool();
            bool compare_result = false;
            string downloadStatus, deleteStatus;

            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();

            // 檢查FTP連線狀態
            ftpserver = "ftp://" + Program.FTP_IP + "/Data_Analysis/Fail_Pin_Log/ST_RT_AT/CSV_Kerwin/";
            bool isFtpConnected = isValidFtpConnection(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
            if (!isFtpConnected)
                return new ImportResult(0, "FTP server connection failed.");

            // 檢查FTP是否有此檔案
            string filename = "ui_status_" + dbKey + ".csv";
            ftpserver = "ftp://" + Program.FTP_IP + "/Data_Analysis/Fail_Pin_Log/ST_RT_AT/CSV_Kerwin/" + filename;
            bool isFileExist = CheckIfFileExistsOnServer(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);
            if (!isFileExist)
                return new ImportResult(0, "File not found.");

            // 確認 pool 連線狀態
            bool isConnect = webApiClient.checkDBConnect(Program.POOL_NAME);
            if (!isConnect) // 沒有pool連線資訊，則建立一個新的連線。如果建立pool失敗就中斷程式
                if (!createPool(webApiClient, writeToLog))
                    return new ImportResult(0, "MySQL database connection failed.");
            

            try
            {
                ftpserver = "ftp://" + Program.FTP_IP + "/Data_Analysis/Fail_Pin_Log/ST_RT_AT/CSV_Kerwin/" + filename;
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                reqFTP.Credentials = new NetworkCredential(Program.FTP_USER, Program.FTP_PASSWORD);
                response = (FtpWebResponse)reqFTP.GetResponse();
                responseStream = response.GetResponseStream();
                reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));

                FailPinLogContentFormat failPinLogContent = FileReadFailPinLog(reader);
                // 讀取失敗或沒有資料
                if (failPinLogContent == null || failPinLogContent.fail_pin_rate_info.Rows.Count < 1)
                {
                    Console.WriteLine("Fail Pin Log 讀取失敗:  " + filename);
                    writeToLog.writeToLog("Fail Pin Log 讀取失敗: " + ftpserver);
                    RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(2, "檔案內容缺失");
                }
                reader.Close();
                if (!failPinLogContent.compareInfo())
                {
                    Console.WriteLine("Fail Pin Log 之 information 欄位名稱不符:  " + filename);
                    writeToLog.writeToLog("Fail Pin Log 之 information 欄位名稱不符: " + ftpserver);
                    RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    return new ImportResult(2, "information 欄位名稱不符");
                }
                isDBKeyExist = fileAccess.isDBKeyExistInDB("fail_pin_rate_info", failPinLogContent.fail_pin_rate_info.Rows[0]["DB Key"].ToString(), webApiClient);
                if (isDBKeyExist)
                {
                    //compare_result = compareTool.compareFailPinLog(failPinLogContent, webApiClient);
                    Console.WriteLine("資料庫已存在此資料: Fail Pin   檔名:" + filename);
                    //Console.WriteLine("資料庫已存在此資料: Fail Pin  " + " 比對: " + compare_result + "   檔名:" + list_filename[i]);
                    //writeToLog.writeToLog("資料庫已存在此資料: " + ftpserver);
                    // Kerwin 的電腦
                    if (macid == "94C6913F94BD")
                    {
                        // 下載檔案到本地端
                        downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\fail_pin_log_temp\" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                    }
                    // 刪除已存在的的CSV檔案
                    deleteStatus = DeleteFile(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);

                    return new ImportResult(3, "資料庫已有相同DB_Key資料");
                }
                else
                {
                    await Task.Run(() =>
                    {
                        import_result = fileAccess.importFailPinLog(failPinLogContent, webApiClient);
                    }).ConfigureAwait(false);
                    //compare_result = compareTool.compareFailPinLog(failPinLogContent, webApiClient);
                    if (import_result)
                    {
                        //Console.WriteLine("匯入完成! Fail Pin    比對: " + compare_result + "   檔名:" + list_filename[i]);
                        Console.WriteLine("匯入完成! Fail Pin      檔名:" + filename);
                        // Kerwin 的電腦
                        if (macid == "94C6913F94BD")
                        {
                            // 下載檔案到本地端
                            downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\fail_pin_log_temp\" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                        }
                        // 刪除完成的CSV檔案
                        deleteStatus = DeleteFile(ftpserver, Program.FTP_USER, Program.FTP_PASSWORD);

                        reader.Close();
                        response.Close();
                    }
                    else
                    {
                        Console.WriteLine("匯入失敗: Fail Pin " + filename);
                        writeToLog.writeToLog("匯入失敗:" + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + filename, Program.FTP_USER, Program.FTP_PASSWORD);
                        
                        reader.Close();
                        response.Close();
                        return new ImportResult(3, "匯入失敗");
                    }
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                writeToLog.writeToLog(ex.ToString());
                return new ImportResult(3, "匯入時發生Exception錯誤");
            }

            GC.Collect();
            //Console.WriteLine("Fail pin log end~");
            return new ImportResult(1, "");
        }

        public FailPinLogContentFormat FileReadFailPinLog(StreamReader reader)
        {
            FailPinLogContentFormat failPinLogContentFormat = new FailPinLogContentFormat();
            try
            {
                string data_format = "";

                //using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                //{
                //    using (var reader = new StreamReader(stream))
                //    {
                int content_part = 1;
                int fail_pin_list_id = 0;


                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = eraseSpecificChar(line);

                    if (values == null)
                    {
                        continue;
                    }
                    if (values.Length < 1) continue;

                    // 看到關鍵字 "Data format" ，取得pin/ball類型
                    if (values[0] == "Data format")
                    {
                        data_format = values[1];
                    }

                    // 看到關鍵字 "DUT"
                    if (values[0] == "DUT")
                    {
                        content_part = 2;
                        continue;
                    }

                    // fail pin rate的上半部分
                    if (content_part == 1)
                    {
                        failPinLogContentFormat.fail_pin_rate_info.Columns.Add(values[0], typeof(string));
                        failPinLogContentFormat.fail_pin_rate_info.Rows[0][values[0]] = (values.Length > 1) ? values[1] : "";
                    }
                    // fail pin rate的下半部分
                    else if (content_part == 2)
                    {
                        if (values.Length >= 3)
                        {
                            DataRow dr_fail_pin_rate_list = failPinLogContentFormat.fail_pin_rate_list.NewRow();
                            // 欄位 DUT, Site, Fail Type
                            for (int i = 0; i < 3; i++)
                            {
                                dr_fail_pin_rate_list[i] = values[i];
                            }
                            failPinLogContentFormat.fail_pin_rate_list.Rows.Add(dr_fail_pin_rate_list);

                            // 讀取 fail pin 與 log 存到 List
                            int fail_pin_part = 1;
                            List<string> fail_pin_list = new List<string>();
                            List<string> fail_pin_log = new List<string>();
                            for (int i = 3; i < values.Length; i++)
                            {
                                // 以 ';' 分開fail pin log 與 remark
                                if (values[i] == ";")
                                {
                                    fail_pin_part = 2;
                                    continue;
                                }

                                if (fail_pin_part == 1)
                                {
                                    fail_pin_list.Add(values[i]);
                                }
                                else if (fail_pin_part == 2)
                                {
                                    fail_pin_log.Add(values[i]);
                                }

                            }

                            fail_pin_list_id++;
                            //  將 fail pin 與 log 存到 DataTable
                            for (int i = 0; i < fail_pin_list.Count; i++)
                            {
                                DataRow dr_fail_pin_rate_list_pin_ball = failPinLogContentFormat.fail_pin_rate_list_pin_ball.NewRow();
                                string[] value_split = fail_pin_list[i].Split('(', ')');
                                if (data_format == "Pin")
                                {
                                    dr_fail_pin_rate_list_pin_ball["pin"] = value_split[0];
                                    dr_fail_pin_rate_list_pin_ball["ball"] = value_split[1];
                                }
                                else if (data_format == "Ball")
                                {
                                    dr_fail_pin_rate_list_pin_ball["ball"] = value_split[0];
                                    dr_fail_pin_rate_list_pin_ball["pin"] = value_split[1];
                                }
                                dr_fail_pin_rate_list_pin_ball["fail_pin_rate_list_id"] = fail_pin_list_id;
                                dr_fail_pin_rate_list_pin_ball["remark"] = String.Join(",", fail_pin_log.ToArray());

                                failPinLogContentFormat.fail_pin_rate_list_pin_ball.Rows.Add(dr_fail_pin_rate_list_pin_ball);
                            }

                        }
                    }


                }

                //    }
                //}

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
            return failPinLogContentFormat;
        }

    }
}

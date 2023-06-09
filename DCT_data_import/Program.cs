using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DCT_data_import.ApiObject;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Threading;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Configuration;
using DCT_data_import.ReadAndImport;

namespace DCT_data_import
{
    class Program
    {
        public static string POOL_NAME = "DB_program";
        public static string HOST = "192.168.0.105";
        public static string PORT = "3308";
        public static string USER = "5910";
        public static string PASSWORD = "5910";
        public static string DATABASE = "dct_test";

        public static string FTP_IP = "10.16.92.65";
        public static string FTP_USER = "tid5910";
        public static string FTP_PASSWORD = "5910@tid";


        public static string API_SIGNIN_USER = ConfigurationManager.ConnectionStrings["ApiSignInUser"].ConnectionString;
        public static string API_SIGNIN_PASSWORD = ConfigurationManager.ConnectionStrings["ApiSignInPassword"].ConnectionString;

        //public static string POOL_NAME = ConfigurationManager.ConnectionStrings["PoolName"].ConnectionString;
        //public static string HOST = ConfigurationManager.ConnectionStrings["Host"].ConnectionString;
        //public static string PORT = ConfigurationManager.ConnectionStrings["Port"].ConnectionString;
        //public static string USER = ConfigurationManager.ConnectionStrings["User"].ConnectionString;
        //public static string PASSWORD = ConfigurationManager.ConnectionStrings["Password"].ConnectionString;
        //public static string DATABASE = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;

        //public static string FTP_IP = ConfigurationManager.ConnectionStrings["FtpIp"].ConnectionString;
        //public static string FTP_USER = ConfigurationManager.ConnectionStrings["FtpUser"].ConnectionString;
        //public static string FTP_PASSWORD = ConfigurationManager.ConnectionStrings["FtpPassword"].ConnectionString;

        static void Main(string[] args)
        {
            FileProcess fileAccess = new FileProcess();
            WebApiClient webApiClient = new WebApiClient();
            WriteToLog writeToLog = new WriteToLog();
            int count = 0;

            List<DbKeyObject> dbKeyList = SelectDbKey(webApiClient, "tester");
            List<DbKeyObject> dbKeyUiStatusList = SelectDbKey(webApiClient, "ui_status");

            // 登入取得 token ，即 API key value (token)
            Pool_signin pool_signin = new Pool_signin
            {
                username = API_SIGNIN_USER,
                password = API_SIGNIN_PASSWORD
            };
            //Signin_response signin_response = webApiClient.getApiKeyValueAsync(pool_signin).GetAwaiter().GetResult();

            RawData rawData = new RawData();
            Tester tester = new Tester();
            UiStatus uiStatus = new UiStatus();
            FailPin failPin = new FailPin();

            string updateImportStatus, remark;
            ImportResult importResult1, importResult2, importResult3, importResult4;
            for (int i = 0; i < dbKeyList.Count; i++)
            {
                Console.WriteLine((i + 1).ToString() + ".DB_Key=" + dbKeyList[i].dbKey + "  ");
                importResult1 = (dbKeyList[i].checkStatus >= 4 && dbKeyList[i].checkStatus <= 7) ?
                    tester.readAndImportTesterStatus(fileAccess, webApiClient, dbKeyList[i].dbKey).GetAwaiter().GetResult() :
                    new ImportResult(0, "");
                importResult2 = (dbKeyList[i].checkStatus == 2 || dbKeyList[i].checkStatus == 3 || dbKeyList[i].checkStatus == 6 || dbKeyList[i].checkStatus == 7) ?
                    rawData.readAndImportRawData(fileAccess, webApiClient, dbKeyList[i].dbKey).GetAwaiter().GetResult() :
                    new ImportResult(0, "");
                importResult3 = (dbKeyList[i].checkStatus == 1 || dbKeyList[i].checkStatus == 3 || dbKeyList[i].checkStatus == 5 || dbKeyList[i].checkStatus == 7) ?
                    failPin.readAndImportFailPinLog(fileAccess, webApiClient, dbKeyList[i].dbKey).GetAwaiter().GetResult() :
                    new ImportResult(0, "");

                remark = "";
                remark += (string.IsNullOrEmpty(importResult1.messege)) ? "" : "tester: " + importResult1.messege + ", ";
                remark += (string.IsNullOrEmpty(importResult2.messege)) ? "" : "test result: " + importResult2.messege + ", ";
                remark += (string.IsNullOrEmpty(importResult3.messege)) ? "" : "fail pin: " + importResult3.messege;
                updateImportStatus = UpdateDbKeyImportStatus(webApiClient, dbKeyList[i].dbKey, importResult1.result, importResult2.result, importResult3.result, remark);
                Console.WriteLine("Update result:" + updateImportStatus);
            }

            for (int i = 0; i < dbKeyUiStatusList.Count; i++)
            {
                importResult4 = uiStatus.readAndImportUIStatus(fileAccess, webApiClient, dbKeyUiStatusList[i].dbKey);
                remark = "";
                remark += (string.IsNullOrEmpty(importResult4.messege)) ? "" : "ui status:" + importResult4.messege;
                updateImportStatus = UpdateDbKeyUiStatusImportStatus(webApiClient, dbKeyList[i].dbKey, importResult4.result, remark);
                Console.WriteLine((i + 1).ToString() + ": " + dbKeyList[i] + ", " + updateImportStatus);
            }

            // 找出需要通報的db_key
            List<DbKeyObject> failDbKeyObject = SelectFailDbKeyResult(webApiClient, "tester");
            // 通報
            string sendResult = SendMailModel(failDbKeyObject);
            // 更新寄信狀態
            string updateMailResult = UpdateMail(webApiClient, failDbKeyObject, "tester");


            bool thread1Alive = false, thread2Alive = false, thread3Alive = false, thread4Alive = false;
            Thread thread1 = new Thread(() => readAndImportRawData(fileAccess, webApiClient));
            Thread thread2 = new Thread(() => readAndImportTesterStatus(fileAccess, webApiClient));
            Thread thread3 = new Thread(() => readAndImportUIStatus(fileAccess, webApiClient));
            Thread thread4 = new Thread(() => readAndImportFailPinLog(fileAccess, webApiClient));
            while (true)
            {
                Console.WriteLine("thread1.IsAlive: " + thread1Alive);
                Console.WriteLine("thread2.IsAlive: " + thread2Alive);
                Console.WriteLine("thread3.IsAlive: " + thread3Alive);
                Console.WriteLine("thread4.IsAlive: " + thread4Alive);

                count++;

                bool isConnect = webApiClient.checkDBConnect(POOL_NAME);
                if (!isConnect)
                {
                    // 如果建立pool失敗就中斷程式
                    if (!createPool(webApiClient, writeToLog)) return;
                }

                //if (!thread1Alive)
                //{
                //    thread1.Interrupt();
                //    thread1.Abort();
                //    thread1 = new Thread(() => readAndImportRawData(fileAccess, webApiClient));
                //    thread1.Start();
                //}
                //if (!thread2Alive)
                //{
                //    thread2.Interrupt();
                //    thread2.Abort();
                //    thread2 = new Thread(() => readAndImportTesterStatus(fileAccess, webApiClient));
                //    thread2.Start();
                //}
                //if (!thread3Alive)
                //{
                //    thread3.Interrupt();
                //    thread3.Abort();
                //    thread3 = new Thread(() => uiStatus.readAndImportUIStatus(fileAccess, webApiClient));
                //    thread3.Start();
                //}
                //if (!thread4Alive)
                //{
                //    thread4.Interrupt();
                //    thread4.Abort();
                //    thread4 = new Thread(() => failPin.readAndImportFailPinLog(fileAccess, webApiClient));
                //    thread4.Start();
                //}


                //readAndImportRawData(fileAccess, webApiClient);
                //readAndImportTesterStatus(fileAccess, webApiClient);
                //readAndImportUIStatus(fileAccess, webApiClient);
                //readAndImportFailPinLog(fileAccess, webApiClient);

                //bool IfRunOver = false;
                //while (!IfRunOver)
                //{
                //    bool IfTimesEnd = thread1.IsAlive;

                //    if (!IfTimesEnd || IfRunOver)
                //    {
                //        thread1.Interrupt();
                //        thread1.Abort();
                //        IfTimesEnd = false;
                //        break;
                //    }
                //}

                //callWebApi();
                //ftpReadFiles();
                Thread.Sleep(600000);
                thread1Alive = thread1.IsAlive;
                thread2Alive = thread2.IsAlive;
                thread3Alive = thread3.IsAlive;
                thread4Alive = thread4.IsAlive;
                Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  Loop" + count + " finished~");
            }
            //Console.Read();
        }
        

        static void callWebApi()
        {
            WebApiClient webApiClient = new WebApiClient();

            // 建立新連線
            Pool pool = new Pool
            {
                pool_name = POOL_NAME,
                host = HOST,
                port = PORT,
                user = USER,
                password = PASSWORD,
                database = DATABASE
            };
            try
            {
                var response = webApiClient.CreatePoolAsync(pool).GetAwaiter().GetResult();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return;
            }

            var get_str = webApiClient.GetPoolAsync().GetAwaiter().GetResult();


            Pool_excute pool_excute = new Pool_excute
            {
                pool = POOL_NAME,
                query = "SELECT 1+1;"
            };
            var response2 = webApiClient.ExcutePoolAsync(pool_excute).GetAwaiter().GetResult();


            Pool_delete pool_delete = new Pool_delete
            {
                pool = POOL_NAME
            };
            var response3 = webApiClient.DeletePoolAsync(pool_delete).GetAwaiter().GetResult();


            Console.ReadLine();
        }
        
        static void ftpReadFiles()
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            string names;
            List<string> list_filename;

            ftpserver = "ftp://10.16.92.65/Data_Analysis/Data_Cloud_CSV/";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFTP.Credentials = new NetworkCredential("tid5910", "5910@tid");
            response = (FtpWebResponse)reqFTP.GetResponse();

            responseStream = response.GetResponseStream();
            reader = new StreamReader(responseStream);

            names = reader.ReadToEnd();
            list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (int i = 0; i < list_filename.Count; i++)
            {
                ftpserver = "ftp://10.16.92.65/Data_Analysis/Data_Cloud_CSV/" + list_filename[i];
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
                response = (FtpWebResponse)reqFTP.GetResponse();
                responseStream = response.GetResponseStream();
                reader = new StreamReader(responseStream);
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    Console.WriteLine(line);
                }
            }
        }

        #region read and import
        static void readAndImportRawData(FileProcess fileAccess, WebApiClient webApiClient)
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            string names;
            List<string> list_filename;
            string[] fileNameSplit;
            WriteToLog writeToLog = new WriteToLog();
            CompareTool compareTool = new CompareTool();
            bool compare_result = false;
            CalculateSPC calculateSPC = new CalculateSPC();
            List<StatisticItem> avg_2;
            string downloadStatus, deleteStatus;
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts2 = stopWatch.Elapsed;

            KeyAccess keyAccess = new KeyAccess();

            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();


            ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
            response = (FtpWebResponse)reqFTP.GetResponse();

            responseStream = response.GetResponseStream();
            reader = new StreamReader(responseStream);

            names = reader.ReadToEnd();
            list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            for (int i = list_filename.Count - 1; i >= 0; i--)
            {
                Console.Write("Raw " + (list_filename.Count - i).ToString() + " : ");

                // 確認 pool 連線是否正常
                bool isConnect = webApiClient.checkDBConnect(POOL_NAME);
                if (!isConnect)
                {
                    // 如果建立pool失敗就中斷程式
                    if (!createPool(webApiClient, writeToLog)) return;
                }


                try
                {
                    fileNameSplit = list_filename[i].Split('.');
                    if (fileNameSplit[fileNameSplit.Length - 1] != "csv") continue;
                    bool fileExist = false;//fileAccess.isFileNameExistInDB(fileNameSplit[0], webApiClient);
                    bool import_result = false;
                    bool isDBKeyExist = false;

                    ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/" + list_filename[i];
                    // 取得編碼格式
                    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
                    response = (FtpWebResponse)reqFTP.GetResponse();
                    responseStream = response.GetResponseStream();
                    reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));


                    if (!fileExist)
                    {
                        RawDataContentFormat rawDataContentFormat = fileAccess.FileReadRawData(reader);
                        reader.Close();

                        if (!string.IsNullOrEmpty(rawDataContentFormat.errMsg))
                        {
                            Console.WriteLine(rawDataContentFormat.errMsg);
                            //重新命名檔案
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/test_result_Chinese_" + rawDataContentFormat.lotInfo.Rows[0]["DB_Key"].ToString() + ".csv", FTP_USER, FTP_PASSWORD);

                            //寫入DB_Key
                            string insertDbKeyResult = keyAccess.InsertDbKey("tester", rawDataContentFormat.lotInfo.Rows[0]["DB_Key"].ToString());
                            Console.Write(insertDbKeyResult);
                            Console.Write("\n");
                            continue;
                        }

                        if (rawDataContentFormat == null || rawDataContentFormat.lotInfo.Rows.Count < 1)
                        {
                            continue;
                        }else
                        {
                            //重新命名檔案
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/test_result_" + rawDataContentFormat.lotInfo.Rows[0]["DB_Key"].ToString() + ".csv", FTP_USER, FTP_PASSWORD);
                            //continue;

                            //寫入DB_Key
                            string insertDbKeyResult = keyAccess.InsertDbKey("tester", rawDataContentFormat.lotInfo.Rows[0]["DB_Key"].ToString());
                            Console.Write(insertDbKeyResult);
                            Console.Write("\n");
                            continue;
                        }

                        if (rawDataContentFormat == null || rawDataContentFormat.lotInfo.Rows.Count < 1)
                        {
                            Console.WriteLine("Raw data 讀檔失敗:  " + list_filename[i]);
                            writeToLog.writeToLog("Raw data  讀檔失敗: " + ftpserver);
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            //RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV_/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            Thread.Sleep(500);
                            continue;
                        }
                        if (!rawDataContentFormat.compareInfo())
                        {
                            Console.WriteLine("Raw data 之 information 欄位名稱不符:  " + list_filename[i]);
                            writeToLog.writeToLog("Raw data 之 information 欄位名稱不符:" + ftpserver);
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            //RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV_/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            Thread.Sleep(500);
                            continue;
                        }
                        if (!rawDataContentFormat.compareStatistic())
                        {
                            Console.WriteLine("Raw data 之 statistic 欄位名稱不符:  " + list_filename[i]);
                            writeToLog.writeToLog("Raw data 之 statistic 欄位名稱不符:" + ftpserver);
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            //RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV_/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            Thread.Sleep(500);
                            continue;
                        }
                        //fileAccess.caculatePpk(rawDataContentFormat.lotStatistic);
                        //if (rawDataContentFormat.lotResult.Rows.Count < 1)
                        //{
                        //    Console.WriteLine("Lot Result 無資料");
                        //}

                        isDBKeyExist = fileAccess.isDBKeyExistInDB("lots_info", rawDataContentFormat.lotInfo.Rows[0]["DB_Key"].ToString(), webApiClient);
                        if (isDBKeyExist)
                        {
                            //compare_result = compareTool.compareRawData(rawDataContentFormat, webApiClient);
                            Console.WriteLine("資料庫已存在此資料: Raw data 比對: " + compare_result + "   檔名:" + list_filename[i]);
                            ////writeToLog.writeToLog("資料庫已存在此資料:" + ftpserver);
                            //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);

                            // Kerwin 的電腦
                            if (macid == "94C6913F94BD")
                            {
                                // 下載檔案到本地端
                                downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\raw_data_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            }

                            // 刪除已存在的的CSV檔案
                            deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);

                            Thread.Sleep(500);
                        }
                        else
                        {
                            ////// 計算平方和
                            ////list_square_sum = calculateSPC.SquareSum(rawDataContentFormat);
                            //// 計算均方和
                            //avg_2 = calculateSPC.AverageOfSumSquare(rawDataContentFormat);
                            //fileAccess.addColumnForDataset(rawDataContentFormat.lotStatistic, "avg_2", avg_2);

                            ////if(fileNameSplit[0]=="65109QH7XV01_46SMZMB002_2022_11_17_14_00_50")
                            ////{
                            ////    Console.WriteLine("");
                            ////}

                            //stopWatch.Reset();
                            //stopWatch.Start();
                            //// 開始匯入
                            //import_result = fileAccess.importRawData(rawDataContentFormat, webApiClient);
                            //stopWatch.Stop();
                            //ts2 = stopWatch.Elapsed;

                            ////compare_result = compareTool.compareRawData(rawDataContentFormat, webApiClient);

                            //if (import_result)
                            //{
                            //    //Console.WriteLine("匯入完成! Raw data    比對: " + compare_result + "   檔名:" + list_filename[i]);
                            //    Console.WriteLine("匯入完成! Raw data    檔名:" + list_filename[i]+ "    耗時: " + Convert.ToInt32(ts2.TotalMilliseconds / 1000).ToString() + " 秒");

                            //    // Kerwin 的電腦
                            //    if (macid == "94C6913F94BD")
                            //    {
                            //        // 下載檔案到本地端
                            //        downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\raw_data_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            //    }

                            //    // 刪除已存在的的CSV檔案
                            //    deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                            //}
                            //else
                            //{
                            //    Console.WriteLine("匯入失敗: Raw data " + list_filename[i]);
                            //    writeToLog.writeToLog("匯入失敗:" + ftpserver);
                            //    RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            //}
                        }

                        //reader.Close();
                        response.Close();
                    }
                    else
                    {
                        Console.WriteLine("資料庫已存在此檔名: Raw data " + list_filename[i]);
                        writeToLog.writeToLog("資料庫已存在此檔名:" + ftpserver);

                        //// 下載檔案到本地端
                        //downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\raw_data_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        //// 刪除已存在的的CSV檔案
                        //deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);

                        reader.Close();
                        response.Close();


                        //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);


                    }
                    //string[] allLines = reader.ReadToEnd().Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                }
                catch (Exception ex)
                {
                    //Console.WriteLine(ftpserver + ": " + ex.ToString());
                    //writeToLog.writeToLog(ftpserver + ": " + ex.ToString());
                    RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                }
                Thread.Sleep(500);
            }

            reader.Close();
            response.Close();
            GC.Collect();
            Console.WriteLine("Raw data end~");
        }

        static void readAndImportTesterStatus(FileProcess fileAccess, WebApiClient webApiClient)
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            string names;
            List<string> list_filename;
            string[] fileNameSplit;
            bool isDBKeyExist = false, import_result = false;
            WriteToLog writeToLog = new WriteToLog();
            CompareTool compareTool = new CompareTool();
            bool compare_result = false;
            string downloadStatus, deleteStatus;


            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();


            // Tester Status
            ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Tester_Status/";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
            response = (FtpWebResponse)reqFTP.GetResponse();

            responseStream = response.GetResponseStream();
            reader = new StreamReader(responseStream);

            names = reader.ReadToEnd();
            list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();


            for (int i = list_filename.Count - 1; i >= 0; i--)
            {
                Console.Write("Tester " + (list_filename.Count - i).ToString() + " : ");

                // 確認 pool 連線是否正常
                bool isConnect = webApiClient.checkDBConnect(POOL_NAME);
                if (!isConnect)
                {
                    // 如果建立pool失敗就中斷程式
                    if (!createPool(webApiClient, writeToLog)) return;
                }

                try
                {
                    fileNameSplit = list_filename[i].Split('.');
                    if (fileNameSplit[fileNameSplit.Length - 1] != "csv") continue;


                    ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Tester_Status/" + list_filename[i];
                    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
                    //ftpserver = "ftp://10.16.92.67/Data_Analysis/Tester_Status/" + list_filename[i];
                    //reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    //reqFTP.Credentials = new NetworkCredential("jacky", "jacky");
                    response = (FtpWebResponse)reqFTP.GetResponse();
                    responseStream = response.GetResponseStream();
                    reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));

                    TestStatusContentFormat testStatusContentFormat = fileAccess.FileReadTesterStatus(reader);
                    reader.Close();

                    //重新命名檔案
                    RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status/tester_" + testStatusContentFormat.tester_device_info.Rows[0]["DB_Key"].ToString() + ".csv", FTP_USER, FTP_PASSWORD);
                    continue;

                    if (testStatusContentFormat == null || testStatusContentFormat.tester_device_info.Rows.Count < 1)
                    {
                        Console.WriteLine("Tester Status 讀檔失敗:  " + list_filename[i]);
                        writeToLog.writeToLog("Tester Status  讀檔失敗: " + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        continue;
                    }
                    if (!testStatusContentFormat.compareInfo())
                    {
                        Console.WriteLine("Tester Status 之 information 欄位名稱不符:  " + list_filename[i]);
                        writeToLog.writeToLog("Tester Status 之 information 欄位名稱不符: " + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        Console.Read();
                        return;
                    }
                    if (!testStatusContentFormat.compareStatus())
                    {
                        Console.WriteLine("Tester Status 之 tester_status 欄位名稱不符:  " + list_filename[i]);
                        writeToLog.writeToLog("Tester Status 之 tester_status 欄位名稱不符: " + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        Console.Read();
                        return;
                    }
                    isDBKeyExist = fileAccess.isDBKeyExistInDB("tester_device_info", testStatusContentFormat.tester_device_info.Rows[0]["DB_Key"].ToString(), webApiClient);
                    if (isDBKeyExist)
                    {
                        //compare_result = compareTool.compareTesterStatus(testStatusContentFormat, webApiClient);
                        Console.WriteLine("資料庫已存在此資料: Tester Status     檔名:" + list_filename[i]);
                        //Console.WriteLine("資料庫已存在此資料: Tester Status  比對" + compare_result + "   檔名:" + list_filename[i]);
                        writeToLog.writeToLog("資料庫已存在此資料: " + ftpserver);

                        // Kerwin 的電腦
                        if (macid == "94C6913F94BD")
                        {
                            // 下載檔案到本地端
                            downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\tester_status_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        }
                        // 刪除已存在的的CSV檔案
                        deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                    }
                    else
                    {
                        import_result = fileAccess.importTesterStatus(testStatusContentFormat, webApiClient);
                        //compare_result = compareTool.compareTesterStatus(testStatusContentFormat, webApiClient);
                        if (import_result)
                        {
                            //Console.WriteLine("匯入完成! Tester Status  比對" + compare_result + "   檔名: " + list_filename[i]);
                            Console.WriteLine("匯入完成! Tester Status   檔名: " + list_filename[i]);

                            // Kerwin 的電腦
                            if (macid == "94C6913F94BD")
                            {
                                // 下載檔案到本地端
                                downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\tester_status_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            }

                            // 刪除已存在的的CSV檔案
                            deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                        }
                        else
                        {
                            Console.WriteLine("匯入失敗: Tester Status " + list_filename[i]);
                            writeToLog.writeToLog("匯入失敗: " + ftpserver);
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine(RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD));
                }

                reader.Close();
                response.Close();

                Thread.Sleep(500);
            }

            Console.WriteLine("Tester status end~");
        }

        static void readAndImportUIStatus(FileProcess fileAccess, WebApiClient webApiClient)
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            string names;
            List<string> list_filename;
            string[] fileNameSplit;

            bool isDBKeyExist = false, import_result = false;
            CompareTool compareTool = new CompareTool();
            WriteToLog writeToLog = new WriteToLog();
            string downloadStatus, deleteStatus;

            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();

            // Tester Status
            ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/UI_Status/";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
            response = (FtpWebResponse)reqFTP.GetResponse();

            responseStream = response.GetResponseStream();
            reader = new StreamReader(responseStream);

            names = reader.ReadToEnd();
            list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            for (int i = list_filename.Count - 1; i >= 0; i--)
            {
                Console.Write("UI_status " + (list_filename.Count - i).ToString() + " : ");

                // 確認 pool 連線是否正常
                bool isConnect = webApiClient.checkDBConnect(POOL_NAME);
                if (!isConnect)
                {
                    // 如果建立pool失敗就中斷程式
                    if (!createPool(webApiClient, writeToLog)) return;
                }

                try
                {
                    fileNameSplit = list_filename[i].Split('.');
                    if (fileNameSplit[fileNameSplit.Length - 1] != "csv") continue;

                    import_result = false;

                    ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/UI_Status/" + list_filename[i];
                    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
                    response = (FtpWebResponse)reqFTP.GetResponse();
                    responseStream = response.GetResponseStream();
                    reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));

                    UIStatusContentFormat uiStatusContentFormat = fileAccess.FileReadUIStatus(reader);
                    reader.Close();
                    if (uiStatusContentFormat == null)
                    {
                        Console.WriteLine("UI Status 讀取失敗: " + list_filename[i]);
                        writeToLog.writeToLog("UI Status 讀取失敗:" + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/UI_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        continue;
                    }
                    if (!uiStatusContentFormat.compareUiStatus())
                    {
                        Console.WriteLine("UI Status 之 ui_status 欄位名稱不符: " + list_filename[i]);
                        writeToLog.writeToLog("UI Status 之 ui_status 欄位名稱不符:" + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/UI_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        Console.Read();
                        return;
                    }

                    import_result = fileAccess.importUIStatus(uiStatusContentFormat, webApiClient);
                    if (import_result)
                    {
                        Console.WriteLine("匯入完成! UI Status " + list_filename[i]);
                        // Kerwin 的電腦
                        if (macid == "94C6913F94BD")
                        {
                            // 下載檔案到本地端
                            downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\ui_status_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        }
                        // 刪除已存在的的CSV檔案
                        deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                    }
                    else
                    {
                        Console.WriteLine("匯入失敗: UI Status " + list_filename[i]);
                        writeToLog.writeToLog("匯入失敗:" + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/UI_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                    }

                    Thread.Sleep(500); reader.Close();
                    response.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine("UI status end~");
        }

        static void readAndImportFailPinLog(FileProcess fileAccess, WebApiClient webApiClient)
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            string names;
            List<string> list_filename;
            string[] fileNameSplit;
            bool import_result = false, isDBKeyExist = false;
            WriteToLog writeToLog = new WriteToLog();
            CompareTool compareTool = new CompareTool();
            bool compare_result = false;
            string downloadStatus, deleteStatus;

            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();

            // Tester Status
            ftpserver = "ftp://" + FTP_IP + "/Data_Analysis/Fail_Pin_Log/ST_RT_AT/CSV_Kerwin/";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
            response = (FtpWebResponse)reqFTP.GetResponse();

            responseStream = response.GetResponseStream();
            reader = new StreamReader(responseStream);

            names = reader.ReadToEnd();
            list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            for (int i = list_filename.Count - 1; i >= 0; i--)
            {
                Console.Write("Fail_pin_rate " + (list_filename.Count - i).ToString() + " : ");

                // 確認 pool 連線是否正常
                bool isConnect = webApiClient.checkDBConnect(POOL_NAME);
                if (!isConnect)
                {
                    // 如果建立pool失敗就中斷程式
                    if (!createPool(webApiClient, writeToLog)) return;
                }

                try
                {
                    fileNameSplit = list_filename[i].Split('.');
                    if (fileNameSplit[fileNameSplit.Length - 1] != "csv") continue;

                    ftpserver = "ftp://" + FTP_IP + "/Data_Analysis/Fail_Pin_Log/ST_RT_AT/CSV_Kerwin/" + list_filename[i];
                    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
                    response = (FtpWebResponse)reqFTP.GetResponse();
                    responseStream = response.GetResponseStream();
                    reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));

                    FailPinLogContentFormat failPinLogContent = fileAccess.FileReadFailPinLog(reader);
                    // 讀取失敗或沒有資料
                    if (failPinLogContent == null || failPinLogContent.fail_pin_rate_info.Rows.Count < 1)
                    {
                        Console.WriteLine("Fail Pin Log 讀取失敗:  " + list_filename[i]);
                        writeToLog.writeToLog("Fail Pin Log 讀取失敗: " + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        continue;
                    }
                    reader.Close();
                    if (!failPinLogContent.compareInfo())
                    {
                        Console.WriteLine("Fail Pin Log 之 information 欄位名稱不符:  " + list_filename[i]);
                        writeToLog.writeToLog("Fail Pin Log 之 information 欄位名稱不符: " + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        return;
                    }
                    isDBKeyExist = fileAccess.isDBKeyExistInDB("fail_pin_rate_info", failPinLogContent.fail_pin_rate_info.Rows[0]["DB Key"].ToString(), webApiClient);
                    if (isDBKeyExist)
                    {
                        //compare_result = compareTool.compareFailPinLog(failPinLogContent, webApiClient);
                        Console.WriteLine("資料庫已存在此資料: Fail Pin   檔名:" + list_filename[i]);
                        //Console.WriteLine("資料庫已存在此資料: Fail Pin  " + " 比對: " + compare_result + "   檔名:" + list_filename[i]);
                        //writeToLog.writeToLog("資料庫已存在此資料: " + ftpserver);
                        // Kerwin 的電腦
                        if (macid == "94C6913F94BD")
                        {
                            // 下載檔案到本地端
                            downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\fail_pin_log_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        }
                        // 刪除已存在的的CSV檔案
                        deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                    }
                    else
                    {
                        import_result = fileAccess.importFailPinLog(failPinLogContent, webApiClient);
                        //compare_result = compareTool.compareFailPinLog(failPinLogContent, webApiClient);
                        if (import_result)
                        {
                            //Console.WriteLine("匯入完成! Fail Pin    比對: " + compare_result + "   檔名:" + list_filename[i]);
                            Console.WriteLine("匯入完成! Fail Pin      檔名:" + list_filename[i]);
                            // Kerwin 的電腦
                            if (macid == "94C6913F94BD")
                            {
                                // 下載檔案到本地端
                                downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\fail_pin_log_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            }
                            // 刪除完成的CSV檔案
                            deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                        }
                        else
                        {
                            Console.WriteLine("匯入失敗: Fail Pin " + list_filename[i]);
                            writeToLog.writeToLog("匯入失敗:" + ftpserver);
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        }
                    }

                    Thread.Sleep(500);
                    reader.Close();
                    response.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    writeToLog.writeToLog(ex.ToString());
                }
            }

            Console.WriteLine("Fail pin log end~");
        }
        #endregion read and import

        #region file access
        static string DeleteFile(string fileName, string user, string password)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fileName);
            request.Method = WebRequestMethods.Ftp.DeleteFile;
            request.Credentials = new NetworkCredential(user, password);

            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                return response.StatusDescription;
            }
        }

        static string RenameFile(string fileName, string newFileName, string user, string password)
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

        static string DownloadFile(string fileName, string newFileName, string user, string password)
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
        #endregion file access

        static bool createPool(WebApiClient webApiClient, WriteToLog writeToLog)
        {
            Pool pool = new Pool
            {
                pool_name = POOL_NAME,
                host = HOST,
                port = PORT,
                user = USER,
                password = PASSWORD,
                database = DATABASE
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

        // 檢查 XX 天內是否有資料匯入，若 XX 天內無資料匯入則寄信通知
        static bool CheckAndSendMail(WebApiClient webApiClient)
        {
            WriteToLog writeToLog = new WriteToLog();

            // 改檢查 XX 天內是否有資料匯入


            // 寄信囉~
            string strAppPath = System.Reflection.Assembly.GetExecutingAssembly().Location; //獲得.exe路徑
            string strWorkPath = System.IO.Path.GetDirectoryName(strAppPath);
            ReadWriteINIfile _readWriteINIfile = new ReadWriteINIfile(strWorkPath + "\\dct_import_mail_list.ini");

            EmailModels Email_class = new EmailModels();
            Email_class.subject = "DCT import check";
            Email_class.body = "Dear all,<br>" +
                "此為確認信<br>" ;

            // 換行<br>
            // 空白&nbsp
            List<string> to_name_list = _readWriteINIfile.ReadINI("mail_list", "mail_to").Split(',').ToList();
            Email_class.tomanlist = to_name_list;

            List<string> cc_name_list = _readWriteINIfile.ReadINI("mail_list", "mail_cc").Split(',').ToList();
            Email_class.cclist = cc_name_list;

            List<string> bcc_name_list = _readWriteINIfile.ReadINI("mail_list", "mail_bcc").Split(',').ToList();
            Email_class.bcclist = bcc_name_list;

            List<string> filelist = new List<string>();

            if (Email_class.SendEmail())
            {
                writeToLog.writeToLog("寄信成功!");
            }
            else
            {
                writeToLog.writeToLog("寄信失敗!");
            }

            return true;
        }

        static List<DbKeyObject> SelectDbKey(WebApiClient webApiClient, string mode="")
        {
            List<DbKeyObject> dbKeyList = new List<DbKeyObject>();
            WriteToLog writeToLog = new WriteToLog();
            string sql = "";

            if (mode == "tester")
            {
                sql = "SELECT id, db_key, check_status FROM `db_key` WHERE `check_status`>0 AND `import_status` =0 AND mail=0;";
            }
            else if (mode == "ui_status")
            {
                sql = "SELECT id, db_key, check_status FROM `db_key_ui_status` WHERE  `check_status`>0 AND `import_status` =0 AND mail=0;";
            }

            try
            {
                // 宣告 Web API body
                Pool_excute pool_excute = new Pool_excute
                {
                    pool = POOL_NAME,
                    query = sql
                };
                // 回傳 {"data":{"fieldCount":0,"affectedRows":1,"insertId":1,"info":"","serverStatus":2,"warningStatus":0},"error":null}
                Pool_excute_response response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.error))
                {
                    writeToLog.writeToLog("SELECT `db_key` error! ");
                }

                for (int i = 0; i < response.data.Count; i++)
                {
                    //Console.WriteLine(response.data[i]["id"].ToString() + ", "+response.data[i]["db_key"].ToString());
                    dbKeyList.Add(new DbKeyObject(int.Parse(response.data[i]["id"].ToString()), response.data[i]["db_key"].ToString(), int.Parse(response.data[i]["check_status"].ToString())));
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return new List<DbKeyObject>();
            }

            return dbKeyList;
        }

        static string UpdateDbKeyImportStatus(WebApiClient webApiClient, string dbKey, int tester, int testResult, int failPin, string remark)
        {
            WriteToLog writeToLog = new WriteToLog();
            int importResult = 4 * tester + 2 * testResult + failPin;
            string id, checkStatus, importStatus="1", mail="0";
            
            try
            {
                // 先select 出check status 比對確認結果
                Pool_excute pool_excute = new Pool_excute
                {
                    pool = POOL_NAME,
                    query = "SELECT id, check_status FROM db_key WHERE db_key='" + dbKey + "';"
                };
                Pool_excute_response response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.error))
                {
                    writeToLog.writeToLog("SELECT `db_key` error! ");
                    return "Fail. Execution 'select' error: " + response.error;
                }
                if(response.data.Count >0)
                {
                    id = response.data[0]["id"].ToString();
                    checkStatus = response.data[0]["check_status"].ToString();
                }
                else
                {
                    return "Fail. There is no infomation which db_key is '" + dbKey + "'";
                }

                // 檢查確認碼
                importStatus = (importResult.ToString() == checkStatus) ? "1" : "2";

                // 更新 import check 相關資訊
                pool_excute = new Pool_excute
                {
                    pool = POOL_NAME,
                    query = "UPDATE db_key " +
                    "SET tester="+ tester .ToString()+ ",test_result="+ testResult .ToString()+ ",fail_pin="+ failPin.ToString()+"," +
                    "import_status="+ importStatus+",mail="+ mail+",remark='"+ remark+"' " +
                    "WHERE `db_key`='"+dbKey+"';"
                };
                response = webApiClient.ExcutePoolAsync(pool_excute, "update").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.error))
                {
                    writeToLog.writeToLog("UPDATE `db_key` error! ");
                    return "Fail. Execution 'update' error: " + response.error;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return "Fail. Exception error";
            }
            

            return "OK";
        }

        static string UpdateDbKeyUiStatusImportStatus(WebApiClient webApiClient, string dbKey, int uiStatus, string remark)
        {
            WriteToLog writeToLog = new WriteToLog();
            int importResult = uiStatus;
            string id, checkStatus, importStatus = "1", mail = "0";

            try
            {
                // 先select 出check status 比對確認結果
                Pool_excute pool_excute = new Pool_excute
                {
                    pool = POOL_NAME,
                    query = "SELECT id, check_status FROM db_key_ui_status WHERE db_key='" + dbKey + "';"
                };
                Pool_excute_response response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.error))
                {
                    writeToLog.writeToLog("SELECT `db_key` error! ");
                    return "Fail. Execution 'select' error: " + response.error;
                }
                if (response.data.Count > 0)
                {
                    id = response.data[0]["id"].ToString();
                    checkStatus = response.data[0]["check_status"].ToString();
                }
                else
                {
                    return "Fail. There is no infomation which db_key is '" + dbKey + "'";
                }

                // 檢查確認碼
                importStatus = (importResult.ToString() == checkStatus) ? "1" : "0";

                // 更新 import check 相關資訊
                pool_excute = new Pool_excute
                {
                    pool = POOL_NAME,
                    query = "UPDATE db_key_ui_status " +
                    "SET ui_status='" + uiStatus.ToString() +
                    "import_status='" + importStatus + "',mail='" + mail + "',remark='" + remark + "' " +
                    "WHERE `db_key`='" + dbKey + "';"
                };
                response = webApiClient.ExcutePoolAsync(pool_excute, "update").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.error))
                {
                    writeToLog.writeToLog("UPDATE `db_key` error! ");
                    return "Fail. Execution 'update' error: " + response.error;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return "Fail. Exception error";
            }


            return "OK";
        }

        static List<DbKeyObject> SelectFailDbKeyResult(WebApiClient webApiClient, string mode = "")
        {
            List<DbKeyObject> dbKeyObject = new List<DbKeyObject>();
            WriteToLog writeToLog = new WriteToLog();
            string sql="", remark="";
            long nowTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            //long threeHourAgoTimeStamp = nowTimeStamp - 10800;  // 3小時=10800秒
            long threeHourAgoTimeStamp = nowTimeStamp + 10800;  // 3小時=10800秒

            if (mode == "tester")
            {
                sql = @"SELECT id, db_key, check_status, remark FROM `db_key` WHERE `mail`=0 AND `import_status`=0 AND datetime <= " + threeHourAgoTimeStamp +
                          @" union ALL 
                                SELECT id, db_key, check_status, remark FROM `db_key` WHERE `mail`= 0 AND `import_status`>=2 AND datetime <= " + threeHourAgoTimeStamp;
            }
            else if (mode == "ui_status")
            {
                sql = @"SELECT id, db_key, check_status, remark FROM `db_key_ui_status` WHERE `mail`=0 AND `import_status`=0 AND datetime <= " + threeHourAgoTimeStamp +
                          @" union ALL 
                                SELECT id, db_key, check_status, remark FROM `db_key_ui_status` WHERE `mail`= 0 AND `import_status` >=2 AND datetime <= " + threeHourAgoTimeStamp;
            }

            try
            {
                Pool_excute pool_excute = new Pool_excute
                {
                    pool = POOL_NAME,
                    query = sql
                };
                Pool_excute_response response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.error))
                {
                    writeToLog.writeToLog("SELECT `db_key` error! ");
                }

                for (int i = 0; i < response.data.Count; i++)
                {
                    //Console.WriteLine(response.data[i]["db_key"].ToString());
                    remark = (response.data[i]["check_status"].ToString()=="0") ?"未更新check status":response.data[i]["remark"].ToString();
                    dbKeyObject.Add(new DbKeyObject(int.Parse(response.data[i]["id"].ToString()),response.data[i]["db_key"].ToString(), remark));
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return dbKeyObject;
            }

            return dbKeyObject;
        }

        static string UpdateMail(WebApiClient webApiClient, List<DbKeyObject> dbKeyObject, string mode = "")
        {
            WriteToLog writeToLog = new WriteToLog();
            string sql = "";
            long nowTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            long threeHourAgoTimeStamp = nowTimeStamp - 10800;  // 3小時=10800秒


            try
            {
                foreach (DbKeyObject item in dbKeyObject)
                {
                    if (mode == "tester")
                    {
                        sql = "UPDATE db_key " +
                            "SET mail=1 " +
                            "WHERE `id`='" + item.id + "';";
                    }
                    else if (mode == "ui_status")
                    {
                        sql = "UPDATE db_key_ui_status " +
                            "SET mail=1 " +
                            "WHERE `id`='" + item.id + "';";
                    }

                    Pool_excute pool_excute = new Pool_excute
                    {
                        pool = Program.POOL_NAME,
                        query = sql
                    };
                    Pool_excute_response response = webApiClient.ExcutePoolAsync(pool_excute, "update").GetAwaiter().GetResult();
                    if (!string.IsNullOrEmpty(response.error))
                    {
                        writeToLog.writeToLog("UPDATE `db_key` error!  id="+ item.id+",  db_key="+ item.dbKey);
                        return "Fail." + response.error;
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return "Fail." + ex.ToString();
            }

            return "OK.";

        }

        static string SendMailModel(List<DbKeyObject> dbKeyList)
        {
            WriteToLog writeToLog = new WriteToLog();
            

            // 寄信囉~
            string strAppPath = System.Reflection.Assembly.GetExecutingAssembly().Location; //獲得.exe路徑
            string strWorkPath = System.IO.Path.GetDirectoryName(strAppPath);
            ReadWriteINIfile _readWriteINIfile = new ReadWriteINIfile(strWorkPath + "\\dct_import_mail_list.ini");

            EmailModels Email_class = new EmailModels();
            Email_class.subject = "DCT data notification";
            Email_class.body = "Dear all,<br>" +
                "下列資料發生異常，請確認檔案內容<br>";

            for(int i=0;i< dbKeyList.Count;i++)
            {
                Email_class.body += (i+1).ToString()+".    DB_Key:" + dbKeyList[i].dbKey+ ",   <b>" + dbKeyList[i].remark + "</b><br>";
            }

            Email_class.body += "" +
                "Thanks. <br>";

            // 換行<br>
            // 空白&nbsp
            List<string> to_name_list = _readWriteINIfile.ReadINI("mail_list", "mail_to").Split(',').ToList();
            Email_class.tomanlist = to_name_list;

            List<string> cc_name_list = _readWriteINIfile.ReadINI("mail_list", "mail_cc").Split(',').ToList();
            Email_class.cclist = cc_name_list;

            List<string> bcc_name_list = _readWriteINIfile.ReadINI("mail_list", "mail_bcc").Split(',').ToList();
            Email_class.bcclist = bcc_name_list;

            List<string> filelist = new List<string>();

            if (Email_class.SendEmail())
            {
                writeToLog.writeToLog("寄信成功!");
            }
            else
            {
                writeToLog.writeToLog("寄信失敗!");
            }
            

            return "Send mail successful.";
        }

    }
}

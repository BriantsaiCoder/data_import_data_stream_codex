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
using System.Reflection;

namespace DCT_data_import
{
    class Program
    {
        //public static string POOL_NAME = "DB_program";
        //public static string HOST = "192.168.0.105";
        //public static string PORT = "3308";
        //public static string USER = "5910";
        //public static string PASSWORD = "5910";
        //public static string DATABASE = "dct_test";

        //public static string FTP_IP = "10.16.92.65";
        //public static string FTP_USER = "tid5910";
        //public static string FTP_PASSWORD = "5910@tid";

        public static string POOL_NAME = ConfigurationManager.ConnectionStrings["PoolName"].ConnectionString;
        public static string HOST = ConfigurationManager.ConnectionStrings["Host"].ConnectionString;
        public static string PORT = ConfigurationManager.ConnectionStrings["Port"].ConnectionString;
        public static string USER = ConfigurationManager.ConnectionStrings["User"].ConnectionString;
        public static string PASSWORD = ConfigurationManager.ConnectionStrings["Password"].ConnectionString;
        public static string DATABASE = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;

        public static string FTP_IP = ConfigurationManager.ConnectionStrings["FtpIp"].ConnectionString;
        public static string FTP_USER = ConfigurationManager.ConnectionStrings["FtpUser"].ConnectionString;
        public static string FTP_PASSWORD = ConfigurationManager.ConnectionStrings["FtpPassword"].ConnectionString;

        static void Main(string[] args)
        {
            FileProcess fileAccess = new FileProcess();
            WebApiClient webApiClient = new WebApiClient();
            WriteToLog writeToLog = new WriteToLog();
            DbAccess dbAccess = new DbAccess();
            int count = 0;


#if true /// ture: 有DB Key檢查; false: 沒有DB Key檢查
            bool threadTesterAlive = false, threadUiStatusAlive = false, threadTsmcAlive = false;
            Thread threadTesterMode = new Thread(() => ImportTesterMode(fileAccess, dbAccess, webApiClient));
            Thread threadUiStatusMode = new Thread(() => ImportUiStatusMode(fileAccess, dbAccess, webApiClient));
            Thread threadTsmcMode = new Thread(() => ImportTsmcMode(fileAccess, dbAccess, webApiClient));

                        while (true)
            {
                bool isConnect = webApiClient.checkDBConnect(POOL_NAME);
                if (!isConnect)
                {
                    // 如果建立pool失敗就中斷程式
                    if (!createPool(webApiClient, writeToLog)) return;
                }
                

                Console.WriteLine("threadTesterAlive.IsAlive: " + threadTesterAlive);
                Console.WriteLine("threadUiStatusAlive.IsAlive: " + threadUiStatusAlive);
                Console.WriteLine("threadTsmcAlive.IsAlive: " + threadTsmcAlive);

                if (!threadTesterAlive)
                {
                    threadTesterMode.Interrupt();
                    threadTesterMode.Abort();
                    threadTesterMode = new Thread(() => ImportTesterMode(fileAccess, dbAccess, webApiClient));
                    threadTesterMode.Start();
                }
                if (!threadUiStatusAlive)
                {
                    threadUiStatusMode.Interrupt();
                    threadUiStatusMode.Abort();
                    threadUiStatusMode = new Thread(() => ImportUiStatusMode(fileAccess, dbAccess, webApiClient));
                    threadUiStatusMode.Start();
                }
                if (!threadTsmcAlive)
                {
                    threadTsmcMode.Interrupt();
                    threadTsmcMode.Abort();
                    threadTsmcMode = new Thread(() => ImportTsmcMode(fileAccess, dbAccess, webApiClient));
                    threadTsmcMode.Start();
                }

            #region 固定時間通報程式還活著
                DateTime nowTime = DateTime.Now;
                if ((int)nowTime.DayOfWeek == 1 && nowTime.Hour == 8 && nowTime.Minute < 10)
                {
                    string mailBody = "Dear all,<br>DCT資料庫匯入程式正常執行中!<br>Thanks. <br>";
                    string mailTitle = "DCT data notification - 正常運行中";
                    string sendResult = SendMailModel(mailBody, mailTitle);
                }
            #endregion 固定時間通報程式還活著

                Thread.Sleep(600000); // 600秒執行一次
                threadTesterAlive = threadTesterMode.IsAlive;
                threadUiStatusAlive = threadUiStatusMode.IsAlive;
                threadTsmcAlive = threadTsmcMode.IsAlive;
                Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  Loop" + (++count) + " finished~");
            }
#else
            bool thread1Alive = false, thread2Alive = false, thread3Alive = false, thread4Alive = false, thread5Alive = false;
            Thread thread1 = new Thread(() => readAndImportRawData(fileAccess, webApiClient));
            Thread thread2 = new Thread(() => readAndImportTesterStatus(fileAccess, webApiClient));
            Thread thread3 = new Thread(() => readAndImportUIStatus(fileAccess, webApiClient));
            Thread thread4 = new Thread(() => readAndImportFailPinLog(fileAccess, webApiClient));
            Thread thread5 = new Thread(() => ImportTsmcMode(fileAccess, dbAccess, webApiClient));

            while (true)
            {
                Console.WriteLine("thread1.IsAlive: " + thread1Alive);
                Console.WriteLine("thread2.IsAlive: " + thread2Alive);
                Console.WriteLine("thread3.IsAlive: " + thread3Alive);
                Console.WriteLine("thread4.IsAlive: " + thread4Alive);
                Console.WriteLine("thread5.IsAlive: " + thread5Alive);

                count++;

                bool isConnect = webApiClient.checkDBConnect(POOL_NAME);
                if (!isConnect)
                {
                    // 如果建立pool失敗就中斷程式
                    if (!createPool(webApiClient, writeToLog)) return;
                }

                if (!thread1Alive)
                {
                    thread1.Interrupt();
                    thread1.Abort();
                    thread1 = new Thread(() => readAndImportRawData(fileAccess, webApiClient));
                    thread1.Start();
                }
                if (!thread2Alive)
                {
                    thread2.Interrupt();
                    thread2.Abort();
                    thread2 = new Thread(() => readAndImportTesterStatus(fileAccess, webApiClient));
                    thread2.Start();
                }
                if (!thread3Alive)
                {
                    thread3.Interrupt();
                    thread3.Abort();
                    thread3 = new Thread(() => readAndImportUIStatus(fileAccess, webApiClient));
                    thread3.Start();
                }
                if (!thread4Alive)
                {
                    thread4.Interrupt();
                    thread4.Abort();
                    thread4 = new Thread(() => readAndImportFailPinLog(fileAccess, webApiClient));
                    thread4.Start();
                }
                if (!thread5Alive)
                {
                    thread5.Interrupt();
                    thread5.Abort();
                    thread5 = new Thread(() => ImportTsmcMode(fileAccess, dbAccess, webApiClient));
                    thread5.Start();
                }

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
                thread5Alive = thread5.IsAlive;
                Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  Loop" + count + " finished~");
            }
#endif



            //Console.Read();
        }
        

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
        

        static string SendMailModel(string mailBody, string mailTitle= "DCT data notification")
        {
            WriteToLog writeToLog = new WriteToLog();
            

            // 寄信囉~
            string strAppPath = System.Reflection.Assembly.GetExecutingAssembly().Location; //獲得.exe路徑
            string strWorkPath = System.IO.Path.GetDirectoryName(strAppPath);
            ReadWriteINIfile _readWriteINIfile = new ReadWriteINIfile(strWorkPath + "\\dct_import_mail_list.ini");

            EmailModels Email_class = new EmailModels();
            Email_class.subject = mailTitle;
            Email_class.body = mailBody;

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
                return "OK";
            }
            else
            {
                writeToLog.writeToLog("寄信失敗!");
                return "FAIL";
            }
            
        }


        static string ImportTesterMode(FileProcess fileAccess, DbAccess dbAccess, WebApiClient webApiClient)
        {
            #region 檢查一天內是否有資料
            int dataCount = dbAccess.SelectDataCountInDays(webApiClient, 1, "tester");
            if (dataCount == 0)
                if (DateTime.Now.TimeOfDay.Hours == 8)
                {
                    if (DateTime.Now.TimeOfDay.Minutes >= 0 && DateTime.Now.TimeOfDay.Minutes < 10)
                    {
                        SendMailModel("Dear all,<br><br>Tester 已超過1天無資料匯入，請確認!<br><br>Thanks.");
                    }
                }
            #endregion

            List<DbKeyObject> dbKeyList = dbAccess.SelectDbKey(webApiClient, "tester");

            RawData rawData = new RawData();
            Tester tester = new Tester();
            FailPin failPin = new FailPin();

            string updateImportStatus, remark;
            ImportResult importResult1, importResult2, importResult3;

            for (int i = 0; i < dbKeyList.Count; i++)
            {
                Console.WriteLine((i + 1).ToString() + ".DB_Key=" + dbKeyList[i].dbKey + "  ");
                if (dbKeyList[i].checkStatus == 2 || dbKeyList[i].checkStatus == 3 || dbKeyList[i].checkStatus == 6 || dbKeyList[i].checkStatus == 7)
                {
                    if (dbKeyList[i].testResult == 0)
                    {
                        importResult2 = rawData.readAndImportRawData(fileAccess, webApiClient, dbKeyList[i].dbKey).GetAwaiter().GetResult();
                    }
                    else
                    {
                        importResult2 = new ImportResult(dbKeyList[i].testResult, "");
                    }
                }
                else
                {
                    importResult2 = new ImportResult(0, "");
                }
                if (dbKeyList[i].checkStatus >= 4 && dbKeyList[i].checkStatus <= 7 && dbKeyList[i].tester == 0)
                {
                    importResult1 = tester.readAndImportTesterStatus(fileAccess, webApiClient, dbKeyList[i].dbKey).GetAwaiter().GetResult();
                }
                else
                {
                    importResult1 = new ImportResult(dbKeyList[i].tester, "");
                }
            
                if (dbKeyList[i].checkStatus == 1 || dbKeyList[i].checkStatus == 3 || dbKeyList[i].checkStatus == 5 || dbKeyList[i].checkStatus == 7)
                {
                    if (dbKeyList[i].failPin == 0)
                    {
                        importResult3 = failPin.readAndImportFailPinLog(fileAccess, webApiClient, dbKeyList[i].dbKey).GetAwaiter().GetResult();
                    }else
                    {
                        importResult3 = new ImportResult(dbKeyList[i].failPin, "");
                    }
                }
                else
                {
                    importResult3 = new ImportResult(0, "");
                }

                remark = "";
                remark += (string.IsNullOrEmpty(importResult1.messege)) ? "" : "tester: " + importResult1.messege + "  ";
                remark += (string.IsNullOrEmpty(importResult2.messege)) ? "" : "test result: " + importResult2.messege + "  ";
                remark += (string.IsNullOrEmpty(importResult3.messege)) ? "" : "fail pin: " + importResult3.messege;
                updateImportStatus = dbAccess.UpdateDbKeyImportStatus(webApiClient, dbKeyList[i].dbKey, importResult1.result, importResult2.result, importResult3.result, remark);
                Console.WriteLine("Update tester import status:" + updateImportStatus);
            }

            Console.WriteLine("Tester mode end~");

            #region 寄信通報
            if ((int)DateTime.Now.DayOfWeek%2 ==0 &&/*DateTime.Now.TimeOfDay.Hours == 0 ||*/ DateTime.Now.TimeOfDay.Hours == 12)
            {
                if (DateTime.Now.TimeOfDay.Minutes >= 0 && DateTime.Now.TimeOfDay.Minutes < 10)
                {
                    #region 找出需要通報的db_key
                    //List<DbKeyObject> failDbKeyObject = dbAccess.SelectFailDbKeyResult(webApiClient, "tester");

                    //List<DbKeyObject> failDbKeyUiStatusObject = dbAccess.SelectFailDbKeyResult(webApiClient, "ui_status");
                    

                    //// 通報
                    //if (failDbKeyObject.Count > 0 || failDbKeyUiStatusObject.Count > 0)
                    //{
                    //    string mailBody = "Dear all,<br>下列資料發生異常，請確認檔案內容<br>";
                    //    for (int i = 0; i < failDbKeyUiStatusObject.Count; i++)
                    //    {
                    //        mailBody += (i + 1).ToString() + ".    DB_Key:" + failDbKeyUiStatusObject[i].dbKey + ",   <b>" + failDbKeyUiStatusObject[i].remark + "</b><br>";
                    //    }
                    //    for (int i = 0; i < failDbKeyObject.Count; i++)
                    //    {
                    //        mailBody += (failDbKeyUiStatusObject.Count + i + 1).ToString() + ".    DB_Key:" + failDbKeyObject[i].dbKey + ",   <b>" + failDbKeyObject[i].remark + "</b><br>";
                    //    }
                    //    mailBody += "Thanks. <br>";
                    //    string sendResult = SendMailModel(mailBody);
                    //    if (sendResult == "OK")
                    //    {
                    //        // 更新寄信狀態
                    //        string updateMailResult = dbAccess.UpdateMail(webApiClient, failDbKeyObject, "tester");
                    //        string updateMailResult2 = dbAccess.UpdateMail(webApiClient, failDbKeyUiStatusObject, "ui_status");
                    //    }
                    //}


                    List<DbKeyObject> failDbKeyFromFile = dbAccess.SelectFailDbKeyFromFile();
                    // 通報
                    if (failDbKeyFromFile.Count > 0)
                    {
                        string mailBody = "Dear all,<br>下列資料發生異常，請確認檔案內容<br>";
                        for (int i = 0; i < failDbKeyFromFile.Count; i++)
                        {
                            mailBody += (i + 1).ToString() + ".    DB_Key:" + failDbKeyFromFile[i].dbKey + ",   <b>" + failDbKeyFromFile[i].remark + "</b><br>";
                        }
                        mailBody += "Thanks. <br>";
                        string sendResult = SendMailModel(mailBody);
                        if (sendResult == "OK")
                        {
                            // 刪除信件暫存檔
                            string log_path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + @"\mail_temp.txt").LocalPath;
                            File.Delete(log_path);
                        }
                    }
                    
                    #endregion 找出需要通報的db_key
                }
            }
            #endregion

            return "";
        }

        static string ImportUiStatusMode(FileProcess fileAccess, DbAccess dbAccess, WebApiClient webApiClient)
        {
            #region 檢查一天內是否有資料
            //int dataCount = dbAccess.SelectDataCountInDays(webApiClient, 1, "ui_status");
            //if (dataCount == 0)
            //{
            //    if (DateTime.Now.TimeOfDay.Hours ==8)
            //    {
            //        if (DateTime.Now.TimeOfDay.Minutes >= 0 && DateTime.Now.TimeOfDay.Minutes < 10)
            //        {
            //            SendMailModel("Dear all,<br><br>ui_status 已超過1天無資料匯入，請確認!<br><br>Thanks.");
            //        }
            //    }
            //}
            #endregion
            
            List<DbKeyObject> dbKeyUiStatusList = dbAccess.SelectDbKey(webApiClient, "ui_status");
            UiStatus uiStatus = new UiStatus();

            string updateImportStatus, remark;
            ImportResult importResult4;
            for (int i = 0; i < dbKeyUiStatusList.Count; i++)
            {
                Console.WriteLine((i + 1).ToString() + ".DB_Key_ui_status=" + dbKeyUiStatusList[i].dbKey + "  ");
                importResult4 = uiStatus.readAndImportUIStatus(fileAccess, webApiClient, dbKeyUiStatusList[i].dbKey);
                remark = "";
                remark += (string.IsNullOrEmpty(importResult4.messege)) ? "" : "ui status:" + importResult4.messege;
                updateImportStatus = dbAccess.UpdateDbKeyUiStatusImportStatus(webApiClient, dbKeyUiStatusList[i].dbKey, importResult4.result, remark);
                Console.WriteLine("Update ui_status import status:" + updateImportStatus);
            }

            Console.WriteLine("ui_status mode end~");

            #region 寄信通報
            //if (DateTime.Now.TimeOfDay.Hours == 0 || DateTime.Now.TimeOfDay.Hours == 12)
            //{
            //    if (DateTime.Now.TimeOfDay.Minutes >= 0 && DateTime.Now.TimeOfDay.Minutes < 10)
            //    {
            //        #region 找出需要通報的db_key_ui_status
            //        List<DbKeyObject> failDbKeyObject = dbAccess.SelectFailDbKeyResult(webApiClient, "ui_status");
            //        // 通報
            //        if (failDbKeyObject.Count > 0)
            //        {
            //            string mailBody = "Dear all,<br>下列 ui_status 資料發生異常，請確認檔案內容<br>";
            //            for (int i = 0; i < failDbKeyObject.Count; i++)
            //            {
            //                mailBody += (i + 1).ToString() + ".    DB_Key:" + failDbKeyObject[i].dbKey + ",   <b>" + failDbKeyObject[i].remark + "</b><br>";
            //            }
            //            mailBody += "Thanks. <br>";
            //            string sendResult = SendMailModel(mailBody);
            //            if (sendResult == "OK")
            //            {
            //                // 更新寄信狀態
            //                string updateMailResult = dbAccess.UpdateMail(webApiClient, failDbKeyObject, "ui_status");
            //            }
            //        }
            //        #endregion 找出需要通報的db_key
            //    }
            //}
            #endregion

            return "";
        }


        static string ImportTsmcMode(FileProcess fileAccess, DbAccess dbAccess, WebApiClient webApiClient)
        {
            ImportResult importResult;
            TsmcIeda tsmcIeda = new TsmcIeda();
            
            importResult = tsmcIeda.readAndImportIeda(fileAccess, webApiClient, "");

            return "";
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

            //抓mac id
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string macid = nics[0].GetPhysicalAddress().ToString();


            //ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/";
            ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
            response = (FtpWebResponse)reqFTP.GetResponse();

            responseStream = response.GetResponseStream();
            reader = new StreamReader(responseStream);

            names = reader.ReadToEnd();
            list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            int countRepeatFileCompareTrue = 0;
            int countRepeatFileCompareFalse = 0;

            for (int i = list_filename.Count - 1; i >= 0; i--)
            {
                //if (list_filename[i] != "test_result_ASEF1-5070-B68-172.22.105.28_TMQJ89A-004C1L1T2DSANAAN-S-Fixed_20240203-103907.csv") continue;
                //if (!list_filename[i].Contains("TMQ")) continue;
                //if (!list_filename[i].Contains("Mustang")) continue;

                string[] fnSplit = list_filename[i].Split('_');
                string date1 = fnSplit[fnSplit.Length - 1].Split('-')[0];
                int outInt = 0;
                int.TryParse(date1, out outInt);
                //if (outInt < 20240401 || outInt > 20240430) continue;

                Console.Write("Raw " + (list_filename.Count - i).ToString() + " : ");

                // 確認 pool 連線是否正常
                bool isConnect = webApiClient.checkDBConnect(POOL_NAME);
                if (!isConnect)
                {
                    // 如果建立pool失敗就中斷程式
                    if (!createPool(webApiClient, writeToLog)) return;
                }

                //if (list_filename.Count - i == 100)
                //{
                //    reader.Close();
                //    response.Close();
                //    GC.Collect();
                //    Console.WriteLine("Raw休息一下");
                //    return;
                //}

                try
                {
                    fileNameSplit = list_filename[i].Split('.');
                    if (fileNameSplit[fileNameSplit.Length - 1] != "csv") continue;
                    bool fileExist = false;//fileAccess.isFileNameExistInDB(fileNameSplit[0], webApiClient);
                    bool import_result = false;
                    bool isDBKeyExist = false;

                    //string[] str_split = list_filename[i].Split('_');
                    //string str_datetime = str_split[str_split.Length - 1].Split('-')[0];
                    //DateTime fileTime = DateTime.Parse(str_datetime.Substring(0,4)+"-"+ str_datetime.Substring(4,2) + "-"+ str_datetime.Substring(6,2));

                    //int countDaysAgo = (DateTime.Now - fileTime).Days;
                    //if (countDaysAgo > 60) continue;

                    //ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i];
                    ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/" + list_filename[i];
                    // 取得編碼格式
                    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
                    response = (FtpWebResponse)reqFTP.GetResponse();
                    responseStream = response.GetResponseStream();
                    reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));

                    //if (list_filename[i]=="test_result_ASEF1-5070-B68-172.22.105.28_TMQJ89A-004C1L1T2DSANAAN-S-Fixed_20240205-152653.csv")
                    //{
                    //    int a = 0;
                    //}

                    if (!fileExist)
                    {
                        RawDataContentFormat rawDataContentFormat = fileAccess.FileReadRawData(reader);
                        reader.Close();

                        if (rawDataContentFormat == null || rawDataContentFormat.lotInfo.Rows.Count < 1)
                        {
                            Console.WriteLine("Raw data 讀檔失敗:  " + list_filename[i]);
                            writeToLog.writeToLog("Raw data  讀檔失敗: " + ftpserver);
                            //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            Thread.Sleep(500);
                            continue;
                        }
                        if (!rawDataContentFormat.compareInfo())
                        {
                            Console.WriteLine("Raw data 之 information 欄位名稱不符:  " + list_filename[i]);
                            writeToLog.writeToLog("Raw data 之 information 欄位名稱不符:" + ftpserver);
                            //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            Thread.Sleep(500);
                            continue;
                        }
                        if (!rawDataContentFormat.compareStatistic())
                        {
                            Console.WriteLine("Raw data 之 statistic 欄位名稱不符:  " + list_filename[i]);
                            writeToLog.writeToLog("Raw data 之 statistic 欄位名稱不符:" + ftpserver);
                            //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV/" + list_filename[i], FTP_USER, FTP_PASSWORD);
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
                            //////writeToLog.writeToLog("資料庫已存在此資料:" + ftpserver);
                            //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);

                            //if(compare_result)
                            //{
                            //    countRepeatFileCompareTrue++;
                            //    deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                            //}
                            //else
                            //{
                            //    countRepeatFileCompareFalse++;
                            //}

                            // Kerwin 的電腦
                            if (macid == "94C6913F94BD")
                            {
                                // 下載檔案到本地端
                                //downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\raw_data_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            }

                            //// 刪除已存在的的CSV檔案
                            //deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);

                            Thread.Sleep(500);
                        }
                        else
                        {
                            //// 計算平方和
                            //list_square_sum = calculateSPC.SquareSum(rawDataContentFormat);
                            // 計算均方和
                            avg_2 = calculateSPC.AverageOfSumSquare(rawDataContentFormat);
                            fileAccess.addColumnForDataset(rawDataContentFormat.lotStatistic, "avg_2", avg_2);

                            //if(fileNameSplit[0]=="65109QH7XV01_46SMZMB002_2022_11_17_14_00_50")
                            //{
                            //    Console.WriteLine("");
                            //}

                            stopWatch.Reset();
                            stopWatch.Start();
                            // 開始匯入
                            import_result = fileAccess.importRawData(rawDataContentFormat, webApiClient);
                            stopWatch.Stop();
                            ts2 = stopWatch.Elapsed;

                            //compare_result = compareTool.compareRawData(rawDataContentFormat, webApiClient);

                            if (import_result)
                            {
                                //Console.WriteLine("匯入完成! Raw data    比對: " + compare_result + "   檔名:" + list_filename[i]);
                                Console.WriteLine("匯入完成! Raw data    檔名:" + list_filename[i] + "    耗時: " + Convert.ToInt32(ts2.TotalMilliseconds / 1000).ToString() + " 秒");

                                // Kerwin 的電腦
                                if (macid == "94C6913F94BD")
                                {
                                    // 下載檔案到本地端
                                    downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\raw_data_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                                }

                                // 刪除已存在的的CSV檔案
                                deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                            }
                            else
                            {
                                Console.WriteLine("匯入失敗: Raw data " + list_filename[i]);
                                Console.WriteLine($"匯入失敗: Raw data: {ftpserver}");
                                writeToLog.writeToLog("匯入失敗:" + ftpserver);
                                //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                                RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            }
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
                    writeToLog.writeToLog(ftpserver + ": " + ex.ToString());
                    //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                    RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                }
                Thread.Sleep(500);
            }

            reader.Close();
            response.Close();
            GC.Collect();

            Console.WriteLine("countRepeatFileCompareTrue:" + countRepeatFileCompareTrue.ToString() + "    countRepeatFileCompareFalse: " + countRepeatFileCompareFalse.ToString());
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
            //ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/";
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
                string[] fnSplit = list_filename[i].Split('_');
                string date1 = fnSplit[fnSplit.Length - 1].Split('-')[0];
                int outInt = 0;
                int.TryParse(date1, out outInt);
                if (outInt < 20240417) continue;

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


                    //ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i];
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
                    if (testStatusContentFormat == null || testStatusContentFormat.tester_device_info.Rows.Count < 1)
                    {
                        Console.WriteLine("Tester Status 讀檔失敗:  " + list_filename[i]);
                        writeToLog.writeToLog("Tester Status  讀檔失敗: " + ftpserver);
                        //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        continue;
                    }
                    if (!testStatusContentFormat.compareInfo())
                    {
                        Console.WriteLine("Tester Status 之 information 欄位名稱不符:  " + list_filename[i]);
                        writeToLog.writeToLog("Tester Status 之 information 欄位名稱不符: " + ftpserver);
                        //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        Console.Read();
                        return;
                    }
                    if (!testStatusContentFormat.compareStatus())
                    {
                        Console.WriteLine("Tester Status 之 tester_status 欄位名稱不符:  " + list_filename[i]);
                        writeToLog.writeToLog("Tester Status 之 tester_status 欄位名稱不符: " + ftpserver);
                        //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status/" + list_filename[i], FTP_USER, FTP_PASSWORD);
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
                            //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    //Console.WriteLine(RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD));
                    Console.WriteLine(RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status/" + list_filename[i], FTP_USER, FTP_PASSWORD));
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
            //ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/UI_Status_Error/";
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
                string[] fnSplit = list_filename[i].Split('_');
                string date1 = fnSplit[fnSplit.Length - 6];
                int outInt = 0;
                int.TryParse(date1, out outInt);
                if (outInt < 2024) continue;

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

                    //ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/UI_Status_Error/" + list_filename[i];
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
                        //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/UI_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/UI_Status/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        continue;
                    }
                    if (!uiStatusContentFormat.compareUiStatus())
                    {
                        Console.WriteLine("UI Status 之 ui_status 欄位名稱不符: " + list_filename[i]);
                        writeToLog.writeToLog("UI Status 之 ui_status 欄位名稱不符:" + ftpserver);
                        //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/UI_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/UI_Status/" + list_filename[i], FTP_USER, FTP_PASSWORD);
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
                        //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/UI_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/UI_Status/" + list_filename[i], FTP_USER, FTP_PASSWORD);
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

            // Fail_Pin
            //ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/";
            ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log/";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
            response = (FtpWebResponse)reqFTP.GetResponse();

            responseStream = response.GetResponseStream();
            reader = new StreamReader(responseStream);

            names = reader.ReadToEnd();
            list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            // 確認 pool 連線是否正常
            bool isConnect = webApiClient.checkDBConnect(POOL_NAME);
            if (!isConnect)
            {
                // 如果建立pool失敗就中斷程式
                if (!createPool(webApiClient, writeToLog)) return;
            }

            for (int i = list_filename.Count - 1; i >= 0; i--)
            {
                try
                {
                    string[] fnSplit = list_filename[i].Split('_');
                    string date1 = fnSplit[fnSplit.Length - 1].Split('-')[0];
                    int outInt = 0;
                    int.TryParse(date1, out outInt);
                    if (outInt < 20240401) continue;

                    //if (list_filename[i] != "fail_pin_ASEF3-5070-9004-172.22.181.19_MT8781V_NAZAHHB-AWOMDS-H-Q_20240312-083034.csv") continue;
                    fileNameSplit = list_filename[i].Split('.');
                    if (fileNameSplit[fileNameSplit.Length - 1] != "csv") continue;

                    Console.Write("Fail_pin_rate " + (list_filename.Count - i).ToString() + " : ");

                    //ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + list_filename[i];
                    ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log/" + list_filename[i];
                    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
                    response = (FtpWebResponse)reqFTP.GetResponse();
                    responseStream = response.GetResponseStream();
                    reader = new StreamReader(responseStream, Encoding.GetEncoding("big5"));


                    //if (i != list_filename.Count - 1) break;
                    //string filePath = @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\20240227_fail_pin_expansion_file\Fail Pin Rate Expansion 20240129_(Security C).csv";
                    //StreamReader reader2 = new StreamReader(filePath);

                    FailPinLogContentFormat failPinLogContent = fileAccess.FileReadFailPinLog(reader);
                    // 讀取失敗或沒有資料
                    if (failPinLogContent == null || failPinLogContent.fail_pin_rate_info.Rows.Count < 1)
                    {
                        Console.WriteLine("Fail Pin Log 讀取失敗:  " + list_filename[i]);
                        writeToLog.writeToLog("Fail Pin Log 讀取失敗: " + ftpserver);
                        //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        continue;
                    }
                    reader.Close();
                    if (!failPinLogContent.compareInfo())
                    {
                        Console.WriteLine("Fail Pin Log 之 information 欄位名稱不符:  " + list_filename[i]);
                        writeToLog.writeToLog("Fail Pin Log 之 information 欄位名稱不符: " + ftpserver);
                        //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log/" + list_filename[i], FTP_USER, FTP_PASSWORD);
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
                            //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log/" + list_filename[i], FTP_USER, FTP_PASSWORD);
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
    }
}

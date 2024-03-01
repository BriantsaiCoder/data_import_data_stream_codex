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

                Thread.Sleep(600000);
                threadTesterAlive = threadTesterMode.IsAlive;
                threadUiStatusAlive = threadUiStatusMode.IsAlive;
                threadTsmcAlive = threadTsmcMode.IsAlive;
                Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  Loop" + (++count) + " finished~");



            }
            
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

    }
}

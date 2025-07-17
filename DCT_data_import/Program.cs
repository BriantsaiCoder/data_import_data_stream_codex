using System;
using System.Collections.Generic;
using System.Linq;
using static DCT_data_import.DbObject;
using System.IO;
using System.Net;
using System.Threading;
using System.Configuration;
using DCT_data_import.ReadAndImport;
using System.Reflection;
using System.Net.Sockets;
namespace DCT_data_import
{
    class Program
    {
        public static string Environment = GetEnvironment(); // Dev : Development 環境(本機資料庫); Prod: Production 環境(Server資料庫)
        public static string HOST = ConfigurationManager.AppSettings[$"{Environment}Host"];
        public static string USER = ConfigurationManager.AppSettings[$"{Environment}UserName"];
        public static string PASSWORD = ConfigurationManager.AppSettings[$"{Environment}Password"];
        public static string PORT = ConfigurationManager.AppSettings[$"{Environment}Port"];
        public static string DATABASE = ConfigurationManager.AppSettings[$"{Environment}Database"];
        public static string POOL_NAME = ConfigurationManager.AppSettings["PoolName"];
        public static string FTP_IP = ConfigurationManager.ConnectionStrings["FtpIp"].ConnectionString;
        public static string FTP_USER = ConfigurationManager.ConnectionStrings["FtpUser"].ConnectionString;
        public static string FTP_PASSWORD = ConfigurationManager.ConnectionStrings["FtpPassword"].ConnectionString;
        static void Main(string[] args)
        {
            Console.WriteLine("HOST: " + HOST);
            Console.WriteLine("USER: " + USER);
            Console.WriteLine("PASSWORD: " + PASSWORD);
            Console.WriteLine("Environment: " + Environment);
            FileProcess fileAccess = new FileProcess();
            DatabaseService DatabaseService = new DatabaseService();
            WriteToLog writeToLog = new WriteToLog();
            DbAccess dbAccess = new DbAccess();
            int count = 0;
            ////TEST CASE
            //Tester tester = new Tester();
            //ImportResult importResult1;
            ////importResult1 = tester.ReadAndImportTesterStatus(fileAccess, DatabaseService , "ASEF3-5070-026-172.21.84.46_MT6897Z_ZAHJC-H-D_20250314-152944").GetAwaiter().GetResult();
            //RawData rawData = new RawData();
            //importResult1 = rawData.ReadAndImportRawData(fileAccess, DatabaseService , "ASEF1-5070-B81-172.22.105.32_TMTY34C-009C1L1T1D1CNAAN-S_Fixed_20250506-220731").GetAwaiter().GetResult();
            //Console.WriteLine("importResult1.Result: " + importResult1.Result);
            //Console.ReadLine();
            //bool isConnect = DatabaseService .checkDBConnect(POOL_NAME);
            //if (!isConnect)
            //{
            //    // 如果建立pool失敗就中斷程式
            //    if (!createPool(DatabaseService , writeToLog)) return;
            //}
#if true /// ture: 有DB Key檢查; false: 沒有DB Key檢查
            bool threadTesterAlive = false, threadUiStatusAlive = false, threadTsmcAlive = false;
            Thread threadTesterMode = new Thread(() => ImportTesterMode(fileAccess, dbAccess, DatabaseService));
            Thread threadUiStatusMode = new Thread(() => ImportUiStatusMode(fileAccess, dbAccess, DatabaseService));
            Thread threadTsmcMode = new Thread(() => ImportTsmcMode(fileAccess, dbAccess, DatabaseService));
            while (true)
            {
                Console.WriteLine("threadTesterAlive.IsAlive: " + threadTesterAlive);
                Console.WriteLine("threadUiStatusAlive.IsAlive: " + threadUiStatusAlive);
                Console.WriteLine("threadTsmcAlive.IsAlive: " + threadTsmcAlive);
                if (!threadTesterAlive)
                {
                    threadTesterMode.Interrupt();
                    threadTesterMode.Abort();
                    threadTesterMode = new Thread(() => ImportTesterMode(fileAccess, dbAccess, DatabaseService));
                    threadTesterMode.Start();
                }
                if (!threadUiStatusAlive)
                {
                    threadUiStatusMode.Interrupt();
                    threadUiStatusMode.Abort();
                    threadUiStatusMode = new Thread(() => ImportUiStatusMode(fileAccess, dbAccess, DatabaseService));
                    threadUiStatusMode.Start();
                }
                if (!threadTsmcAlive)
                {
                    threadTsmcMode.Interrupt();
                    threadTsmcMode.Abort();
                    threadTsmcMode = new Thread(() => ImportTsmcMode(fileAccess, dbAccess, DatabaseService));
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
                Thread.Sleep(432000); // 432000秒 --> 2H 執行一次
                threadTesterAlive = threadTesterMode.IsAlive;
                threadUiStatusAlive = threadUiStatusMode.IsAlive;
                threadTsmcAlive = threadTsmcMode.IsAlive;
                Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  Loop" + (++count) + " finished~");
            }
#else
            bool thread1Alive = false, thread2Alive = false, thread3Alive = false, thread4Alive = false, thread5Alive = false;
            Thread thread1 = new Thread(() => readAndImportRawData(fileAccess, DatabaseService ));
            Thread thread2 = new Thread(() => readAndImportTesterStatus(fileAccess, DatabaseService ));
            Thread thread3 = new Thread(() => readAndImportUIStatus(fileAccess, DatabaseService ));
            Thread thread4 = new Thread(() => readAndImportFailPinLog(fileAccess, DatabaseService ));
            Thread thread5 = new Thread(() => ImportTsmcMode(fileAccess, dbAccess, DatabaseService ));
            while (true)
            {
                Console.WriteLine("thread1.IsAlive: " + thread1Alive);
                Console.WriteLine("thread2.IsAlive: " + thread2Alive);
                Console.WriteLine("thread3.IsAlive: " + thread3Alive);
                Console.WriteLine("thread4.IsAlive: " + thread4Alive);
                Console.WriteLine("thread5.IsAlive: " + thread5Alive);
                count++;
                bool isConnect = DatabaseService .checkDBConnect(POOL_NAME);
                if (!isConnect)
                {
                    // 如果建立pool失敗就中斷程式
                    if (!createPool(DatabaseService , writeToLog)) return;
                }
                if (!thread1Alive)
                {
                    thread1.Interrupt();
                    thread1.Abort();
                    thread1 = new Thread(() => readAndImportRawData(fileAccess, DatabaseService ));
                    thread1.Start();
                }
                if (!thread2Alive)
                {
                    thread2.Interrupt();
                    thread2.Abort();
                    thread2 = new Thread(() => readAndImportTesterStatus(fileAccess, DatabaseService ));
                    thread2.Start();
                }
                if (!thread3Alive)
                {
                    thread3.Interrupt();
                    thread3.Abort();
                    thread3 = new Thread(() => readAndImportUIStatus(fileAccess, DatabaseService ));
                    thread3.Start();
                }
                if (!thread4Alive)
                {
                    thread4.Interrupt();
                    thread4.Abort();
                    thread4 = new Thread(() => readAndImportFailPinLog(fileAccess, DatabaseService ));
                    thread4.Start();
                }
                if (!thread5Alive)
                {
                    thread5.Interrupt();
                    thread5.Abort();
                    thread5 = new Thread(() => ImportTsmcMode(fileAccess, dbAccess, DatabaseService ));
                    thread5.Start();
                }
                //readAndImportRawData(fileAccess, DatabaseService );
                //readAndImportTesterStatus(fileAccess, DatabaseService );
                //readAndImportUIStatus(fileAccess, DatabaseService );
                //readAndImportFailPinLog(fileAccess, DatabaseService );
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
        // 方法：取得本機 IPv4 地址
        static string GetEnvironment()
        {
            // Dev : Development 環境; Prod: Production 環境
            // 偵測本機 IP 地址
            string localIp = GetLocalIPAddress();
            // 判斷環境
            string environment = string.Empty;
            string[] productionIps = { "10.16.92.67", "10.16.92.68" };
            if (string.IsNullOrEmpty(localIp))
            {
                Console.WriteLine("無法取得本機 IP，預設為 Development 環境");
                environment = "Dev";
            }
            else
            {
                environment = Array.Exists(productionIps, ip => ip == localIp) ? "Prod" : "Dev";
            }
            return environment;
        }
        static string GetLocalIPAddress()
        {
            try
            {
                string hostName = Dns.GetHostName();
                IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
                foreach (IPAddress ip in hostEntry.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork) // 過濾 IPv4 地址
                    {
                        return ip.ToString();
                    }
                }
                throw new Exception("未找到本機 IPv4 地址");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"取得本機 IP 時發生錯誤: {ex.Message}");
                return string.Empty;
            }
        }
        static string SendMailModel(string mailBody, string mailTitle = "DCT data notification")
        {
            WriteToLog writeToLog = new WriteToLog();
            // 寄信囉~
            string strAppPath = Assembly.GetExecutingAssembly().Location; //獲得.exe路徑
            string strWorkPath = Path.GetDirectoryName(strAppPath);
            ReadWriteINIfile _readWriteINIfile = new ReadWriteINIfile(strWorkPath + "\\dct_import_mail_list.ini");
            EmailModels Email_class = new EmailModels();
            Email_class.Subject = mailTitle;
            Email_class.Body = mailBody;
            // 換行<br>
            // 空白&nbsp
            List<string> to_name_list = _readWriteINIfile.ReadINI("mail_list", "mail_to").Split(',').ToList();
            Email_class.ToList = to_name_list;
            List<string> cc_name_list = _readWriteINIfile.ReadINI("mail_list", "mail_cc").Split(',').ToList();
            Email_class.CCList = cc_name_list;
            List<string> bcc_name_list = _readWriteINIfile.ReadINI("mail_list", "mail_bcc").Split(',').ToList();
            Email_class.BccList = bcc_name_list;
            List<string> filelist = new List<string>();
            if (Email_class.SendEmail())
            {
                writeToLog.WriteToDataImportLog("寄信成功!");
                return "OK";
            }
            else
            {
                writeToLog.WriteToDataImportLog("寄信失敗!");
                return "FAIL";
            }
        }
        static string ImportTesterMode(FileProcess fileAccess, DbAccess dbAccess, DatabaseService DatabaseService)
        {
            #region 檢查一天內是否有資料
            int dataCount = dbAccess.SelectDataCountInDays(DatabaseService, 1, "tester");
            if (dataCount == 0)
                if (DateTime.Now.TimeOfDay.Hours == 8)
                {
                    if (DateTime.Now.TimeOfDay.Minutes >= 0 && DateTime.Now.TimeOfDay.Minutes < 10)
                    {
                        SendMailModel("Dear all,<br><br>Tester 已超過1天無資料匯入，請確認!<br><br>Thanks.");
                    }
                }
            #endregion
            List<DbKeyObject> dbKeyList = dbAccess.SelectDbKey(DatabaseService, "tester");
            RecoveryRate recoveryRate = new RecoveryRate();
            RawData rawData = new RawData();
            Tester tester = new Tester();
            FailPin failPin = new FailPin();
            string updateImportStatus, remark;
            ImportResult importResult, importResult1, importResult2, importResult3;
            for (int i = 0; i < dbKeyList.Count; i++)
            {
                Console.WriteLine((i + 1).ToString() + ".DB_Key=" + dbKeyList[i].DbKey + "  ");
                if (dbKeyList[i].CheckStatus >= 8 && dbKeyList[i].CheckStatus <= 15 && dbKeyList[i].RecoveryRate == 0)
                {
                    importResult = recoveryRate.ReadAndImportRecoveryRateData(fileAccess, DatabaseService, dbKeyList[i].DbKey).GetAwaiter().GetResult();
                }
                else
                {
                    importResult = new ImportResult(dbKeyList[i].RecoveryRate, "");
                }
                if (dbKeyList[i].CheckStatus == 2 || dbKeyList[i].CheckStatus == 3 || dbKeyList[i].CheckStatus == 6 || dbKeyList[i].CheckStatus == 7 || dbKeyList[i].CheckStatus == 10 || dbKeyList[i].CheckStatus == 11 || dbKeyList[i].CheckStatus == 14 || dbKeyList[i].CheckStatus == 15)
                //if (dbKeyList[i].CheckStatus == 2 || dbKeyList[i].CheckStatus == 3 || dbKeyList[i].CheckStatus == 6 || dbKeyList[i].CheckStatus == 7)
                {
                    if (dbKeyList[i].TestResult == 0)
                    {
                        importResult2 = rawData.ReadAndImportRawData(fileAccess, DatabaseService, dbKeyList[i].DbKey).GetAwaiter().GetResult();
                    }
                    else
                    {
                        importResult2 = new ImportResult(dbKeyList[i].TestResult, "");
                    }
                }
                else
                {
                    importResult2 = new ImportResult(0, "");
                }
                if (dbKeyList[i].CheckStatus >= 4 && dbKeyList[i].CheckStatus <= 7 && dbKeyList[i].Tester == 0 || dbKeyList[i].CheckStatus >= 12 && dbKeyList[i].CheckStatus <= 15 && dbKeyList[i].Tester == 0)
                //if (dbKeyList[i].CheckStatus >= 4 && dbKeyList[i].CheckStatus <= 7 && dbKeyList[i].Tester == 0)
                {
                    importResult1 = tester.ReadAndImportTesterStatus(fileAccess, DatabaseService, dbKeyList[i].DbKey).GetAwaiter().GetResult();
                }
                else
                {
                    importResult1 = new ImportResult(dbKeyList[i].Tester, "");
                }
                if (dbKeyList[i].CheckStatus == 1 || dbKeyList[i].CheckStatus == 3 || dbKeyList[i].CheckStatus == 5 || dbKeyList[i].CheckStatus == 7 || dbKeyList[i].CheckStatus == 9 || dbKeyList[i].CheckStatus == 11 || dbKeyList[i].CheckStatus == 13 || dbKeyList[i].CheckStatus == 15)
                //if (dbKeyList[i].CheckStatus == 1 || dbKeyList[i].CheckStatus == 3 || dbKeyList[i].CheckStatus == 5 || dbKeyList[i].CheckStatus == 7)
                {
                    if (dbKeyList[i].FailPin == 0)
                    {
                        importResult3 = failPin.ReadAndImportFailPinLog(fileAccess, DatabaseService, dbKeyList[i].DbKey).GetAwaiter().GetResult();
                    }
                    else
                    {
                        importResult3 = new ImportResult(dbKeyList[i].FailPin, "");
                    }
                }
                else
                {
                    importResult3 = new ImportResult(0, "");
                }
                remark = "";
                remark += (string.IsNullOrEmpty(importResult.Message)) ? "" : "recovery rate: " + importResult.Message + "  ";
                remark += (string.IsNullOrEmpty(importResult1.Message)) ? "" : "tester: " + importResult1.Message + "  ";
                remark += (string.IsNullOrEmpty(importResult2.Message)) ? "" : "test result: " + importResult2.Message + "  ";
                remark += (string.IsNullOrEmpty(importResult3.Message)) ? "" : "fail pin: " + importResult3.Message;
                updateImportStatus = dbAccess.UpdateDbKeyImportStatus(DatabaseService, dbKeyList[i].DbKey, importResult.Result, importResult1.Result, importResult2.Result, importResult3.Result, remark);
                Console.WriteLine("Update tester import status:" + updateImportStatus);
            }
            Console.WriteLine("Tester mode end~");
            #region 寄信通報
            //if ((int)DateTime.Now.DayOfWeek%2 ==0 &&/*DateTime.Now.TimeOfDay.Hours == 0 ||*/ DateTime.Now.TimeOfDay.Hours == 12)
            //{
            //if (DateTime.Now.TimeOfDay.Minutes >= 0 && DateTime.Now.TimeOfDay.Minutes < 10)
            //{
            #region 找出需要通報的db_key
            //List<DbKeyObject> failDbKeyObject = dbAccess.SelectFailDbKeyResult(DatabaseService , "tester");
            //List<DbKeyObject> failDbKeyUiStatusObject = dbAccess.SelectFailDbKeyResult(DatabaseService , "ui_status");
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
            //        string updateMailResult = dbAccess.UpdateMail(DatabaseService , failDbKeyObject, "tester");
            //        string updateMailResult2 = dbAccess.UpdateMail(DatabaseService , failDbKeyUiStatusObject, "ui_status");
            //    }
            //}
            List<DbKeyObject> failDbKeyFromFile = dbAccess.SelectFailDbKeyFromFile();
            // 通報
            if (failDbKeyFromFile.Count > 0)
            {
                // 統計 Remark 出現次數
                var remarkCountDict = new Dictionary<string, int>();
                foreach (var item in failDbKeyFromFile)
                {
                    if (remarkCountDict.ContainsKey(item.Remark))
                        remarkCountDict[item.Remark]++;
                    else
                        remarkCountDict[item.Remark] = 1;
                }
                // 移除重複 Remark，只保留第一筆
                var distinctFailList = failDbKeyFromFile
                    .GroupBy(x => x.Remark)
                    .Select(g => g.First())
                    .ToList();
                // 將統計結果轉為 List<string>，格式為 "Remark內容 x 數量"
                var remarkSummaryList = remarkCountDict
                    .Select(kvp => $"{kvp.Key} x {kvp.Value}")
                    .ToList();
                // 組成 mailTitle
                string mailTitle = "DCT data notification - " + string.Join(", ", remarkSummaryList);
                string mailBody = "Dear all,<br>下列資料發生異常，請確認檔案內容<br>";
                for (int i = 0; i < failDbKeyFromFile.Count; i++)
                {
                    mailBody += (i + 1).ToString() + ".    DB_Key:" + failDbKeyFromFile[i].DbKey + ",   <b>" + failDbKeyFromFile[i].Remark + "</b><br>";
                }
                mailBody += "Thanks. <br>";
                string sendResult = SendMailModel(mailBody, mailTitle);
                if (sendResult == "OK")
                {
                    // 刪除信件暫存檔
                    string log_path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + @"\mail_temp.txt").LocalPath;
                    File.Delete(log_path);
                }
            }
            #endregion 找出需要通報的db_key
            //}
            //}
            #endregion
            return "";
        }
        static string ImportUiStatusMode(FileProcess fileAccess, DbAccess dbAccess, DatabaseService DatabaseService)
        {
            #region 檢查一天內是否有資料
            //int dataCount = dbAccess.SelectDataCountInDays(DatabaseService , 1, "ui_status");
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
            List<DbKeyObject> dbKeyUiStatusList = dbAccess.SelectDbKey(DatabaseService, "ui_status");
            UiStatus uiStatus = new UiStatus();
            string updateImportStatus, remark;
            ImportResult importResult4;
            for (int i = 0; i < dbKeyUiStatusList.Count; i++)
            {
                Console.WriteLine((i + 1).ToString() + ".DB_Key_ui_status=" + dbKeyUiStatusList[i].DbKey + "  ");
                importResult4 = uiStatus.ReadAndImportUIStatus(fileAccess, DatabaseService, dbKeyUiStatusList[i].DbKey);
                remark = "";
                remark += (string.IsNullOrEmpty(importResult4.Message)) ? "" : "ui status:" + importResult4.Message;
                updateImportStatus = dbAccess.UpdateDbKeyUiStatusImportStatus(DatabaseService, dbKeyUiStatusList[i].DbKey, importResult4.Result, remark);
                Console.WriteLine("Update ui_status import status:" + updateImportStatus);
            }
            Console.WriteLine("ui_status mode end~");
            #region 寄信通報
            //if (DateTime.Now.TimeOfDay.Hours == 0 || DateTime.Now.TimeOfDay.Hours == 12)
            //{
            //    if (DateTime.Now.TimeOfDay.Minutes >= 0 && DateTime.Now.TimeOfDay.Minutes < 10)
            //    {
            //        #region 找出需要通報的db_key_ui_status
            //        List<DbKeyObject> failDbKeyObject = dbAccess.SelectFailDbKeyResult(DatabaseService , "ui_status");
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
            //                string updateMailResult = dbAccess.UpdateMail(DatabaseService , failDbKeyObject, "ui_status");
            //            }
            //        }
            //        #endregion 找出需要通報的db_key
            //    }
            //}
            #endregion
            return "";
        }
        static string ImportTsmcMode(FileProcess fileAccess, DbAccess dbAccess, DatabaseService DatabaseService)
        {
            ImportResult importResult;
            TsmcIeda tsmcIeda = new TsmcIeda();
            importResult = tsmcIeda.ReadAndImportIeda(fileAccess, DatabaseService, "");
            return "";
        }
    }
}
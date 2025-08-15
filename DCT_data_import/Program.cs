using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using DCT_data_import.Common;
using DCT_data_import.ReadAndImport;
using static DCT_data_import.DbObject;
namespace DCT_data_import
{
    class Program
    {
        private static readonly NotificationService _notificationService = new NotificationService();
        public static string Environment = GetEnvironment(); // Dev : Development 環境(本機資料庫); Prod: Production 環境(Server資料庫)
        public static string HOST = ConfigurationManager.AppSettings[$"{Environment}Host"];
        public static string USER = ConfigurationManager.AppSettings[$"{Environment}UserName"];
        public static string PASSWORD = ConfigurationManager.AppSettings[$"{Environment}Password"];
        public static string PORT = ConfigurationManager.AppSettings[$"{Environment}Port"];
        public static string DATABASE = ConfigurationManager.AppSettings[$"{Environment}Database"];
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
            //TEST CASE
            RecoveryRate recoveryRate = new RecoveryRate();
            RawData rawData = new RawData();
            Tester tester = new Tester();
            FailPin failPin = new FailPin();
            UiStatus uiStatus = new UiStatus();
            TsmcIeda tsmcIeda = new TsmcIeda();
            ImportResult importResult1;
            importResult1 = tsmcIeda.ReadAndImportIeda(fileAccess, DatabaseService, string.Empty);
            Console.WriteLine("tsmcIeda importResult1.Result: " + importResult1.Result);
            importResult1 = recoveryRate.ReadAndImportRecoveryRateData(fileAccess, DatabaseService, "ASEF3-5070-9003-172.22.181.18_MT8755V_TNZBHHB-AWOMD-H-D_20250713-230625").GetAwaiter().GetResult();
            Console.WriteLine("recoveryRate importResult1.Result: " + importResult1.Result);
            importResult1 = tester.ReadAndImportTesterStatus(fileAccess, DatabaseService, "ASEF3-5070-9003-172.22.181.18_MT8755V_TNZBHHB-AWOMD-H-D_20250714-035452").GetAwaiter().GetResult();
            Console.WriteLine("tester importResult1.Result: " + importResult1.Result);
            importResult1 = rawData.ReadAndImportRawData(fileAccess, DatabaseService, "ASEF3-5070-9003-172.22.181.18_MT8755V_TNZBHHB-AWOMD-H-D_20250714-040624").GetAwaiter().GetResult();
            Console.WriteLine("rawData importResult1.Result: " + importResult1.Result);
            importResult1 = failPin.ReadAndImportFailPinLog(fileAccess, DatabaseService, "ASE03-5070-033-10.10.187.94_AAH@A237390002-A_1007_T_D_20250718-065227").GetAwaiter().GetResult();
            Console.WriteLine("failPin importResult1.Result: " + importResult1.Result);
            importResult1 = uiStatus.ReadAndImportUIStatus(fileAccess, DatabaseService, "KH_K6B_OSH075_2025_08_04_14_02_33");
            Console.WriteLine("uiStatus importResult1.Result: " + importResult1.Result);
            Console.ReadLine();
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
                // 原始邏輯 (備份):
                // DateTime nowTime = DateTime.Now;
                // if ((int)nowTime.DayOfWeek == 1 && nowTime.Hour == 8 && nowTime.Minute < 10)
                // {
                //     string mailBody = "Dear all,<br>DCT資料庫匯入程式正常執行中!<br>Thanks. <br>";
                //     string mailTitle = "DCT data notification - 正常運行中";
                //     string sendResult = SendMailModel(mailBody, mailTitle);
                // }
                // 新的簡化版實作:
                if (_notificationService.ShouldSendProgramStatusNotification())
                {
                    _notificationService.SendProgramStatusNotification();
                }
                #endregion 固定時間通報程式還活著
                Thread.Sleep(432000); // 432000秒 --> 2H 執行一次
                threadTesterAlive = threadTesterMode.IsAlive;
                threadUiStatusAlive = threadUiStatusMode.IsAlive;
                threadTsmcAlive = threadTsmcMode.IsAlive;
                Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  Loop" + (++count) + " finished~");
            }
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
                WriteToLog writeToLogService = new WriteToLog();
                writeToLogService.WriteErrorLog($"[GetLocalIPAddress] 取得本機 IP 時發生錯誤: {ex.Message}");
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
                writeToLog.WriteErrorLog("寄信失敗!");
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
                        // 原始邏輯 (備份): SendMailModel("Dear all,<br><br>Tester 已超過1天無資料匯入，請確認!<br><br>Thanks.");
                        _notificationService.SendDataMissingNotification("Tester");
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
                    importResult = new ImportResult(dbKeyList[i].RecoveryRate, string.Empty);
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
                        importResult2 = new ImportResult(dbKeyList[i].TestResult, string.Empty);
                    }
                }
                else
                {
                    importResult2 = new ImportResult(0, string.Empty);
                }
                if (dbKeyList[i].CheckStatus >= 4 && dbKeyList[i].CheckStatus <= 7 && dbKeyList[i].Tester == 0 || dbKeyList[i].CheckStatus >= 12 && dbKeyList[i].CheckStatus <= 15 && dbKeyList[i].Tester == 0)
                //if (dbKeyList[i].CheckStatus >= 4 && dbKeyList[i].CheckStatus <= 7 && dbKeyList[i].Tester == 0)
                {
                    importResult1 = tester.ReadAndImportTesterStatus(fileAccess, DatabaseService, dbKeyList[i].DbKey).GetAwaiter().GetResult();
                }
                else
                {
                    importResult1 = new ImportResult(dbKeyList[i].Tester, string.Empty);
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
                        importResult3 = new ImportResult(dbKeyList[i].FailPin, string.Empty);
                    }
                }
                else
                {
                    importResult3 = new ImportResult(0, string.Empty);
                }
                remark = string.Empty;
                remark += (string.IsNullOrEmpty(importResult.Message)) ? string.Empty : "recovery rate: " + importResult.Message + "  ";
                remark += (string.IsNullOrEmpty(importResult1.Message)) ? string.Empty : "tester: " + importResult1.Message + "  ";
                remark += (string.IsNullOrEmpty(importResult2.Message)) ? string.Empty : "test result: " + importResult2.Message + "  ";
                remark += (string.IsNullOrEmpty(importResult3.Message)) ? string.Empty : "fail pin: " + importResult3.Message;
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
                // 原始邏輯 (備份):
                /*
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
                */
                // 新的簡化版實作:
                var details = failDbKeyFromFile.Select(x => $"DB_Key:{x.DbKey}, {x.Remark}").ToList();
                bool sendResult = _notificationService.SendErrorNotification("下列資料發生異常，請確認檔案內容", details);
                if (sendResult)
                {
                    _notificationService.CleanupMailTempFiles();
                }
            }
            #endregion 找出需要通報的db_key
            //}
            //}
            #endregion
            return string.Empty;
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
                remark = string.Empty;
                remark += (string.IsNullOrEmpty(importResult4.Message)) ? string.Empty : "ui status:" + importResult4.Message;
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
            return string.Empty;
        }
        static string ImportTsmcMode(FileProcess fileAccess, DbAccess dbAccess, DatabaseService DatabaseService)
        {
            ImportResult importResult;
            TsmcIeda tsmcIeda = new TsmcIeda();
            importResult = tsmcIeda.ReadAndImportIeda(fileAccess, DatabaseService, string.Empty);
            return string.Empty;
        }
    }
}
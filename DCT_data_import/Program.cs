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
            WriteToLog writeToLog = new WriteToLog();
            try
            {
                Console.WriteLine("HOST: " + HOST);
                Console.WriteLine("USER: " + USER);
                Console.WriteLine("PASSWORD: " + PASSWORD);
                Console.WriteLine("Environment: " + Environment);
                FileProcess fileAccess = new FileProcess();
                DatabaseService DatabaseService = new DatabaseService();
                DbAccess dbAccess = new DbAccess();
                int count = 0;
                ////TEST CASE
                //RecoveryRate recoveryRate = new RecoveryRate();
                //MultiSpecRawData multiSpecRawData = new MultiSpecRawData();
                //RawData rawData = new RawData();
                //Tester tester = new Tester();
                //FailPin failPin = new FailPin();
                //UiStatus uiStatus = new UiStatus();
                //TsmcIeda tsmcIeda = new TsmcIeda();
                //ImportResult importResult1;
                //#region 寄信通報
                //try
                //{
                //    List<DbKeyObject> failDbKeyFromFile = dbAccess.SelectFailDbKeyFromFile();
                //    // 通報
                //    if (failDbKeyFromFile.Count > 0)
                //    {
                //        // 新的簡化版實作:
                //        var details = failDbKeyFromFile.Select(x => $"DB_Key:{x.DbKey}, {x.Remark}").ToList();
                //        bool sendResult = _notificationService.SendErrorNotification("下列資料發生異常(test)，請確認檔案內容", details);
                //        if (sendResult)
                //        {
                //            _notificationService.CleanupMailTempFiles();
                //        }
                //    }
                //}
                //catch (Exception ex)
                //{
                //    writeToLog.WriteErrorLog($"[ImportTesterMode] 寄信通報失敗: {ex.Message}");
                //    Console.WriteLine($"[ImportTesterMode] 寄信通報失敗: {ex.Message}");
                //}
                //#endregion
                //rawData.RenameFile("ftp://10.16.92.67/DCT_Log/DCT_DB_DATA_Dev/Data_Cloud_CSV/test_result_OSH101-192.168.137.1_20250425 建的405是PC Wafer-1_20250603-010640.csv", "ftp://10.16.92.67/DCT_Log/DCT_DB_DATA_Dev/Data_Cloud_CSV_Error/test_result_OSH101-192.168.137.1_20250425 建的405是PC Wafer-1_20250603-010640.csv", Program.FTP_USER, Program.FTP_PASSWORD);
                //Console.WriteLine("renameFile done");
                //Console.ReadLine();
                //importResult1 = tsmcIeda.ReadAndImportIeda(fileAccess, DatabaseService, string.Empty);
                //Console.WriteLine("tsmcIeda importResult1.Result: " + importResult1.Result);
                //importResult1 = recoveryRate.ReadAndImportRecoveryRateData(fileAccess, DatabaseService, "ASEF3-5070-9003-172.22.181.18_MT8755V_TNZBHHB-AWOMD-H-D_20250712-204923").GetAwaiter().GetResult();
                //Console.WriteLine("recoveryRate importResult1.Result: " + importResult1.Result);
                //importResult1 = tester.ReadAndImportTesterStatus(fileAccess, DatabaseService, "ASEF3-5070-9003-172.22.181.18_MT8755V_TNZBHHB-AWOMD-H-D_20250713-230625").GetAwaiter().GetResult();
                //Console.WriteLine("tester importResult1.Result: " + importResult1.Result);
                //importResult1 = rawData.ReadAndImportRawData(fileAccess, DatabaseService, "ASE03-5070-035-10.10.187.89_AAH@A311530002-0_0727_T_D_fixed_20250827-061849").GetAwaiter().GetResult();
                //Console.WriteLine("rawData importResult1.Result: " + importResult1.Result);
                //importResult1 = multiSpecRawData.ReadAndImportMultiSpecRawData(fileAccess, DatabaseService, "ASE07-5070-032-127.0.0.1_AAH@A190640075-0_0410_N_LeakChecked_6STD_NewSpecLeak_20250625-150640").GetAwaiter().GetResult();
                //Console.WriteLine("multiSpecRawData importResult1.Result: " + importResult1.Result);
                //importResult1 = failPin.ReadAndImportFailPinLog(fileAccess, DatabaseService, "789").GetAwaiter().GetResult();
                //Console.WriteLine("failPin importResult1.Result: " + importResult1.Result);
                //importResult1 = uiStatus.ReadAndImportUIStatus(fileAccess, DatabaseService, "KH_K6B_OSH083_2025_08_04_13_14_33");
                //Console.WriteLine("uiStatus importResult1.Result: " + importResult1.Result);
                //Console.ReadLine();
                bool threadTesterAlive = false, threadUiStatusAlive = false, threadTsmcAlive = false;
                Thread threadTesterMode = new Thread(() => ImportTesterMode(fileAccess, dbAccess, DatabaseService));
                Thread threadUiStatusMode = new Thread(() => ImportUiStatusMode(fileAccess, dbAccess, DatabaseService));
                Thread threadTsmcMode = new Thread(() => ImportTsmcMode(fileAccess, dbAccess, DatabaseService));
                while (true)
                {
                    try
                    {
                        Console.WriteLine("threadTesterAlive.IsAlive: " + threadTesterAlive);
                        Console.WriteLine("threadUiStatusAlive.IsAlive: " + threadUiStatusAlive);
                        Console.WriteLine("threadTsmcAlive.IsAlive: " + threadTsmcAlive);
                        if (!threadTesterAlive)
                        {
                            try
                            {
                                threadTesterMode.Interrupt();
                                threadTesterMode.Abort();
                                threadTesterMode = new Thread(() => ImportTesterMode(fileAccess, dbAccess, DatabaseService));
                                threadTesterMode.Start();
                            }
                            catch (ThreadStateException threadEx)
                            {
                                writeToLog.WriteErrorLog($"[Main] TesterMode執行緒狀態錯誤: {threadEx.Message}");
                                Console.WriteLine($"TesterMode執行緒狀態錯誤: {threadEx.Message}");
                            }
                            catch (ThreadAbortException abortEx)
                            {
                                writeToLog.WriteErrorLog($"[Main] TesterMode執行緒被強制中止: {abortEx.Message}");
                                Console.WriteLine($"TesterMode執行緒被強制中止: {abortEx.Message}");
                            }
                            catch (Exception ex)
                            {
                                writeToLog.WriteErrorLog($"[Main] TesterMode執行緒操作失敗: {ex.Message}");
                                Console.WriteLine($"TesterMode執行緒操作失敗: {ex.Message}");
                            }
                        }
                        if (!threadUiStatusAlive)
                        {
                            try
                            {
                                threadUiStatusMode.Interrupt();
                                threadUiStatusMode.Abort();
                                threadUiStatusMode = new Thread(() => ImportUiStatusMode(fileAccess, dbAccess, DatabaseService));
                                threadUiStatusMode.Start();
                            }
                            catch (ThreadStateException threadEx)
                            {
                                writeToLog.WriteErrorLog($"[Main] UiStatusMode執行緒狀態錯誤: {threadEx.Message}");
                                Console.WriteLine($"UiStatusMode執行緒狀態錯誤: {threadEx.Message}");
                            }
                            catch (ThreadAbortException abortEx)
                            {
                                writeToLog.WriteErrorLog($"[Main] UiStatusMode執行緒被強制中止: {abortEx.Message}");
                                Console.WriteLine($"UiStatusMode執行緒被強制中止: {abortEx.Message}");
                            }
                            catch (Exception ex)
                            {
                                writeToLog.WriteErrorLog($"[Main] UiStatusMode執行緒操作失敗: {ex.Message}");
                                Console.WriteLine($"UiStatusMode執行緒操作失敗: {ex.Message}");
                            }
                        }
                        if (!threadTsmcAlive)
                        {
                            try
                            {
                                threadTsmcMode.Interrupt();
                                threadTsmcMode.Abort();
                                threadTsmcMode = new Thread(() => ImportTsmcMode(fileAccess, dbAccess, DatabaseService));
                                threadTsmcMode.Start();
                            }
                            catch (ThreadStateException threadEx)
                            {
                                writeToLog.WriteErrorLog($"[Main] TsmcMode執行緒狀態錯誤: {threadEx.Message}");
                                Console.WriteLine($"TsmcMode執行緒狀態錯誤: {threadEx.Message}");
                            }
                            catch (ThreadAbortException abortEx)
                            {
                                writeToLog.WriteErrorLog($"[Main] TsmcMode執行緒被強制中止: {abortEx.Message}");
                                Console.WriteLine($"TsmcMode執行緒被強制中止: {abortEx.Message}");
                            }
                            catch (Exception ex)
                            {
                                writeToLog.WriteErrorLog($"[Main] TsmcMode執行緒操作失敗: {ex.Message}");
                                Console.WriteLine($"TsmcMode執行緒操作失敗: {ex.Message}");
                            }
                        }
                        #region 固定時間通報程式還活著
                        try
                        {
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
                        }
                        catch (Exception notificationEx)
                        {
                            writeToLog.WriteErrorLog($"[Main] 程式狀態通知失敗: {notificationEx.Message}");
                            Console.WriteLine($"程式狀態通知失敗: {notificationEx.Message}");
                        }
                        #endregion 固定時間通報程式還活著
                        Thread.Sleep(432000); // 432000秒 --> 2H 執行一次
                        threadTesterAlive = threadTesterMode.IsAlive;
                        threadUiStatusAlive = threadUiStatusMode.IsAlive;
                        threadTsmcAlive = threadTsmcMode.IsAlive;
                        Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  Loop" + (++count) + " finished~");
                    }
                    catch (Exception loopEx)
                    {
                        writeToLog.WriteErrorLog($"[Main] 主要迴圈執行錯誤: {loopEx.Message}");
                        Console.WriteLine($"主要迴圈執行錯誤: {loopEx.Message}");
                        Thread.Sleep(30000); // 錯誤時等待30秒再重試
                    }
                }
            }
            catch (ConfigurationErrorsException configEx)
            {
                writeToLog.WriteErrorLog($"[Main] 配置檔案錯誤: {configEx.Message}");
                Console.WriteLine($"配置檔案錯誤: {configEx.Message}");
                Console.WriteLine("請檢查App.config檔案設定，按任意鍵退出...");
                Console.ReadKey();
            }
            catch (UnauthorizedAccessException authEx)
            {
                writeToLog.WriteErrorLog($"[Main] 存取權限不足: {authEx.Message}");
                Console.WriteLine($"存取權限不足: {authEx.Message}");
                Console.WriteLine("請以管理員身分執行程式，按任意鍵退出...");
                Console.ReadKey();
            }
            catch (OutOfMemoryException memEx)
            {
                writeToLog.WriteErrorLog($"[Main] 記憶體不足: {memEx.Message}");
                Console.WriteLine($"記憶體不足: {memEx.Message}");
                Console.WriteLine("請檢查系統記憶體使用狀況，按任意鍵退出...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog($"[Main] 程式執行時發生嚴重錯誤: {ex.Message}");
                Console.WriteLine($"程式執行時發生嚴重錯誤: {ex.Message}");
                Console.WriteLine("程式即將退出，按任意鍵繼續...");
                Console.ReadKey();
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
            try
            {
                // 寄信囉~
                string strAppPath = Assembly.GetExecutingAssembly().Location; //獲得.exe路徑
                string strWorkPath = Path.GetDirectoryName(strAppPath);
                ReadWriteINIfile _readWriteINIfile = null;
                try
                {
                    _readWriteINIfile = new ReadWriteINIfile(strWorkPath + "\\dct_import_mail_list.ini");
                }
                catch (Exception ex)
                {
                    writeToLog.WriteErrorLog($"[SendMailModel] INI檔案初始化失敗: {ex.Message}");
                    Console.WriteLine($"[SendMailModel] INI檔案初始化失敗: {ex.Message}");
                    return "FAIL";
                }
                EmailModels Email_class = new EmailModels();
                Email_class.Subject = mailTitle;
                Email_class.Body = mailBody;
                try
                {
                    // 換行<br>
                    // 空白&nbsp
                    List<string> to_name_list = _readWriteINIfile.ReadINI("mail_list", "mail_to").Split(',').ToList();
                    Email_class.ToList = to_name_list;
                    List<string> cc_name_list = _readWriteINIfile.ReadINI("mail_list", "mail_cc").Split(',').ToList();
                    Email_class.CCList = cc_name_list;
                    List<string> bcc_name_list = _readWriteINIfile.ReadINI("mail_list", "mail_bcc").Split(',').ToList();
                    Email_class.BccList = bcc_name_list;
                }
                catch (Exception ex)
                {
                    writeToLog.WriteErrorLog($"[SendMailModel] 郵件清單讀取失敗: {ex.Message}");
                    Console.WriteLine($"[SendMailModel] 郵件清單讀取失敗: {ex.Message}");
                    return "FAIL";
                }
                List<string> filelist = new List<string>();
                try
                {
                    if (Email_class.SendEmail())
                    {
                        writeToLog.WriteToDataImportLog("寄信成功!");
                        Console.WriteLine("寄信成功!");
                        return "OK";
                    }
                    else
                    {
                        writeToLog.WriteErrorLog("寄信失敗!");
                        Console.WriteLine("寄信失敗!");
                        return "FAIL";
                    }
                }
                catch (Exception ex)
                {
                    writeToLog.WriteErrorLog($"[SendMailModel] 寄信執行失敗: {ex.Message}");
                    Console.WriteLine($"[SendMailModel] 寄信執行失敗: {ex.Message}");
                    return "FAIL";
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog($"[SendMailModel] 方法執行失敗: {ex.Message}");
                Console.WriteLine($"[SendMailModel] 方法執行失敗: {ex.Message}");
                return "FAIL";
            }
        }
        static string ImportTesterMode(FileProcess fileAccess, DbAccess dbAccess, DatabaseService DatabaseService)
        {
            WriteToLog writeToLog = new WriteToLog();
            try
            {
                #region 檢查一天內是否有資料
                try
                {
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
                }
                catch (Exception ex)
                {
                    writeToLog.WriteErrorLog($"[ImportTesterMode] 檢查一天內資料數量失敗: {ex.Message}");
                    Console.WriteLine($"[ImportTesterMode] 檢查一天內資料數量失敗: {ex.Message}");
                }
                #endregion
                List<DbKeyObject> dbKeyList = null;
                try
                {
                    dbKeyList = dbAccess.SelectDbKey(DatabaseService, "tester");
                }
                catch (Exception ex)
                {
                    writeToLog.WriteErrorLog($"[ImportTesterMode] 取得資料庫鍵值清單失敗: {ex.Message}");
                    Console.WriteLine($"[ImportTesterMode] 取得資料庫鍵值清單失敗: {ex.Message}");
                    return string.Empty;
                }
                RecoveryRate recoveryRate = new RecoveryRate();
                RawData rawData = new RawData();
                MultiSpecRawData multiSpecRawData = new MultiSpecRawData();
                Tester tester = new Tester();
                FailPin failPin = new FailPin();
                string updateImportStatus, remark;
                ImportResult importResult, importResult1, importResult2, importResult3;
                for (int i = 0; i < dbKeyList.Count; i++)
                {
                    try
                    {
                        Console.WriteLine((i + 1).ToString() + ".DB_Key=" + dbKeyList[i].DbKey + "  ");
                        // Recovery Rate processing with exception handling
                        try
                        {
                            if (dbKeyList[i].CheckStatus >= 8 && dbKeyList[i].CheckStatus <= 15 && dbKeyList[i].RecoveryRate == 0)
                            {
                                importResult = recoveryRate.ReadAndImportRecoveryRateData(fileAccess, DatabaseService, dbKeyList[i].DbKey).GetAwaiter().GetResult();
                            }
                            else
                            {
                                importResult = new ImportResult(dbKeyList[i].RecoveryRate, string.Empty);
                            }
                        }
                        catch (Exception ex)
                        {
                            writeToLog.WriteErrorLog($"[ImportTesterMode] Recovery Rate處理失敗, DB_Key: {dbKeyList[i].DbKey}, 錯誤: {ex.Message}");
                            Console.WriteLine($"[ImportTesterMode] Recovery Rate處理失敗, DB_Key: {dbKeyList[i].DbKey}, 錯誤: {ex.Message}");
                            importResult = new ImportResult(0, ex.Message);
                        }
                        // Raw Data processing with exception handling
                        try
                        {
                            if (dbKeyList[i].CheckStatus == 2 || dbKeyList[i].CheckStatus == 3 || dbKeyList[i].CheckStatus == 6 || dbKeyList[i].CheckStatus == 7 || dbKeyList[i].CheckStatus == 10 || dbKeyList[i].CheckStatus == 11 || dbKeyList[i].CheckStatus == 14 || dbKeyList[i].CheckStatus == 15)
                            {
                                if (dbKeyList[i].TestResult == 0)
                                {
                                    importResult2 = rawData.ReadAndImportRawData(fileAccess, DatabaseService, dbKeyList[i].DbKey).GetAwaiter().GetResult();
                                    if (importResult2.Result == 0)
                                    {
                                        // File not found
                                        importResult2 = multiSpecRawData.ReadAndImportMultiSpecRawData(fileAccess, DatabaseService, dbKeyList[i].DbKey).GetAwaiter().GetResult();
                                    }
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
                        }
                        catch (Exception ex)
                        {
                            writeToLog.WriteErrorLog($"[ImportTesterMode] Raw Data處理失敗, DB_Key: {dbKeyList[i].DbKey}, 錯誤: {ex.Message}");
                            Console.WriteLine($"[ImportTesterMode] Raw Data處理失敗, DB_Key: {dbKeyList[i].DbKey}, 錯誤: {ex.Message}");
                            importResult2 = new ImportResult(0, ex.Message);
                        }
                        // Tester Status processing with exception handling
                        try
                        {
                            if (dbKeyList[i].CheckStatus >= 4 && dbKeyList[i].CheckStatus <= 7 && dbKeyList[i].Tester == 0 || dbKeyList[i].CheckStatus >= 12 && dbKeyList[i].CheckStatus <= 15 && dbKeyList[i].Tester == 0)
                            {
                                importResult1 = tester.ReadAndImportTesterStatus(fileAccess, DatabaseService, dbKeyList[i].DbKey).GetAwaiter().GetResult();
                            }
                            else
                            {
                                importResult1 = new ImportResult(dbKeyList[i].Tester, string.Empty);
                            }
                        }
                        catch (Exception ex)
                        {
                            writeToLog.WriteErrorLog($"[ImportTesterMode] Tester Status處理失敗, DB_Key: {dbKeyList[i].DbKey}, 錯誤: {ex.Message}");
                            Console.WriteLine($"[ImportTesterMode] Tester Status處理失敗, DB_Key: {dbKeyList[i].DbKey}, 錯誤: {ex.Message}");
                            importResult1 = new ImportResult(0, ex.Message);
                        }
                        // Fail Pin processing with exception handling
                        try
                        {
                            if (dbKeyList[i].CheckStatus == 1 || dbKeyList[i].CheckStatus == 3 || dbKeyList[i].CheckStatus == 5 || dbKeyList[i].CheckStatus == 7 || dbKeyList[i].CheckStatus == 9 || dbKeyList[i].CheckStatus == 11 || dbKeyList[i].CheckStatus == 13 || dbKeyList[i].CheckStatus == 15)
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
                        }
                        catch (Exception ex)
                        {
                            writeToLog.WriteErrorLog($"[ImportTesterMode] Fail Pin處理失敗, DB_Key: {dbKeyList[i].DbKey}, 錯誤: {ex.Message}");
                            Console.WriteLine($"[ImportTesterMode] Fail Pin處理失敗, DB_Key: {dbKeyList[i].DbKey}, 錯誤: {ex.Message}");
                            importResult3 = new ImportResult(0, ex.Message);
                        }
                        // Update import status with exception handling
                        try
                        {
                            remark = string.Empty;
                            remark += (string.IsNullOrEmpty(importResult.Message)) ? string.Empty : "recovery rate: " + importResult.Message + "  ";
                            remark += (string.IsNullOrEmpty(importResult1.Message)) ? string.Empty : "tester: " + importResult1.Message + "  ";
                            remark += (string.IsNullOrEmpty(importResult2.Message)) ? string.Empty : "test result: " + importResult2.Message + "  ";
                            remark += (string.IsNullOrEmpty(importResult3.Message)) ? string.Empty : "fail pin: " + importResult3.Message;
                            updateImportStatus = dbAccess.UpdateDbKeyImportStatus(DatabaseService, dbKeyList[i].DbKey, importResult.Result, importResult1.Result, importResult2.Result, importResult3.Result, remark);
                            Console.WriteLine("Update tester import status:" + updateImportStatus);
                        }
                        catch (Exception ex)
                        {
                            writeToLog.WriteErrorLog($"[ImportTesterMode] 更新匯入狀態失敗, DB_Key: {dbKeyList[i].DbKey}, 錯誤: {ex.Message}");
                            Console.WriteLine($"[ImportTesterMode] 更新匯入狀態失敗, DB_Key: {dbKeyList[i].DbKey}, 錯誤: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        writeToLog.WriteErrorLog($"[ImportTesterMode] 處理DB_Key失敗: {(i < dbKeyList.Count ? dbKeyList[i].DbKey : "Unknown")}, 錯誤: {ex.Message}");
                        Console.WriteLine($"[ImportTesterMode] 處理DB_Key失敗: {(i < dbKeyList.Count ? dbKeyList[i].DbKey : "Unknown")}, 錯誤: {ex.Message}");
                    }
                }
                Console.WriteLine("Tester mode end~");
                #region 寄信通報
                try
                {
                    List<DbKeyObject> failDbKeyFromFile = dbAccess.SelectFailDbKeyFromFile();
                    // 通報
                    if (failDbKeyFromFile.Count > 0)
                    {
                        // 新的簡化版實作:
                        var details = failDbKeyFromFile.Select(x => $"DB_Key:{x.DbKey}, {x.Remark}").ToList();
                        bool sendResult = _notificationService.SendErrorNotification("下列資料發生異常，請確認檔案內容", details);
                        if (sendResult)
                        {
                            _notificationService.CleanupMailTempFiles();
                        }
                    }
                }
                catch (Exception ex)
                {
                    writeToLog.WriteErrorLog($"[ImportTesterMode] 寄信通報失敗: {ex.Message}");
                    Console.WriteLine($"[ImportTesterMode] 寄信通報失敗: {ex.Message}");
                }
                #endregion 找出需要通報的db_key
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog($"[ImportTesterMode] 執行失敗: {ex.Message}");
                Console.WriteLine($"[ImportTesterMode] 執行失敗: {ex.Message}");
            }
            return string.Empty;
        }
        static string ImportUiStatusMode(FileProcess fileAccess, DbAccess dbAccess, DatabaseService DatabaseService)
        {
            WriteToLog writeToLog = new WriteToLog();
            try
            {
                #region 檢查一天內是否有資料
                // Note: This section is commented out in original code
                #endregion
                List<DbKeyObject> dbKeyUiStatusList = null;
                try
                {
                    dbKeyUiStatusList = dbAccess.SelectDbKey(DatabaseService, "ui_status");
                }
                catch (Exception ex)
                {
                    writeToLog.WriteErrorLog($"[ImportUiStatusMode] 取得UI Status資料庫鍵值清單失敗: {ex.Message}");
                    Console.WriteLine($"[ImportUiStatusMode] 取得UI Status資料庫鍵值清單失敗: {ex.Message}");
                    return string.Empty;
                }
                UiStatus uiStatus = new UiStatus();
                string updateImportStatus, remark;
                ImportResult importResult4;
                for (int i = 0; i < dbKeyUiStatusList.Count; i++)
                {
                    try
                    {
                        Console.WriteLine((i + 1).ToString() + ".DB_Key_ui_status=" + dbKeyUiStatusList[i].DbKey + "  ");
                        try
                        {
                            importResult4 = uiStatus.ReadAndImportUIStatus(fileAccess, DatabaseService, dbKeyUiStatusList[i].DbKey);
                        }
                        catch (Exception ex)
                        {
                            writeToLog.WriteErrorLog($"[ImportUiStatusMode] UI Status讀取匯入失敗, DB_Key: {dbKeyUiStatusList[i].DbKey}, 錯誤: {ex.Message}");
                            Console.WriteLine($"[ImportUiStatusMode] UI Status讀取匯入失敗, DB_Key: {dbKeyUiStatusList[i].DbKey}, 錯誤: {ex.Message}");
                            importResult4 = new ImportResult(0, ex.Message);
                        }
                        try
                        {
                            remark = string.Empty;
                            remark += (string.IsNullOrEmpty(importResult4.Message)) ? string.Empty : "ui status:" + importResult4.Message;
                            updateImportStatus = dbAccess.UpdateDbKeyUiStatusImportStatus(DatabaseService, dbKeyUiStatusList[i].DbKey, importResult4.Result, remark);
                            Console.WriteLine("Update ui_status import status:" + updateImportStatus);
                        }
                        catch (Exception ex)
                        {
                            writeToLog.WriteErrorLog($"[ImportUiStatusMode] UI Status狀態更新失敗, DB_Key: {dbKeyUiStatusList[i].DbKey}, 錯誤: {ex.Message}");
                            Console.WriteLine($"[ImportUiStatusMode] UI Status狀態更新失敗, DB_Key: {dbKeyUiStatusList[i].DbKey}, 錯誤: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        writeToLog.WriteErrorLog($"[ImportUiStatusMode] 處理UI Status DB_Key失敗: {(i < dbKeyUiStatusList.Count ? dbKeyUiStatusList[i].DbKey : "Unknown")}, 錯誤: {ex.Message}");
                        Console.WriteLine($"[ImportUiStatusMode] 處理UI Status DB_Key失敗: {(i < dbKeyUiStatusList.Count ? dbKeyUiStatusList[i].DbKey : "Unknown")}, 錯誤: {ex.Message}");
                    }
                }
                Console.WriteLine("ui_status mode end~");
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog($"[ImportUiStatusMode] 執行失敗: {ex.Message}");
                Console.WriteLine($"[ImportUiStatusMode] 執行失敗: {ex.Message}");
            }
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
            WriteToLog writeToLog = new WriteToLog();
            try
            {
                ImportResult importResult;
                TsmcIeda tsmcIeda = new TsmcIeda();
                try
                {
                    importResult = tsmcIeda.ReadAndImportIeda(fileAccess, DatabaseService, string.Empty);
                    Console.WriteLine($"TSMC IEDA import completed with result: {importResult.Result}");
                    if (!string.IsNullOrEmpty(importResult.Message))
                    {
                        writeToLog.WriteInfoLog($"[ImportTsmcMode] TSMC IEDA匯入訊息: {importResult.Message}");
                        Console.WriteLine($"TSMC IEDA匯入訊息: {importResult.Message}");
                    }
                }
                catch (Exception ex)
                {
                    writeToLog.WriteErrorLog($"[ImportTsmcMode] TSMC IEDA匯入失敗: {ex.Message}");
                    Console.WriteLine($"[ImportTsmcMode] TSMC IEDA匯入失敗: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog($"[ImportTsmcMode] 執行失敗: {ex.Message}");
                Console.WriteLine($"[ImportTsmcMode] 執行失敗: {ex.Message}");
            }
            return string.Empty;
        }
    }
}
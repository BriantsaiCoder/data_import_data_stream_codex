using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static DCT_data_import.DbObject;

namespace DCT_data_import.Common
{
    /// <summary>
    /// 通知服務
    /// 統一管理郵件通知邏輯
    /// </summary>
    public class NotificationService
    {
        private readonly WriteToLog _writeToLog;

        public NotificationService()
        {
            _writeToLog = new WriteToLog();
        }

        /// <summary>
        /// 郵件配置類別
        /// </summary>
        public class MailConfiguration
        {
            public List<string> ToList { get; set; }
            public List<string> CcList { get; set; }
            public List<string> BccList { get; set; }

            public MailConfiguration()
            {
                ToList = new List<string>();
                CcList = new List<string>();
                BccList = new List<string>();
            }
        }

        /// <summary>
        /// 郵件內容類別
        /// </summary>
        public class MailContent
        {
            public string Subject { get; set; }
            public string Body { get; set; }
            public List<string> Attachments { get; set; }

            public MailContent()
            {
                Attachments = new List<string>();
            }
        }

        /// <summary>
        /// 發送郵件結果
        /// </summary>
        public class MailResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }

            public MailResult(bool success, string message)
            {
                Success = success;
                Message = message;
            }
        }

        /// <summary>
        /// 發送一般通知郵件
        /// </summary>
        public async Task<MailResult> SendNotificationAsync(string subject, string body)
        {
            try
            {
                var mailConfig = LoadMailConfiguration();
                var mailContent = new MailContent
                {
                    Subject = subject,
                    Body = body
                };

                return await SendMailAsync(mailConfig, mailContent);
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("發送通知郵件時發生錯誤: {0}", ex.Message));
                return new MailResult(false, ex.Message);
            }
        }

        /// <summary>
        /// 發送程式狀態通知
        /// </summary>
        public async Task<MailResult> SendProgramStatusNotificationAsync()
        {
            try
            {
                string subject = "DCT data notification - 正常運行中";
                string body = "Dear all,<br>DCT資料庫匯入程式正常執行中!<br>Thanks. <br>";

                return await SendNotificationAsync(subject, body);
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("發送程式狀態通知時發生錯誤: {0}", ex.Message));
                return new MailResult(false, ex.Message);
            }
        }

        /// <summary>
        /// 載入郵件配置
        /// </summary>
        private MailConfiguration LoadMailConfiguration()
        {
            try
            {
                string strAppPath = Assembly.GetExecutingAssembly().Location;
                string strWorkPath = Path.GetDirectoryName(strAppPath);
                ReadWriteINIfile readWriteINIfile = new ReadWriteINIfile(strWorkPath + "\\dct_import_mail_list.ini");

                var config = new MailConfiguration();

                // 讀取收件人清單
                string toListStr = readWriteINIfile.ReadINI("mail_list", "mail_to");
                if (!string.IsNullOrEmpty(toListStr))
                {
                    config.ToList = toListStr.Split(',').Where(email => !string.IsNullOrWhiteSpace(email)).ToList();
                }

                // 讀取副本清單
                string ccListStr = readWriteINIfile.ReadINI("mail_list", "mail_cc");
                if (!string.IsNullOrEmpty(ccListStr))
                {
                    config.CcList = ccListStr.Split(',').Where(email => !string.IsNullOrWhiteSpace(email)).ToList();
                }

                // 讀取密送清單
                string bccListStr = readWriteINIfile.ReadINI("mail_list", "mail_bcc");
                if (!string.IsNullOrEmpty(bccListStr))
                {
                    config.BccList = bccListStr.Split(',').Where(email => !string.IsNullOrWhiteSpace(email)).ToList();
                }

                return config;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("載入郵件配置時發生錯誤: {0}", ex.Message));
                return new MailConfiguration();
            }
        }

        /// <summary>
        /// 發送郵件
        /// </summary>
        private async Task<MailResult> SendMailAsync(MailConfiguration config, MailContent content)
        {
            try
            {
                return await Task.Run(() =>
                {
                    EmailModels emailModel = new EmailModels
                    {
                        Subject = content.Subject,
                        Body = content.Body,
                        ToList = config.ToList,
                        CCList = config.CcList,
                        BccList = config.BccList
                    };

                    bool result = emailModel.SendEmail();
                    string message = result ? "寄信成功!" : "寄信失敗!";
                    
                    _writeToLog.WriteToDataImportLog(message);
                    return new MailResult(result, message);
                });
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("發送郵件時發生錯誤: {0}", ex.Message));
                return new MailResult(false, ex.Message);
            }
        }

        /// <summary>
        /// 檢查是否應該發送程式狀態通知
        /// </summary>
        public bool ShouldSendProgramStatusNotification()
        {
            DateTime nowTime = DateTime.Now;
            return (int)nowTime.DayOfWeek == 1 && nowTime.Hour == 8 && nowTime.Minute < 10;
        }

        /// <summary>
        /// 清理郵件暫存檔
        /// </summary>
        public void CleanupMailTempFiles()
        {
            try
            {
                string logPath = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + @"\mail_temp.txt").LocalPath;
                if (File.Exists(logPath))
                {
                    File.Delete(logPath);
                    _writeToLog.WriteToDataImportLog("郵件暫存檔已清理");
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("清理郵件暫存檔時發生錯誤: {0}", ex.Message));
            }
        }
    }
}
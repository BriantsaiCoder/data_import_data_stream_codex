using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DCT_data_import.Common
{
    /// <summary>
    /// 簡化版通知服務 - 適合小型專案
    /// 移除過度設計的抽象層級，直接整合現有的郵件發送邏輯
    /// </summary>
    public class NotificationService
    {
        private readonly WriteToLog _writeToLog;

        public NotificationService()
        {
            _writeToLog = new WriteToLog();
        }

        /// <summary>
        /// 發送程式狀態通知
        /// </summary>
        public bool SendProgramStatusNotification()
        {
            try
            {
                string subject = "DCT data notification - 正常運行中";
                string body = "Dear all,<br>DCT資料庫匯入程式正常執行中!<br>Thanks. <br>";
                return SendNotification(subject, body);
            }
            catch (Exception ex)
            {
                _writeToLog.WriteErrorLog($"發送程式狀態通知時發生錯誤: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 發送異常通知
        /// </summary>
        /// <param name="errorMessage">錯誤訊息</param>
        /// <param name="details">詳細資料清單</param>
        public bool SendErrorNotification(string errorMessage, List<string> details = null)
        {
            try
            {
                string subject = BuildErrorNotificationSubject(details);
                string body = BuildErrorNotificationBody(errorMessage, details);
                return SendNotification(subject, body);
            }
            catch (Exception ex)
            {
                _writeToLog.WriteErrorLog($"發送異常通知時發生錯誤: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 發送資料遺失警告通知
        /// </summary>
        /// <param name="dataType">資料類型 (如: "Tester", "ui_status")</param>
        public bool SendDataMissingNotification(string dataType)
        {
            try
            {
                string subject = $"DCT data notification - {dataType} 資料遺失警告";
                string body = $"Dear all,<br><br>{dataType} 已超過1天無資料匯入，請確認!<br><br>Thanks.";
                return SendNotification(subject, body);
            }
            catch (Exception ex)
            {
                _writeToLog.WriteErrorLog($"發送資料遺失通知時發生錯誤: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 統一的郵件發送方法
        /// </summary>
        private bool SendNotification(string subject, string body)
        {
            try
            {
                string result = SendMailModelInternal(body, subject);
                return result == "OK";
            }
            catch (Exception ex)
            {
                _writeToLog.WriteErrorLog($"發送通知失敗: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 建立錯誤通知的主旨
        /// </summary>
        private string BuildErrorNotificationSubject(List<string> details)
        {
            if (details == null || details.Count == 0)
            {
                return "DCT data notification - 處理異常";
            }

            // 統計 Remark 出現次數（模仿原始邏輯）
            var remarkCounts = new Dictionary<string, int>();
            foreach (var detail in details)
            {
                // 假設 detail 格式為 "DB_Key:xxx, Remark"，提取 Remark 部分
                var parts = detail.Split(',');
                if (parts.Length >= 2)
                {
                    var remark = parts[1].Trim();
                    remarkCounts[remark] = remarkCounts.ContainsKey(remark) ? remarkCounts[remark] + 1 : 1;
                }
            }

            if (remarkCounts.Count > 0)
            {
                var remarkSummary = string.Join(", ", remarkCounts.Select(kvp => $"{kvp.Key} x {kvp.Value}"));
                return $"DCT data notification - {remarkSummary}";
            }

            return "DCT data notification - 處理異常";
        }

        /// <summary>
        /// 建立錯誤通知的內容
        /// </summary>
        private string BuildErrorNotificationBody(string errorMessage, List<string> details)
        {
            var body = $"Dear all,<br>{errorMessage}<br>";

            if (details != null && details.Count > 0)
            {
                for (int i = 0; i < details.Count; i++)
                {
                    body += $"{i + 1}. {details[i]}<br>";
                }
            }

            body += "Thanks. <br>";
            return body;
        }

        /// <summary>
        /// 整合現有的 SendMailModel 邏輯
        /// 這是從 Program.cs 複製過來的邏輯，移除對靜態方法的依賴
        /// </summary>
        private string SendMailModelInternal(string mailBody, string mailTitle)
        {
            try
            {
                // 獲取執行檔路徑
                string strAppPath = Assembly.GetExecutingAssembly().Location;
                string strWorkPath = Path.GetDirectoryName(strAppPath);

                // 讀取郵件清單配置
                ReadWriteINIfile readWriteINIfile = new ReadWriteINIfile(strWorkPath + "\\dct_import_mail_list.ini");

                // 建立郵件物件
                EmailModels emailModel = new EmailModels
                {
                    Subject = mailTitle,
                    Body = mailBody
                };

                // 設定收件人清單
                string toListStr = readWriteINIfile.ReadINI("mail_list", "mail_to");
                if (!string.IsNullOrEmpty(toListStr))
                {
                    emailModel.ToList = toListStr.Split(',').Where(email => !string.IsNullOrWhiteSpace(email)).ToList();
                }

                // 設定副本清單
                string ccListStr = readWriteINIfile.ReadINI("mail_list", "mail_cc");
                if (!string.IsNullOrEmpty(ccListStr))
                {
                    emailModel.CCList = ccListStr.Split(',').Where(email => !string.IsNullOrWhiteSpace(email)).ToList();
                }

                // 設定密送清單
                string bccListStr = readWriteINIfile.ReadINI("mail_list", "mail_bcc");
                if (!string.IsNullOrEmpty(bccListStr))
                {
                    emailModel.BccList = bccListStr.Split(',').Where(email => !string.IsNullOrWhiteSpace(email)).ToList();
                }

                // 發送郵件
                if (emailModel.SendEmail())
                {
                    _writeToLog.WriteInfoLog("寄信成功!");
                    return "OK";
                }
                else
                {
                    _writeToLog.WriteErrorLog("寄信失敗!");
                    return "FAIL";
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteErrorLog($"SendMailModelInternal 發生錯誤: {ex.Message}");
                return "ERROR";
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
            // DryRun(影子驗證):不刪除 mail_temp.txt(影子試跑保留待寄信暫存,供事後比對)。
            if (RuntimeMode.IsDryRun)
            {
                return;
            }
            try
            {
                string logPath = Path.Combine(AppContext.BaseDirectory, "mail_temp.txt");
                if (File.Exists(logPath))
                {
                    File.Delete(logPath);
                    _writeToLog.WriteInfoLog("郵件暫存檔已清理");
                }
            }
            catch (Exception ex)
            {
                _writeToLog.WriteErrorLog($"清理郵件暫存檔時發生錯誤: {ex.Message}");
            }
        }
    }
}

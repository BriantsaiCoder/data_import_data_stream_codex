using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Text;
namespace DCT_data_import
{
    public class EmailModels
    {
        public string Subject { get; set; }
        public string Body { get; set; }
        public List<string> ToList { get; set; }
        public List<string> CCList { get; set; }
        public List<string> BccList { get; set; }
        public List<string> FileList { get; set; }
        public string SendResult { get; set; }
        public bool SendEmail()
        {
            // DryRun(影子驗證):在 Ping 郵件伺服器前就回 true(視為寄信成功),不連網、不送信。
            // 回 true 讓上層 SendMailModelInternal(NotificationService.cs:191)視為成功,不會走失敗分支。
            if (RuntimeMode.IsDryRun)
            {
                SendResult = "DryRun: email send skipped";
                return true;
            }
            // SMTP 伺服器位址外部化至 App.config(S4);TryParse 在 try 外,未設定/格式錯都回 false
            // (不丟未捕捉例外 crash 輪詢執行緒;null 亦回 false),行為對齊下方「Connect email server fail」失敗路徑。
            string ipString = ConfigurationManager.AppSettings["SmtpServer"];
            if (!IPAddress.TryParse(ipString, out IPAddress tIP))
            {
                SendResult = "SMTP 伺服器位址未設定或格式錯誤(App.config SmtpServer)";
                return false;
            }
            using (Ping tPingControl = new Ping())
            {
                PingReply tReply = tPingControl.Send(tIP);
                if (tReply.Status != IPStatus.Success)
                {
                    SendResult = "Connect email server fail!";
                    return false;
                }
            }
            // 寄件人外部化至 App.config(S4)。net462 無 MailAddress.TryParse,故以小範圍 try 預先建立並驗證,
            // 與上方伺服器位址守衛對稱:位址 null/空/格式錯、顯示名 null/空都回一致的明確設定錯誤訊息,
            // 不落到主 try 的通用 catch 被誤標為「未預期的錯誤」,也避免與收件人位址解析錯誤混淆。
            MailAddress fromAddress;
            try
            {
                fromAddress = new MailAddress(
                    ConfigurationManager.AppSettings["SmtpFromAddress"],
                    ConfigurationManager.AppSettings["SmtpFromDisplayName"],
                    Encoding.UTF8);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
            {
                SendResult = "寄件人設定未設定或格式錯誤(App.config SmtpFromAddress / SmtpFromDisplayName)";
                Console.WriteLine($"[EmailModels] 寄件人設定錯誤: {ex.Message}");
                return false;
            }
            try
            {
                using (MailMessage mailObj = new MailMessage())
                using (SmtpClient mysmtp = new SmtpClient(tIP.ToString()))
                {
                    //設定編碼
                    mailObj.SubjectEncoding = Encoding.UTF8;
                    //設定標題
                    mailObj.Subject = Subject;
                    //設定內文
                    mailObj.Body = Body;
                    //設定寄件人(已於上方守衛預先驗證,見 fromAddress)
                    mailObj.From = fromAddress;
                    //設定to名單
                    for (int i = 0; i < ToList.Count; i++)
                    {
                        mailObj.To.Add(ToList[i]);
                    }
                    //設定cc名單
                    if (CCList != null)
                    {
                        for (int i = 0; i < CCList.Count; i++)
                        {
                            mailObj.CC.Add(CCList[i]);
                        }
                    }
                    //設定bcc名單
                    if (BccList != null)
                    {
                        for (int i = 0; i < BccList.Count; i++)
                        {
                            mailObj.Bcc.Add(BccList[i]);
                        }
                    }
                    //設定夾檔
                    if (FileList != null)
                    {
                        for (int i = 0; i < FileList.Count; i++)
                        {
                            if (!File.Exists(FileList[i]))
                            {
                                SendResult = $"附件檔案不存在: {FileList[i]}";
                                return false;
                            }
                            Console.WriteLine(FileList[i]);
                            mailObj.Attachments.Add(new Attachment(FileList[i]));
                        }
                    }
                    //寄信
                    mailObj.IsBodyHtml = true;
                    mysmtp.Send(mailObj);
                }
                SendResult = "Sent the email success!";
                return true;
            }
            catch (SmtpException ex)
            {
                SendResult = $"SMTP 錯誤: {ex.Message}";
                Console.WriteLine($"[EmailModels] SMTP 錯誤: {ex.Message}");
                return false;
            }
            catch (FileNotFoundException ex)
            {
                SendResult = $"找不到附件檔案: {ex.FileName}";
                Console.WriteLine($"[EmailModels] 找不到附件檔案: {ex.FileName}");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                SendResult = $"檔案存取權限不足: {ex.Message}";
                Console.WriteLine($"[EmailModels] 檔案存取權限不足: {ex.Message}");
                return false;
            }
            catch (ArgumentException ex)
            {
                SendResult = $"參數錯誤: {ex.Message}";
                Console.WriteLine($"[EmailModels] 參數錯誤: {ex.Message}");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                SendResult = $"操作無效: {ex.Message}";
                Console.WriteLine($"[EmailModels] 操作無效: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                SendResult = $"未預期的錯誤: {ex.Message}";
                Console.WriteLine($"[EmailModels] 未預期的錯誤: {ex.Message}");
                return false;
            }
        }
    }
}
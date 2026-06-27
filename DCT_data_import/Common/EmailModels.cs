using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Linq;
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
            // 收件人清單為部署設定(INI mail_to);null(未設定)/空/全為空白皆屬設定錯誤。先濾掉空白項:
            // 部分 caller(Program.cs 的 legacy SendMailModel)只做 Split(',') 未過濾,mail_to=","/",," 會
            // 產生含空字串的 list(Count>0),不濾會在下方 mailObj.To.Add("") 丟例外、落到主 try 的 catch 被誤標。
            // 在 Ping 前以濾後清單明確攔下並回明確訊息(對齊 S4 設定錯誤模式),也省去無收件人時的無謂連網。
            List<string> toRecipients = ToList?.Where(addr => !string.IsNullOrWhiteSpace(addr)).ToList();
            if (toRecipients == null || toRecipients.Count == 0)
            {
                SendResult = "收件人清單為空(dct_import_mail_list.ini mail_list/mail_to)";
                Console.WriteLine($"[EmailModels] {SendResult}");
                return false;
            }
            // SMTP 伺服器位址外部化至 App.config(S4);TryParse 在 try 外,未設定/格式錯都回 false
            // (不丟未捕捉例外 crash 輪詢執行緒;null 亦回 false),行為對齊下方「Connect email server fail」失敗路徑。
            string ipString = ConfigurationManager.AppSettings["SmtpServer"];
            if (!IPAddress.TryParse(ipString, out IPAddress tIP))
            {
                SendResult = "SMTP 伺服器位址未設定或格式錯誤(App.config SmtpServer)";
                Console.WriteLine($"[EmailModels] {SendResult}");
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
            // 寄件人外部化至 App.config(S4)。位址與顯示名皆為部署設定,缺漏屬部署錯誤、不是執行期輸入。
            // 先以 IsNullOrWhiteSpace 明確守衛兩者,不依賴 MailAddress 對 null/空顯示名的框架語意——該語意
            // 在「文件契約 vs 實際 source」有已知落差;顯式守衛使行為不依賴 BCL 細節。
            // 再建立 MailAddress 以攔位址格式錯(FormatException)。任一錯誤都回明確
            // 設定錯誤訊息,不落到主 try 的通用 catch 被誤標為「未預期的錯誤」,也不與收件人位址解析錯誤混淆。
            string fromAddrCfg = ConfigurationManager.AppSettings["SmtpFromAddress"];
            string fromNameCfg = ConfigurationManager.AppSettings["SmtpFromDisplayName"];
            if (string.IsNullOrWhiteSpace(fromAddrCfg) || string.IsNullOrWhiteSpace(fromNameCfg))
            {
                SendResult = "寄件人設定未設定(App.config SmtpFromAddress / SmtpFromDisplayName)";
                Console.WriteLine($"[EmailModels] {SendResult}");
                return false;
            }
            MailAddress fromAddress;
            try
            {
                fromAddress = new MailAddress(fromAddrCfg, fromNameCfg, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
            {
                SendResult = "寄件人設定格式錯誤(App.config SmtpFromAddress / SmtpFromDisplayName)";
                Console.WriteLine($"[EmailModels] 寄件人設定錯誤: {ex.Message}");
                return false;
            }
            try
            {
                using (MailMessage mailObj = new MailMessage())
                // host 用 tIP.ToString()(正規化位址)與上方 Ping 目標一致,勿改回原始字串以免兩者分歧
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
                    //設定to名單(已於上方守衛濾除空白項,見 toRecipients)
                    for (int i = 0; i < toRecipients.Count; i++)
                    {
                        mailObj.To.Add(toRecipients[i]);
                    }
                    //設定cc名單(逐項濾空白:cc 屬 optional,空/全空白即不設定;未過濾的 caller(Program.cs
                    //SendMailModel)mail_cc 含空字串時,CC.Add("") 會丟 ArgumentException 讓整封信失敗)
                    if (CCList != null)
                    {
                        for (int i = 0; i < CCList.Count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(CCList[i]))
                            {
                                mailObj.CC.Add(CCList[i]);
                            }
                        }
                    }
                    //設定bcc名單(同 cc:逐項濾空白,空/全空白即不設定,不讓 Bcc.Add("") 丟例外)
                    if (BccList != null)
                    {
                        for (int i = 0; i < BccList.Count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(BccList[i]))
                            {
                                mailObj.Bcc.Add(BccList[i]);
                            }
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

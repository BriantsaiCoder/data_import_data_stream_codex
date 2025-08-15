using System;
using System.Collections.Generic;
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
            string ipString = string.Empty;
            IPAddress tIP = IPAddress.Parse(ipString);
            using (Ping tPingControl = new Ping())
            {
                PingReply tReply = tPingControl.Send(tIP);
                if (tReply.Status != IPStatus.Success)
                {
                    SendResult = "Connect email server fail!";
                    return false;
                }
            }
            try
            {
                using (MailMessage mailObj = new MailMessage())
                using (SmtpClient mysmtp = new SmtpClient(ipString))
                {
                    //設定編碼
                    mailObj.SubjectEncoding = Encoding.UTF8;
                    //設定標題
                    mailObj.Subject = Subject;
                    //設定內文
                    mailObj.Body = Body;
                    //設定寄件人
                    mailObj.From = new MailAddress("CTRD5900@aseglobal.com", "CTRD Sender", Encoding.UTF8);
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
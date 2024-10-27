using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
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
        public Boolean SendEmail()
        {
            int i = 0;
            IPAddress tIP = IPAddress.Parse("10.12.10.31");
            Ping tPingControl = new Ping();
            PingReply tReply = tPingControl.Send(tIP);
            tPingControl.Dispose();
            if (tReply.Status == IPStatus.Success)
            {
                string tolog = string.Empty;
                string cclog = string.Empty;
                string bcclog = string.Empty;
                string filelog = string.Empty;
                try
                {
                    MailMessage mailObj = new MailMessage();
                    //設定編碼
                    mailObj.SubjectEncoding = Encoding.UTF8;
                    //設定標題
                    mailObj.Subject = Subject;
                    //設定內文
                    mailObj.Body = Body;
                    //設定寄件人
                    mailObj.From = new MailAddress("CTRD5900@aseglobal.com", "CTRD Sender", System.Text.Encoding.UTF8);
                    //設定to名單
                    for (i = 0; i < ToList.Count(); i++)
                    {
                        mailObj.To.Add(ToList[i]);
                        if (i == 0)
                        {
                            tolog = ToList[i];
                        }
                        else
                        {
                            tolog = tolog + "," + ToList[i];
                        }
                    }
                    //設定cc名單
                    if (CCList != null)
                    {
                        for (i = 0; i < CCList.Count(); i++)
                        {
                            mailObj.CC.Add(CCList[i]);
                            if (i == 0)
                            {
                                cclog = CCList[i];
                            }
                            else
                            {
                                cclog = cclog + "," + CCList[i];
                            }
                        }
                    }
                    //設定bcc名單
                    if (BccList != null)
                    {
                        for (i = 0; i < BccList.Count(); i++)
                        {
                            mailObj.Bcc.Add(BccList[i]);
                            if (i == 0)
                            {
                                bcclog = BccList[i];
                            }
                            else
                            {
                                bcclog = bcclog + "," + BccList[i];
                            }
                        }
                    }
                    //設定夾檔
                    if (FileList != null)
                    {
                        for (i = 0; i < FileList.Count(); i++)
                        {
                            Console.WriteLine(FileList[i]);
                            mailObj.Attachments.Add(new System.Net.Mail.Attachment(FileList[i]));
                            if (i == 0)
                            {
                                filelog = FileList[i];
                            }
                            else
                            {
                                filelog = filelog + "," + FileList[i];
                            }
                        }
                    }
                    //寄信
                    mailObj.IsBodyHtml = true;
                    SmtpClient mysmtp = new SmtpClient("10.12.10.31");
                    mysmtp.Send(mailObj);
                }
                catch (Exception ex)
                {
                    SendResult = ex.ToString();
                    return false;
                }
            }
            else
            {
                SendResult = "Connect email server fail!";
                return false;
            }
            SendResult = "Sent the email success!";
            return true;
        }
    }
}
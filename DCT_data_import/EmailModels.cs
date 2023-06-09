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
        public string subject { get; set; }
        public string body { get; set; }
        public List<string> tomanlist { get; set; }
        public List<string> cclist { get; set; }
        public List<string> bcclist { get; set; }
        public List<string> filelist { get; set; }
        public string sResult { get; set; }

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
                    mailObj.Subject = subject;
                    //設定內文
                    mailObj.Body = body;
                    //設定寄件人
                    mailObj.From = new MailAddress("CTRD5900@aseglobal.com", "CTRD Sender", System.Text.Encoding.UTF8);
                    //設定to名單
                    for (i = 0; i < tomanlist.Count(); i++)
                    {
                        mailObj.To.Add(tomanlist[i]);
                        if (i == 0)
                        {
                            tolog = tomanlist[i];
                        }
                        else
                        {
                            tolog = tolog + "," + tomanlist[i];
                        }

                    }

                    //設定cc名單
                    if (cclist != null)
                    {
                        for (i = 0; i < cclist.Count(); i++)
                        {
                            mailObj.CC.Add(cclist[i]);
                            if (i == 0)
                            {
                                cclog = cclist[i];
                            }
                            else
                            {
                                cclog = cclog + "," + cclist[i];
                            }
                        }
                    }

                    //設定bcc名單
                    if (bcclist != null)
                    {
                        for (i = 0; i < bcclist.Count(); i++)
                        {
                            mailObj.Bcc.Add(bcclist[i]);
                            if (i == 0)
                            {
                                bcclog = bcclist[i];
                            }
                            else
                            {
                                bcclog = bcclog + "," + bcclist[i];
                            }
                        }
                    }

                    //設定夾檔
                    if (filelist != null)
                    {
                        for (i = 0; i < filelist.Count(); i++)
                        {
                            Console.WriteLine(filelist[i]);
                            mailObj.Attachments.Add(new System.Net.Mail.Attachment(filelist[i]));
                            if (i == 0)
                            {
                                filelog = filelist[i];
                            }
                            else
                            {
                                filelog = filelog + "," + filelist[i];
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
                    sResult = ex.ToString();
                    return false;
                }


            }
            else
            {
                sResult = "Connect email server fail!";
                return false;
            }



            sResult = "Sent the email success!";
            return true;
        }
    }
}

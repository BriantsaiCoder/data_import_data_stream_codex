using System.Collections.Generic;
using DCT_data_import;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// 釘住 NI-3 收件人守衛契約:<see cref="EmailModels.ToList"/> 為 null/空時,<see cref="EmailModels.SendEmail"/>
    /// 須在 Ping 郵件伺服器前回 false 並給明確「收件人清單為空」設定錯誤訊息(對齊 S4 設定錯誤回報模式),
    /// 不可落到主 try 的通用 catch 被誤標為「未預期的錯誤」。
    /// seam:守衛置於 Ping 之前,測試不打網路;明確設 DryRun=false 確保走守衛而非 DryRun 短路。
    /// </summary>
    [Collection("RuntimeMode")]
    public class EmailRecipientGuardTests : System.IDisposable
    {
        public void Dispose() => RuntimeMode.SetDryRunOverrideForTests(null);

        [Fact]
        public void SendEmail_WhenToListNull_ReturnsConfigError_WithoutNetwork()
        {
            RuntimeMode.SetDryRunOverrideForTests(false);

            var model = new EmailModels { Subject = "t", Body = "b", ToList = null };
            bool ok = model.SendEmail();

            Assert.False(ok, "收件人為 null 應回 false");
            Assert.Contains("收件人", model.SendResult);
            Assert.DoesNotContain("未預期", model.SendResult);
        }

        [Fact]
        public void SendEmail_WhenToListEmpty_ReturnsConfigError_WithoutNetwork()
        {
            RuntimeMode.SetDryRunOverrideForTests(false);

            // mail_to 只含逗號/空白時,NotificationService 的 Split+Where 會濾成空 list(非 null);
            // 守衛須一併涵蓋,否則空收件人會送到 SmtpClient.Send 丟 InvalidOperationException。
            var model = new EmailModels { Subject = "t", Body = "b", ToList = new List<string>() };
            bool ok = model.SendEmail();

            Assert.False(ok, "收件人清單為空應回 false");
            Assert.Contains("收件人", model.SendResult);
            Assert.DoesNotContain("未預期", model.SendResult);
        }

        [Fact]
        public void SendEmail_WhenToListAllBlank_ReturnsConfigError_WithoutNetwork()
        {
            RuntimeMode.SetDryRunOverrideForTests(false);

            // Program.cs 的 legacy caller 只做 Split(',') 未過濾,mail_to=","/",," 會得到含空字串
            // 的 list(Count>0);守衛須把全空白視為無收件人,否則空位址送到 SmtpClient.To.Add 會丟例外。
            var model = new EmailModels { Subject = "t", Body = "b", ToList = new List<string> { "", "  " } };
            bool ok = model.SendEmail();

            Assert.False(ok, "收件人全為空白應回 false");
            Assert.Contains("收件人", model.SendResult);
            Assert.DoesNotContain("未預期", model.SendResult);
        }
    }
}

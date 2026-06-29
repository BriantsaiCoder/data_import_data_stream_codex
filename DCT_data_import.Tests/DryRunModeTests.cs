using System;
using System.IO;
using DCT_data_import;
using DCT_data_import.Common;
using DCT_data_import.MySqlApi;
using DCT_data_import.ReadAndImport;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// 釘住 P1-7b 影子驗證(DRY-RUN)契約:<see cref="RuntimeMode.IsDryRun"/> 為 true 時,六個副作用 chokepoint
    /// (DB 非 select 寫入 / FTP DeleteFile / FTP RenameFile / 寄信 / mail_temp 清理)一律被短路、回 no-op 成功,
    /// 且不依賴真實 MySQL / FTP / SMTP;DryRun=false(預設)時行為與遷移前一致(以 SELECT 仍照常走連線檢查為憑)。
    /// seam:以 <see cref="RuntimeMode.SetDryRunOverrideForTests"/> 覆寫旗標,測試不打外部資源。
    /// </summary>
    [Collection("RuntimeMode")]
    public class DryRunModeTests : IDisposable
    {
        public void Dispose() => RuntimeMode.SetDryRunOverrideForTests(null);

        [Fact]
        public void ExecuteCommand_WhenDryRun_ReturnsTypedNoOpSuccess_WithoutConnection()
        {
            RuntimeMode.SetDryRunOverrideForTests(true);

            var response = new DBmysql().ExecuteCommand("INSERT INTO dummy(x) VALUES(1)");

            Assert.True(string.IsNullOrEmpty(response.Error),
                $"DryRun command 應回 no-op 成功(Error 空),實際 Error='{response.Error}'");
            Assert.Equal(0, response.AffectedRows);
            Assert.Equal(0, response.InsertId);
        }

        [Fact]
        public void ExecuteQuery_WhenDryRunAndSelect_NotShortCircuited()
        {
            RuntimeMode.SetDryRunOverrideForTests(true);

            // 不變式守衛:DryRun 不可短路讀取,SELECT 仍須照常走連線檢查(未連線 -> 回初始化錯誤)。
            var response = new DBmysql().ExecuteQuery("SELECT 1");

            Assert.False(string.IsNullOrEmpty(response.Error),
                "DryRun 不應短路 SELECT,未連線時應回初始化錯誤");
        }

        [Fact]
        public void DeleteFile_WhenDryRun_ReturnsSkipMessage_WithoutFtp()
        {
            RuntimeMode.SetDryRunOverrideForTests(true);

            var result = new ImportData().DeleteFile("ftp://dryrun.invalid/f.csv", "user", "pwd");

            Assert.Contains("DryRun", result);
        }

        [Fact]
        public void RenameFile_WhenDryRun_ReturnsSkipMessage_WithoutFtp()
        {
            RuntimeMode.SetDryRunOverrideForTests(true);

            var result = new ImportData().RenameFile(
                "ftp://dryrun.invalid/a.csv", "ftp://dryrun.invalid/b.csv", "user", "pwd");

            Assert.Contains("DryRun", result);
        }

        [Fact]
        public void SendEmail_WhenDryRun_ReturnsTrue_WithoutNetwork()
        {
            RuntimeMode.SetDryRunOverrideForTests(true);

            // 正常路徑開頭會 Ping 郵件伺服器;DryRun 須在 Ping 前回 true,測試才不依賴網路。
            var ok = new EmailModels { Subject = "t", Body = "b" }.SendEmail();

            Assert.True(ok, "DryRun 寄信應回 true(跳過實際送信)");
        }

        [Fact]
        public void CleanupMailTempFiles_WhenDryRun_DoesNotDeleteFile()
        {
            RuntimeMode.SetDryRunOverrideForTests(true);
            string path = Path.Combine(AppContext.BaseDirectory, "mail_temp.txt");
            File.WriteAllText(path, "shadow run marker");
            try
            {
                new NotificationService().CleanupMailTempFiles();
                Assert.True(File.Exists(path), "DryRun 不應刪除 mail_temp.txt");
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}

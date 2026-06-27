using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// 隱性資料契約守門:<c>App.config</c> 的 key 必須與 <see cref="Program"/> 在 type-init 階段讀的 key 完全對齊。
    ///
    /// <para>背景:<see cref="Program"/> 的 8 個 static 欄位(HOST/USER/PASSWORD/PORT/DATABASE/FTP_*)在型別初始化時
    /// (即 <c>Main</c> 第一行之前、且在 <c>Main</c> 的 try/catch 之外)就讀 <c>ConfigurationManager</c>。任一 key 缺漏 →
    /// 索引器回 null → <c>ConnectionStrings[...].ConnectionString</c> 擲 NRE → 包成 <c>TypeInitializationException</c> →
    /// 服務一啟動就 crash,且攔不到。<c>Environment</c> 由執行期 IP 決定(Dev/Prod 二選一),故 Dev 與 Prod
    /// 兩組 key 都必須存在。</para>
    ///
    /// <para>為何要測:這條契約編譯器與 grep 都抓不到(key 是執行期字串),只有實際讀 config 才驗得出。
    /// net8 升級會重生 config(<c>.exe.config</c>→<c>.dll.config</c>),本測試讀「出貨的」config
    /// (主組件旁的 <c>*.config</c>),確保遷移不漏 key。</para>
    ///
    /// <para>A4 後專案只保留 net8.0-windows;本測試直接驗主組件旁的出貨 config,
    /// 確保 App.config 在 SDK-style build 後仍隨產品組件複製。</para>
    ///
    /// 注意:不可寫成直接讀 <c>Program.HOST</c> 的測試——測試行程載入的是 <c>DCT_data_import.Tests.dll.config</c>
    /// (不含這些 key),那樣會假紅。本測試刻意讀主組件旁的 config 檔。
    /// </summary>
    public class AppConfigContractTests
    {
        // 鏡射 Program.cs:19-26 的讀取;Environment 執行期決定,故 Dev/Prod 兩組都要在
        private static readonly string[] EnvPrefixes = { "Dev", "Prod" };
        private static readonly string[] AppSettingSuffixes = { "Host", "UserName", "Password", "Port", "Database" };
        private static readonly string[] ConnectionStringNames = { "FtpIp", "FtpUser", "FtpPassword" };

        [Fact]
        public void ShippedConfig_ContainsEveryKeyProgramReadsAtTypeInit()
        {
            string configPath = typeof(Program).Assembly.Location + ".config";
            Assert.True(File.Exists(configPath),
                $"找不到主組件旁的設定檔(預期 {configPath});build 未複製 App.config 會讓服務啟動即 TypeInitializationException。");

            XDocument doc = XDocument.Load(configPath);

            HashSet<string> appKeys = new HashSet<string>(doc
                .Descendants("appSettings").Elements("add")
                .Select(e => (string)e.Attribute("key"))
                .Where(k => k != null));

            HashSet<string> connNames = new HashSet<string>(doc
                .Descendants("connectionStrings").Elements("add")
                .Select(e => (string)e.Attribute("name"))
                .Where(n => n != null));

            var missing = new List<string>();
            foreach (string prefix in EnvPrefixes)
            {
                foreach (string suffix in AppSettingSuffixes)
                {
                    string key = prefix + suffix;
                    if (!appKeys.Contains(key))
                    {
                        missing.Add($"appSettings/{key}");
                    }
                }
            }
            foreach (string name in ConnectionStringNames)
            {
                if (!connNames.Contains(name))
                {
                    missing.Add($"connectionStrings/{name}");
                }
            }

            Assert.True(missing.Count == 0,
                "出貨設定檔缺少 Program type-init 會讀的 key(缺 → 啟動即 TypeInitializationException):" +
                Environment.NewLine + string.Join(Environment.NewLine, missing));
        }
    }
}

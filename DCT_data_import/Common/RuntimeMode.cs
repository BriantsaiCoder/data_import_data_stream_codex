using System;
using System.Configuration;

namespace DCT_data_import
{
    /// <summary>
    /// 影子驗證(DRY-RUN)旗標。為 true 時 ETL 只解析/比對,不真正寫入 DB、不搬/刪 FTP 檔、不寄信,
    /// 供 .NET 8 cutover 的影子試跑(Q4)與資料污染回滾(Rollback L3)防護。預設 false(維持正式行為)。
    /// </summary>
    /// <remarks>
    /// 由 App.config 的 <c>DryRun</c> appSetting 驅動:缺 key → <see cref="string.Equals(string,string,StringComparison)"/>
    /// 比對 null 得 false → fail-safe OFF(未設定就照正常 production 跑,不會意外進影子模式)。
    /// 刻意不用 <c>bool.Parse</c>(缺 key 會擲例外),且自帶 static、不掛進 Program 的 type-init 鏈,
    /// 與既有 TypeInitializationException 脆弱性隔離。
    /// </remarks>
    internal static class RuntimeMode
    {
        private static readonly bool _configDryRun =
            string.Equals(ConfigurationManager.AppSettings["DryRun"], "true", StringComparison.OrdinalIgnoreCase);

        private static bool? _overrideForTests;

        /// <summary>影子驗證是否啟用。</summary>
        public static bool IsDryRun => _overrideForTests ?? _configDryRun;

        /// <summary>
        /// 測試專用 seam:覆寫 <see cref="IsDryRun"/>。傳 null 還原為 App.config 設定值。正式程式碼勿用。
        /// </summary>
        internal static void SetDryRunOverrideForTests(bool? value) => _overrideForTests = value;
    }
}

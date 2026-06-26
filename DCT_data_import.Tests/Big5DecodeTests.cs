using System.Text;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause A(net8 升級閘門):net8 預設不含 codepage 950(big5),
    /// <c>Encoding.GetEncoding("big5")</c> / <c>GetEncoding(950)</c> 會擲 <see cref="System.ArgumentException"/>,
    /// 除非啟動時 <c>Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)</c>
    /// (需 <c>System.Text.Encoding.CodePages</c> 套件)。全 importer 的 FTP CSV 解碼都走 big5。
    ///
    /// 本測試在 net462 上以 committed big5 bytes 做解碼 smoke:net462 內建 codepage 950 → 解出
    /// "測試資料"(綠燈)。升級到 net8 後、未註冊 provider 前本測試會「先紅」——這正是 root cause A 的
    /// 升級閘門訊號:看到此測試在 net8 變紅,就知道要在啟動處註冊 CodePagesEncodingProvider。
    ///
    /// 與 capture 測試不同,本檔是「硬斷言 smoke」(big5 解碼語意框架不變、net462 上必綠),非 capture-don't-assert,
    /// 故不掛 CaptureBaseline、留在綠燈門檻內當升級閘門。provider 註冊本身屬遷移階段工作,本階段(net462 基準)
    /// 不引入 <c>System.Text.Encoding.CodePages</c> 套件、不改啟動碼(對齊 Option A / Q2 決策)。
    /// </summary>
    public class Big5DecodeTests
    {
        // 由 iconv 產生的權威 big5(cp950)位元組:B4FA=測 B8D5=試 B8EA=資 AEC6=料。
        // 純語意樣本、不含真實憑證 / lot 資料(對齊策略 §1 fixtures 規範)。
        private static readonly byte[] Big5Bytes =
            { 0xB4, 0xFA, 0xB8, 0xD5, 0xB8, 0xEA, 0xAE, 0xC6 };

        [Fact]
        public void GetEncoding_Big5_DecodesCommittedBytes_OnNet462()
        {
            Encoding big5 = Encoding.GetEncoding("big5");

            string decoded = big5.GetString(Big5Bytes);

            Assert.Equal("測試資料", decoded);
        }

        [Fact]
        public void GetEncoding_Codepage950_IsAvailable_OnNet462()
        {
            // 與上題等價但走數字 codepage(部分呼叫點以 950 取得)。net8 未註冊 provider 時同樣會擲例外。
            Encoding cp950 = Encoding.GetEncoding(950);

            Assert.Equal("測試資料", cp950.GetString(Big5Bytes));
        }
    }
}

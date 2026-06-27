using System.Text;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause A(net8 codepage provider):net8 預設不含 codepage 950(big5),
    /// <c>Encoding.GetEncoding("big5")</c> / <c>GetEncoding(950)</c> 會擲 <see cref="System.ArgumentException"/>,
    /// 除非啟動時 <c>Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)</c>
    /// (需 <c>System.Text.Encoding.CodePages</c> 套件)。全 importer 的 FTP CSV 解碼都走 big5。
    ///
    /// 本測試以 committed big5 bytes 做解碼 smoke,確保測試行程已透過
    /// <see cref="EncodingTestBootstrap"/> 註冊 provider,避免 FTP CSV 解析在 net8 上退化。
    /// </summary>
    public class Big5DecodeTests
    {
        // 由 iconv 產生的權威 big5(cp950)位元組:B4FA=測 B8D5=試 B8EA=資 AEC6=料。
        // 純語意樣本、不含真實憑證 / lot 資料(對齊策略 §1 fixtures 規範)。
        private static readonly byte[] Big5Bytes =
            { 0xB4, 0xFA, 0xB8, 0xD5, 0xB8, 0xEA, 0xAE, 0xC6 };

        [Fact]
        public void GetEncoding_Big5_DecodesCommittedBytes_OnNet8()
        {
            Encoding big5 = Encoding.GetEncoding("big5");

            string decoded = big5.GetString(Big5Bytes);

            Assert.Equal("測試資料", decoded);
        }

        [Fact]
        public void GetEncoding_Codepage950_IsAvailable_OnNet8()
        {
            // 與上題等價但走數字 codepage(部分呼叫點以 950 取得)。net8 未註冊 provider 時同樣會擲例外。
            Encoding cp950 = Encoding.GetEncoding(950);

            Assert.Equal("測試資料", cp950.GetString(Big5Bytes));
        }
    }
}

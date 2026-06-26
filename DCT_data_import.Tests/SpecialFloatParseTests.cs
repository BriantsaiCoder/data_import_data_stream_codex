using System.Data;
using System.Globalization;
using DCT_data_import;
using Xunit;
using Xunit.Abstractions;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause B(double.TryParse 特殊浮點語意)的特性化 capture。
    ///
    /// 原假設:net462 的 <c>double.TryParse</c> 會接受 Windows CRT 舊式 token(<c>-1.#IND</c>/
    /// <c>1.#QNAN</c>/<c>1.#INF</c>/<c>-1.#INF</c>)、net8 一律拒收。**CI golden-master capture
    /// 推翻此假設**:兩框架皆拒收這些 CRT token(parsed=false → out 0);<c>NaN</c>/<c>Infinity</c>/
    /// <c>-Infinity</c> 兩框架皆接受。唯一真正翻轉的是 <c>1E400</c>——net462 溢位成 +∞
    /// (parsed=true、bits=0x7FF0000000000000),net8 解析失敗(回 false → out 0)。此差異會悄悄改變
    /// 下游數值:net462 把 1E400 當 +∞ 一路流進 <see cref="CalculateSPC"/>。
    ///
    /// 本檔全屬 capture-don't-assert(<c>[Trait("Category","CaptureBaseline")]</c>):只把 net462 實跑值
    /// 經 <see cref="ITestOutputHelper"/> 印出當基準,**不硬斷言**。net462 真實值要等 CI(windows-latest)
    /// capture step 跑出來才回填,即使結果出乎意料也照貼(對齊策略 §4)。升級 net8 後同檔再跑、比對輸出差異
    /// = 回歸訊號。本類測試由綠燈門檻濾除(`Category!=CaptureBaseline`),只在專屬 capture step 收集。
    /// </summary>
    public class SpecialFloatParseTests
    {
        private readonly ITestOutputHelper output;

        public SpecialFloatParseTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        // 涵蓋 net462/net8 解析語意可能翻轉的代表 token。InvariantCulture 排除 culture 雜訊,
        // 純粹釘住「特殊浮點 token」這一維的框架差異。
        [Theory]
        [InlineData("-1.#IND")]
        [InlineData("1.#QNAN")]
        [InlineData("1.#INF")]
        [InlineData("-1.#INF")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("-Infinity")]
        [InlineData("1E400")]
        [Trait("Category", "CaptureBaseline")]
        public void TryParse_SpecialFloatToken_CaptureNet462Semantics(string token)
        {
            bool ok = double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value);

            // capture-only:印出 (token → 成功與否 / 解析值 / bit pattern),不斷言。
            output.WriteLine(
                $"token=\"{token}\" parsed={ok} value={value.ToString("R", CultureInfo.InvariantCulture)} " +
                $"bits=0x{System.BitConverter.DoubleToInt64Bits(value):X16}");
        }

        /// <summary>
        /// B 的下游後果特性化:把 <c>-1.#IND</c> 餵進 <see cref="CalculateSPC.AverageOfSumSquare"/>。
        /// 原假設:net462 把 token 解析成 NaN → <c>Convert.ToDecimal(NaN*NaN)</c> 擲 OverflowException
        /// → catch 後回空 list(count=0);net8 解析失敗跳過 → count=1。**CI golden-master 推翻**:
        /// 兩框架皆拒收 <c>-1.#IND</c>(見類別註解),故該 token 一律被跳過、兩框架皆算出 1 筆
        /// (count=1, pass_n=3, avg=2, avg2≈4.667)——此案無 TFM 差異。
        /// fixture 純為數值樣本、不含真實 lot / 憑證(對齊策略 §1)。
        /// </summary>
        [Fact]
        [Trait("Category", "CaptureBaseline")]
        public void AverageOfSumSquare_WithSpecialFloatToken_CaptureNet462Behavior()
        {
            var content = new RawDataContentFormat();
            DataTable table = new DataTable();
            table.Columns.Add("value");
            table.Columns.Add("# of FAIL");
            table.Columns.Add("Spec MAX");
            table.Columns.Add("Spec MIN");
            table.Rows.Add("[1,2,-1.#IND,3]", "0", "", "");
            content.LotStatistic.Tables.Add(table);

            var result = new CalculateSPC().AverageOfSumSquare(content);

            output.WriteLine($"item_count={result.Count}");
            for (int i = 0; i < result.Count; i++)
            {
                output.WriteLine(
                    $"item[{i}] pass_n={result[i].pass_n} avg={result[i].avg} avg2={result[i].avg2}");
            }
        }
    }
}

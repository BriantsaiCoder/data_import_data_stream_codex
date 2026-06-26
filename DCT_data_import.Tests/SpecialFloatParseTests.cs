using System.Data;
using System.Globalization;
using DCT_data_import;
using Xunit;
using Xunit.Abstractions;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause B(double.TryParse 特殊浮點語意翻轉)的特性化 capture。
    ///
    /// net462 的 <c>double.TryParse</c> 接受 Windows CRT 舊式 token:<c>-1.#IND</c>(→NaN)、
    /// <c>1.#QNAN</c>(→NaN)、<c>1.#INF</c>(→+∞)、<c>-1.#INF</c>(→-∞);net8(及 netcoreapp)
    /// 一律解析失敗(回 false → out 0)。`NaN`/`Infinity`/`-Infinity` 兩框架皆接受;`1E400` 兩框架
    /// 皆溢位成 +∞。這層翻轉會悄悄改變下游數值:解析成功的 NaN 會一路流進 <see cref="CalculateSPC"/>。
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
        /// net462 路徑:token 解析成 NaN → <c>Convert.ToDecimal(NaN*NaN)</c> 擲 OverflowException →
        /// catch 後回「該表之前累積的項」(單表故為空 list)。net8 路徑:token 解析失敗被跳過 →
        /// 正常算出 1 筆(pass_n=3, avg=2)。故本 capture 在 net462 應印出 count=0,net8 印出 count=1。
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

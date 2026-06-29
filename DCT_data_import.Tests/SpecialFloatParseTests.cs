using System.Data;
using System.Globalization;
using DCT_data_import;
using DCT_data_import.Common;
using DCT_data_import.FileAccess;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause B(double.TryParse 特殊浮點語意)的 net8 特性化測試。
    ///
    /// 原假設:net462 的 <c>double.TryParse</c> 會接受 Windows CRT 舊式 token(<c>-1.#IND</c>/
    /// <c>1.#QNAN</c>/<c>1.#INF</c>/<c>-1.#INF</c>)。capture 推翻此假設:net8 拒收這些 CRT token
    /// (parsed=false → out 0),但標準 <c>NaN</c>/<c>Infinity</c>/<c>-Infinity</c> 皆接受。
    /// A4 固化後亦釘住 <c>1E400</c> 在 InvariantCulture 下溢位成 +∞ 的目前行為。
    ///
    /// A4 後專案只保留 net8.0-windows;這裡改為固定斷言 net8 的解析語意,避免 capture-only 測試繼續空轉。
    /// </summary>
    public class SpecialFloatParseTests
    {
        // 涵蓋特殊浮點 token。InvariantCulture 排除 culture 雜訊,純粹釘住此維度的 net8 行為。
        [Theory]
        [InlineData("-1.#IND", false, "0000000000000000")]
        [InlineData("1.#QNAN", false, "0000000000000000")]
        [InlineData("1.#INF", false, "0000000000000000")]
        [InlineData("-1.#INF", false, "0000000000000000")]
        [InlineData("NaN", true, "FFF8000000000000")]
        [InlineData("Infinity", true, "7FF0000000000000")]
        [InlineData("-Infinity", true, "FFF0000000000000")]
        [InlineData("1E400", true, "7FF0000000000000")]
        public void TryParse_SpecialFloatToken_ReturnsExpectedNet8Semantics(string token, bool expectedParsed, string expectedBits)
        {
            bool ok = double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value);

            Assert.Equal(expectedParsed, ok);
            Assert.Equal(expectedBits, System.BitConverter.DoubleToInt64Bits(value).ToString("X16"));
        }

        /// <summary>
        /// B 的下游後果特性化:把 <c>-1.#IND</c> 餵進 <see cref="CalculateSPC.AverageOfSumSquare"/>。
        /// 舊式 token <c>-1.#IND</c> 在 net8 解析失敗,故被跳過;此測試釘住下游結果
        /// (count=1, pass_n=3, avg=2, avg2≈4.667)。
        /// fixture 純為數值樣本、不含真實 lot / 憑證(對齊策略 §1)。
        /// </summary>
        [Fact]
        public void AverageOfSumSquare_WithSpecialFloatToken_ReturnsExpectedNet8Behavior()
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

            Assert.Single(result);
            Assert.Equal(3, result[0].pass_n);
            Assert.Equal("2", result[0].avg.ToString(CultureInfo.InvariantCulture));
            Assert.Equal("4.6666666666666666666666666667", result[0].avg2.ToString(CultureInfo.InvariantCulture));
        }
    }
}

using System.Globalization;
using Xunit;
using Xunit.Abstractions;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause C(double→string 預設格式化規則改變)的特性化 capture。
    ///
    /// net462 的 <c>double.ToString()</c>(無格式)用 "G15"——最多 15 位有效數字;net8(netcoreapp3.0+)
    /// 改用 IEEE-754「最短可往返(shortest round-trippable)」表示。例:<c>(1.0/3.0).ToString()</c>
    /// net462 → "0.333333333333333"(15 個 3),net8 → "0.3333333333333333"(16 個 3)。
    /// 這會直接改變 SQL literal 的字面值。
    ///
    /// 為何直接特性化 <c>double.ToString()</c> 而非抽 FileProcess 的 DoubleToSqlString seam:
    /// FileProcess 兩處 double→字串轉換點(賦值點 ~265 的 <c>Convert.ToString(double)</c>、
    /// 讀取點 ~314/617 的 boxed double <c>item.ToString()</c>)在 CurrentCulture 下皆等價於
    /// <c>double.ToString(CurrentCulture)</c>;直接特性化此 BCL 行為可證等價且同時覆蓋兩條路徑,
    /// 免動高扇入的 FileProcess(對齊策略 §5 的「評估後免動」修訂)。注意 AVG 欄走
    /// decimal(<c>Math.Round(decimal,9)</c>)→string 屬格式穩定、非本風險點;只有 avg_2
    /// (decimal→double→string)落入 C。
    ///
    /// 全屬 capture-don't-assert(<c>[Trait("Category","CaptureBaseline")]</c>):印出 net462 實跑字串
    /// + 不變的 bit pattern(證明底層 double 值兩框架一致、差異純在格式化)當基準,**不硬斷言**;
    /// 真實值待 CI capture step 回填。綠燈門檻濾除本類。
    /// </summary>
    public class DoubleToStringFormatTests
    {
        private readonly ITestOutputHelper output;

        public DoubleToStringFormatTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public static System.Collections.Generic.IEnumerable<object[]> RepresentativeDoubles()
        {
            // 涵蓋:無限小數(1/3 系列)、二進位無法精確表示的小數(0.1/0.3)、
            // 經 Math.Round(9) 的值、大整數小數、極大/極小指數、邊界小數。
            yield return new object[] { 1.0 / 3.0 };
            yield return new object[] { 2.0 / 3.0 };
            yield return new object[] { -1.0 / 3.0 };
            yield return new object[] { 0.1 };
            yield return new object[] { 0.3 };
            yield return new object[] { System.Math.Round(1.0 / 3.0, 9) };
            yield return new object[] { 123456789.123456789 };
            yield return new object[] { 1e21 };
            yield return new object[] { 1e-7 };
            yield return new object[] { 0.0001 };
        }

        [Theory]
        [MemberData(nameof(RepresentativeDoubles))]
        [Trait("Category", "CaptureBaseline")]
        public void DoubleToString_CaptureNet462Format(double value)
        {
            // 預設 ToString()(吃 CurrentCulture,等同 FileProcess 路徑)+ Invariant(排除 culture 雜訊)
            // + bit pattern(底層值錨,證明差異純在格式化而非數值本身)。
            output.WriteLine(
                $"bits=0x{System.BitConverter.DoubleToInt64Bits(value):X16} " +
                $"default=\"{value.ToString()}\" " +
                $"invariant=\"{value.ToString(CultureInfo.InvariantCulture)}\"");
        }
    }
}

using System.Globalization;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause C(double→string 預設格式化規則)的 net8 特性化測試。
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
    /// A4 後專案只保留 net8.0-windows;這裡固定斷言 net8 格式化語意。
    /// </summary>
    public class DoubleToStringFormatTests
    {
        public static System.Collections.Generic.IEnumerable<object[]> RepresentativeDoubles()
        {
            // 涵蓋:無限小數(1/3 系列)、二進位無法精確表示的小數(0.1/0.3)、
            // 經 Math.Round(9) 的值、大整數小數、極大/極小指數、邊界小數。
            yield return new object[] { 1.0 / 3.0, "3FD5555555555555", "0.3333333333333333" };
            yield return new object[] { 2.0 / 3.0, "3FE5555555555555", "0.6666666666666666" };
            yield return new object[] { -1.0 / 3.0, "BFD5555555555555", "-0.3333333333333333" };
            yield return new object[] { 0.1, "3FB999999999999A", "0.1" };
            yield return new object[] { 0.3, "3FD3333333333333", "0.3" };
            yield return new object[] { System.Math.Round(1.0 / 3.0, 9), "3FD5555554F9B516", "0.333333333" };
            yield return new object[] { 123456789.123456789, "419D6F34547E6B75", "123456789.12345679" };
            yield return new object[] { 1e21, "444B1AE4D6E2EF50", "1E+21" };
            yield return new object[] { 1e-7, "3E7AD7F29ABCAF48", "1E-07" };
            yield return new object[] { 0.0001, "3F1A36E2EB1C432D", "0.0001" };
        }

        [Theory]
        [MemberData(nameof(RepresentativeDoubles))]
        public void DoubleToString_ReturnsExpectedNet8Format(double value, string expectedBits, string expectedText)
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

                Assert.Equal(expectedBits, System.BitConverter.DoubleToInt64Bits(value).ToString("X16"));
                Assert.Equal(expectedText, value.ToString());
                Assert.Equal(expectedText, value.ToString(CultureInfo.InvariantCulture));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }
    }
}

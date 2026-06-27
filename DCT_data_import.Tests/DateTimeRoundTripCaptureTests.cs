using System;
using System.Globalization;
using System.Threading;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause D 在生產 datetime literal **寫入點**的 parse+format round-trip net8 特性化測試。
    ///
    /// <see cref="FileProcess"/> FileProcess.cs:804-806 對 <c>start_time</c>/<c>end_time</c> 欄先以**無 culture
    /// 參數**的 <c>DateTime.TryParse(cell.Trim(), out datetime)</c>(2-arg → 吃 <see cref="CultureInfo.CurrentCulture"/>)
    /// 解析,成功才 <c>datetime.ToString("yyyy-MM-dd HH:mm:ss")</c> 寫進 SQL,失敗則寫 <c>null</c>(:810)。
    /// <b>format 腿低漂移</b>(明確數字格式、無月名),真正風險在**上游 parse 腿**:
    /// 模糊日期字串的 parse 成功/失敗會決定寫 datetime 還是 <c>null</c> fallback。
    /// 本檔釘住 net8 的「(parsedOk, literal 或 null)」合成結果。
    ///
    /// 附帶釘住 <c>hh</c>-vs-<c>HH</c> 12h 截斷:<see cref="FileProcess"/>.<c>CustomizeDateTimeParser</c>(:60/:65)
    /// 用 <c>hh</c>(12 小時、無 <c>tt</c>),把 <c>13:00</c> 寫成 <c>01:00</c>——<b>屬既有
    /// correctness bug</b>。本測試只釘住既有行為,不在 A4 內順手修。
    ///
    /// 純 BCL 重現(<c>DateTime.TryParse</c>+<c>ToString</c>),<b>零 production 改動</b>。
    /// A4 後專案只保留 net8.0-windows;這裡固定斷言 net8 的 parse/format 結果。
    /// </summary>
    public class DateTimeRoundTripCaptureTests
    {
        public static System.Collections.Generic.IEnumerable<object[]> DateInputsAndCultures()
        {
            // ISO、美式 MM/dd/yyyy、斜線無補零、空、非日期、破折號歧義——涵蓋 parse 成功/失敗翻轉面。
            string[] inputs = { "2022-06-06 13:08:22", "06/07/2022", "2022/6/6", "", "NA", "6-7-2022" };
            string[] cultures = { "en-US", "zh-TW", "" /* InvariantCulture */ };
            foreach (string input in inputs)
            {
                foreach (string culture in cultures)
                {
                    (bool parsedOk, string literal) = Expected(input);
                    yield return new object[] { input, culture, parsedOk, literal };
                }
            }
        }

        private static (bool ParsedOk, string Literal) Expected(string input)
        {
            if (input == "" || input == "NA")
                return (false, "null");
            if (input == "2022-06-06 13:08:22")
                return (true, "2022-06-06 13:08:22");
            if (input == "2022/6/6")
                return (true, "2022-06-06 00:00:00");
            return (true, "2022-06-07 00:00:00");
        }

        /// <summary>
        /// (1) 鏡像 FileProcess.cs:804-806 的 parse→format round-trip:2-arg <c>TryParse</c>(CurrentCulture)
        /// 後成功印 <c>HH</c> literal、失敗印 <c>null</c>(對齊 :810 fallback)。
        /// </summary>
        [Theory]
        [MemberData(nameof(DateInputsAndCultures))]
        public void ParseFormatRoundTrip_UnderCulture_ReturnsExpectedNet8Result(
            string input,
            string culture,
            bool expectedParsedOk,
            string expectedLiteral)
        {
            CultureInfo target = string.IsNullOrEmpty(culture)
                ? CultureInfo.InvariantCulture
                : new CultureInfo(culture);

            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = target;

                bool parsedOk = DateTime.TryParse(input == null ? null : input.Trim(), out DateTime dt);
                string literal = parsedOk ? dt.ToString("yyyy-MM-dd HH:mm:ss") : "null";

                Assert.Equal(expectedParsedOk, parsedOk);
                Assert.Equal(expectedLiteral, literal);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        /// <summary>
        /// (2) hh-vs-HH 12h 截斷基準:固定下午時間,印 <c>HH</c>(24h、生產 :806 用)與 <c>hh</c>(12h、
        /// CustomizeDateTimeParser :60/:65 用)兩種渲染。釘住既有 bug,非 A4 行為變更。
        /// </summary>
        [Fact]
        public void HourFormat_HH_vs_hh_ReturnsExpectedNet8TruncationBehavior()
        {
            DateTime pm = new DateTime(2022, 6, 6, 13, 8, 22);

            Assert.Equal("2022-06-06 13:08:22", pm.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Assert.Equal("2022-06-06 01:08:22", pm.ToString("yyyy-MM-dd hh:mm:ss", CultureInfo.InvariantCulture));
        }
    }
}

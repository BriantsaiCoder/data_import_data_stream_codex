using System.Globalization;
using System.Threading;
using DCT_data_import;
using DCT_data_import.FileAccess;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// DateTime 解析漂移(findings A/J)的回歸防護:<see cref="FileProcess.CustomizeDateTimeParser"/>
    /// 修復後的 culture-invariant 斷言。
    ///
    /// 該方法以 <c>_</c> 切成 6 段、重組為 "MMM dd yyyy H:m:s" 後,以
    /// <c>DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)</c> 解析,
    /// 成功回 <c>dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)</c>(24 小時制、
    /// culture-invariant)。parse 與 format 兩腿皆綁 InvariantCulture,使結果不受執行緒
    /// <see cref="CultureInfo.CurrentCulture"/> 影響(對齊 FileProcess 的 ValidateDateTime)。
    ///
    /// 本檔固定下午/午夜邊界(<c>23:59:59</c>、<c>00:00:00</c>)防 <c>hh</c>(12 小時制)回歸;
    /// 並含 <c>ar-SA</c> 案例:修復前其 parse 失敗→回非決定性 <c>DateTime.Now</c>、format 退化為
    /// Hijri 曆(年份 1443),修復後回固定的西曆 24h literal。
    /// </summary>
    public class DateTimeParserCultureTests
    {
        public static System.Collections.Generic.IEnumerable<object[]> DateInputsAndCultures()
        {
            // 真實 importer 觀察到的格式 "MMM_dd_yyyy_HH_mm_ss"(6 段)+ 兩個變體。
            string[] inputs = { "Jun_06_2022_12_08_22", "Dec_31_2021_23_59_59", "Mar_01_2023_00_00_00" };
            // ar-SA(Umm al-Qura 曆)固定住跨 culture 不變性:修復前其 parse 腿會漂移。
            string[] cultures = { "en-US", "zh-TW", "" /* InvariantCulture */, "ar-SA" };
            foreach (string input in inputs)
            {
                foreach (string culture in cultures)
                {
                    yield return new object[] { input, culture, ExpectedResult(input) };
                }
            }
        }

        private static string ExpectedResult(string input)
        {
            if (input.StartsWith("Jun_06_2022"))
                return "2022-06-06 12:08:22";
            if (input.StartsWith("Dec_31_2021"))
                return "2021-12-31 23:59:59";
            return "2023-03-01 00:00:00";
        }

        [Theory]
        [MemberData(nameof(DateInputsAndCultures))]
        public void CustomizeDateTimeParser_UnderCulture_ReturnsExpectedNet8Result(string input, string culture, string expectedResult)
        {
            CultureInfo target = string.IsNullOrEmpty(culture)
                ? CultureInfo.InvariantCulture
                : new CultureInfo(culture);

            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = target;
                string result = new FileProcess().CustomizeDateTimeParser(input);
                Assert.Equal(expectedResult, result);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}

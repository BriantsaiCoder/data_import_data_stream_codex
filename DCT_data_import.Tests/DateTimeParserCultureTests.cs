using System.Globalization;
using System.Threading;
using DCT_data_import;
using DCT_data_import.FileAccess;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause D(DateTime 解析漂移)的 net8 特性化測試。
    ///
    /// <see cref="FileProcess.CustomizeDateTimeParser"/>(FileProcess.cs:52)以 <c>_</c> 切成 6 段、
    /// 重組為 "MMM dd yyyy H:m:s" 後呼叫 <c>DateTime.TryParse(s, out dt)</c>——**無 culture 參數**,
    /// 吃當前執行緒 <see cref="CultureInfo.CurrentCulture"/>。成功則回
    /// <c>dt.ToString("yyyy-MM-dd hh:mm:ss")</c>(注意 <c>hh</c> 為 12 小時制、無 AM/PM),
    /// **失敗則回 <c>DateTime.Now</c>(非決定性)**。
    /// (對照:FileProcess.cs:68 的 ValidateDateTime 用 InvariantCulture,culture-safe、不在此風險面。)
    ///
    /// A4 後專案只保留 net8.0-windows;這裡固定斷言 net8 的 CurrentCulture 解析結果。
    /// </summary>
    public class DateTimeParserCultureTests
    {
        public static System.Collections.Generic.IEnumerable<object[]> DateInputsAndCultures()
        {
            // 真實 importer 觀察到的格式 "MMM_dd_yyyy_HH_mm_ss"(6 段)+ 兩個變體。
            string[] inputs = { "Jun_06_2022_12_08_22", "Dec_31_2021_23_59_59", "Mar_01_2023_00_00_00" };
            string[] cultures = { "en-US", "zh-TW", "" /* InvariantCulture */ };
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
                return "2021-12-31 11:59:59";
            return "2023-03-01 12:00:00";
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

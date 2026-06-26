using System.Globalization;
using System.Threading;
using DCT_data_import;
using Xunit;
using Xunit.Abstractions;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause D(DateTime 解析漂移)的特性化 capture。
    ///
    /// <see cref="FileProcess.CustomizeDateTimeParser"/>(FileProcess.cs:52)以 <c>_</c> 切成 6 段、
    /// 重組為 "MMM dd yyyy H:m:s" 後呼叫 <c>DateTime.TryParse(s, out dt)</c>——**無 culture 參數**,
    /// 吃當前執行緒 <see cref="CultureInfo.CurrentCulture"/>。net462 走 Windows NLS、net8 走 ICU,
    /// 對「Jun 之類英文月份縮寫在非英語 culture 下能否解析」可能給出不同答案;成功則回
    /// <c>dt.ToString("yyyy-MM-dd hh:mm:ss")</c>(注意 <c>hh</c> 為 12 小時制、無 AM/PM),
    /// **失敗則回 <c>DateTime.Now</c>(非決定性)**——這正是只能 capture、不能硬斷言的原因。
    /// (對照:FileProcess.cs:68 的 ValidateDateTime 用 InvariantCulture,culture-safe、不在此風險面。)
    ///
    /// 全屬 capture-don't-assert(<c>[Trait("Category","CaptureBaseline")]</c>):在 en-US / zh-TW /
    /// Invariant 三種 CurrentCulture 下印出實跑回傳。判讀提示:回傳含輸入年份(如 2022-)= 解析成功;
    /// 回傳為「今天日期」= 落入 <c>DateTime.Now</c> fallback(該 culture 解析失敗)。升級 net8 後比對:
    /// 原本成功的 (input,culture) 若變成今天日期 = 解析語意翻轉的回歸訊號。綠燈門檻濾除本類。
    /// </summary>
    public class DateTimeParserCultureTests
    {
        private readonly ITestOutputHelper output;

        public DateTimeParserCultureTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public static System.Collections.Generic.IEnumerable<object[]> DateInputsAndCultures()
        {
            // 真實 importer 觀察到的格式 "MMM_dd_yyyy_HH_mm_ss"(6 段)+ 兩個變體。
            string[] inputs =
            {
                "Jun_06_2022_12_08_22",
                "Dec_31_2021_23_59_59",
                "Mar_01_2023_00_00_00",
            };
            string[] cultures = { "en-US", "zh-TW", "" /* InvariantCulture */ };
            foreach (string input in inputs)
            {
                foreach (string culture in cultures)
                {
                    yield return new object[] { input, culture };
                }
            }
        }

        [Theory]
        [MemberData(nameof(DateInputsAndCultures))]
        [Trait("Category", "CaptureBaseline")]
        public void CustomizeDateTimeParser_UnderCulture_CaptureNet462Result(string input, string culture)
        {
            CultureInfo target = string.IsNullOrEmpty(culture)
                ? CultureInfo.InvariantCulture
                : new CultureInfo(culture);

            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = target;
                string result = new FileProcess().CustomizeDateTimeParser(input);
                output.WriteLine(
                    $"input=\"{input}\" culture=\"{(string.IsNullOrEmpty(culture) ? "Invariant" : culture)}\" " +
                    $"result=\"{result}\"");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}

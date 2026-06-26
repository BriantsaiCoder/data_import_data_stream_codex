using System;
using System.Globalization;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause D 在生產 datetime literal **寫入點**的 parse+format round-trip 特性化 capture。
    ///
    /// <see cref="FileProcess"/> FileProcess.cs:804-806 對 <c>start_time</c>/<c>end_time</c> 欄先以**無 culture
    /// 參數**的 <c>DateTime.TryParse(cell.Trim(), out datetime)</c>(2-arg → 吃 <see cref="CultureInfo.CurrentCulture"/>)
    /// 解析,成功才 <c>datetime.ToString("yyyy-MM-dd HH:mm:ss")</c> 寫進 SQL,失敗則寫 <c>null</c>(:810)。
    /// <b>format 腿低漂移</b>(明確數字格式、無月名故 NLS-vs-ICU 不適用),真正風險在**上游 parse 腿**:
    /// net462(NLS)vs net8(ICU)對模糊日期字串的 parse 成功/失敗可能翻轉 → 決定寫 datetime 還是
    /// <c>null</c> fallback。本檔 capture「(parsedOk, literal 或 null)」這個合成結果。
    ///
    /// 附帶釘住 <c>hh</c>-vs-<c>HH</c> 12h 截斷:<see cref="FileProcess"/>.<c>CustomizeDateTimeParser</c>(:60/:65)
    /// 用 <c>hh</c>(12 小時、無 <c>tt</c>),把 <c>13:00</c> 寫成 <c>01:00</c>——<b>兩框架皆同、不漂移,但屬既有
    /// correctness bug</b>。capture 下基準,避免升級後 diff 把這條既有 bug 誤算到 net8 頭上。
    /// ⚠️ <c>hh</c> 為 <b>flagged pre-existing bug,本升級 task 內不修</b>(CLAUDE.md:不順手改既有行為)。
    ///
    /// 純 BCL 重現(<c>DateTime.TryParse</c>+<c>ToString</c>),<b>零 production 改動</b>——不去 seam 私有
    /// SQL-builder。全屬 capture-don't-assert(<c>[Trait("Category","CaptureBaseline")]</c>):經
    /// <see cref="ITestOutputHelper"/> 印 net462 實跑值當基準、不硬斷言;真實值待 CI(windows-latest)
    /// capture step 回填。升級 net8 後同檔再跑、逐列 diff = 回歸訊號。綠燈門檻濾除本類。
    /// </summary>
    public class DateTimeRoundTripCaptureTests
    {
        private readonly ITestOutputHelper output;

        public DateTimeRoundTripCaptureTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public static System.Collections.Generic.IEnumerable<object[]> DateInputsAndCultures()
        {
            // ISO、美式 MM/dd/yyyy、斜線無補零、空、非日期、破折號歧義——涵蓋 parse 成功/失敗翻轉面。
            string[] inputs =
            {
                "2022-06-06 13:08:22",
                "06/07/2022",
                "2022/6/6",
                "",
                "NA",
                "6-7-2022",
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

        /// <summary>
        /// (1) 鏡像 FileProcess.cs:804-806 的 parse→format round-trip:2-arg <c>TryParse</c>(CurrentCulture)
        /// 後成功印 <c>HH</c> literal、失敗印 <c>null</c>(對齊 :810 fallback)。
        /// </summary>
        [Theory]
        [MemberData(nameof(DateInputsAndCultures))]
        [Trait("Category", "CaptureBaseline")]
        public void ParseFormatRoundTrip_UnderCulture_CaptureNet462Result(string input, string culture)
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

                output.WriteLine(
                    $"input=\"{input}\" culture=\"{(string.IsNullOrEmpty(culture) ? "Invariant" : culture)}\" " +
                    $"parsedOk={parsedOk} literal=\"{literal}\"");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        /// <summary>
        /// (2) hh-vs-HH 12h 截斷基準:固定下午時間,印 <c>HH</c>(24h、生產 :806 用)與 <c>hh</c>(12h、
        /// CustomizeDateTimeParser :60/:65 用)兩種渲染。兩框架皆同;釘住既有 bug,非升級漂移。
        /// </summary>
        [Fact]
        [Trait("Category", "CaptureBaseline")]
        public void HourFormat_HH_vs_hh_CaptureTruncationBaseline()
        {
            DateTime pm = new DateTime(2022, 6, 6, 13, 8, 22);

            output.WriteLine(
                $"source=\"2022-06-06 13:08:22\" " +
                $"HH=\"{pm.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}\" " +
                $"hh=\"{pm.ToString("yyyy-MM-dd hh:mm:ss", CultureInfo.InvariantCulture)}\"  " +
                $"// hh 為既有 12h 截斷 bug,升級不修");
        }
    }
}

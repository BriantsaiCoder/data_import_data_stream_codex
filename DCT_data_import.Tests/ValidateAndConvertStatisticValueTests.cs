using System.Globalization;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause B(parse)＋C(format)在生產**合成點**的特性化 capture。
    ///
    /// <see cref="FileProcess"/>.<c>ValidateAndConvertStatisticValue</c>(FileProcess.cs:1515)是 B 與 C
    /// 唯一合成處:先以**無 culture 參數**的 <c>double.TryParse(trimmed, out result)</c>(:1527,2-arg
    /// overload → 吃 <see cref="CultureInfo.CurrentCulture"/> 的 <see cref="NumberStyles.Float"/>|
    /// <c>AllowThousands</c>)把字串轉 double(B 腿),回傳值經呼叫端 :260 的 <c>validatedValue.ToString()</c>
    /// (CurrentCulture)序列化進 SQL literal(C 腿)。net462 的 B 腿接受 Windows-CRT token
    /// <c>-1.#IND</c>/<c>1.#QNAN</c>/<c>1.#INF</c> 並受 NLS culture 影響、C 腿用 G15;net8 的 B 腿拒收舊 token
    /// (回 0)、走 ICU、C 腿改最短往返——兩腿差異在此函式合成後一起改變寫進 MySQL 的字面值。
    ///
    /// 既有 <see cref="SpecialFloatParseTests"/>(B 孤立腿、Invariant 釘 float-token 維)與
    /// <see cref="DoubleToStringFormatTests"/>(C 孤立腿)各測**單腿**;本檔補的是「合成後的分支」:
    /// whitespace/null → 0(:1520)、parse 失敗 → 0(:1532),以及 B→C 在同一 CurrentCulture 下串起來的
    /// 結果。<b>不</b>呼叫真實 instance 方法——其 empty/parse-fail 分支會觸發 <c>writeToLog.WriteInfoLog</c>
    /// → 寫 <c>C:\temp\…</c>(WriteToLog.cs:29/61),在 CI 屬不必要副作用;故以**鏡像兩條 live 分支 + 生產同款
    /// 2-arg overload** 純 BCL 重現(對齊本suite既有 capture 紀律,零 production 改動)。
    ///
    /// 全屬 capture-don't-assert(<c>[Trait("Category","CaptureBaseline")]</c>):每列同時印回傳 double 的
    /// bit pattern(B 腿錨)與 <c>.ToString()</c> 渲染(C 腿 literal),經 <see cref="ITestOutputHelper"/> 印出
    /// 當 net462 基準、**不硬斷言**;真實值待 CI(windows-latest)capture step 回填。升級 net8 後同檔再跑、
    /// 逐列 diff = 回歸訊號。綠燈門檻濾除本類(<c>Category!=CaptureBaseline</c>)。
    /// </summary>
    public class ValidateAndConvertStatisticValueTests
    {
        private readonly ITestOutputHelper output;

        public ValidateAndConvertStatisticValueTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// 鏡像 <c>FileProcess.ValidateAndConvertStatisticValue</c> 的兩條 live 分支
        /// (FileProcess.cs:1520-1533):空白/null → 0、Trim 後 2-arg <c>TryParse</c> 成功回值、否則 0。
        /// 用與生產**同款 2-arg overload**(吃 CurrentCulture),維持 B 腿的 culture 敏感性。
        /// 純算術 + `TryParse` 路徑,對 string 輸入不擲例外,故無 try/catch。
        /// </summary>
        private static double Mirror(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }
            string trimmed = value.Trim();
            if (double.TryParse(trimmed, out double result))
            {
                return result;
            }
            return 0;
        }

        public static System.Collections.Generic.IEnumerable<object[]> InputsAndCultures()
        {
            // B 腿翻轉 token + locale 數字 + trim/空/null/非數字,涵蓋合成函式的每條分支。
            string[] inputs =
            {
                "-1.#IND", "1.#QNAN", "1.#INF", "-1.#INF", // Windows-CRT 舊 token(net462 收、net8 拒)
                "NaN", "Infinity", "1E400",                 // 兩框架皆收(1E400 溢位成 +∞)
                "1,5", "1.5",                               // locale 小數 / 千分位歧義(culture 敏感)
                "  3.14  ",                                 // 前後空白(Trim 分支)
                "", null, "abc",                            // 空 → 0 / null → 0 / 非數字 → 0
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
        [MemberData(nameof(InputsAndCultures))]
        [Trait("Category", "CaptureBaseline")]
        public void ValidateAndConvert_Synthesis_CaptureNet462Behavior(string input, string culture)
        {
            CultureInfo target = string.IsNullOrEmpty(culture)
                ? CultureInfo.InvariantCulture
                : new CultureInfo(culture);

            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = target;

                double returned = Mirror(input);           // B 腿:合成後回傳的 double
                string literal = returned.ToString();      // C 腿:呼叫端 :260 的 CurrentCulture 序列化

                output.WriteLine(
                    $"input={(input == null ? "<null>" : "\"" + input + "\"")} " +
                    $"culture=\"{(string.IsNullOrEmpty(culture) ? "Invariant" : culture)}\" " +
                    $"bits=0x{System.BitConverter.DoubleToInt64Bits(returned):X16} " +
                    $"literal=\"{literal}\"");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}

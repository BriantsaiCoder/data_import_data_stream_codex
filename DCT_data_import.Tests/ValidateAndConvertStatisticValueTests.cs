using System.Globalization;
using System.Threading;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause B(parse)＋C(format)在生產**合成點**的 net8 特性化測試。
    ///
    /// <see cref="FileProcess"/>.<c>ValidateAndConvertStatisticValue</c>(FileProcess.cs:1515)是 B 與 C
    /// 唯一合成處:先以**無 culture 參數**的 <c>double.TryParse(trimmed, out result)</c>(:1527,2-arg
    /// overload → 吃 <see cref="CultureInfo.CurrentCulture"/> 的 <see cref="NumberStyles.Float"/>|
    /// <c>AllowThousands</c>)把字串轉 double(B 腿),回傳值經呼叫端 :260 的 <c>validatedValue.ToString()</c>
    /// (CurrentCulture)序列化進 SQL literal(C 腿)。A4 後固定斷言 net8 對 Windows-CRT token、標準
    /// <c>NaN</c>/<c>Infinity</c>、<c>1E400</c>、locale 數字與 trim/空值分支的合成結果。
    ///
    /// 既有 <see cref="SpecialFloatParseTests"/>(B 孤立腿、Invariant 釘 float-token 維)與
    /// <see cref="DoubleToStringFormatTests"/>(C 孤立腿)各測**單腿**;本檔補的是「合成後的分支」:
    /// whitespace/null → 0(:1520)、parse 失敗 → 0(:1532),以及 B→C 在同一 CurrentCulture 下串起來的
    /// 結果。<b>不</b>呼叫真實 instance 方法——其 empty/parse-fail 分支會觸發 <c>writeToLog.WriteInfoLog</c>
    /// → 寫 <c>C:\temp\…</c>(WriteToLog.cs:29/61),在 CI 屬不必要副作用;故以**鏡像兩條 live 分支 + 生產同款
    /// 2-arg overload** 純 BCL 重現(對齊本suite既有 capture 紀律,零 production 改動)。
    ///
    /// A4 後專案只保留 net8.0-windows;這裡固定斷言 net8 的合成行為。
    /// </summary>
    public class ValidateAndConvertStatisticValueTests
    {
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
                "-1.#IND", "1.#QNAN", "1.#INF", "-1.#INF", // Windows-CRT 舊 token(net8 拒收→0)
                "NaN", "Infinity", "1E400",                 // 標準特殊值與溢位字串(net8 CurrentCulture 行為)
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
        public void ValidateAndConvert_Synthesis_ReturnsExpectedNet8Behavior(string input, string culture)
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

                (string expectedBits, string expectedLiteral) = Expected(input, culture);
                Assert.Equal(expectedBits, System.BitConverter.DoubleToInt64Bits(returned).ToString("X16"));
                Assert.Equal(expectedLiteral, literal);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        private static (string Bits, string Literal) Expected(string input, string culture)
        {
            if (string.IsNullOrWhiteSpace(input) || input == "-1.#IND" || input == "1.#QNAN"
                || input == "1.#INF" || input == "-1.#INF" || input == "abc")
            {
                return ("0000000000000000", "0");
            }

            if (input == "NaN")
            {
                return string.IsNullOrEmpty(culture) || culture == "en-US"
                    ? ("FFF8000000000000", "NaN")
                    : ("0000000000000000", "0");
            }

            if (input == "Infinity")
            {
                return string.IsNullOrEmpty(culture)
                    ? ("7FF0000000000000", "Infinity")
                    : ("0000000000000000", "0");
            }

            if (input == "1E400")
            {
                return string.IsNullOrEmpty(culture)
                    ? ("7FF0000000000000", "Infinity")
                    : ("7FF0000000000000", "∞");
            }

            if (input == "1,5")
                return ("402E000000000000", "15");
            if (input == "1.5")
                return ("3FF8000000000000", "1.5");

            return ("40091EB851EB851F", "3.14");
        }
    }
}

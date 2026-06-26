using DCT_data_import;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// 釘住 CONCERNS.md R5:<see cref="DbAccess.ComputeImportResult"/> 的加權和契約。
    /// 公式 <c>8*recoveryRate + 4*tester + 2*testResult + failPin</c> 假設每個分量只回 0/1,
    /// 但匯入函式實際回 0/1/2/3。任一回 2/3 會讓加權和溢位、污染高位 bit,
    /// 使 <c>UpdateDbKeyImportStatus</c>(DbAccess.cs:205)的 <c>importResult == check_status</c> 恆 false → 誤判失敗。
    ///
    /// 本檔含兩種測試:
    ///   - <see cref="ComputeImportResult_MatchesBitmask_WhenComponentsAreZeroOrOne"/> 描述 happy path(分量皆 0/1),目前 GREEN。
    ///   - 其餘兩個 *_R5_ 測試斷言「正確契約」,在 R5 未修前 by-design RED;任一合理修法(成功才 set bit、或把分量正規化為 0/1)會讓它們轉 GREEN。
    /// </summary>
    public class CheckStatusWeightedSumTests
    {
        // happy path:每個分量都是 0/1 時,加權和正好是各分量 bit 的疊加,範圍落在 4-bit mask(0..15)。
        // 任何正確修法都應維持此行為,故此測試在修前修後都該 GREEN。
        [Theory]
        [InlineData(0, 0, 0, 0, 0)]
        [InlineData(0, 0, 0, 1, 1)]   // failPin       -> bit0
        [InlineData(0, 0, 1, 0, 2)]   // testResult    -> bit1
        [InlineData(0, 1, 0, 0, 4)]   // tester        -> bit2
        [InlineData(1, 0, 0, 0, 8)]   // recoveryRate  -> bit3
        [InlineData(1, 1, 1, 1, 15)]  // 全成功         -> 0b1111
        public void ComputeImportResult_MatchesBitmask_WhenComponentsAreZeroOrOne(
            int recoveryRate, int tester, int testResult, int failPin, int expected)
        {
            Assert.Equal(expected, DbAccess.ComputeImportResult(recoveryRate, tester, testResult, failPin));
        }

        // R5 pin #1(溢位):合法 check_status 是 4-bit mask(0..15)。
        // 某分量回 2(匯入函式實際值域)不該讓結果衝出此範圍。
        // 目前 = 8+4+(2*2)+1 = 17 > 15 → RED,直接證明「加權和溢位污染高位 bit」。
        // ByDesignRed:R5 未修前必紅,CI 以 Category!=ByDesignRed 排除,避免假紅。
        [Fact]
        [Trait("Category", "ByDesignRed")]
        public void ComputeImportResult_StaysWithinValidFourBitMask_WhenAComponentReturnsTwo_R5()
        {
            int result = DbAccess.ComputeImportResult(recoveryRate: 1, tester: 1, testResult: 2, failPin: 1);

            Assert.InRange(result, 0, 15);
        }

        // R5 pin #2(資訊遺失):兩種語意完全不同的分量狀態,bitmask 應可區分。
        //   - testResult 匯入函式回 2 → ComputeImportResult(0,0,2,0) = 4
        //   - tester 成功回 1        → ComputeImportResult(0,1,0,0) = 4
        // 兩者相等代表 bit 被污染、無法分辨「testResult 回了錯誤碼 2」與「tester 成功」→ RED。
        // ByDesignRed:R5 未修前必紅,CI 以 Category!=ByDesignRed 排除,避免假紅。
        [Fact]
        [Trait("Category", "ByDesignRed")]
        public void ComputeImportResult_DoesNotConflateDistinctComponentStates_R5()
        {
            int testResultReturnedTwo = DbAccess.ComputeImportResult(0, 0, 2, 0);
            int testerSucceeded       = DbAccess.ComputeImportResult(0, 1, 0, 0);

            Assert.NotEqual(testerSucceeded, testResultReturnedTwo);
        }
    }
}

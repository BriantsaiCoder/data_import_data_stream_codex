using DCT_data_import;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// 釘住 CONCERNS.md R5:<see cref="DbAccess.ComputeImportResult"/> 的 bitmask 契約。
    /// 各匯入分量回傳碼值域為 0/1/2/3,唯有成功(1)才應設對應 bit;失敗碼(2/3)與缺席同視為未設位(0)。
    /// <c>ComputeImportResult</c> 已把每個分量正規化為 0/1 再加權(見該方法 remarks),故結果恆落在 4-bit mask(0..15),
    /// 不會溢位污染高位 bit、不會使 <c>UpdateDbKeyImportStatus</c> 的 <c>importResult == check_status</c> 誤判失敗。
    ///
    /// 本檔測試:
    ///   - <see cref="ComputeImportResult_MatchesBitmask_WhenComponentsAreZeroOrOne"/>:happy path(分量皆 0/1)。
    ///   - 三個 *_R5 測試:釘住正確契約(不溢位、不混淆狀態、失敗碼貢獻 0 且可分辨正確修法)。R5 修正後皆 GREEN。
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
        // 修正前 = 8+4+(2*2)+1 = 17 > 15(溢位);正規化修正後 = 8+4+0+1 = 13,落在範圍內。
        [Fact]
        public void ComputeImportResult_StaysWithinValidFourBitMask_WhenAComponentReturnsTwo_R5()
        {
            int result = DbAccess.ComputeImportResult(recoveryRate: 1, tester: 1, testResult: 2, failPin: 1);

            Assert.InRange(result, 0, 15);
        }

        // R5 pin #2(資訊遺失):兩種語意完全不同的分量狀態,bitmask 應可區分。
        //   - testResult 匯入函式回 2(失敗)→ ComputeImportResult(0,0,2,0)
        //   - tester 成功回 1            → ComputeImportResult(0,1,0,0)
        // 修正前兩者皆 = 4、無法分辨;正規化修正後失敗碼 2 映成未設位(0)、tester 成功映成 bit2(4),可區分。
        [Fact]
        public void ComputeImportResult_DoesNotConflateDistinctComponentStates_R5()
        {
            int testResultReturnedTwo = DbAccess.ComputeImportResult(0, 0, 2, 0);
            int testerSucceeded       = DbAccess.ComputeImportResult(0, 1, 0, 0);

            Assert.NotEqual(testerSucceeded, testResultReturnedTwo);
        }

        // R5 pin #3(判別性,鎖定正確修法):匯入失敗碼(2/3)對 bitmask 的貢獻必須等同「未設位」(0)——
        // 既不可殘留高位污染,也不可被當成「成功」(1)。
        // 此測試刻意設計成能分辨兩種候選修法,避免日後被誤「簡化」:
        //   - 正確:每分量正規化 testResult==1?1:0 → 失敗碼 2 映成 0(未設位)。
        //   - 錯誤:Math.Min(testResult,1) → 失敗碼 2 映成 1,把失敗當成功 → 被本測試第一個斷言擋下。
        // 因此本測試不掛 ByDesignRed:修前 RED(失敗碼貢獻 2≠0),套用正確修法後 GREEN。
        [Fact]
        public void ComputeImportResult_FailureCodeContributesNoBit_DistinctFromSuccess_R5()
        {
            int testResultFailed    = DbAccess.ComputeImportResult(0, 0, 2, 0);
            int testResultAbsent    = DbAccess.ComputeImportResult(0, 0, 0, 0);
            int testResultSucceeded = DbAccess.ComputeImportResult(0, 0, 1, 0);

            Assert.Equal(testResultAbsent, testResultFailed);        // 失敗碼貢獻 == 未設位(擋下 Math.Min 誤修)
            Assert.NotEqual(testResultSucceeded, testResultFailed);  // 失敗 != 成功
        }
    }
}

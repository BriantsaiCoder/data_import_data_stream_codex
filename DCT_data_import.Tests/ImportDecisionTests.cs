using DCT_data_import;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// 特性化(characterization)測試:釘住 <see cref="ImportDecision"/> 的 check_status bitmask 分派判斷,
    /// 作為 net8 維運時的行為基準。
    ///
    /// 這些判斷是純整數 bitmask,屬「不變量回歸樁」:
    /// 若有人「順手整理」這些條件而改變語意,本測試即轉紅。
    ///
    /// 判斷抽自 <c>Program.ImportTesterMode</c>(Program.cs:405/423/453/471)的外層 check_status 條件,
    /// 與原 inline 判斷逐字相同。check_status 為 4-bit bitmask(0..15):
    /// bit0=FailPin、bit1=RawData、bit2=Tester、bit3=RecoveryRate。
    /// (各匯入器另有「該分量目前值 ==0」的內層守衛,屬 trivial 條件,保留在 Program.cs inline,不在 seam 內。)
    /// </summary>
    public class ImportDecisionTests
    {
        // RecoveryRate 外層(Program.cs:405): bit3 set,即 8..15。
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(2, false)]
        [InlineData(3, false)]
        [InlineData(4, false)]
        [InlineData(5, false)]
        [InlineData(6, false)]
        [InlineData(7, false)]
        [InlineData(8, true)]
        [InlineData(9, true)]
        [InlineData(10, true)]
        [InlineData(11, true)]
        [InlineData(12, true)]
        [InlineData(13, true)]
        [InlineData(14, true)]
        [InlineData(15, true)]
        public void IsRecoveryRateCheckStatus_TrueForBit3(int checkStatus, bool expected)
        {
            Assert.Equal(expected, ImportDecision.IsRecoveryRateCheckStatus(checkStatus));
        }

        // RawData 外層(Program.cs:423): bit1 set,即 {2,3,6,7,10,11,14,15}。
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(2, true)]
        [InlineData(3, true)]
        [InlineData(4, false)]
        [InlineData(5, false)]
        [InlineData(6, true)]
        [InlineData(7, true)]
        [InlineData(8, false)]
        [InlineData(9, false)]
        [InlineData(10, true)]
        [InlineData(11, true)]
        [InlineData(12, false)]
        [InlineData(13, false)]
        [InlineData(14, true)]
        [InlineData(15, true)]
        public void IsRawDataCheckStatus_TrueForBit1(int checkStatus, bool expected)
        {
            Assert.Equal(expected, ImportDecision.IsRawDataCheckStatus(checkStatus));
        }

        // Tester 外層(Program.cs:453): bit2 set,即 {4..7} 或 {12..15}。
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(2, false)]
        [InlineData(3, false)]
        [InlineData(4, true)]
        [InlineData(5, true)]
        [InlineData(6, true)]
        [InlineData(7, true)]
        [InlineData(8, false)]
        [InlineData(9, false)]
        [InlineData(10, false)]
        [InlineData(11, false)]
        [InlineData(12, true)]
        [InlineData(13, true)]
        [InlineData(14, true)]
        [InlineData(15, true)]
        public void IsTesterCheckStatus_TrueForBit2(int checkStatus, bool expected)
        {
            Assert.Equal(expected, ImportDecision.IsTesterCheckStatus(checkStatus));
        }

        // FailPin 外層(Program.cs:471): bit0 set,即奇數 {1,3,5,7,9,11,13,15}。
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(3, true)]
        [InlineData(4, false)]
        [InlineData(5, true)]
        [InlineData(6, false)]
        [InlineData(7, true)]
        [InlineData(8, false)]
        [InlineData(9, true)]
        [InlineData(10, false)]
        [InlineData(11, true)]
        [InlineData(12, false)]
        [InlineData(13, true)]
        [InlineData(14, false)]
        [InlineData(15, true)]
        public void IsFailPinCheckStatus_TrueForBit0(int checkStatus, bool expected)
        {
            Assert.Equal(expected, ImportDecision.IsFailPinCheckStatus(checkStatus));
        }

        // MultiSpec fallback(Program.cs:428): RawData 匯入回 0(檔案不存在)時才改試 MultiSpec。
        // ImportResult.Result 語意:0=檔案不存在、1=成功、2=驗證/讀檔失敗、3=重複或匯入失敗。
        [Theory]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(2, false)]
        [InlineData(3, false)]
        public void ShouldFallbackToMultiSpec_OnlyWhenRawDataFileNotFound(int rawDataResult, bool expected)
        {
            Assert.Equal(expected, ImportDecision.ShouldFallbackToMultiSpec(rawDataResult));
        }
    }
}

namespace DCT_data_import
{
    /// <summary>
    /// 從 <c>Program.ImportTesterMode</c> 抽出的純判斷 seam:依 db_key 的 check_status bitmask 決定該觸發哪些匯入器。
    /// 各 check_status 條件與原 inline 外層判斷(Program.cs:405/423/453/471/428)逐字相同,抽出僅為可測試性,不改語意。
    /// check_status 為 4-bit bitmask(0..15):bit0=FailPin、bit1=RawData、bit2=Tester、bit3=RecoveryRate。
    /// (各匯入器另有「該分量目前值 ==0(尚未匯入)」的內層守衛,屬 trivial 條件,保留在 Program.cs inline。)
    /// </summary>
    internal static class ImportDecision
    {
        // Program.cs:405 — bit3,即 8..15。
        internal static bool IsRecoveryRateCheckStatus(int checkStatus)
            => checkStatus >= 8 && checkStatus <= 15;

        // Program.cs:423 — bit1,即 {2,3,6,7,10,11,14,15}。
        internal static bool IsRawDataCheckStatus(int checkStatus)
            => checkStatus == 2 || checkStatus == 3 || checkStatus == 6 || checkStatus == 7
               || checkStatus == 10 || checkStatus == 11 || checkStatus == 14 || checkStatus == 15;

        // Program.cs:453 — bit2,即 {4..7} 或 {12..15}。
        internal static bool IsTesterCheckStatus(int checkStatus)
            => (checkStatus >= 4 && checkStatus <= 7) || (checkStatus >= 12 && checkStatus <= 15);

        // Program.cs:471 — bit0(奇數),即 {1,3,5,7,9,11,13,15}。
        internal static bool IsFailPinCheckStatus(int checkStatus)
            => checkStatus == 1 || checkStatus == 3 || checkStatus == 5 || checkStatus == 7
               || checkStatus == 9 || checkStatus == 11 || checkStatus == 13 || checkStatus == 15;

        // Program.cs:428 — RawData 匯入回 0(檔案不存在)時才改試 MultiSpec。
        internal static bool ShouldFallbackToMultiSpec(int rawDataResult)
            => rawDataResult == 0;
    }
}

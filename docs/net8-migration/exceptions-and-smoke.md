# Phase 0 例外登記 + 上線後 manual smoke 清單（P0-5）

> 有些路徑**無穩定基準可 capture**——硬寫 characterization 只會抓到每台機器 / 每框架枚舉順序都不同的非決定值。
> 這些**登記為例外**並轉成 Phase 1 cutover 後的人工 smoke,不是丟著不管。上游：[phase-0-safety-net.md](phase-0-safety-net.md) P0-5。

## 三類例外（不 capture，登記理由）

### 1. `macid` dead code ×8（機器相依、非決定，故不 capture）

8 個 importer 在啟動處取本機網卡 MAC：`string macid = nics[0].GetPhysicalAddress().ToString();`——**賦值後從不讀取**（已驗證 dead code）。

| 檔 | 行 |
|----|----|
| [Tester.cs:28](../../DCT_data_import/ReadAndImport/Tester.cs#L28) · [FailPin.cs:29](../../DCT_data_import/ReadAndImport/FailPin.cs#L29) · [UiStatus.cs:28](../../DCT_data_import/ReadAndImport/UiStatus.cs#L28) · [RecoveryRate.cs:28](../../DCT_data_import/ReadAndImport/RecoveryRate.cs#L28) | 各 1 處 |
| [RawData.cs:32](../../DCT_data_import/ReadAndImport/RawData.cs#L32) · [MultiSpecRawData.cs:122](../../DCT_data_import/ReadAndImport/MultiSpecRawData.cs#L122) | 各 1 處 |
| [TsmcIeda.cs:32](../../DCT_data_import/ReadAndImport/TsmcIeda.cs#L32) · [TsmcIeda.cs:207](../../DCT_data_import/ReadAndImport/TsmcIeda.cs#L207) | 2 處 |

- **為何不 capture**：值被丟棄,故 net8 改變網卡枚舉來源 / 順序**不影響任何輸出**。MAC 字串本身機器相依,寫進 characterization 只會抓到非決定值。
- **唯一 live 風險**：`nics` 為空時 `nics[0]` 擲 `IndexOutOfRangeException`。net8 對此**一致或更好**（照樣擲例外,屬顯性失敗、非靜默漂移）→ 列為下方 manual smoke。
- **dead code 處置**：依 CLAUDE.md「自己造成的 dead code 必清,pre-existing dead code 提及但不刪」——**提及但不刪**（非本升級 task 範圍）。

### 2. MySql.Data 9.4.0 DateTime materialization（integration scope，非 unit capture）

DB row → `DateTime` 的物化（[DBmysql.cs:147](../../DCT_data_import/MySQL_api/DBmysql.cs#L147) 的上游）依賴 MySql.Data driver,需**真實 MySQL + 升級後 driver** 才能重現。

- **為何不 capture**：無真實 DB 連線的 unit 無法重現 driver 物化行為；P0-3 的純格式 capture（`DateTime.ToString("yyyy-MM-dd HH:mm:ss")`）只涵蓋 format 腿,materialization 腿屬 integration。
- **處置**：登記為 manual smoke（見下）。

### 3. ASCII StreamReader ×2（reviewed — 非 big5 資料路徑、無漂移）

[ImportData.cs:28](../../DCT_data_import/ReadAndImport/ImportData.cs#L28) 與 [TsmcIeda.cs:55](../../DCT_data_import/ReadAndImport/TsmcIeda.cs#L55) 的 `new StreamReader(responseStream)`（無 encoding 參數）。

- **為何不 capture / 不加 big5**：已驗證讀的是 **FTP 目錄列表（ASCII 檔名）**,非 big5 CSV 內容。UTF-8 預設對純 ASCII 兩框架一致。
- **對照**：真正讀 big5 CSV 內容的點明確帶 `Encoding.GetEncoding("big5")`（如 TsmcIeda.cs:86/240/297）——那些由 Phase 1 **P1-6**（註冊 `CodePagesEncodingProvider`）+ `Big5DecodeTests` 閘門守護,不在本例外內。
- **處置**：登記為「reviewed — 非 big5 資料路徑、無漂移」,FTP 目錄列表解析列為 manual smoke。

## 上線後 manual smoke 清單（A3 已回報通過）

使用者已回報 A3 在 Windows PC 順利 pass；下列項目保留為 cutover smoke 歷史紀錄與未來回歸參考。

- [x] **空網卡啟動**：各 importer 在無可用 NIC 環境啟動,確認 `nics[0]` 仍是顯性 `IndexOutOfRangeException`（非靜默漂移）——與 net462 行為一致即可。
- [x] **MySql.Data 9.4.0 DateTime 物化**：從真實 DB 撈出的 `DateTime` 經 [DBmysql.cs:147](../../DCT_data_import/MySQL_api/DBmysql.cs#L147) `ToString("yyyy-MM-dd HH:mm:ss")` 後,字串格式與 net462 抽樣比對一致。
- [x] **FTP 目錄列表解析**：net8 下 [ImportData.cs:28](../../DCT_data_import/ReadAndImport/ImportData.cs#L28) / [TsmcIeda.cs:55](../../DCT_data_import/ReadAndImport/TsmcIeda.cs#L55) 解出的檔名 pattern match 結果與 net462 一致。
- [x] **big5 CSV 內容解碼**：`Big5DecodeTests` 由紅轉綠（P1-6 provider 註冊生效）後,實跑一筆真實 big5 CSV,確認繁中欄位無亂碼。
- [x] **worker-hang 故障注入**：主動觸發 worker hang / dead-worker supervisor 路徑,確認 net8 不再走 `Thread.Abort` PNSE 失敗路徑。

> 這份清單在 [phase-1-migration.md](phase-1-migration.md) 的 cutover runbook 引用——cutover 前確認每項 owner 與驗證資料來源。

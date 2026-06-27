# Architecture

## Core Sections (Required)

### 1) Architectural Style

- Primary style：**分層 + 批次輪詢的多執行緒 ETL pipeline**（Layered + polling worker）。
- Why this classification：清楚的三層分工——`Program.cs`（編排）→ `ReadAndImport/*`（業務解析）→ `DbApi`+`MySQL_api`（資料存取）；每層只依賴下層。`Program.Main` 以 3 條長駐 `Thread` 週期性輪詢 `db_key` 旗標表，將 FTP 上的 CSV 拉下、解析、寫入 MySQL（`Program.cs:27-`、`DbApi/DbAccess.cs:69-150`）。
- Primary constraints：
  1. Windows-only（`kernel32` P/Invoke INI、hardcoded `C:\temp` log、FTP 經 `System.Net.FtpWebRequest`）。
  2. 同步阻塞模型——`async` importer 以 `.GetAwaiter().GetResult()` 同步等待（`Program.cs`、`DbApi/DatabaseService.cs:101,108`、`DbAccess.cs:40` 等）。
  3. 狀態機由 `db_key` 表的 `check_status`（bitmask）/`import_status`/`mail` 欄位驅動，而非程式內狀態。

### 2) System Flow

```text
[Program.Main 環境偵測 + 3 執行緒]
   -> [DbAccess.SelectDbKey 撈出 check_status>0 且 import_status=0 的待處理旗標]
   -> [依 CheckStatus bitmask + 各分量旗標決定要跑哪些 importer]
   -> [ReadAndImport.*：FTP 下載 CSV(big5) -> FileContentFormat 欄位驗證 -> 解析成 DataTable]
   -> [FileProcess.Import*：DataTable -> Dapper 參數化 INSERT(分批) -> DatabaseService.ExecuteSqlAsync]
   -> [DBmysql：MySqlConnection + Dapper Query/Execute(parameters)，結果轉 JArray]
   -> [DbAccess.UpdateDbKeyImportStatus：比對 importResult==check_status 設 import_status=1/2，失敗寫 mail_temp]
   -> [成功刪 FTP 檔 / 失敗搬到 *_Error 目錄；NotificationService 依條件寄信]
```

4–6 步 file-backed 說明：
1. `Program.cs:18` 偵測環境 → 由 `App.config` 取 DB 連線；`Main` 啟動三模式執行緒並監督重啟。
2. `DbApi/DbAccess.cs:69-150` `SelectDbKey(mode)`：以 `SELECT ... WHERE check_status>0 AND import_status=0 AND mail=0` 取得待匯入清單。
3. importer（如 `ReadAndImport/RawData.cs:16`）依 `ImportData.GetFilePath()`（`ReadAndImport/ImportData.cs:274`）組 FTP 路徑、下載、以 `Encoding.GetEncoding("big5")` 解析。
4. `FileContentFormat`（`FileAccess/FileContentFormat.cs`）的 `CompareInfo()`/`CompareStatistic()` 等做欄位名驗證後，importer 把資料填入 `DataTable`。
5. `FileProcess.Import*`（`FileAccess/FileProcess.cs:81-1357`）把 `DataTable` 轉為 INSERT placeholders + `DynamicParameters`，呼叫 `FileProcess.ExecuteInsert`（`:1376`）→ `DatabaseService.ExecuteSqlAsync`（`DbApi/DatabaseService.cs:18`）→ `DBmysql.Excute_mysql_cmd`（`MySQL_api/DBmysql.cs:51`）。
6. `DbAccess.UpdateDbKeyImportStatus`（`DbApi/DbAccess.cs:163-247`）比對 `importResult == check_status`，相符設 `import_status=1`，否則 `import_status=2` + `mail=1` 並 `WriteToMailTemp`。

### 3) Layer/Module Responsibilities

| Layer or module | Owns | Must not own | Evidence |
|-----------------|------|--------------|----------|
| `Program.cs` | 環境偵測、執行緒監督、依 bitmask 派工 | 解析、SQL | `Program.cs:15-26` |
| `ReadAndImport/*` | FTP 下載、格式驗證、解析、清理、回傳 `ImportResult` | 連線字串、Dapper | `ReadAndImport/RawData.cs:16`、`ImportData.cs:274` |
| `FileAccess/FileProcess.cs` | DataTable→參數化 INSERT、分批、級聯刪除 | FTP 存取 | `FileProcess.cs:81-1660` |
| `FileAccess/FileContentFormat.cs` | 6 種 CSV 欄位契約 + 驗證 | DB / FTP | `FileContentFormat.cs:6,79,138,204,233,277` |
| `DbApi/DatabaseService.cs` | 連線參數驗證、執行 SQL、DB/table 存在性檢查 | 業務語意 | `DatabaseService.cs:18,62,95` |
| `MySQL_api/DBmysql.cs` | MySqlConnection 生命週期、Dapper 執行、結果轉 JArray、錯誤碼對應 | 何時匯入 | `DBmysql.cs:51,123,186,245` |
| `Common/*` | log（Mutex）、SMTP 寄信、SPC 統計、INI | 匯入流程 | `Common/WriteToLog.cs`、`NotificationService.cs`、`CalculateSPC.cs` |

### 4) Reused Patterns

| Pattern | Where found | Why it exists |
|---------|-------------|---------------|
| Template Method（基底 + 7 子類） | `ReadAndImport/ImportData.cs:7` 基底，`Tester`/`FailPin`/`RawData`… 繼承 | 共用 FTP/路徑/檔案工具，子類各自實作 `ReadAndImport{Type}` |
| Singleton（連線字串） | `MySQL_api/DBmysql.cs:274` `MySqlConnectionManager`（`volatile` + `lock`，只初始化一次） | 全域共用連線字串 |
| Service 包裝 | `DbApi/DatabaseService.cs` 包 `DBmysql` | 統一輸入驗證與錯誤訊息脫敏（`GetSafeErrorMessage`，`:126`） |
| Async-over-sync | 全專案 `.GetAwaiter().GetResult()` | importer 宣告 `async Task<ImportResult>`，但呼叫端同步等待 |
| Status-flag state machine | `db_key`/`db_key_ui_status` 表的 `check_status`/`import_status`/`mail` | 以 DB 旗標驅動「待處理/已匯入/待寄信」 |

### 5) Known Architectural Risks

- **Async-over-sync 全面阻塞**：`async` 方法以 `.GetAwaiter().GetResult()` 同步呼叫，喪失非同步效益且有死結風險（`DatabaseService.cs:101,108`、`DbAccess.cs:40,98,170,221`）。影響：吞吐受限、難以平行化。
- **殘餘動態 SQL 組裝**：S2 已將外部值改為 Dapper parameters；但 `FileProcess` 仍組 table / column / placeholder text，必須維持 `ExecuteInsert` 的 identifier guard，不得新增繞路 SQL。
- **架構文件需持續同步**：根目錄 `專案架構報告.md` / `專案架構視覺化.html` 已刷新至 S2；後續 module boundary 或 data-flow 變更仍需同步更新。
- **O(n²) placeholder 字串累加**：`FileProcess` 仍以 `values += ...` 逐列累加 placeholders（`FileProcess.cs:110-156` 等），大檔效能差（應 `StringBuilder`）。
- **TSMC IEDA importer 自走一套**：`TsmcIeda` 不經 `FileProcess`，但 S2 後 INSERT 與 `DataTable.Select` filter value 已做參數化/escaping；流程邊界仍與其他 importer 不一致。

### 6) Evidence

- `DCT_data_import/Program.cs`
- `DCT_data_import/ReadAndImport/ImportData.cs`、`RawData.cs`、`Tester.cs`、`FailPin.cs`、`RecoveryRate.cs`、`UiStatus.cs`、`MultiSpecRawData.cs`、`TsmcIeda.cs`
- `DCT_data_import/FileAccess/FileProcess.cs`、`FileContentFormat.cs`
- `DCT_data_import/DbApi/DatabaseService.cs`、`DbAccess.cs`、`DbObject.cs`
- `DCT_data_import/MySQL_api/DBmysql.cs`

## Extended Sections (Optional)

- `CheckStatus` bit 語意（已用 `Program.cs` 派工條件交叉驗證，非單純反推）：
  - Bit0(1)=**FailPin**、Bit1(2)=**RawData/TestResult**、Bit2(4)=**Tester**、Bit3(8)=**RecoveryRate**（公式 `importResult = 8*recoveryRate + 4*tester + 2*testResult + failPin`，純函式 `DbAccess.cs:160`；引數順序對應 `Program.cs:483`）。
  - Bit4(16)=**UiStatus** **不在上式內**——走獨立 pipeline（`db_key_ui_status` 表 + `UpdateDbKeyUiStatusImportStatus`，公式 `importResult = uiStatus`，`DbAccess.cs:263`）。把它與 Bit0–3 並列為同一 mask 是概念混用。
  - ✅ **已修復脆弱隱性契約（見 CONCERNS R5，2026-06-27 `fix/r5-checkstatus`）**：上式（純函式 `DbAccess.ComputeImportResult`，`DbAccess.cs:160`）原只在「各 component 的 `Result` 都=1」時才能反推回 `check_status`；但匯入函式 `Result` 實際回 0/1/2/3，任一回 2/3 會讓加權和溢位、污染高位 bit，使 `importResult == check_status`（`DbAccess.cs:211`）恆為 false → 一律判失敗+寄信。**修法**：在 `ComputeImportResult` 內把各分量正規化為 `Result == 1 ? 1 : 0`（明確排除 `Math.Min`——它會把失敗碼 2/3 也映成 1、反把失敗當成功），0/1 輸入行為不變、僅修正 ≥2 溢位；`DCT_data_import.Tests` 3 條 `_R5` 回歸樁轉綠並移除 `ByDesignRed` trait、重納 CI gate。
  - [ASK USER] 對照正式規格確認：bit 定義、各 component 合法值域（是否該只存 0/1）、UiStatus 是否本就獨立、成功判定是否要求「全數成功」。

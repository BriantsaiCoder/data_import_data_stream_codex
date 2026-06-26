# Concerns

> 依嚴重度排序的技術債、安全、效能、正確性風險。每條附 evidence 與建議方向。不臆測——僅列程式碼可佐證者。

## Core Sections (Required)

### 1) Security

| # | Severity | 問題 | Evidence | 建議 |
|---|----------|------|----------|------|
| S1 | **High** | **明文憑證入版控**：`App.config` 內含 DB/FTP 帳密明文，隨 git 提交 | `App.config`（`{Env}User`/`{Env}Password`、FTP ConnectionStrings） | 移出版控，改 secret manager / 環境變數；提供只含 key name 的 `.env.example` |
| S2 | **High** | **SQL injection（全字串串接、零參數化）**：外部來源（`db_key`、CSV 欄位值）直接拼進 SQL | `FileProcess.cs:1350`、`DbAccess.cs:180,224,264,309`、`TsmcIeda.cs:141,225`（`DataTable.Select("tsmc_lot='"+v+"'")`） | 改參數化查詢（Dapper 已支援具名參數）；`DBmysql.FilterSqlCommand` 的 regex 補救屬症狀處理，非根治 |
| S3 | **High** | **憑證明文輸出至 Console**：啟動時 `Console.WriteLine` 印出 HOST/USER/PASSWORD | `Program.cs:32-34` | 移除或遮罩；至少不印 PASSWORD |
| S4 | Medium | **SMTP 無認證 + hardcoded IP**：寄信走匿名 SMTP，IP `10.12.10.31`、寄件者 `CTRD5900@aseglobal.com` 寫死 | `EmailModels.cs:21,44` | 移入設定；評估是否需認證/TLS |

### 2) Technical Debt

| # | Severity | 問題 | Evidence | 建議 |
|---|----------|------|----------|------|
| D1 | **High** | **架構文件嚴重脫節**：`專案架構報告.md` 引用多個不存在的檔（`*Refactored.cs`、`DatabaseSchemaDefinitions.cs`、`ConfigurationService.cs`、`ThreadManager.cs`）與錯誤 API 形狀（`ImportResult.IsSuccess`/`.ErrorMessage` vs 實際 `.Result`/`.Message`） | `專案架構報告.md` vs 實際 `ReadAndImport/`、`.csproj`（註解記錄這些檔已移除） | 重寫或刪除該報告；以本 `docs/codebase/` 取代 |
| D2 | ~~Medium~~ ✅已修(Q5) | **DEAD CONFIG / 殘留命名**：API dead config（ApiUrl/AuthKey/ApiUser/ApiPassword）已自 `App.config` 刪除;`ExecuteInsertWithAPI` 已改名 `ExecuteInsert`;「Web API body」誤導註解已清。`Execute_query`/`Execute_query_response` 型別名未改（blast radius 較大,仍為歷史殘留） | `App.config`（現無 API 鍵）、`FileProcess.cs:1337`（`ExecuteInsert`）、git 歷史 | ✅ 完成;型別改名可日後評估 |
| D3 | Medium | **大量註解掉的 dead code**：`Program.cs:40-70` 整段 TEST CASE、各 `DbAccess` 方法內舊邏輯 | `Program.cs:40-70` | 清除（git 已留歷史） |
| D4 | Medium | **TSMC IEDA importer 自走一套**：不經 `FileProcess`，自行組 INSERT + `DataTable.Select` 查詢，與其餘 importer 不一致 | `TsmcIeda.cs:141,225` | 收斂至共用路徑或明確記錄為例外 |
| D5 | Low | **命名/慣例不一致**：namespace 與資料夾不對齊；`db_key` CSV 欄位名 `"DB_Key"` vs `"DB Key"`；log 方法混用 | STRUCTURE.md §4、CONVENTIONS.md | 漸進統一 |

### 3) Performance

| # | Severity | 問題 | Evidence | 建議 |
|---|----------|------|----------|------|
| P1 | Medium | **O(n²) SQL 字串累加**：`values += ...` 逐列串接，大檔記憶體/CPU 差 | `FileProcess.cs:110-156` 等 | 改 `StringBuilder` 或批次參數化 |
| P2 | Medium | **Async-over-sync 全面阻塞**：`async` 方法以 `.GetAwaiter().GetResult()` 同步等待，喪失非同步效益且有死結風險 | `DatabaseService.cs:101,108`、`DbAccess.cs:40,98,170,221` | 全鏈改真 async 或全改同步（擇一，勿混用） |
| P3 | Low | **每次匯入後 `GC.Collect()`**：手動觸發 full GC，通常弊大於利 | 各 importer 成功分支 | 移除，交給 runtime 管理 |

### 4) Reliability / Correctness

| # | Severity | 問題 | Evidence | 建議 |
|---|----------|------|----------|------|
| R1 | **High** | **零自動化測試**：無單元/整合測試、無 CI，重構無安全網 | TESTING.md | 先為純函式（`CalculateSPC`、`FileContentFormat.Compare*`）補 characterization test |
| R2 | Medium | **SPC 負號根值無 NaN 防護**：遇 `sum_sq/N - avg² < 0` 只 `Console` 警告「發現根號負值!」，未阻斷 | `Common/CalculateSPC.cs:90` | 加防護/釐清業務預期 |
| R3 | Medium | **Windows-only 硬綁定**：`kernel32` P/Invoke 讀寫 INI、hardcoded `C:\temp` log 路徑 | `ReadWriteINIfile.cs:10-13`、`WriteToLog.cs:29` | 路徑改設定；跨平台需求才重構 INI |
| R4 | Low | **log 無 rotation/上限**：每日分檔但無大小限制與清理 | `WriteToLog.cs` | 視磁碟壓力決定是否加 |
| R5 | **High**（已釘住,修法待規格） | **CheckStatus 加權和的脆弱契約**：`importResult = 8*recoveryRate + 4*tester + 2*testResult + failPin` 假設各 component `Result` 只回 0/1，但實際回 0/1/2/3。任一回 2/3 → 加權和溢位污染高位 bit → `importResult == check_status`（`DbAccess.cs:205`）恆 false → 部分成功一律判失敗+寄信，且重跑會把錯誤碼再餵回公式延續污染 | `DbAccess.cs:161,205,218`、各 importer `Result` 0/1/2/3、`Program.cs:501`、`DCT_data_import.Tests/CheckStatusWeightedSumTests.cs` | 改 bitwise 旗標（成功才 set 該 bit）或將 component 結果正規化為 0/1；先 [ASK USER] 確認規格意圖。**已抽純函式 `ComputeImportResult`（`DbAccess.cs:159-162`,行為不變）+ `DCT_data_import.Tests` 2 條 by-design RED 測試釘住**,修法待規格確認 |

### 5) Evidence

- `DCT_data_import/App.config`、`Program.cs:32-34`
- `DCT_data_import/FileAccess/FileProcess.cs`
- `DCT_data_import/DbApi/DbAccess.cs`、`DatabaseService.cs`
- `DCT_data_import/ReadAndImport/TsmcIeda.cs`
- `DCT_data_import/Common/EmailModels.cs`、`WriteToLog.cs`、`CalculateSPC.cs`、`ReadWriteINIfile.cs`
- `專案架構報告.md`（脫節文件）
- `docs/codebase/.codebase-scan.txt`

## Extended Sections (Optional)

### 領域知識（程式看不出 why，值得保存）
- **SPC 暖機排除**：`CalculateSPC.SeperatePassValue`（:112）會剔除 spec_max/spec_min 範圍外的 fail points 才算 stdev/avg——這是業務定義的良率計算規則，非單純技術過濾（剔除邏輯 `CalculateSPC.cs:127-132`）。
- **統計值異常轉換**：`FileProcess.ValidateAndConvertStatisticValue`（:1527）把 `-1.#IND`/`1.#QNAN` 等非數值 stdev/cp/cpk/avg 轉 0——對應 git log `479f29e` 的資料完整性處理。
- **CheckStatus bit 定義**（交叉驗證後修正）：Bit0(1)=FailPin、Bit1(2)=RawData/TestResult、Bit2(4)=Tester、Bit3(8)=RecoveryRate（公式 `DbAccess.cs:161`,已抽純函式 `ComputeImportResult` + `Program.cs:501` 引數順序）；Bit4(16)=UiStatus 走另一張表、不在此公式內（`DbAccess.cs:257`）。詳見 ARCHITECTURE.md Extended 與 R5。

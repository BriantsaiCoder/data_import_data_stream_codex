# Concerns

> 依嚴重度排序的技術債、安全、效能、正確性風險。每條附 evidence 與建議方向。不臆測——僅列程式碼可佐證者。

## Core Sections (Required)

### 1) Security

| # | Severity | 問題 | Evidence | 建議 |
|---|----------|------|----------|------|
| S1 | **High** | **明文憑證入版控**：`App.config` 內含 DB/FTP 帳密明文，隨 git 提交 | `App.config`（`{Env}User`/`{Env}Password`、FTP ConnectionStrings） | 移出版控，改 secret manager / 環境變數；提供只含 key name 的 `.env.example` |
| S2 | **High（已修復：A/PR-1 + A/PR-2）** | **SQL injection（全字串串接、零參數化）**：`DbAccess` / `TsmcIeda` / identifier chokepoint / `FileProcess` 批次 INSERT CSV values 已改參數化或跳脫 | 已修：`DbAccess.cs` 4 個 db_key 值站、`TsmcIeda.cs` IEDA INSERT + `DataTable.Select`、`FileProcess.ExecuteInsert` optional parameters/identifier guard、`FileProcess.Import*` 批次 INSERT values、`DatabaseService.CheckDatabaseAndTableExists` value parameters | 保持新 SQL 走 Dapper parameters；`DBmysql.FilterSqlCommand` 僅保留為既有防線，非主要控制 |
| S3 | ~~High~~ ✅已修 | **憑證明文輸出至 Console**：PASSWORD 已遮罩為 set/unset，不再印明文 | `Program.cs` | 維持遮罩；新增設定輸出不得洩露 secret |
| S4 | ~~Medium~~ ✅已修 | **SMTP hardcode**：server/from 已移至 `App.config`；目前仍採內網匿名 relay，為環境決策 | `EmailModels.cs`、`App.config` | 若環境要求，再評估 auth/TLS |

### 2) Technical Debt

| # | Severity | 問題 | Evidence | 建議 |
|---|----------|------|----------|------|
| D1 | ~~High~~ ✅已修 | **架構文件曾嚴重脫節**：root 架構報告 / HTML 已刷新至 S2 與 net8-only 現況 | `專案架構報告.md`、`專案架構視覺化.html` | 後續 module/data-flow 變更需同步更新 |
| D2 | ~~Medium~~ ✅已修(Q5) | **DEAD CONFIG / 殘留命名**：API dead config（ApiUrl/AuthKey/ApiUser/ApiPassword）已自 `App.config` 刪除;`ExecuteInsertWithAPI` 已改名 `ExecuteInsert`;「Web API body」誤導註解已清。Task 5 後 DB result surface 已是 typed-only；剩餘歷史命名債限於 `Execute_query` request DTO | `App.config`（現無 API 鍵）、`FileProcess.cs:1337`（`ExecuteInsert`）、git 歷史 | ✅ 完成;request DTO 改名可日後評估 |
| D3 | Medium | **大量註解掉的 dead code**：`Program.cs:40-70` 整段 TEST CASE、各 `DbAccess` 方法內舊邏輯 | `Program.cs:40-70` | 清除（git 已留歷史） |
| D4 | Medium | **TSMC IEDA importer 自走一套**：不經 `FileProcess`，自行組 INSERT + `DataTable.Select` 查詢，與其餘 importer 不一致 | `TsmcIeda.cs:141,225` | 收斂至共用路徑或明確記錄為例外 |
| D5 | Low | **命名/慣例不一致**：namespace 與資料夾不對齊；`db_key` CSV 欄位名 `"DB_Key"` vs `"DB Key"`；log 方法混用 | STRUCTURE.md §4、CONVENTIONS.md | 漸進統一 |

### 3) Performance

| # | Severity | 問題 | Evidence | 建議 |
|---|----------|------|----------|------|
| P1 | Medium | **O(n²) SQL 字串累加**：`values += ...` 逐列串接，大檔記憶體/CPU 差 | `FileProcess.cs:110-156` 等 | 改 `StringBuilder` 或批次參數化 |
| P2 | ~~Medium~~ ✅已修 | **Async-over-sync 全面阻塞**：active code 已全改同步;DB typed API 直接呼叫同步 DB path,importer 回 `ImportResult`,`Program.cs` 不再 `.GetAwaiter().GetResult()` | `DatabaseService.cs`、`Program.cs`、`ReadAndImport/*`、`DbAccess.cs`、`FileProcess.cs` | 維持明確同步;若未來要真 async,另立完整 DB/FTP async 設計 |
| P3 | ~~Low~~ ✅已修 | **每次匯入後 `GC.Collect()`**：各 importer 成功/finally 分支的手動 full GC 已移除,交給 runtime 管理 | `ReadAndImport/FailPin.cs`、`RawData.cs`、`RecoveryRate.cs`、`Tester.cs`、`UiStatus.cs`、`MultiSpecRawData.cs` | 維持 `using`/`Dispose` 釋放資源;不要以手動 GC 代替 cleanup |

### 4) Reliability / Correctness

| # | Severity | 問題 | Evidence | 建議 |
|---|----------|------|----------|------|
| R1 | Medium | **整合測試仍不足**：已有 `net8.0-windows` xUnit regression/characterization suite，但 importer / FTP / 真 MySQL 寫入仍缺自動化整合測試與 coverage gate | TESTING.md、`DCT_data_import.Tests/` | 續補 parser、`FileContentFormat.Compare*` 與 controlled DB/FTP smoke |
| R2 | ~~Medium~~ ✅已修 | **SPC 負號根值 NaN 防護**：`AverageOfSumSquare` 已把 rounding residue 造成的負 variance clamp 為 0，避免 `Math.Sqrt` 產生 NaN 後整列統計被 catch 掉 | `Common/CalculateSPC.cs`、`CalculateSpcTests.cs` | 維持 regression test；若未來要輸出 stdev，再補 stdev 欄位契約 |
| R3 | Medium | **Windows-only 硬綁定**：`kernel32` P/Invoke 讀寫 INI、hardcoded `C:\temp` log 路徑 | `ReadWriteINIfile.cs:10-13`、`WriteToLog.cs:29` | 路徑改設定；跨平台需求才重構 INI |
| R4 | ~~Low~~ ✅已修 | **log retention cleanup**：每日分檔仍不做大小切檔；已新增 `DataImportLogRetentionDays` 清理過期 `data_import_logs` / `check_logs` 檔案 | `WriteToLog.cs`、`App.config` | 維持預設 90 天；若單日檔案過大再評估 size-based rotation |
| R5 | ~~High~~ **已修復（2026-06-27，`fix/r5-checkstatus`）** | **CheckStatus 加權和的脆弱契約**：`importResult = 8*recoveryRate + 4*tester + 2*testResult + failPin` 假設各 component `Result` 只回 0/1，但實際回 0/1/2/3。任一回 2/3 → 加權和溢位污染高位 bit → `importResult == check_status`（`DbAccess.cs:211`）恆 false → 部分成功一律判失敗+寄信，且重跑會把錯誤碼再餵回公式延續污染 | `DbAccess.cs:160,179,211`、各 importer `Result` 0/1/2/3、`Program.cs:483`、`DCT_data_import.Tests/CheckStatusWeightedSumTests.cs` | **規格決定（用戶 2026-06-27）：分量正規化規則 = `Result == 1 ? 1 : 0`（成功才設位；明確排除 `Math.Min(x,1)`——後者會把失敗碼 2/3 也映成 1、反把失敗當成功）。** 已於純函式 `ComputeImportResult`（`DbAccess.cs:160`）單點套用此正規化，0/1 輸入行為不變、僅修正 ≥2 溢位。`CheckStatusWeightedSumTests` 3 條 `_R5` 測試（含一條判別 `Math.Min` 誤修的 pin）轉綠、移除 `ByDesignRed` trait 重納 CI gate。 |

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
- **CheckStatus bit 定義**（交叉驗證後修正）：Bit0(1)=FailPin、Bit1(2)=RawData/TestResult、Bit2(4)=Tester、Bit3(8)=RecoveryRate（公式 `DbAccess.cs:161`,已抽純函式 `ComputeImportResult` + `Program.cs:483` 引數順序）；Bit4(16)=UiStatus 走另一張表、不在此公式內（`DbAccess.cs:257`）。詳見 ARCHITECTURE.md Extended 與 R5。

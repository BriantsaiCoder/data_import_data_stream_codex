# Coding Conventions

> 以下為從現有程式碼觀察到的「實際慣例」（descriptive），非規定。專案無 linter/formatter 強制，故存在不一致處，已標註。

## Core Sections (Required)

### 1) Naming Rules

| Item | Rule | Example | Evidence |
|------|------|---------|----------|
| Files | PascalCase，一檔一主類別 | `FailPin.cs`、`DatabaseService.cs` | 全 `ReadAndImport/`、`DbApi/` |
| Classes | PascalCase | `class FileProcess`、`class DBmysql` | `FileProcess.cs:10`、`DBmysql.cs:10` |
| Methods | PascalCase | `ReadAndImportRawData`、`ExecuteQuery` | `RawData.cs:16`、`DatabaseService.cs` |
| 區域變數 | camelCase（但常見區域變數沿用型別名，如 `DatabaseService DatabaseService`） | `var writeToLog`、`DatabaseService DatabaseService` | `DbAccess.cs:69`、`DatabaseService.cs:45` |
| 靜態全域設定 | 全大寫 | `HOST`、`USER`、`FTP_IP` | `Program.cs:19-26` |
| DB 物件型別 | DB result/request contracts 用 PascalCase | `DbQueryResult`、`DbCommandResult`、`DbSqlRequest` | `DbObject.cs` |
| MySQL table/欄位 | snake_case | `db_key`、`import_status`、`lots_info` | `DbAccess.cs:84`、`FileProcess.cs:211` |

### 2) Formatting and Linting

- Formatter：[TODO] 無設定檔（無 `.editorconfig`）。觀察：4 空格縮排、Allman 大括號、`using` 置頂。
- Linter：[TODO] 無（無 StyleCop/Roslyn analyzer 設定）。
- 註解語言：以繁體中文為主（XML doc + 行內），夾雜少量簡體（如 `RawData`/`TsmcIeda` 部分註解）。Evidence：`DatabaseService.cs:12-17`、`ImportData.cs:227-234`（簡體「创建」「响应」）。
- Run commands：N/A（無 lint/format 工具）。

### 3) Import and Module Conventions

- `using` 置於檔首，未分組排序；混用 `using static`（`using static DCT_data_import.DbObject;`，見 `DBmysql.cs:7`、`DatabaseService.cs:3`）。
- 無路徑別名（C# 無此概念）；以 namespace 引用。
- Namespace 與資料夾不一致（見 STRUCTURE.md §4）：根層類別常掛 `DCT_data_import` 而非 `DCT_data_import.FileAccess` 等。

### 4) Error and Logging Conventions

- 錯誤策略：每個 importer 以一個大 `try/catch(Exception)` 包住整段流程，catch 內 `WriteToLog.WriteErrorLog(...)` + `Console.WriteLine(...)`，回傳 `ImportResult(code, message)`。`ImportResult.Result` 慣例：`0`=檔案不存在、`1`=成功、`2`=驗證/讀檔失敗、`3`=重複或匯入失敗（`RawData.cs`、`Tester.cs` 等一致）。
- DB 層錯誤：primary API 回 `DbQueryResult` / `DbCommandResult`，錯誤仍放 `Error` 字串；Task 5 後 DB result surface 已是 typed-only。`DBmysql` 對 `MySqlException` 依錯誤碼補中文說明（`FormatMySqlError`）；`DatabaseService.GetSafeErrorMessage` 只回 `ex.Message`、不含 StackTrace（脫敏）。
- Logging 樣式：`{yyyy/MM/dd HH:mm:ss} [INFO|ERROR] {message}`（`WriteToLog`）。production call sites 使用 `WriteInfoLog(...)` / `WriteErrorLog(...)` 表達層級；`WriteToDataImportLog(string)` 保留為相容 wrapper，不作為新 production call-site 慣例。多執行緒以命名 `Mutex` 保護寫檔，逾時 30 秒（`WriteToLog` 的 data / success / check log 寫入路徑一致）。log 寫到 `DataImportLogRoot\{exeName}\data_import_logs\DCT_data_import_Log_{yyyy_MM_dd}.txt`（預設 `C:\temp`，每日分檔，UTF-8 BOM）；`DataImportLogRetentionDays` 預設 90 天，0/負數可關閉過期檔清理。
- 敏感資料脫敏：啟動輸出已遮罩 PASSWORD，只印 set/unset；`App.config` 仍含既有明文 DB/FTP 設定（見 CONCERNS S1）。
- `WriteToCheckLog(...)` 與 `WriteImportSuccessLog(...)` 分別保留給 timing/check CSV 與成功匯入 audit file，不併入一般 data import log helper。

### 5) Testing Conventions

- 測試專案：`DCT_data_import.Tests`（SDK-style `net8.0-windows` xUnit），涵蓋 R5、遷移契約與 net8 characterization；仍缺 importer / FTP / DB 整合測試（見 TESTING.md）。
- Mocking：N/A。
- Coverage 期望：N/A。

### 6) Evidence

- `DCT_data_import/Common/WriteToLog.cs`
- `DCT_data_import/DbApi/DatabaseService.cs`
- `DCT_data_import/MySQL_api/DBmysql.cs`
- `DCT_data_import/ReadAndImport/RawData.cs`、`Tester.cs`（`ImportResult` 回傳碼樣式）
- `DCT_data_import/Program.cs:19-34`

## Extended Sections (Optional)

### 已知慣例違規（待清理）
- `db_key` 欄位名在不同格式不一致：RawData 用 `"DB_Key"`、FailPin/RecoveryRate 用 `"DB Key"`（含空格），造成 CSV 欄位比對對大小寫/空格敏感（`RawData.cs:91`、`FailPin.cs:75`、`RecoveryRate.cs:98`）。
- active importer / DB 呼叫已是明確同步模型;`Program.cs` TEST CASE 註解區塊仍保留舊呼叫範例。
- 大量被註解掉的測試案例與舊邏輯留存於 `Program.cs:40-` 與各 `DbAccess` 方法內（dead code）。
- DB result caller migration 與 Task 5 cleanup 已完成：SELECT callers 吃 `DbQueryResult`;INSERT/UPDATE/DELETE callers 吃 `DbCommandResult`。DB SQL request DTO 已改為 `DbSqlRequest`。

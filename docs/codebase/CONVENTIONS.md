# Coding Conventions

> 以下為從現有程式碼觀察到的「實際慣例」（descriptive），非規定。專案無 linter/formatter 強制，故存在不一致處，已標註。

## Core Sections (Required)

### 1) Naming Rules

| Item | Rule | Example | Evidence |
|------|------|---------|----------|
| Files | PascalCase，一檔一主類別 | `FailPin.cs`、`DatabaseService.cs` | 全 `ReadAndImport/`、`DbApi/` |
| Classes | PascalCase | `class FileProcess`、`class DBmysql` | `FileProcess.cs:10`、`DBmysql.cs:10` |
| Methods | PascalCase | `ReadAndImportRawData`、`ExecuteSqlAsync` | `RawData.cs:16`、`DatabaseService.cs:18` |
| 區域變數 | camelCase（但常見區域變數沿用型別名，如 `DatabaseService DatabaseService`） | `var writeToLog`、`DatabaseService DatabaseService` | `DbAccess.cs:69`、`DatabaseService.cs:45` |
| 靜態全域設定 | 全大寫 | `HOST`、`USER`、`FTP_IP` | `Program.cs:19-26` |
| DB 物件型別 | 蛇底線混 Pascal（外部 API 殘留） | `Execute_query`、`Execute_query_response` | `DbObject.cs:58,62` |
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
- DB 層錯誤：`DBmysql.Excute_mysql_cmd` 對 `MySqlException` 依錯誤碼補中文說明（`FormatMySqlError`，`DBmysql.cs:245-272`）；`DatabaseService.GetSafeErrorMessage` 只回 `ex.Message`、不含 StackTrace（脫敏，`DatabaseService.cs:126-138`）。
- Logging 樣式：`{yyyy/MM/dd HH:mm:ss} [INFO|ERROR] {message}`（`WriteToLog.cs:40`）。多執行緒以命名 `Mutex` 保護寫檔，逾時 30 秒（`WriteToLog.cs:37-83,113-145,171-211`）。log 寫到 `C:\temp\{exeName}\data_import_logs\DCT_data_import_Log_{yyyy_MM_dd}.txt`（每日分檔，UTF-8 BOM）。
- 敏感資料脫敏：**不足**——`Program.cs:32-34` 直接 `Console.WriteLine` 印出 HOST/USER/PASSWORD 明文（見 CONCERNS）。
- 慣例不一致：log 方法混用 `WriteErrorLog` / `WriteToDataImportLog` / `WriteInfoLog` / `WriteToCheckLog`，同類事件在不同 importer 用不同方法（如 `RecoveryRate`/`UiStatus` 偏好 `WriteToDataImportLog`，其餘偏 `WriteErrorLog`）。

### 5) Testing Conventions

- [TODO] 無測試：專案無測試專案、無測試框架、無 `*Test*.cs`（見 TESTING.md）。
- Mocking：N/A。
- Coverage 期望：N/A。

### 6) Evidence

- `DCT_data_import/Common/WriteToLog.cs:24-212`
- `DCT_data_import/DbApi/DatabaseService.cs:12-138`
- `DCT_data_import/MySQL_api/DBmysql.cs:245-272`
- `DCT_data_import/ReadAndImport/RawData.cs`、`Tester.cs`（`ImportResult` 回傳碼樣式）
- `DCT_data_import/Program.cs:19-34`

## Extended Sections (Optional)

### 已知慣例違規（待清理）
- `db_key` 欄位名在不同格式不一致：RawData 用 `"DB_Key"`、FailPin/RecoveryRate 用 `"DB Key"`（含空格），造成 CSV 欄位比對對大小寫/空格敏感（`RawData.cs:91`、`FailPin.cs:75`、`RecoveryRate.cs:98`）。
- `async` 方法被 `.GetAwaiter().GetResult()` 同步呼叫（全專案）；`UiStatus`/`TsmcIeda` 進入方法本身就非 `async`，與其餘 5 個 importer 不一致。
- 大量被註解掉的測試案例與舊邏輯留存於 `Program.cs:40-` 與各 `DbAccess` 方法內（dead code）。

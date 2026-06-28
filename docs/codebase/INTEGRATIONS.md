# Integrations

> 本系統與外部世界的接點：MySQL、FTP、SMTP、檔案系統、INI。所有設定來源為 `App.config` 與少數 hardcoded 值（已標註）。

## Core Sections (Required)

### 1) External Services

| Service | 用途 | 連線方式 | 認證 | Evidence |
|---------|------|----------|------|----------|
| MySQL DB | 主資料儲存（讀 `db_key` 待辦、寫各 import table） | `MySqlConnection` 直連 + Dapper | 帳密來自 `App.config`（`{Env}User`/`{Env}Password`） | `MySQL_api/DBmysql.cs:71`、`Program.cs:19-23` |
| FTP server | CSV 原始檔來源（下載 + 成功後刪除/改名） | `System.Net.FtpWebRequest`，big5 編碼 | 帳密來自 `App.config` ConnectionStrings | `ReadAndImport/ImportData.cs`（FTP 工具）、`Program.cs:24-26` |
| SMTP server | 寄送週報/錯誤/缺資料通知 | `SmtpClient`，server/from 由 `App.config` 指定；目前為內網匿名 relay（無 Credentials/Port/SSL） | 無 | `Common/EmailModels.cs`、`App.config`、`Common/NotificationService.cs` |
| ~~HTTP API（ApiUrl/ApiUser/ApiPassword）~~ | **已移除** — 曾為 DEAD CONFIG（`.cs` 零引用），DB 存取純走直連 MySQL;4 個死鍵已自 `App.config` 刪除 | N/A | git 歷史；現 `App.config` 已無此鍵 |

> 早期透過 Web API 存取的殘留:`ExecuteInsertWithAPI` 方法、API dead config 與「Web API body」誤導註解已清理（改名為 `ExecuteInsert`、刪 dead config 與註解）。`Execute_query`/`Execute_query_response` 型別名仍為歷史殘留（未改名,blast radius 較大），實際走 MySQL 直連（見 ARCHITECTURE/CONCERNS）。

### 2) Data Stores

- Primary store：MySQL（schema 由 `{Env}Database` 指定）。
- 讀取：`db_key` / `db_key_ui_status` 旗標表（`SELECT ... WHERE check_status>0 AND import_status=0 AND mail=0`，`DbAccess.cs:84`）。
- 寫入 table（由 `FileProcess` 各 Import 方法 + `TsmcIeda` 組 INSERT）：
  - `recovery_rate`
  - `lots_info`、`lots_statistic`、`lots_result`
  - `tester_device_info`、`tester_status`、`tester_sw_version`、`tester_production_analysis`
  - `ui_status`
  - `fail_pin_rate_info`、`fail_pin_rate_list`、`fail_pin_rate_list_pin_ball`、`fail_pin_rate_test_result`
  - `lot_mapping`（TSMC IEDA 經 `lot_mapping.csv` 快取查詢）
  - Evidence：`FileAccess/FileProcess.cs:81-1336`、`ReadAndImport/TsmcIeda.cs`
- 連線池：`server=...;Pooling=true;Min Pool Size=5;Max Pool Size=100;...;Charset=utf8mb4;`（`DBmysql.cs` `MySqlConnectionManager.Initialize`）。
- Migration：無 migration 工具；`DCT_data_import/sql/dct.sql` 可作影子 schema 初始化參考，正式 schema 仍由外部維護。
- Local file 儲存：
  - log → `C:\temp\{exeName}\data_import_logs\DCT_data_import_Log_{yyyy_MM_dd}.txt`（`WriteToLog.cs:29`）
  - `mail_temp.txt` → exe 目錄
  - `lot_mapping.csv` → `ImportSource` / `LocalImportRoot` 或 FTP 對應的 `TSMC_DATA/LotID/lot_mapping.csv`
  - `dct_import_mail_list.ini` → exe 目錄/設定路徑

### 3) Auth and Identity

- 應用程式本身無使用者登入概念（背景批次服務）。
- DB 認證：`App.config` 明文帳密（依環境鍵 `{Env}User`/`{Env}Password`）。
- FTP 認證：`App.config` ConnectionStrings 明文帳密。
- SMTP：無認證（匿名寄信）。
- [ASK USER] 上述明文憑證是否應改為 secret manager / 環境變數？（見 CONCERNS）

### 4) Monitoring and Telemetry

- Logging：自製 `WriteToLog`（檔案 + Console），無結構化日誌、無集中式 log sink。
- Metrics / Tracing / APM：[TODO] 無。
- 健康通知：`NotificationService` 以 email 充當告警（錯誤、缺資料 >1 天、週一 8:00 狀態回報）；以 `Ping` 檢查 SMTP 可達性。
- Error tracking 平台（Sentry 等）：[TODO] 無。

### 5) Evidence

- `DCT_data_import/App.config`
- `DCT_data_import/MySQL_api/DBmysql.cs`
- `DCT_data_import/ReadAndImport/ImportData.cs`、`TsmcIeda.cs`
- `DCT_data_import/FileAccess/FileProcess.cs`
- `DCT_data_import/Common/NotificationService.cs`、`EmailModels.cs`、`WriteToLog.cs`
- `DCT_data_import/Program.cs:19-26`

## Extended Sections (Optional)

### 環境路由
- 執行期偵測本機 IPv4：命中正式 IP（`10.16.92.67` / `10.16.92.68`）→ `Prod`，否則 `Dev`（`Program.cs:18` `GetEnvironment()`）。
- 所有外部連線（DB/FTP）以 `$"{Environment}Host"` 等鍵動態組合，故同一份 binary 跨環境共用。
- FTP 根路徑亦分環境：Dev `/DCT_Log/DCT_DB_DATA_Dev/`、Prod `/DCT_Log/DCT_DB_DATA/`（`ImportData.cs`）。

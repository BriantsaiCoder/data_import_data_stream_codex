# Codebase Structure

## Core Sections (Required)

### 1) Top-Level Map

| Path | Purpose | Evidence |
|------|---------|----------|
| `DCT_data_import.sln` | Visual Studio 方案檔（單一專案） | `DCT_data_import.sln` |
| `DCT_data_import/` | 主專案根目錄 | `.csproj` 位置 |
| `DCT_data_import/Program.cs` | 程式進入點；環境偵測 + 3 執行緒監督迴圈 | `Program.cs:20,29` |
| `DCT_data_import/ReadAndImport/` | 各資料格式 importer 與 FTP/Local 檔案來源抽象 | `ReadAndImport/*.cs`（7 個 importer + `ImportData` / `ImportFileSource`） |
| `DCT_data_import/FileAccess/` | 檔案↔DB 橋接、CSV 格式契約與欄位常數、INI 讀寫 | `FileAccess/FileProcess.cs`、`FileContentFormat.cs`、`CsvColumnNames.cs`、`ReadWriteINIfile.cs` |
| `DCT_data_import/DbApi/` | DB 服務介面與資料物件 | `DbApi/DatabaseService.cs`、`DbAccess.cs`、`DbObject.cs` |
| `DCT_data_import/MySqlApi/` | MySQL 直連與 SQL 執行（Dapper） | `MySqlApi/DBmysql.cs` |
| `DCT_data_import/Common/` | 共用工具：log、寄信、SPC 統計、email 模型、執行模式旗標（DryRun 影子驗證） | `Common/WriteToLog.cs`、`NotificationService.cs`、`EmailModels.cs`、`CalculateSPC.cs`、`RuntimeMode.cs` |
| `DCT_data_import/Properties/AssemblyInfo.cs` | Assembly 中繼資料（版本 `2026.2.5.0`） | `Properties/AssemblyInfo.cs:33` |
| `DCT_data_import/App.config` | 執行期設定（DB/FTP/SMTP/資料來源切換） | `App.config` |
| `DCT_data_import/DCT_data_import.csproj` | SDK-style `net8.0-windows` + PackageReference 套件清單 | `DCT_data_import.csproj` |
| `專案架構報告.md` / `專案架構視覺化.html` | 已刷新至 S2 SQL 參數化後的 root 架構導覽 | repo 根目錄 |

### 2) Entry Points

- Main runtime entry：`DCT_data_import/Program.cs:29`（`static void Main`）。
- Secondary entry points：無（單一 console exe，無 worker/CLI 子命令）。
- 啟動如何選擇行為：無命令列參數解析；行為由 `App.config` 與執行期 IP 偵測（`Dev`/`Prod`）決定（`Program.cs:224` `GetEnvironment()`）。`Main` 內以 3 條 `Thread` 分別跑 Tester / UiStatus / TSMC 模式，並以監督迴圈在執行緒死亡時重啟（`Program.cs` 監督段）。

### 3) Module Boundaries

| Boundary | What belongs here | What must not be here |
|----------|-------------------|------------------------|
| `Program.cs`（Presentation/Orchestration） | 環境偵測、執行緒生命週期、依 `CheckStatus` 位元決定跑哪些 importer | 解析邏輯、SQL、FTP 細節 |
| `ReadAndImport/`（Business Logic） | 每種資料格式的下載→驗證→解析→呼叫匯入→清理流程 | 連線字串組裝、MySQL 驅動細節 |
| `FileAccess/FileProcess.cs`（Bridge） | DataTable→SQL INSERT 組裝、批次切割、級聯刪除 | FTP 存取（屬 importer/`ImportData`） |
| `FileAccess/FileContentFormat.cs`（Data Contract） | 6 種 CSV 格式的欄位結構與 `CompareXxx()` 欄位驗證 | DB 寫入、FTP |
| `DbApi/` + `MySqlApi/`（Data Access） | 連線參數驗證、SQL 執行、結果轉 `JArray`、`db_key` 旗標查詢/更新 | 業務解析邏輯 |
| `Common/`（Cross-cutting） | 日誌（Mutex 保護）、SMTP 寄信、SPC 統計、INI | 匯入流程控制 |

### 4) Naming and Organization Rules

- 檔案命名：PascalCase，一檔一主類別（例：`FailPin.cs` → `class FailPin`）。例外：`FileContentFormat.cs` 內含 6 個 format 類別；`DBmysql.cs` 內含 `DBmysql` + `MySqlConnectionManager`；`DbObject.cs` 內含巢狀型別。
- 目錄組織：依「層/職責」分目錄（`ReadAndImport`/`FileAccess`/`DbApi`/`MySqlApi`/`Common`），非依 feature。
- Namespace 對齊：`Common/` → `DCT_data_import.Common`、`FileAccess/` → `DCT_data_import.FileAccess`、`DbApi/` → `DCT_data_import.DbApi`、`MySqlApi/` → `DCT_data_import.MySqlApi`、`ReadAndImport/` → `DCT_data_import.ReadAndImport`。`Program` / `ImportDecision` 保留於 root `DCT_data_import`。

### 5) Evidence

- `docs/codebase/.codebase-scan.txt`（初次掃描快照，當時 21 個 `.cs`；**現況 25 個 `.cs`【含 `AssemblyInfo`，24 個源碼檔】、約 7,900 行**，檔數/行數以此處為準）
- `DCT_data_import/Program.cs:15-38`
- `DCT_data_import.sln`
- 各層代表檔（見上表 Evidence 欄）

## Extended Sections (Optional)

- importer ↔ 目標 MySQL table 對應，見 ARCHITECTURE.md「System Flow」與 INTEGRATIONS.md「Data Stores」。

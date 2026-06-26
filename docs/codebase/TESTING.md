# Testing

> 結論先講：**本專案沒有任何自動化測試**。以下記錄現況與可驗證的「測試替代物」。

## Core Sections (Required)

### 1) Test Frameworks

- 單元/整合測試框架：[TODO] **無**。`packages.config` 無 xUnit / NUnit / MSTest；`.sln` 無測試專案；全庫無 `*Test*.cs` / `*Tests.cs`。
- Evidence：`docs/codebase/.codebase-scan.txt`（21 個 `.cs`，全為產品碼）、`DCT_data_import.sln`（單一專案）、`packages.config`（6 套件皆非測試框架）。

### 2) Test Organization

- 測試目錄：[TODO] 無 `tests/` / `*.Tests` 專案。
- 既有「測試」形式：`Program.cs:40-70` 有一大段**被註解掉的手動 TEST CASE**（示範各 importer 呼叫與範例 FTP 路徑），屬開發者手動驗證遺跡，非自動化測試。
- Evidence：`DCT_data_import/Program.cs:40-70`（commented TEST CASE block）。

### 3) Mocking and Fixtures

- Mocking framework：N/A（無測試）。
- 可測試性障礙（影響未來導入測試）：
  - 直接 `new DBmysql()` + 靜態 `Program.HOST/USER/...`，無 DI、無介面抽象（`DatabaseService.cs:45-`）。
  - importer 直接觸發 FTP / 檔案系統 / `GC.Collect()`，副作用未隔離。
  - `MySqlConnectionManager` 為 process 級 Singleton，跨測試難重置。
- Evidence：`DbApi/DatabaseService.cs`、`MySQL_api/DBmysql.cs`。

### 4) Coverage and CI

- Coverage 工具/門檻：[TODO] 無。
- CI/CD：[TODO] 無 `.github/workflows/`、無 pipeline 設定（見 STACK.md §3）。
- 建置驗證：本機為 macOS，**未能實際建置**（net462 + packages.config 需 Windows MSBuild）。建置是否成功屬 [TODO]。

### 5) Evidence

- `docs/codebase/.codebase-scan.txt`
- `DCT_data_import.sln`
- `DCT_data_import/packages.config`
- `DCT_data_import/Program.cs:40-70`

## Extended Sections (Optional)

### [ASK USER] 測試策略決策
- 是否要為此 ETL pipeline 補測試？若要，建議優先順序：
  1. 先把 `FileContentFormat.CompareXxx()` 欄位驗證與 `CalculateSPC.AverageOfSumSquare`（純函式、無副作用）以 characterization test 釘住行為——這兩處最易測且最易回歸。
  2. 再以 Testcontainers MySQL 對 `FileProcess.Import*` 做整合測試。
- 此屬團隊意圖，需確認後才動工。

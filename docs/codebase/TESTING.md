# Testing

> 結論先講：本專案**原本沒有任何自動化測試**；後續新增了**唯一一個** `DCT_data_import.Tests` 專案，僅為釘住 CONCERNS R5 加權和 bug（非完整測試套件）。以下記錄現況與可驗證的「測試替代物」。

## Core Sections (Required)

### 1) Test Frameworks

- 單元/整合測試框架：產品碼端**無**內建測試；後續新增 `DCT_data_import.Tests`（SDK-style、net462、**xUnit**）作為唯一測試專案，僅針對 R5 純函式 `DbAccess.ComputeImportResult`（1 條 happy-path 綠 + 2 條 by-design RED 釘 R5）。主專案本身仍無測試。
- Evidence：`docs/codebase/.codebase-scan.txt`（審計當時 21 個 `.cs`，全為產品碼）、`DCT_data_import.sln`（含主專案 + Tests 專案）、`DCT_data_import.Tests/CheckStatusWeightedSumTests.cs`、`DCT_data_import.Tests/README.md`。

### 2) Test Organization

- 測試目錄：[TODO] 無 `tests/` / `*.Tests` 專案。
- 既有「測試」形式：`Program.cs:40-70` 有一大段**被註解掉的手動 TEST CASE**（示範各 importer 呼叫與範例 FTP 路徑），屬開發者手動驗證遺跡，非自動化測試。
- Evidence：`DCT_data_import/Program.cs:40-70`（commented TEST CASE block）。

### 3) Mocking and Fixtures

- Mocking framework：N/A（現有測試為純函式 `ComputeImportResult`，無 mocking 需求）。
- 可測試性障礙（影響未來導入測試）：
  - 直接 `new DBmysql()` + 靜態 `Program.HOST/USER/...`，無 DI、無介面抽象（`DatabaseService.cs:45-`）。
  - importer 直接觸發 FTP / 檔案系統 / `GC.Collect()`，副作用未隔離。
  - `MySqlConnectionManager` 為 process 級 Singleton，跨測試難重置。
- Evidence：`DbApi/DatabaseService.cs`、`MySQL_api/DBmysql.cs`。

### 4) Coverage and CI

- Coverage 工具/門檻：[TODO] 無。
- CI/CD：GitHub Actions（`.github/workflows/ci.yml`）— windows-latest 上 `nuget restore` → `msbuild` build → `dotnet test`，push / PR to master 觸發（見 STACK.md §3）。CI test filter 為 `Category!=ByDesignRed&Category!=CaptureBaseline`：`CaptureBaseline` 是 emit-only golden-master 基準（不進綠燈門檻）；`ByDesignRed` 在 **R5 已修復（2026-06-27）** 後**已無對應測試**（3 條 `_R5` 已轉綠並移除 trait、重納綠燈門檻），filter 保留供未來 net8 by-design 框架差異測試沿用同機制。
- 建置驗證：主專案（net462）已實測可在 macOS 以 `dotnet build` + `FrameworkPathOverride`→Mono `4.6.2-api` 參考組件**零錯誤編譯**（`./packages` 經 NuGet 還原）；genuinely Windows-only 的是**執行期**（P/Invoke / `C:\temp` / FTP / MySQL）。測試專案在該 override 下尚有 `System.Runtime` facade 小坑（CS0012）未解，實跑 net462 測試以 Windows / Mono 為準；R5 修復後以 net8.0 + xUnit 跑同套加權和邏輯為 **9 綠 / 0 紅**（6 happy-path Theory + 3 條 `_R5`，全綠代表 R5 已修），full suite 188 綠。

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

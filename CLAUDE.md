# CLAUDE.md

> DCT_data_import — 半導體測試資料 ETL 批次服務。本檔給 AI 協作者；工程細節以 `docs/codebase/` 七檔為權威來源，本檔只放「不知道就會踩雷」的關鍵約束。

## 這是什麼

C# **.NET 8 (`net8.0-windows`)** Console App（`OutputType=Exe`），長駐輪詢式多執行緒 ETL：3 條 `Thread`（Tester / UiStatus / TSMC 模式）週期性輪詢 MySQL `db_key` 旗標表 → 從 FTP 或 Local 來源取得 big5 CSV → 解析驗證 → 以 Dapper parameters 寫回 MySQL → 依結果寄信/搬檔。入口 `DCT_data_import/Program.cs`。

詳見 [docs/codebase/ARCHITECTURE.md](docs/codebase/ARCHITECTURE.md) 與 [專案架構報告.md](專案架構報告.md)。

## Build / Run / Test

> ⚠️ **執行期 Windows-oriented**：`kernel32.dll` P/Invoke、hardcoded `C:\temp` log、FTP、MySQL 連線等需要 Windows/實際環境才能做 production-like smoke。macOS/Linux 可作 `dotnet build` / `dotnet test` evidence，但不等於完整 runtime 驗證。

```powershell
dotnet restore DCT_data_import.sln
dotnet build DCT_data_import.sln --configuration Release --no-restore
dotnet test DCT_data_import.Tests\DCT_data_import.Tests.csproj --configuration Release --no-build
dotnet publish DCT_data_import\DCT_data_import.csproj --configuration Release --runtime win-x64 --self-contained true
DCT_data_import\bin\Release\net8.0-windows\DCT_data_import.exe
```

- 主專案與測試專案皆為 SDK-style PackageReference。
- CI 為 GitHub Actions（`.github/workflows/ci.yml`，windows-latest，restore + NuGetAudit + build + test）。
- 無 linter / formatter 設定；觀察到的格式：**4 空格縮排、Allman 大括號、`using` 置頂**。

## 關鍵約束（踩雷點）

1. **絕不把憑證印到 Console / log**。`App.config` 內含既有 DB/FTP 帳密，屬已知債 S1。`Program.cs` 目前只印 PASSWORD set/unset，不印明文；新增或修改設定輸出時要維持遮罩。
2. **`ImportResult.Result` 回傳碼語意固定**：`0`=檔案不存在、`1`=成功、`2`=驗證/讀檔失敗、`3`=重複或匯入失敗。各 importer 一致，動其中之一要同步全部理解。
3. **CheckStatus 加權和（R5，已修復）**：`DbAccess.ComputeImportResult` = `8*recoveryRate + 4*tester + 2*testResult + failPin`，分量必須先正規化為 `Result == 1 ? 1 : 0`。不可改成 `Math.Min(x,1)`，那會把失敗碼 `2/3` 當成功。
4. **新增 SQL 必須走 Dapper parameters / identifier guard**。S2 已完成 `DbAccess` / `TsmcIeda` / `FileProcess` 批次 INSERT 參數化；不要回到手動拼接外部值。`DBmysql.FilterSqlCommand` 只是殘留防線，不是主要安全控制。
5. **狀態機在 DB 不在程式**：`db_key` 表的 `check_status` / `import_status` / `mail` 欄位驅動「待處理/已匯入/待寄信」，非程式內狀態。

## 慣例與風險

- 沿用既有檔風格；註解以繁中為主。
- **namespace 與資料夾不對齊**是既有現象，勿順手「修正」。
- `async` 方法多被 `.GetAwaiter().GetResult()` 同步呼叫；現代化非本專案目標，維運沿用。
- 完整風險清單見 [docs/codebase/CONCERNS.md](docs/codebase/CONCERNS.md)。

## 文件地圖

| 檔 | 內容 |
|----|------|
| `docs/codebase/*.md` | 七檔工程文件（STACK/STRUCTURE/ARCHITECTURE/CONVENTIONS/INTEGRATIONS/TESTING/CONCERNS） |
| `docs/codebase/NET8_UPGRADE_TEST_STRATEGY.md` | 歷史 migration 測試策略，描述 net462→net8 過程，不是 live stack 權威 |
| `docs/net8-migration/REMAINING-WORK.md` | net8 遷移收尾 backlog 與 A1-A4 狀態 |
| `專案架構報告.md` | 架構報告 |
| `專案架構視覺化.html` | self-contained 互動視覺化 |
| `DCT_data_import.Tests/README.md` | 測試說明 |

## AI 協作守則

- **.NET 8 維運**：沿用既有風格，**不主動現代化**（不擅自把 fake async 改真 async、不重排 namespace、不改既有 SQL 路徑除非任務要求）。
- 改高扇入共用檔（`FileProcess` / `DbAccess` / `ImportData` 基底）前先列依賴方。
- 修 bug 先寫或確認 failing regression test，再修；若沒有可測 seam，要回報替代驗證。
- 改動 `docs/codebase/` 對應的程式碼時同步更新文件，doc rot 視同 bug。

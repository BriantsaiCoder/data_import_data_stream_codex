# AGENTS.md

> DCT_data_import — 半導體測試資料 ETL 批次服務。本檔給 AI 協作者（Codex 及相容工具）；工程細節以 `docs/codebase/` 七檔為權威來源，本檔只放「不知道就會踩雷」的關鍵約束。

## 這是什麼

C# **.NET Framework 4.6.2** Console App（`OutputType=Exe`），長駐輪詢式多執行緒 ETL：3 條 `Thread`（Tester / UiStatus / TSMC 模式）週期性輪詢 MySQL `db_key` 旗標表 → 從 FTP 拉 CSV（big5）→ 解析驗證 → 串接 SQL INSERT 寫回 MySQL → 依結果寄信/搬檔。入口 `DCT_data_import/Program.cs:27`。

詳見 `docs/codebase/ARCHITECTURE.md`。

## Build / Run / Test

> ⚠️ **Windows-only，macOS/Linux 無法 build/run**。專案依賴 `kernel32.dll` P/Invoke（INI 讀寫）、hardcoded `C:\temp` log 路徑；net462 + `packages.config` 需 **Windows MSBuild**，非單純 `dotnet build`。

```powershell
nuget restore DCT_data_import.sln                          # packages.config 模式,dotnet restore 不還原它
msbuild DCT_data_import.sln /p:Configuration=Release       # VS Developer Command Prompt
DCT_data_import\bin\Release\DCT_data_import.exe            # 產物 console exe

dotnet test DCT_data_import.Tests\DCT_data_import.Tests.csproj   # 唯一測試:R5 回歸樁(見下)
```

- 主專案用 `packages.config`（舊式），測試專案用 PackageReference（SDK-style）— 兩者共存於同一 `.sln`，`dotnet test` 不還原 packages.config，需先 VS / `nuget restore`。
- 無 linter / formatter 設定，無 CI。觀察到的格式：**4 空格縮排、Allman 大括號、`using` 置頂**。

## 關鍵約束（踩雷點）

1. **絕不把憑證印到 Console / log**。`App.config` 內含明文 DB/FTP 帳密（已在版控,屬已知債 CONCERNS S1），`Program.cs:32-34` 現況會印出 HOST/USER/PASSWORD 明文（S3）— 改動該區時不要擴大洩露，最好遮罩。新增設定一律 key name 進範例、值走外部。
2. **`ImportResult.Result` 回傳碼語意固定**：`0`=檔案不存在、`1`=成功、`2`=驗證/讀檔失敗、`3`=重複或匯入失敗。各 importer 一致，動其中之一要同步全部理解。
3. **CheckStatus 加權和是脆弱契約（R5,High）**：`DbAccess.ComputeImportResult` = `8*recoveryRate + 4*tester + 2*testResult + failPin`，假設各分量只回 0/1，但實際回 0/1/2/3 → 任一回 2/3 會溢位污染高位 bit → 誤判失敗+寄信。**已抽純函式 + `DCT_data_import.Tests` 2 條 by-design RED 釘住，修法待規格確認**。碰這塊前先讀 `docs/codebase/CONCERNS.md` R5 與測試 README。
4. **SQL 全字串串接、零參數化**（S2）：`FileProcess` / `DbAccess` / `TsmcIeda` 直接把外部值拼進 SQL。沿用既有風格時務必意識到 injection 風險；新寫 SQL 優先參數化（Dapper 已支援具名參數）。
5. **狀態機在 DB 不在程式**：`db_key` 表的 `check_status`(bitmask) / `import_status` / `mail` 欄位驅動「待處理/已匯入/待寄信」，非程式內狀態。

## 慣例與風險

- 沿用既有檔風格（縮排/命名/註解）；註解以**繁中為主**（夾雜少量簡體）。命名：檔/類/方法 PascalCase、區域變數 camelCase（常沿用型別名）、靜態全域設定全大寫、DB 物件 snake_case。
- **namespace 與資料夾不對齊**（根層類別常掛 `DCT_data_import` 而非 `DCT_data_import.FileAccess`）— 既有現象，勿順手「修正」。
- `async` 方法全被 `.GetAwaiter().GetResult()` 同步呼叫（fake async）；現代化非本專案目標，維運沿用。
- 完整風險清單見 `docs/codebase/CONCERNS.md`。

## 文件地圖

| 檔 | 內容 |
|----|------|
| `docs/codebase/*.md` | 七檔工程文件（STACK/STRUCTURE/ARCHITECTURE/CONVENTIONS/INTEGRATIONS/TESTING/CONCERNS），每條附 file:line evidence — **權威來源** |
| `專案架構報告.md` | 架構報告 v2.0.0（已對齊實際程式碼） |
| `專案架構視覺化.html` | self-contained 互動視覺化 |
| `DCT_data_import.Tests/README.md` | R5 回歸樁說明（含「故意紅燈」提醒） |

## AI 協作守則

- **.NET Framework 維運**：沿用既有風格，**不主動現代化**（不擅自把 fake async 改真 async、不重排 namespace、不改既有 SQL 串接風格除非任務要求）。
- 改高扇入共用檔（`FileProcess` / `DbAccess` / `ImportData` 基底）前先列依賴方。
- 修 bug 先寫 failing regression test 再修（R5 已示範此模式）。
- 改動 `docs/codebase/` 對應的程式碼時同步更新文件，doc rot 視同 bug。

# Phase 1：.NET 8 遷移本體（含 rollback 策略）

> .NET Framework 4.6.2 → .NET 8 升級的**第二階段**：實際換框架 + 修 net8 阻擋點 + golden-master 驗證 + production cutover。
> **前置硬 gate**：[phase-0-safety-net.md](phase-0-safety-net.md) 全綠且 net462 基準已 capture（策略檔 §1 硬性順序）。
> 高風險變更（migration），**rollback 策略見本檔末節，為交付必要條件**（全域 CLAUDE.md）。

## 目標框架決策（鎖死，非偏好）

**TFM = `net8.0-windows`**（不是純 `net8.0`，不是 net10）。任一原因即足以排除純 net8.0：

1. `FileAccess/ReadWriteINIfile.cs:10-13` 對 `kernel32.dll` 的 P/Invoke（`WritePrivateProfileString`/`GetPrivateProfileString`）是 Windows-only Win32 API；`-windows` 讓 assembly 視為 Windows-only、消 CA1416 平台相容性警告。
2. `FtpWebRequest` + big5 CSV 讀取。
3. hardcoded `C:\temp` log 路徑（`WriteToLog.cs:29/154`）。
4. MySQL 連線。

Console exe **不需** WindowsDesktop SDK / `UseWindowsForms`，普通 `Microsoft.NET.Sdk` + `<TargetFramework>net8.0-windows</TargetFramework>` 即可。測試專案因 `ProjectReference` 主專案，net8 端同須 `net8.0-windows` 才能引用 kernel32 程式碼路徑。**不選 net10**：本升級鎖定 .NET 8（LTS），先求「跨大版本一次到位且可長期維運」，避免在升級當下又追逐更新版 runtime 的相依風險；net10 待 net8 落穩後另議。此為專案既定升級約束，非臨時偏好。

## 關鍵路徑

```
P0-4(big5 閘門) → P1-1(SDK-style) → P1-1b(CI) → P1-2(切 TFM) → P1-6(big5 provider) → P1-8(golden-master+cutover)
```

P1-3/P1-4/P1-5/P1-7 都 dependsOn P1-2，可在 P1-2 後平行修，全部收斂進 P1-8。**P1-7b（dry-run 開關）亦 dependsOn P1-2，但它另是 P1-8 影子步驟（cutover step 5 / Rollback L3）的硬前置——必須在影子跑之前落地。**

## 決策待確認（Phase 1 gating；未回覆採■預設）

這 5 項實質改變 Phase 1 內容。**已標推薦預設，未回覆即按預設執行**；要改任一項在動工前提出。

| # | 決策 | ■預設（推薦） | 替代 | trade-off |
|---|---|---|---|---|
| Q1 | App.config vs appsettings.json | **保留 App.config**（加 `System.Configuration.ConfigurationManager` 套件繼續讀，自動轉 `.dll.config`） | 趁升級把明文帳密搬外部 | 預設符合 CLAUDE.md「不主動現代化」、設定面 diff 最小、不擴大洩露；替代解 S1 安全債但超出純升級範圍、改動大 |
| Q2 | `Thread.Abort` 修法 | **最小維運修**（刪 3 個 `.Abort()` + 死碼 catch，保 `Interrupt()`+recreate，supervisor 語意不變） | `CancellationToken` 協作式取消 | 預設最小 diff、符合不主動現代化；替代是官方正解、唯一保證 FtpWebRequest/MySQL/mail_temp 資源乾淨釋放，但改動大、需更完整 regression |
| Q3 | 雙 TFM 過渡 | **雙 TFM**（`net462;net8.0-windows`，net462 半全程綠當 L1 fallback + 兩 runtime 自動產 golden-master 兩份 capture） | 直接 single `net8.0-windows` | 預設較安全、分支內可回 net462、自動雙 runtime diff；代價是過渡期維護兩套設定。替代較快但失去 fallback 與自動 golden-master |
| Q4 | cutover 影子模式範圍/時長 | **dry-run 影子跑滿 ≥1 完整營運週期**（涵蓋各 importer × 各 check_status bit × 輪詢）+ DB 快照/binlog 保留窗 | 縮短/省略 | 影響 P1-8 cutover 能否落地。**dry-run 開關已列為正式 task P1-7b**（盤點完成、3 chokepoint gate 法已定），P1-8 影子步驟硬依賴它先落地 |
| Q5 | MySql.Data 版本 | **pin 9.4.0** | 評估特定 net8 相容版本 | 不 pin 會自動升降版改變 Convert Zero Datetime/微秒/DateTimeKind（root cause D 風險面） |

> **Q4 的隱含相依（已收斂為 P1-7b）**：cutover「dry-run（只解析比對、不真 INSERT/搬檔/寄信）」假設程式有 dry-run 開關，**但現況沒有**。此缺口已立為正式 task **P1-7b**（見下方任務清單）：dry-run inventory workflow 盤點全 41 個 production mutation 站（DB 寫/FTP 搬檔/SMTP 寄信），completeness critic 獨立 re-scan 確認無遺漏，gate 法收斂到 **6 個 chokepoint**（從設定讀、預設 off、on 時於 chokepoint 短路）。若不建此開關，影子模式無法落地，rollback L3 的資料污染防護退化為僅靠 DB 快照/binlog。

---

## 任務清單

### P1-1：csproj 轉 SDK-style + packages.config→PackageReference（維持 net462 single-TFM）｜Blocker

dependsOn：Phase 0 全綠。**硬前置、最先做、隔離成獨立 commit（只轉結構不換框架）。**

**targetFiles**：`DCT_data_import/DCT_data_import.csproj`、`DCT_data_import/packages.config`、`DCT_data_import/Properties/AssemblyInfo.cs`

**步驟**：
- (A) packages.config→PackageReference：6 套件搬進 csproj `<PackageReference>`，刪 `packages.config`。
- (B) csproj 轉 SDK-style：`<Project Sdk="Microsoft.NET.Sdk">` 取代 `ToolsVersion=15.0`；刪 `Import Microsoft.Common.props/.CSharp.targets`、`ProjectGuid`、`FileAlignment`、Debug/Release 樣板 PropertyGroup。**此步暫保留** `<TargetFramework>net462</TargetFramework>`（下一 task 才換）。
- 刪整段顯式 `<Compile Include>`（含註解 `Removed` 行）改隱式 glob——**勿與 glob 並存**（NETSDK1022 duplicate）。
- 刪裸 `<Reference>` `System`/`System.Core`/`Microsoft.CSharp`/`System.Data`/`System.Configuration`（shared framework 隱含或改套件提供）。保留 `OutputType=Exe`/`RootNamespace`/`AssemblyName`。
- **AssemblyInfo 撞名**：SDK-style 預設 `GenerateAssemblyInfo=true` 會與手寫 `Properties/AssemblyInfo.cs` 撞 CS0579 → 設 `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>`（因 P0-1 的 `InternalsVisibleTo` 在手寫 AssemblyInfo.cs，保留該檔）。
- App.config 不需顯式 `<None>`（SDK 自動轉 `.dll.config`）。`.sln` 不改。

**高扇入結構變更紀律**（CLAUDE.md「改高扇入檔先列依賴方」）：轉換前後比對 `bin` 輸出檔清單一致；隱式 glob 後確認編譯 `.cs` 檔數與原 `<Compile>` 清單相符（排除原標 `Removed`）。

**驗證**：net462 single-TFM 下 `dotnet restore` + `dotnet build` + `dotnet test` 全綠（P0 安全網不變紅）；產物檔清單一致；無 NETSDK1022/CS0579。
**Rollback（L0）**：全在 `feat/net8-migration` 分支，master 保留可 build net462 真相。此步單一 commit，`git revert` 即回舊式 csproj + packages.config。

---

### P1-1b：更新 CI workflow（nuget+msbuild → dotnet 全鏈 + net8 SDK）｜Blocker（critic 補入）

dependsOn：P1-1（緊接，先於 P1-2）

**為什麼**：現 `.github/workflows/ci.yml` 用 `nuget restore` + `msbuild`（因 packages.config）。P1-1 轉 SDK-style 後這條鏈會壞——**若不同步改 CI，PR 一開就紅、整條關鍵路徑卡住**。原 8 task 草案遺漏此步（critic 標 Blocker）。

**targetFiles**：`.github/workflows/ci.yml`

**步驟**：
- P1-1 後（仍 net462 SDK-style）：`nuget restore`+`msbuild` → `dotnet restore`/`dotnet build`/`dotnet test`。runner 維持 `windows-latest`。
- P1-2 後（加 net8）：`actions/setup-dotnet` 補 net8 SDK；build matrix 兩框架（`net462`、`net8.0-windows`）皆 build。
- 加 **capture step**：`--filter Category=CaptureBaseline --logger "console;verbosity=detailed"` 在兩 runtime 各跑、各存 log artifact；比對步驟 `fail-on-diff`（排除 `DateTimeParserCulture` 的 `Now` fallback 非決定列）。
- 維持 `--filter Category!=ByDesignRed` 排除 R5 紅燈。

**驗證**：CI 兩框架 build pass 當 gate；capture step 兩 runtime 各產 log；fail-on-diff 步驟存在。
**Rollback**：ci.yml 獨立 revert；既有開著的 PR 須改在其 head 分支才觸發。

---

### P1-2：切 TargetFramework 到 net8.0-windows + 套件收斂 6→4 +（預設）雙 TFM 過渡｜Blocker

dependsOn：P1-1

**targetFiles**：`DCT_data_import/DCT_data_import.csproj`、`DCT_data_import.Tests/DCT_data_import.Tests.csproj`

**步驟**：
- **TFM**（依 Q3）：預設雙 TFM `<TargetFrameworks>net462;net8.0-windows</TargetFrameworks>`（net462 半當 L1 fallback）；若 Q3 選直接切則 `<TargetFramework>net8.0-windows</TargetFramework>`。**必為 `net8.0-windows` 非純 net8.0。**
- **套件收斂（6→4）**：
  - 移除 `System.Runtime.CompilerServices.Unsafe 6.1.2`、`System.Threading.Tasks.Extensions 4.6.3`（net8 shared framework 內建，留著 NU1605/重複引用衝突）；雙 TFM 期用 `Condition="'$(TargetFramework)'=='net462'"` 只在 net462 引。
  - `System.Configuration.ConfigurationManager` 9.0.7 → **8.0.1**（對齊 net8 runtime servicing line；9.0.x 屬 .NET 9 會拉 transitive 衝突，SDK 把 NU1605 當 error）。雙 TFM 期可 TFM-conditional Version（net462 仍 9.0.7）。
  - `Dapper 2.1.66` / `MySql.Data 9.4.0` / `Newtonsoft.Json 13.0.3` 保留；**MySql.Data 顯式 pin 9.4.0**（Q5；不 pin 會改 Convert Zero Datetime/微秒/DateTimeKind）。
  - net8 半最終剩 **4 條 PackageReference**。
- **測試專案同步**：改 `<TargetFrameworks>net462;net8.0-windows</TargetFrameworks>`，`Microsoft.NETFramework.ReferenceAssemblies` 用 Condition 只在 net462 → 同一套測試在兩 runtime 各跑一次產兩份 capture log 直接 diff（golden-master）。

> 此步切 TFM 後 net8 半「會編不會跑」——big5/Thread.Abort/App.config 等 runtime blocker 由 P1-3..P1-7 修。

**驗證**：net8.0-windows 半 `dotnet build` 綠（證內建滿足移除的兩套件、無 NU1605）；net462 半（雙 TFM）仍 build+test 綠；`dotnet list package` 確認 net8 剩 4 條且 ConfigurationManager=8.0.1、MySql.Data pin 9.4.0；CI matrix 兩框架皆 build pass。
**Rollback（L1）**：雙 TFM 下 net462 半全程綠，分支內隨時 build net462。回退此 commit = 回 net462 single-TFM（P1-1 狀態）。雙 TFM 是腳手架，P1-8 收尾才砍。

---

### P1-3：清理 App.config（刪 `<startup>` 與 bindingRedirect）+ 確認 8 個 static config key 防 `TypeInitializationException`｜Blocker

dependsOn：P1-2｜**targetFiles**：`DCT_data_import/App.config`、`DCT_data_import/Program.cs:19`

**步驟**：
- App.config **原樣保留** `appSettings`/`connectionStrings`（經 ConfigurationManager 套件，net8 編譯時自動輸出 `DCT_data_import.dll.config`）——**不改 appsettings.json**（Q1 預設、CLAUDE.md 不主動現代化）。
- 刪 `<runtime><assemblyBinding>` 整段 bindingRedirect（net8 用 deps.json 自動解析）+ 刪 `<startup><supportedRuntime .NETFramework v4.6.2>`（net8 不需；雙 TFM 期 net462 仍讀，net8 忽略無害，可保留至 P1-8 或用 `GenerateBindingRedirectsOutputType` TFM-conditional 分流）。
- **最大地雷（本 task 核心）**：`Program.cs:19-26` 的 8 個 static 欄位（`HOST`/`USER`/`PASSWORD`/`PORT`/`DATABASE` 走 AppSettings；`FTP_IP`/`FTP_USER`/`FTP_PASSWORD` 走 `ConnectionStrings[...].ConnectionString`）在 **type-init（早於 Main）執行**，任一 key 缺失或 `ConnectionStrings["x"]` 回 null 再 `.ConnectionString` → NRE，會在 type-init 拋 `TypeInitializationException` **落在 Main try/catch 之外**，現有例外處理攔不到、啟動即崩。升級後第一件事確認 `.dll.config` 有產出且 8 個 key（`DevHost`/`ProdHost`/...、`FtpIp`/`FtpUser`/`FtpPassword`）名稱**完全一致**。
- **安全（CLAUDE.md Hard Rule）**：App.config 明文 DB/FTP 帳密屬已知債（S1）不在本次處理，但清理過程**絕不把帳密印進 build log/console**。

**驗證**：net8 build 後 bin 有 `DCT_data_import.dll.config` 含 8 個 key；寫 net8 啟動 smoke（或 type-init 觸發測試）確認 8 個 static 欄位不拋 `TypeInitializationException`；diff 確認只刪 `<startup>`/`<runtime>` 段、未動帳密值、未新增帳密列印。
**Rollback**：App.config 改動可獨立 revert（刪的段 net8 本不需，回退無功能損失）。純設定、資料層無關。

---

### P1-4：移除 `Thread.Abort()` ×3 + 清理 net8 永不觸發的 `catch(ThreadAbortException)` 死碼｜Blocker

dependsOn：P1-2｜**targetFiles**：`Program.cs:106`、`Program.cs:131`、`Program.cs:156`

**為什麼**：`.Abort()` 在 net5+ 是 SYSLIB0006 obsolete 且 runtime **無條件擲 `PlatformNotSupportedException`**（非 ThreadAbortException）。只抑制警告不移除 = 每次 supervisor 判定 thread 死的迭代仍擲 PNSE 被 `catch(Exception)` 吞。

> **critic 修正（已對源碼查證）**：先前草案誤稱「`threadXxxAlive` 初始化 false 且未從 IsAlive 重新賦值」。**實際 `Program.cs:200-202` 確有 `threadTesterAlive = threadTesterMode.IsAlive;`（三條，在迴圈底部重新賦值）**——supervisor **真的**靠 `IsAlive` 輪詢 thread 存活、死了才重建。所以移除 `Abort()` 安全：對「已判定死亡」的 thread 呼叫 `Abort()` 在 net8 只會丟 PNSE，無實益。

**步驟**（Q2 預設最小維運修）：
- 刪 3 個 `.Abort()` 呼叫（:106/:131/:156），保留 `.Interrupt()`（net8 仍支援，喚醒 Sleep/Wait/Join 阻塞 thread）+ recreate+Start 序列。
- 刪因此永不觸發的 `catch(ThreadAbortException)` 區塊（:115-119/:140-144/:165-169，net8 從不擲此例外）——自引入死碼依 CLAUDE.md 必清。
- 此修保 threads 仍被遺棄（無 Join）+ recreate，與今日一致，僅去掉只會 throw 的呼叫。
- **先寫 failing regression test** 釘住 supervisor「thread 死→重建」語意再改（CLAUDE.md 修 bug 先寫 test）。
- 協作式取消（`CancellationToken`，Q2 替代）為 opt-in 正解，非預設。

**驗證**：net8 下寫 supervisor regression test（模擬 `threadXxxAlive=false`→確認新 Thread 被建+Start、不擲 PNSE）先紅後綠；grep 確認 3 個 `Abort()` 與 3 個 `catch(ThreadAbortException)` 已移除、`Interrupt()`+recreate 保留；net8 build 無 SYSLIB0006；**驗證期主動注入「worker 卡住」故障逼出此路徑**（正常輸入/影子模式不一定跑到）無 PNSE。
**Rollback**：此 commit revert 即回含 Abort() 版（net8 本就崩，回退僅在分支內 net462 半有意義）。語意保 supervisor 不變，風險低。

---

### P1-5：`Assembly.GetExecutingAssembly().CodeBase` ×3 → `AppContext.BaseDirectory` + `Path.Combine`｜High

dependsOn：P1-2｜**targetFiles**：`DbAccess.cs:394`、`NotificationService.cs:225`、`WriteToLog.cs:112`

**為什麼**：三處以 `new Uri(Path.GetDirectoryName(...CodeBase)+"\\mail_temp.txt").LocalPath` 推導 app 目錄。net8：SYSLIB0012 obsolete；single-file publish 會擲。

**正解**（非 SYSLIB0012 字面 workaround `Assembly.Location`——那 single-file 下回空字串會讓路徑解析錯誤）：
```csharp
string log_path = Path.Combine(AppContext.BaseDirectory, "mail_temp.txt");
```
IL3000 官方指引「只需容器目錄時用 `AppContext.BaseDirectory`」。每 site 一行，丟掉 obsolete API + 脆弱 `Uri.LocalPath` round-trip（順帶修含 `#`/空格路徑被 mangle）+ single-file 安全。`AppContext.BaseDirectory` 有尾端分隔符，用 `Path.Combine` 勿字串拼 `\\`。3 site 同 idiom 一致改。

> `WriteToLog.cs:34/:160`、`TsmcIeda.cs` 的 CodeBase 是**註解掉的 inert 行**，無編譯警告、非必要，若順手碰可清。

**驗證**：net8 build 無 SYSLIB0012；3 site 改後 `mail_temp.txt` 解析到 exe 同目錄（與舊 CodeBase 行為一致）；可加小 capture 確認 net462 與 net8 回相同相對結構（行為保持，既有測試不應變紅）。
**Rollback**：3 site 各一行替換，可獨立 revert。低風險、行為保持。

---

### P1-6：啟動處註冊 `CodePagesEncodingProvider`（big5/950）+ 加 `System.Text.Encoding.CodePages` 8.0.x｜Blocker

dependsOn：P1-2｜**targetFiles**：`Program.cs:27`（Main 第一行）、`DCT_data_import.csproj`

**為什麼**：net8 預設 provider 不含 big5(950)，**9 個 active** `GetEncoding("big5")` site 首次呼叫即擲：`Tester.cs:46`、`RecoveryRate.cs:60`、`UiStatus.cs:47`、`RawData.cs:54`、`MultiSpecRawData.cs:171`、`TsmcIeda.cs:86/240/297`、`FailPin.cs:47`。
> （`TsmcIeda.cs:187` 是註解掉的 inert 行，不算。critic 報「10 sites」是把該註解行也數進去；**實際 active = 9**。provider 註冊是全域的，site 數僅供參考。）

**步驟**：
- 加 `System.Text.Encoding.CodePages` 套件（pin 8.0.x 對齊其他 System.* 降版；雙 TFM 期 net462 內建 950 不需，用 Condition 只在 net8 加）。
- 在 `Program.Main` **第一行**（任何 `GetEncoding("big5")` 與 worker thread 啟動之前）加：
  ```csharp
  System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
  ```
- 註冊一次後 9 site 全部不改即可用。**timing**：須在第一次 `GetEncoding("big5")` 前；worker thread 在 Main 較後啟動、importer 在讀迴圈才解 big5，放 Main 第一行 race-free。
- **不要** per-call 改 UTF-8/別的編碼——CSV 真的是 big5 繁中，只缺 provider 註冊。

> **安全順手項**：Main 第一行就在 `Program.cs:32-34` 的 `Console.WriteLine("HOST/USER/PASSWORD: ...")` 旁邊。改此區時對齊 CLAUDE.md 約束 1「不擴大洩露、最好遮罩」——把 `PASSWORD`（:34）那行遮罩或移除（S3 已知債）。**不新增任何帳密列印。**

**驗證**：net8 下 **P0-4 `Big5DecodeTests` 由紅轉綠**（RegisterProvider 前紅、後綠）= 閘門通過；net8 跑真實 big5 CSV bytes round-trip 解出正確繁中；`dotnet list package` 確認 `System.Text.Encoding.CodePages=8.0.x`；9 個 GetEncoding site 無編輯。
**Rollback**：一行 RegisterProvider + 一條 PackageReference，可獨立 revert。回退後 net8 big5 路徑擲 `NotSupportedException`（故此步是 net8 hard gate，不可長期缺）。net462 半不受影響。

---

### P1-7：`IsWindowsPlatform()` 內部換 `OperatingSystem.IsWindows()`（簽章不變，消 CA1416）｜High

dependsOn：P1-2｜**targetFiles**：`ReadWriteINIfile.cs:265`、`ReadWriteINIfile.cs:10`

**步驟**：
- `ReadWriteINIfile.cs:10-13` 的 kernel32 `DllImport`（`WritePrivateProfileString`/`GetPrivateProfileString`）在 net8.0-windows **原樣可用、不改**（DllImport 在 .NET Core/5/8 全程支援；SYSLIB1054 建議 `LibraryImport` 僅 info 非強制，CLAUDE.md 不主動現代化→保留；若 build 因 `TreatWarningsAsErrors` 紅可 `<NoWarn>SYSLIB1054</NoWarn>`）。
- `:265-268` `IsWindowsPlatform()` 的 `Environment.OSVersion.Platform==PlatformID.Win32NT` 在 net8 仍可編可跑，但 net8.0-windows 啟用 **CA1416 平台相容性分析器**只認 `OperatingSystem.IsWindows()`/`RuntimeInformation.IsOSPlatform` 當有效守衛包住 kernel32 呼叫。改內部 `return OperatingSystem.IsWindows();`。
- **簽章保持** `public static bool` 不變（不動呼叫端，符合不主動現代化，屬一行內聚替換）。若 CA1416 仍報，最小修法即此換內部實作或在方法/類別掛 `[SupportedOSPlatform("windows")]`。
- **勿移除** `IsWindowsPlatform()`（既有守衛點，換內部即可）。`GetPrivateProfileString` 的 StringBuilder 緩衝、null `lpKeyName` 讀整段 section（:156）等 INI 語意 net8 與 net462 一致不需調。

**驗證**：net8.0-windows build 無 CA1416；`IsWindowsPlatform()` 簽章未變、呼叫端編譯通過；INI 讀寫 smoke（`WritePrivateProfileString` 後 `GetPrivateProfileString` 回相同值，含 null `lpKeyName` 讀整段）net8 行為與 net462 一致。
**Rollback**：單一方法內部一行替換，可獨立 revert。簽章不變故零呼叫端風險。

---

### P1-7b：新增 DRY-RUN / SHADOW 開關（影子驗證時只解析比對、不真 INSERT/搬檔/寄信）｜Blocker

dependsOn：P1-2（需 net8 已可 build；本開關在 net8 半驗證影子模式）。**必須在 P1-8 影子步驟（cutover step 5 / Rollback L3 §2）之前落地**——P1-8 的 dry-run 影子跑假設此開關存在（見「決策待確認 Q4」）。原 8 task 草案遺漏此步，dry-run inventory workflow 的 completeness critic 標為 Blocker（它 gate cutover 安全）。

**targetFiles**：`DCT_data_import/MySQL_api/DBmysql.cs:79`、`DCT_data_import/ReadAndImport/ImportData.cs:124`、`DCT_data_import/ReadAndImport/ImportData.cs:134`、`DCT_data_import/Common/EmailModels.cs:82`、`DCT_data_import/Common/NotificationService.cs:228`、`DCT_data_import/App.config`（appSettings 加一個 key）、新增 `DCT_data_import/Common/RuntimeMode.cs`、`DCT_data_import.Tests`（先寫 failing regression test）

**為什麼**：

P1-8 的 production cutover 採「平行運行影子模式」當 rollback L3 的資料污染防護——net8 半餵真實 FTP CSV、與 net462 production 同期逐筆比對 db_key 決策/匯入結果，**但比對期間絕不能污染 production 資料或騷擾收信人**。現況程式**沒有任何 dry-run 開關**：只要 net8 半一跑，就會真的 INSERT/UPDATE/DELETE MySQL、FTP 刪檔/搬檔到 `*_Error`、SMTP 寄信給 `dct_import_mail_list.ini` 的真實收信人。沒有此開關，影子模式無法落地，L3 退化為「僅靠 DB 快照/binlog 事後復原」（最貴、可能不可逆）。

關鍵設計：**gate 副作用的「執行」，不 gate 副作用的「決策」**。`DbAccess.cs:216/300` 設定 `import_status=2`/`mail=1` 並 `WriteToMailTemp` 是「決策點」（要不要寄信、要不要標失敗），**刻意保留**，讓影子跑記錄「本應寄什麼、本應寫什麼」供 diff/replay；真正被攔的是下游的「實體寫入 / 搬檔 / 寄信」。

架構已乾淨收斂到三類 chokepoint（dry-run inventory workflow 盤點 + critic 獨立 re-scan 無遺漏的 write/file/mail site），故**在 chokepoint gate，不逐站 gate**——逐站要改 60+ 處，漏一個分支就污染，純屬負債：

1. **DB（單一物理寫入點）**：所有 INSERT/UPDATE/DELETE 唯一經 `DBmysql.cs:193`（`connection.Execute`，`:210` Commit），且唯一經 `DBmysql.cs:79` 的 `else`（`mode.ToLower() != "select"`）分支進入。每個 ExecuteInsert（`FileProcess.cs:1351`）、UPDATE（`DbAccess.cs:233/314`）、DELETE（`FileProcess.cs:1387/1417/1445/1459/1473`）都傳 `mode` ∈ {insert,update,delete} funnel 到此。**在 `:79` 的 else 分支開頭 gate**：single point 涵蓋全部 DB mutation、SELECT（含 `LAST_INSERT_ID` 與所有讀查詢）完全不受影響，且**自動攔住破壞性多表 cascade DELETE**（naive「只跳過 INSERT」會漏掉 delete → 仍毀既有列）。critic 補充：所有呼叫點的 `mode` 都是字串字面量（無變數 mode），讀寫在每個呼叫點靜態可判定。

2. **FTP 檔案 mutation（兩個 primitive，皆在共用基底 `ImportData`）**：`ImportData.DeleteFile`（`:124`，FTP DeleteFile，匯入成功後**不可逆刪 FTP 來源 CSV**）與 `ImportData.RenameFile`（`:134`，FTP Rename 搬檔到 `*_Error`）。所有 importer 都呼叫這兩個基底方法（~60 呼叫點收斂成 2 個方法體）。**在 DeleteFile 與 RenameFile 方法體開頭各 gate 一次**（early-return 假 `StatusDescription`），兩處改涵蓋每個 importer。影子模式下若刪/搬不被攔，會把 net462 production 也要動的來源檔誤刪/誤搬進 `_Error`。

3. **寄信（單一物理寄信點）**：唯一 SMTP 傳輸在 `EmailModels.SendEmail`（`EmailModels.cs:82`，`mysmtp.Send`）。**在此 `Send` 前 early-return**，同時涵蓋 active 路徑（`NotificationService.cs:191`）與 legacy 死碼路徑（`Program.cs:327`，若日後重啟註解區塊也安全）——比 gate 三個 `NotificationService.Send*` 入口更乾淨。**另須一併 gate `NotificationService.CleanupMailTempFiles`（`:228`，`File.Delete mail_temp.txt`）**，使影子跑「既不寄信、也不清空佇列」，佇列原樣保留供 diff/replay。收信人來自 `dct_import_mail_list.ini` 的**真實信箱**，一封 stray mail 就騷擾真人。

**Gate 落點（實際只改 6 處，涵蓋 100% production 副作用）**：

| Gate | 檔:行 | DryRun ON 行為 | 涵蓋 |
|---|---|---|---|
| DB 寫入 | `DBmysql.cs:79`（else 分支） | early-return 假 response，不進 `ExecuteNonQueryCommand`(:193)/`Commit`(:210) | 下表 33 個 INSERT/UPDATE/DELETE 站 |
| FTP 刪檔 | `ImportData.cs:124`（DeleteFile） | 方法首 early-return 假 StatusDescription | 所有 importer 刪來源 CSV |
| FTP 搬檔 | `ImportData.cs:134`（RenameFile） | 同上 | 所有 importer 搬 `*_Error` |
| 寄信 | `EmailModels.cs:82`（SendEmail） | `Send` 前 early-return true | NotificationService(:191) + legacy Program(:327) 兩路徑 |
| 佇列清理 | `NotificationService.cs:228`（CleanupMailTempFiles） | early-return，不刪 mail_temp.txt | 影子佇列保留供 diff |
| 開關來源 | 新增 `Common/RuntimeMode.cs` + `App.config` 一個 key | — | — |

**被涵蓋的 mutation 站點（盤點證據，critic 獨立 re-scan 確認無遺漏；41 站全收斂進上述 6 gate）**：

- **DB INSERT（全經 `DBmysql.cs:79`）**：chokepoint `FileProcess.cs:1351`（ExecuteInsert）；RecoveryRate `:131/157`、RawData `:211/320/341/426/453`、MultiSpec `:509/623/644/729/756`、Tester `:827/894/942/992`、UIStatus `:1060`、FailPin `:1109/1160/1220/1241/1289/1318`；TsmcIeda `:350/407`
- **DB UPDATE**：`DbAccess.cs:233`（db_key）、`:314`（db_key_ui_status）
- **DB DELETE（破壞性 cascade）**：`FileProcess.cs:1387`（raw 3 表）、`:1417`（tester 4 表）、`:1445/1459/1473`（failpin）
- **物理寫入/funnel**：`DBmysql.cs:193`（Execute）、`:210`（Commit）、`DatabaseService.cs:47`（mode!=select funnel）
- **FTP**：`ImportData.cs:124`（DeleteFile）、`:134`（RenameFile）
- **寄信/佇列**：`EmailModels.cs:82`（SendEmail）、`NotificationService.cs:228`（CleanupMailTempFiles）

**不 gate（刻意保留，影子跑期間照常）**：決策點 `DbAccess.cs:216/300`（保留記錄供 diff/replay）；所有 `WriteToLog` 診斷寫入（`data_import_logs`/`check_logs`/`C:\temp` 目錄建立，`WriteToLog.cs:33/56/61/69/158/184/190/197`）——append-only 診斷紀錄，影子跑時**正是想要的證據**，benign 無需 gate；`DBmysql.cs:200` 的 `SELECT LAST_INSERT_ID()` 是寫交易內的讀、INSERT 被 no-op 時根本不執行，不另 gate；`ReadWriteINIfile.cs:70` 的 `WritePrivateProfileString` 是死碼（無 production 呼叫者），latent 不 gate。

**步驟**：

- **(0) 先寫 failing regression test**（CLAUDE.md「修 bug/改行為先寫 failing test」，R5 已示範此模式）：在 `DCT_data_import.Tests` 寫測試釘住「DryRun=true 時，寫入/搬檔/寄信 chokepoint 被短路、回傳 no-op 結果；DryRun=false（預設）時行為與今日一致」。seam：以可注入的 `RuntimeMode.IsDryRun` 取代直接讀 ConfigurationManager（見步驟 1），讓測試不依賴實際 MySQL/FTP/SMTP——可對 `DBmysql.Excute_mysql_cmd`（傳 `mode="insert"`）斷言 DryRun 下不進 `ExecuteNonQueryCommand`（回傳 `affectedRows=0` 之類的假 response 且 `Error` 為空），對 `EmailModels.SendEmail` 斷言 DryRun 下回 true 但不呼叫 `mysmtp.Send`。先紅。
- **(1) 設定讀取（沿用既有 ConfigurationManager.AppSettings pattern）**：依「決策待確認 Q1 預設＝保留 App.config」，**不改 appsettings.json**。在 `App.config` 的 `<appSettings>` 新增**一個** key：
  ```xml
  <add key="DryRun" value="false" />
  ```
  讀取沿用 `Program.cs:19-26` 的 `ConfigurationManager.AppSettings[...]` 風格，集中成一個 lazy static（避免散落各處重複 parse），新增 `DCT_data_import/Common/RuntimeMode.cs`：
  ```csharp
  internal static class RuntimeMode
  {
      // DryRun（影子驗證）：true 時只解析比對，不真 INSERT/UPDATE/DELETE、不搬/刪 FTP 檔、不寄信。預設 false。
      // 對齊 P1-8 cutover 影子模式 / Rollback L3 資料污染防護。
      private static readonly bool _isDryRun =
          string.Equals(ConfigurationManager.AppSettings["DryRun"], "true", StringComparison.OrdinalIgnoreCase);
      public static bool IsDryRun => _isDryRun;
  }
  ```
  > **key 缺失語意**：`AppSettings["DryRun"]` 缺 key 回 `null` → `string.Equals(null,"true")` = false → **預設 OFF**（fail-safe：未設定就照正常 production 行為跑，不會意外進影子）。**不要**用會在缺 key 時拋例外的 parse（如 `bool.Parse`），與 `Program.cs:19-26` type-init 早於 Main 的脆弱性（P1-3 已標 `TypeInitializationException` 地雷）一致——本 key 的讀取放在自己的 static class、用容錯比較，不掛進 `Program` 的 type-init 鏈。
- **(2) DB chokepoint gate（`DBmysql.cs:79` else 分支）**：在進入 `ExecuteNonQueryCommand` 前加守衛。沿用既有 4 空格縮排 / Allman 大括號 / `Execute_query_response`(`response`) 回傳慣例：
  ```csharp
  else
  {
      if (RuntimeMode.IsDryRun)
      {
          // DryRun: 跳過所有非 select（insert/update/delete）寫入，回傳 no-op 成功
          response.Error = "";
          // 影子模式不寫入；上層以 response 判定流程，回假成功避免誤判失敗→誤寄信
      }
      else
      {
          ExecuteNonQueryCommand(connection, filteredCmdString, parameters, response);
      }
  }
  ```
  > **回傳值契約**：上層（DbAccess/FileProcess）以 `response`（含 `Error`/`InsertId`/`AffectedRows` 等欄位）判流程。DryRun 回「無 Error 的假成功」避免讓上層誤判成寫入失敗 → 反而觸發 `import_status=2`/`mail=1` → 污染影子佇列。**確認 `Execute_query_response` 各欄位在 no-op 下的合理預設值**（`InsertId` 子表 FK 用——但子表 INSERT 同樣被本 gate 攔，不會真用到假 InsertId）。實際欄位以該型別定義為準，動工時讀 `DbObject`/`Execute_query_response` 定義填正確欄位，**不臆造欄位名**。
- **(3) FTP 檔案 chokepoint gate（`ImportData.cs:124` DeleteFile、`:134` RenameFile）**：各在方法體開頭加：
  ```csharp
  if (RuntimeMode.IsDryRun)
  {
      return "DryRun: FTP DeleteFile skipped";   // RenameFile 對應字串
  }
  ```
  回傳型別是 `string`（既有回 `response.StatusDescription`），回一個非空字串即可（呼叫端僅記 log / 判空）。**沿用既有 `#region Comman tool` 區塊風格與註解語言（繁中為主）。**
- **(4) 寄信 chokepoint gate（`EmailModels.cs:82` SendEmail）**：在 `mysmtp.Send(mailObj)` 前 early-return：
  ```csharp
  if (RuntimeMode.IsDryRun)
  {
      SendResult = "DryRun: email send skipped";
      return true;   // 回 true：上層 SendMailModelInternal 據此走「寄成功」分支，不重試、不報錯
  }
  ```
  > 回 `true` 的理由：active 呼叫者 `NotificationService.SendMailModelInternal`（`:191`）以 bool 判成功/失敗。回 true 讓影子跑不把「跳過寄信」誤判成寄信失敗。
- **(5) mail_temp 清理 gate（`NotificationService.cs:228` CleanupMailTempFiles）**：方法開頭加 `if (RuntimeMode.IsDryRun) return;`。與寄信一起 gate：影子跑既不寄信、也不清空 `mail_temp.txt`，佇列保留供 diff/replay。
- **(6) 不動的部分**：**不 gate** 決策點 `DbAccess.cs:216/300`（保留記錄）、**不 gate** 任何 `WriteToLog` 診斷寫入、**不改** 既有 SQL 字串串接風格（CLAUDE.md「不主動現代化」、約束 4：沿用既有 concat，本 task 不順手參數化）、**不刪** legacy `Program.SendMailModel` 死碼（CLAUDE.md：死碼提及不刪，等 user 確認；它已被 `EmailModels.SendEmail` 的 gate 一併涵蓋）。
- **(7) 安全（CLAUDE.md Hard Rule / 約束 1）**：**絕不**在任何新增 log / DryRun 訊息印出 DB/FTP 帳密或連線字串值（`Program.cs:32-34` 既有 HOST/USER/PASSWORD 列印屬 S3 已知債，本 task 不擴大，最好順手遮罩 PASSWORD）。DryRun 守衛訊息只記「skipped」語意，不夾帶 SQL 全文（可能含外部值）或憑證。

**驗證**：
- 步驟 0 的 regression test **先紅後綠**：DryRun=true 下三類 chokepoint 確認被短路（DB 不進 ExecuteNonQueryCommand、FTP DeleteFile/RenameFile 回假字串、SendEmail 回 true 不呼叫 mysmtp.Send、CleanupMailTempFiles 不刪檔）；DryRun=false（預設）行為與今日一致（既有 P0 安全網 / golden-master 不變紅）。
- `dotnet list package` / build：net8 半 `dotnet build` 綠，無新增外部依賴（只用既有 `System.Configuration.ConfigurationManager`）。
- **缺 key fail-safe**：移除 App.config 的 `DryRun` key（或設非 "true" 值）→ `RuntimeMode.IsDryRun=false` → 正常 production 路徑（不可意外進影子）。
- **影子煙霧驗證（net8）**：DryRun=true 跑一輪真實 FTP CSV → 確認 MySQL **零寫入**（INSERT/UPDATE/DELETE 計數 0）、FTP 來源檔**未被刪/搬**、SMTP **零寄出**（收信人零騷擾）、但 `data_import_logs`/`check_logs` 診斷 log **照常產出**（影子證據）、`mail_temp.txt` 佇列保留可供 diff。
- grep 確認 gate 落在 6 個 chokepoint（`DBmysql.cs:79`、`ImportData.cs:124/134`、`EmailModels.cs:82`、`NotificationService.cs:228`），且決策點 `DbAccess.cs:216/300` 與 `WriteToLog` 診斷寫入**未被改動**。

**Rollback**：
- 此 task 為**純新增守衛 + 一個 appSettings key**，可單一 commit `git revert`，回退後 `DryRun` 預設不存在 → fail-safe OFF → 完全等同今日行為，零功能損失。
- 部署層 fail-safe：production 上線（cutover step 6）**必確認 `DryRun=false`**（或 key 不存在）才指向 net8 產物；影子跑（step 5）才設 `DryRun=true` 且跑在非 production schema / 獨立工作目錄。回切（L2）時無論 net462/net8，production 一律 false，不影響回切。
- 此開關本身即 Rollback L3 的**資料污染防護**：有它，影子驗證期的資料污染擋在驗證期外；無它，L3 退化為僅靠 DB 快照/binlog 事後復原。

> **影子佇列 replay 注意（openConcerns）**：決策點不 gate 代表 DryRun 下 `mail=1` flag 與 `mail_temp.txt` 仍真實寫入本機檔案。若影子用同一 `mail_temp.txt` 路徑，replay 語意須在 P1-8 釐清——**建議影子指向獨立工作目錄**，或明確接受佇列為唯讀證據。另：R5 `ComputeImportResult` bitmask 脆弱契約在 DryRun 下仍會計算並可能誤判 `import_status=2`，使影子佇列出現假失敗列；影子比對時須與 net462 同口徑判讀，否則 false email 會被誤當回歸（此非本 task 引入，屬既有 CONCERNS R5）。

---

### P1-8：golden-master 兩 runtime 逐值 diff + 收尾砍雙 TFM + 更新 docs/CLAUDE.md｜High

dependsOn：P1-3, P1-4, P1-5, P1-6, P1-7, **P1-7b**（影子步驟 step 5 硬依賴 dry-run 開關）

**步驟**：
1. **golden-master**：5 個 `CaptureBaseline` 檔（P0-1/P0-2/P0-3 新增 + 既有 SpecialFloatParse/DoubleToStringFormat/DateTimeParserCulture）在 net8 的輸出對 net462 CI capture log **逐值 diff**，差異 = 回歸訊號逐條判定（A big5 已 P1-6 修綠；C double→string 位數漂移／D DateTime/MySql.Data／B 特殊浮點若漂移須評估對 SQL 寫入與 dup-detect/寄信決策影響）；`DateTimeParser` 的 `Now` fallback 非決定性需排除/正規化。全部判定可接受才往下。
2. **Big5DecodeTests** 硬斷言在 net8 綠 = 閘門通過。
3. **穩定觀察期後收尾**：移除雙 TFM 的 net462 半回 `<TargetFramework>net8.0-windows</TargetFramework>`（主 + 測試專案）、移除 net462-only Condition 套件參考與 `Microsoft.NETFramework.ReferenceAssemblies`、清 App.config 剩餘 net462-only binding redirect/startup 段、同步 `docs/codebase` 七檔（STACK/BUILD 指令 nuget→dotnet、TFM、Thread.Abort 改動）與 CLAUDE.md。**doc rot 視同 bug。**

**驗證**：CI 兩 runtime capture step 各產 log、fail-on-diff（排除 Now fallback 列）無未判定差異；net8 Big5DecodeTests 綠；砍雙 TFM 後 single net8.0-windows build+test 全綠；docs/codebase 與 CLAUDE.md 的 TFM/build/Thread 段已更新且與程式碼一致。

---

## Rollback 策略（migration 強制；分層 L0→L3，由便宜到昂貴）

> 對齊全域 CLAUDE.md「auth/payment/migration/crypto 高風險變更 MUST 附 rollback」。**這是長駐 ETL，真正的 rollback 對象是「正在跑、正在寫 MySQL 的那個 process」**，不只是程式碼。

### L0 — 版控隔離（最便宜）
全部 net8 工作在 `feat/net8-migration` 分支，**master 始終保留可 build/可部署的 net462 真相**。code-level rollback = 不 merge / `git checkout master`。已部署 production 仍跑舊 master 產物，完全不受 feature branch 影響。**硬前置 gate**：master 上要先有綠的 Phase 0 安全網 commit + net462 基準，再切 TargetFramework。

### L1 — 雙 TFM 並存（分支內隨時回 net462 build）
轉 SDK-style 後 `net462;net8.0-windows` 並存，net462 路徑全程綠當分支內 fallback。雙 TFM 是**腳手架非終態**，cutover 穩定後在 P1-8 收尾砍 net462 回 single net8.0-windows（避免長期雙框架設定債）。

### L2 — 產物層 rollback（production 回切，最關鍵）
- (a) 兩版產物**獨立資料夾**（`app_net462\` / `app_net8\`）+ **同一份 App.config**（帳密不變），回切 = 停 net8 process、改回啟動指向 net462 資料夾、重啟。
- (b) net8 **自包含發佈**（`dotnet publish -r win-x64 --self-contained`）使回切不依賴目標機是否裝 .NET 8 runtime，且與 net462 並存無衝突。
- (c) 回切點**預先寫成 runbook + 演練一次**，不要等出事才想流程。

### L3 — 資料層 rollback（最貴、可能不可逆，須事前防護）
net8 靜默差異（C double→string 位數漂移、D DateTime/MySql.Data 換代、B 特殊浮點）會把**已寫進 MySQL 的資料改掉**——process 回切救不回。對策：
1. cutover 期 net8 寫入的 db_key/匯入結果先做 **DB 快照 / binlog 保留窗**，確認正確才過保留期。
2. 平行運行期 net8 用 **dry-run**（只解析比對、不真 INSERT/搬檔/寄信），把資料污染擋在驗證期外。**此 dry-run 開關由任務 P1-7b 落地（6 chokepoint gate），為本層防護的硬前置。**

### Cutover 步驟（runbook，預演過）
| # | 動作 |
|---|---|
| 0 | 前置 gate（master）：Phase 0 安全網綠 + net462 基準已 capture，否則不得進遷移 |
| 1 | 開 `feat/net8-migration`；先只 P1-1（SDK-style，net462 single-TFM）驗綠，commit |
| 2 | P1-2 加 net8 雙 TFM + 條件式套件分流；net462 半全程綠（L1） |
| 3 | 修 net8 硬崩潰/PNSE 阻擋點：P1-6 CodePages 註冊（Big5DecodeTests 紅→綠）、P1-4 Thread.Abort、P1-3 App.config type-init、P1-5 CodeBase、P1-7 CA1416 |
| 4 | net8 跑完整 golden-master 比對（P1-8 step 1），差異逐條判定可接受 |
| 5 | 平行運行影子模式（dry-run，**開關由 P1-7b 先落地**，設 `DryRun=true`）：非 production 機/獨立 schema、影子佇列指獨立工作目錄，餵真實 FTP CSV，與 net462 production 同期逐筆比對 db_key 決策/匯入結果/時間字串，跑滿 ≥1 完整營運週期（各 importer × 各 check_status bit × 輪詢） |
| 6 | 切 production：停 net462 process → DB 快照/確認 binlog 保留窗 → 啟動 net8 自包含產物（指 `app_net8\`、同一 App.config）→ 觀察首幾輪寫入正確（正確才過 binlog 保留期） |
| 7 | 回切點（L2/L3）：net8 異常 → 停 net8、改回指 `app_net462\`、重啟 net462 → 若已污染資料，從步驟 6 快照/binlog 復原該段。**runbook 只記資料夾路徑與 process 名，不記帳密** |
| 8 | 穩定觀察期（≥1 完整營運週期）後收尾：master squash-merge → 砍雙 TFM 回 single net8.0-windows → 清 App.config 冗餘 → 同步 docs/CLAUDE.md |

### 主要風險與緩解
| 風險 | 緩解 |
|---|---|
| `Thread.Abort` 在 net8 擲 PNSE，**只在 worker hang 故障路徑觸發**，影子模式正常輸入可能漏過 | P1-4 移除呼叫 + 保 supervisor 語意；**驗證期主動注入 worker-hang 故障演練**逼出此路徑 |
| root cause C/D/B 靜默差異污染已寫 MySQL 資料（L3 不可逆），時間字串流入 dup-detect/寄信 → 誤判 | dry-run 影子擋污染於驗證期外；golden-master 逐值 diff；DB 快照+binlog 保留窗；MySql.Data pin 9.4.0 |
| csproj 轉 SDK-style 是高扇入結構變更：glob 多納/漏納、binding redirect 漏依賴 | 隔離變數（P1-1 先只轉 SDK 維持 net462 驗綠再加 net8）；轉換前後比對 bin 輸出/編譯檔數 |
| 雙 TFM 條件式套件配錯（Unsafe/Tasks.Extensions net8 重複引用、ConfigurationManager 未降版、TFM 誤設純 net8.0） | 套件三類分流（共用/net462-only Condition/版本對齊）；TFM 鎖 net8.0-windows；CI 兩框架 build 當 gate |
| 影子模式未涵蓋全 importer × check_status bit × 輪詢分支，稀有路徑在 net8 已變未觀察到 | 跑滿 ≥1 完整營運週期；ImportDecision 已抽純函式+測試覆蓋雙維派工；cutover 後保留觀察期+binlog |
| runbook/cutover log 不慎印 App.config 明文帳密（S1/S3），擴大洩露（違反 Hard Rule） | runbook 只記資料夾/process 名不記值；改 Program.cs 啟動區順手遮罩 `PASSWORD`（:34）；新設定 key name 進範例、值走外部 |

---

## 文件相依
- 上游策略：[docs/codebase/NET8_UPGRADE_TEST_STRATEGY.md](../codebase/NET8_UPGRADE_TEST_STRATEGY.md)
- 前置階段：[phase-0-safety-net.md](phase-0-safety-net.md)
- 工程權威來源：`docs/codebase/*.md`（七檔，每條附 file:line evidence）

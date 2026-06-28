# .NET 8 升級前測試策略（characterization / golden-master）

> ⚠️ **歷史文件**：本檔記錄 net462→net8 遷移前的測試策略與當時決策。A4 後 live stack 已是 single `net8.0-windows`，目前 build/test 指令與測試狀態以 `docs/codebase/STACK.md`、`docs/codebase/TESTING.md`、`DCT_data_import.Tests/README.md` 為準。

> 結論先講：升級 **.NET Framework 4.6.2 → .NET 8** 前，先補一批 characterization 測試，**在 net462 上跑出基準值並釘住**，升級後在 net8 上跑同一批比對——**值有差異 = 回歸**。
>
> 本檔是兩份來源合併後的權威清單：
> - **廣度安全網計劃**（seam 機制 + 整條 ETL 行為覆蓋）提供「HOW」與可執行骨架；
> - **四大 root cause 遷移探針報告**（只打 net462→net8 會悄悄翻掉的點）提供「WHAT-must-not-be-missed」與深度。
>
> 兩者互補不互斥：廣度計劃單獨用會有「綠燈假象」（見 §4 capture-don't-assert）；深度報告單獨用沒有可執行 seam。本檔的優先級骨架（§7）是合併後的最終清單。所有宣稱已對實際程式碼核對（file:line evidence）。

---

## 1) 範圍與非範圍

> **升級目標鎖定 .NET 8（非最新版）**：公司軟體環境限制在 .NET 8，**不升 net9 / net10**。全域「新專案預設最新 LTS」規則不適用本專案——這是受限維運升級，目標版本由公司環境決定。
>
> **執行順序（硬性）**：先把本檔規劃的測試安全網（characterization + golden-master 基準）建立完成、在 net462 跑出基準值，**再**進行 net8 migration；不可在安全網就緒前先切 `TargetFramework`。

**範圍**：升級前在 net462 上建立可重跑的行為基準（測試 + golden-master 快照），升級後比對。

**非範圍（本階段不做）**：
- 不執行 net8 migration 本身（`TargetFramework` 切換、`packages.config`→`PackageReference`、`Thread.Abort` / INI P/Invoke / `C:\temp` log path 等 runtime 相容性問題屬遷移階段）。
- 不引入 Moq / FluentAssertions / Testcontainers / DB container（避免為測試先改架構）。
- 不主動現代化（不擅自把同步模型改真 async、不重排 namespace、不改既有 SQL 串接風格）——對齊專案 [CLAUDE.md](../../CLAUDE.md) 「.NET 8 維運」守則。
- fixtures 不含真實憑證 / 內部主機 / 正式 lot 資料；DB/FTP/SMTP 副作用一律不觸發。

---

## 2) 兩種測試哲學的定位（為何合併）

| | 廣度安全網 | 深度差異探針 |
|---|---|---|
| 問題 | 「現況整條 ETL 行為是什麼？」 | 「net462→net8 在哪裡會悄悄翻掉？」 |
| 覆蓋 | 每個 parser / SPC / 派工決策 | 只在四大 root cause 落點 |
| seam | `InternalsVisibleTo` + internal 化 3 個 private parser + 抽 `ImportDecision` | golden-master + capture 基準 |
| 強項 | 接得住**沒預測到**的回歸 | 抓得到**框架靜默差異** |
| 弱點 | 對遷移敏感值會**綠燈假象**（§4） | 無具體執行機制 |

升級驗證的真正價值在「深度探針」；「廣度網」是低成本的兜底，但**要標籤分流**，別讓它排擠遷移探針。

---

## 3) 四大 root cause（net462→net8 靜默差異落點）

| 代號 | 差異 | 性質 | 落點（file:line） |
|---|---|---|---|
| **A** | **big5 (codepage 950) 未註冊**：net8 預設無 codepage 950，`Encoding.GetEncoding("big5")` 直接 `ArgumentException` | 硬崩潰（非靜默），但**唯一的升級閘門** | 啟動需 `System.Text.Encoding.CodePages` + `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`；全 importer 解碼路徑 |
| **B** | **`double.TryParse` 解析翻轉**：`-1.#IND`/`1.#QNAN`/`1.#INF` 等 Windows legacy 字面量在 net462 **實測解析失敗→`false`/out=0**（見 §9，非原宣稱的 NaN/Infinity）；標準 `NaN`/`Infinity`/超界 `1E400` 行為見 §9；net8 對應行為待比對 | 靜默（解析失敗的 out=0 是否被當有效值；net8 行為待比對） | [CalculateSPC.cs:48/52/65/118](../../DCT_data_import/Common/CalculateSPC.cs:65)、[FileProcess.cs:1527](../../DCT_data_import/FileAccess/FileProcess.cs:1527)、[FailPin.cs](../../DCT_data_import/ReadAndImport/FailPin.cs) |
| **C** | **`double`→`string` 最短可往返格式**：.NET Core 3.0+ 改 IEEE754 最短往返輸出，**寫進 SQL 的數值字面量會變**（位數漂移） | 靜默（DB 內容變） | validated double 存 row 未經 round（[FileProcess.cs:265/567](../../DCT_data_import/FileAccess/FileProcess.cs:265)）→ 隱式 `ToString()` 拼進 INSERT（[FileProcess.cs:314/617](../../DCT_data_import/FileAccess/FileProcess.cs:314)） |
| **D** | **`DateTime` 漂移 + MySql.Data 驅動換代**：(d1) 輸出 `ToString` 無 `IFormatProvider`；(d2) **輸入** `DateTime.TryParse` 無 culture；(d3) PackageReference 遷移可能換驅動版本 → `Convert Zero Datetime` / 微秒 / `DateTimeKind` 對映變 | 靜默（時間字串用於 dup-detect / 寄信決策） | 輸出 [DBmysql.cs:147](../../DCT_data_import/MySQL_api/DBmysql.cs:147)、[FileProcess.cs:806](../../DCT_data_import/FileAccess/FileProcess.cs:806)；輸入見 §3.1；驅動見 §6 |

### 3.1 新發現：`CustomizeDateTimeParser`（輸入端 D，兩份原本都漏）

[FileProcess.cs:52-58](../../DCT_data_import/FileAccess/FileProcess.cs:58) 的 `CustomizeDateTimeParser` 用**無 culture 的 `DateTime.TryParse`**，餵 lot 起訖時間（[lines 198/496](../../DCT_data_import/FileAccess/FileProcess.cs:198)）。net8 ICU 下對歧義日期字串的 parse 會偏移 → lot 時間錯 → 連帶 dup-detect / 寄信決策錯。**必須進測試**。

> 對照：同檔 `ValidateDateTime`（[FileProcess.cs:71](../../DCT_data_import/FileAccess/FileProcess.cs:71)）有用 `CultureInfo.InvariantCulture`，是 culture-safe 的——**別誤測那個**。

---

## 4) capture-don't-assert 紀律（最重要的合併原則）

廣度計劃對遷移敏感輸入用**硬編斷言**（例：「`-1.#IND`/`1.#QNAN`/blank → 0」）。但 root cause B 已驗證：**這無法從程式碼判定，是 runtime 框架差異**。net462 實測（§9）這些 token 是 `TryParse=false`/out=0——硬編 `Assert.Equal(0, …)` 雖在 net462 綠燈，卻綠在「解析失敗的 default」而非「成功得 0」，且 net8 行為未經驗證；一旦 net8 改變（throw 或產不同值）即**綠燈但錯**，零防護。

**規則**：
- **遷移敏感子集**（特殊浮點、culture 敏感日期、`double→string`）→ **不硬編期望值**；先在 **net462 跑出實際值再貼**，即使結果出乎意料。golden-master 是「記錄」不是「假設」。
- **穩定子集**（parser 形狀、`Compare*` 邏輯、整數運算）→ 才可用斷言式。

---

## 5) seam 機制（哪些要動程式、哪些不用）

| 目標 | 現況可見性 | seam | 風險 |
|---|---|---|---|
| Tester / FailPin / UiStatus / TsmcIeda 的 `FileRead*` | **public** | 直接測，**不需動程式** | 無 |
| RecoveryRate / RawData / MultiSpec 的 `FileRead*` | **private** | `InternalsVisibleTo("DCT_data_import.Tests")` 或 internal 化（僅這 3 個） | 低 |
| `ValidateAndConvertStatisticValue` | private + 寫 `writeToLog` 實例變數 | `InternalsVisibleTo`；測試需處理 log 副作用（stub 或抽純函式） | 低-中 |
| `SeperatePassValue` / `AverageOfSumSquare` | private / public | 經 public `AverageOfSumSquare` 測即可涵蓋 | 低（順帶記 [CalculateSPC.cs:124](../../DCT_data_import/Common/CalculateSPC.cs:124) list-aliasing dead code，**本階段只記不改**） |
| `ImportDecision`（從 `ImportTesterMode` 抽派工決策） | [Program.cs:354](../../DCT_data_import/Program.cs:354) | 抽純函式 `bool ShouldRun*(int checkStatus, int currentState)`；副作用全在 importer 內，**不需碰業務邏輯** | 低（~40-60 行純提升） |
| ~~`DoubleToSqlString`（root cause C 的探針 seam）~~ **評估後免動** | 賦值點 ~265 `Convert.ToString(double)`、讀取點 314/617 boxed double `item.ToString()` | **不抽 seam**：兩處在 `CurrentCulture` 下皆等價於 `double.ToString(CurrentCulture)`，直接對 BCL `double.ToString()` 做 golden-master 可證等價且同時覆蓋兩條路徑 → 見 [`DoubleToStringFormatTests.cs`](../../DCT_data_import.Tests/DoubleToStringFormatTests.cs)。免碰高扇入 `FileProcess` | 無（不改任何 production 程式） |

> **派工測試修正**：`ImportTesterMode` 派工同時取決於 `check_status` **與**各 importer 當前 int 狀態（recovery_rate / test_result / …），**不是 check_status 單一維度**。測試要帶第二維（bit→importer：bit0 FailPin / bit1 RawData / bit2 Tester / bit3 RecoveryRate；RawData `Result==0` 才 fallback MultiSpec，見 [Program.cs:427-431](../../DCT_data_import/Program.cs:427)）。

---

## 6) MySql.Data 驅動換代風險（root cause D-3）

- 現況 pin **9.4.0**（[packages.config:4](../../DCT_data_import/packages.config)、[.csproj:50](../../DCT_data_import/DCT_data_import.csproj)）。
- 連線字串含 `Convert Zero Datetime=true`（[DBmysql.cs:298](../../DCT_data_import/MySQL_api/DBmysql.cs:298)）——`0000-00-00` → `DateTime.MinValue`。
- PackageReference 遷移若不顯式 pin 版本，可能自動升/降版 → `Convert Zero Datetime` / 微秒精度 / `DateTimeKind` 對映改變。
- **對策**：net8 `.csproj` **顯式 pin 驅動版本** + 對 MySQL 5.6+ `DATETIME` 欄位做 round-trip golden master（zero-date、微秒、Kind）。

---

## 7) 合併後優先級骨架

> **兩條軸別混淆**：下方 P0/P1/P2 是「**衝擊度**」（會不會悄悄改資料 / 影響大小）；實作時另看「**執行層級**」（必寫 / 該寫 / 可延後）。重點：**可延後 ≠ 刪除**——P2 留在計劃內，實作時若選擇不寫，須在 commit / PR 附一行書面理由（例「遷移不敏感、已驗證純 LINQ 跨框架一致」），而非從清單消失。這正是本檔對廣度清單「維持完整 + 標籤分層」而不壓縮的理由（characterization 鐵律：事前不知道哪個行為會翻，砍要有依據，不是規劃時就先消失）。
>
> | 執行層級 | 內容 | 對映本節 |
> |---|---|---|
> | **Tier 1 必寫** | 遷移敏感 characterization（所有 ★，走 §4 capture）——升級驗證的全部理由 | P0 的 A/B/C/D ★ 項 + P1 的 D ★ |
> | **Tier 2 該寫** | 風險 dispatch / 決策 seam（非遷移敏感但錯了影響大） | ImportDecision（雙維派工）、R5 整數運算守門 |
> | **Tier 3 可延後** | 遷移不敏感廣度回歸網（斷言式，兩框架預期一致） | P2 全部 + 附錄 A.1/A.2 非 ★ 項 |

```
[base = 廣度計劃；★ = 嫁接的遷移探針]

P0（升級閘門 / 會悄悄改資料）
  A  ★ big5 解碼 smoke + 啟動註冊 CodePagesEncodingProvider（覆蓋全 importer 解碼路徑，非單一 GetEncoding）
  C  ★ double.ToString() golden-master（直接特性化 BCL，等價於 FileProcess 賦值點/讀取點，免抽 seam，net462 基準先跑，capture）
  D  ★ CustomizeDateTimeParser 在固定 + 非invariant culture 下 capture（逼出漂移）
  B  ★ double.TryParse 特殊浮點 token characterization（已 capture:net462 對 `1.#xxx`/`1E400` 回 false/out=0,見 §9）+ AverageOfSumSquare 下游（**已 capture** 於 `SpecialFloatParseTests.cs:62`,以 `-1.#IND` 餵入,釘住「TryParse=false/out=0 → 不產 NaN」的下游後果;原「net462 NaN→OverflowException→空 list」推測前提已於 §9 證偽。fail_n≠0 的 SeperatePassValue spec 汙染鏈,留待 Tier 3 廣度擴張）+ B+C 合成點（**已 capture** 於 `ValidateAndConvertStatisticValueTests.cs`,鏡像 `ValidateAndConvertStatisticValue` 兩條 live 分支 + 生產同款 2-arg overload）
  -    ImportDecision：check_status × importer 狀態「雙維」dispatch

P1（遷移敏感但影響較小 / 需驅動）
  D  ★ DateTime 輸出（DBmysql:147 / FileProcess:806）+ MySql.Data 驅動 round-trip golden master + 版本 pin
  -    SeperatePassValue spec 篩選 characterization
  -    既有 R5 紅/綠維持原狀（整數運算未變的守門，見 CONCERNS R5）

P2（廣度回歸網，遷移不敏感，可保留但別擴張 → 逐條清單見附錄 A）
  -    Compare*() 寬鬆欄位驗證、parser 形狀、TsmcIeda 固定寬度、FailPin 新舊格式
```

> **B→SPC 汙染鏈（觸發前提已證偽，下游仍須測）**：原假設「若 net462 把 `-1.#IND`→NaN，NaN 在 `SeperatePassValue` 的 `> spec_max || < spec_min` 比較**永遠 false（不被篩掉）**→ 流進 sum-of-square / stdev（[CalculateSPC.cs:83-92](../../DCT_data_import/Common/CalculateSPC.cs:83)）→ 整組 SPC 輸出變 NaN」。net462 實測（§9）`1.#xxx` 是 `TryParse=false`/out=0、**不產 NaN**，故此 NaN 汙染鏈不觸發。改關注點：呼叫端若忽略 `TryParse` 回傳值、直接用 out=0，則「`1.#xxx`→0」流進計算；net8 對這些 token 若 throw 或產不同值，下游即不同。故 P0 的 `AverageOfSumSquare` 測試**仍須含一列特殊浮點輸入**（capture，勿硬編）。

---

## 8) golden-master 建置順序

1. **先在主專案啟動處註冊 `CodePagesEncodingProvider`**（big5 閘門，net8 不註冊會在 P0-A 先紅）。
2. 依 §5 開最小 seam（`InternalsVisibleTo` + 抽 `ImportDecision`），**不碰業務邏輯**。`DoubleToSqlString` 經評估**免抽**——直接特性化 BCL `double.ToString()` 即等價覆蓋，不動 `FileProcess`（見 §5 修訂）。
3. **先在 net462 跑全套 → 產出 golden-master 基準值**（capture，§4）。期望值由「實際輸出」決定，不由人猜。
4. 切 `net8.0-windows`（P/Invoke kernel32 + FtpWebRequest 需要）+ `PackageReference`，**顯式 pin MySql.Data 版本**（§6）。
5. 在 net8 跑同一套 → 與 net462 基準比對。
6. 確認屬「by-design 框架差異」者標 `[Trait("Category","ByDesignRed")]`（同 R5 模式，CI 以 `Category!=ByDesignRed` 排除）。
7. 快照 transitive 套件版本（packages.config 原本攤平 pin，PackageReference 不再 pin 間接相依）。

> golden-master 快照可用純 committed 期望值常數（由 net462 實跑產生），**不需** Verify/snapshot 套件——對齊 §1「不加新依賴」。前提是常數來自實跑，非人為假設。

---

## 9) 現場確認結果（root cause B 已由 net462 實跑釐清；C 仍待 net8）

**root cause B —— 原方向已被 net462 CI 實跑推翻。** 原 agent 宣稱「net462 把 `-1.#IND`/`1.#QNAN`/`1.#INF` parse 成 NaN/Infinity」**不成立**。`SpecialFloatParseTests`（CaptureBaseline）在 net462（windows-latest）實測：

| token | `double.TryParse` | value |
|---|---|---|
| `-1.#IND` / `1.#QNAN` / `1.#INF` / `-1.#INF` / `1E400` | **False** | 0 |
| `NaN` / `Infinity` / `-Infinity`（標準 .NET 字面量） | **True** | NaN / ±∞ |

即 Windows legacy `1.#xxx` 字面量在 net462 就**解析失敗**（回 `false`、out 維持 0），只有標準字面量才解析成功。這正印證 §4 capture-don't-assert：若當初硬編 `Assert.Equal(0, value)`，net462 會**綠燈**——但那個 `0` 是「`TryParse` 失敗的 out default」而非「解析成功的 0」，語意不同；**net8 對應值仍待升級後比對**（預期亦 false→0，但須實測釘住，勿假設）。evidence：[CI run #28223861278](https://github.com/BriantsaiCoder/DCT_data_import_data_stream_codex/actions/runs/28223861278) 的「Capture net462 baseline」step。

**root cause C** 中「`Infinity`/`NaN` 在 `zh-Hant-TW` CurrentCulture（ICU）下會本地化成非 ASCII 字面量（`∞` / `非數值`）注入 INSERT」——屬 agent 宣稱，net462 階段不觸發 ICU 無法驗，**須 net8 + 對應 culture 實跑確認**。

以上不影響測試該不該寫（要寫），只影響期望值方向——故一律走 capture-don't-assert。

---

## 附錄 A) 廣度安全網逐條測試清單（來源：使用者提供之「測試安全網實作計劃」，忠實保留）

> 此清單為 §7 P2 的展開，對應廣度計劃的完整測試枚舉。標 ★ 者為遷移敏感（走 §4 capture-don't-assert）；其餘為 general regression net（可用斷言式，兩框架預期一致）。

**A.1 Pure behavior tests**
- `FileContentFormat.Compare*()`：合法欄位回 `true`、未知欄位回 `false`、無 row/table 回 `false`；**保留現況「不要求所有 expected columns 都存在」的寬鬆語意**（[FileContentFormat.cs](../../DCT_data_import/FileAccess/FileContentFormat.cs)）。
- `CalculateSPC.AverageOfSumSquare()`：(a) 無 fail；(b) 含 fail 且依 spec 篩掉 out-of-spec；(c) 無有效數字時回 0 組。**★ 另加一列特殊浮點輸入**（見 §7 B→SPC 汙染鏈）。
- `FileProcess` helper：
  - ★ 日期 parser（`CustomizeDateTimeParser`，固定 + 非invariant culture，§3.1）；
  - 空字串轉 `No Data`（`ConvertEmptyToDefaultString`）；
  - `AddColumnForDataset` round 9 digits（[FileProcess.cs:1489-1490](../../DCT_data_import/FileAccess/FileProcess.cs:1489)）；
  - ★ `-1.#IND` / `1.#QNAN` / 空白統計值的處理（`ValidateAndConvertStatisticValue`，**capture 實際結果，勿硬編 0**，§4）。

**A.2 Parser characterization tests**
- `Tester.FileReadTesterStatus`：四段資料能產生 device / status / sw / version / production rows。
- `FailPin.FileReadFailPinLog`：舊格式 `sn_num` 空字串、新格式 `SN Num` 正確填入。
- `UiStatus.FileReadUIStatus`：header + one row 可通過 `CompareUiStatus()`。
- `RecoveryRate.FileReadRecoveryRateData`：basic info + rate rows 正確合併到 `FinalRecoveryRateTable`。
- `RawData` / `MultiSpec` parser：用**非 TSMC** fixture，確認 `LotInfo` / `LotStatistic` / `LotResult` / `retest_loc` / `value` 欄位形狀不變。
- `TsmcIeda.FileReadIeda`：用固定寬度 builder 產生一筆 title + content，確認欄位切割穩定。

**A.3 Migration-sensitive smoke tests**
- ★ `Encoding.GetEncoding("big5")` 能解碼 committed big5 bytes；net8 若未註冊 `CodePagesEncodingProvider`，此測試應**先失敗再修正**（root cause A）。
- `ImportDecision`：測 `check_status` 0..15，RecoveryRate / RawData / Tester / FailPin 是否執行，以及 RawData `Result == 0` 才 fallback MultiSpec（**注意 §5 派工測試修正：尚有 importer 當前狀態第二維**）。

> 與 §7 對照：A.1/A.2 多為 **P2 遷移不敏感**（驗證已確認純 LINQ/DataTable 跨框架一致），標 ★ 的少數項升 P0/P1；big5 與 ImportDecision 屬 P0。

---

## 附錄 B) baseline / Test Plan 指令（來源：同上計劃）

```powershell
# 先跑既有 baseline（與 CI 一致）
nuget restore DCT_data_import.sln
msbuild DCT_data_import.sln /p:Configuration=Release /m
dotnet test DCT_data_import.Tests\DCT_data_import.Tests.csproj --configuration Release --no-build --filter "Category!=ByDesignRed"

# 每個測試群完成後
dotnet test DCT_data_import.Tests\DCT_data_import.Tests.csproj --filter "Category!=ByDesignRed"

# 最終驗證同 CI：restore + Release build + filtered test 全綠
```

- R5 測試已於 2026-06-27 修復（`fix/r5-checkstatus`）、移除 `ByDesignRed` trait、重納綠燈門檻；`ByDesignRed` 機制現專供本節新增的 net8 by-design 框架差異測試沿用（標 trait → CI 以 `Category!=ByDesignRed` 排除）。
- 與 §8 的關係：附錄 B 是**單框架（net462）的紅綠跑法**；§8 是**跨框架 golden-master 比對順序**，net8 段在附錄 B 之上再加「切 net8 → 跑同套 → 比對 → 標 ByDesignRed」。

---

## 10) Evidence

- [CalculateSPC.cs](../../DCT_data_import/Common/CalculateSPC.cs)（:13 AverageOfSumSquare、:48/52/65/118 TryParse、:83-92 sum-of-square/stdev、:112-135 SeperatePassValue、:124 dead code）
- [FileProcess.cs](../../DCT_data_import/FileAccess/FileProcess.cs)（:52-58 CustomizeDateTimeParser、:71 ValidateDateTime、:265/567 row 存值、:314/617 隱式 ToString 拼 SQL、:806 datetime ToString、:1500 ConvertEmptyToDefaultString、:1489-1490 Math.Round(,9)、:1515-1540 ValidateAndConvertStatisticValue）
- [FileContentFormat.cs](../../DCT_data_import/FileAccess/FileContentFormat.cs)（6 個 Compare*，遷移不敏感）
- [Program.cs](../../DCT_data_import/Program.cs)（:354 ImportTesterMode、:405/423/453/471 bit dispatch、:427-431 MultiSpec fallback）
- [DBmysql.cs](../../DCT_data_import/MySQL_api/DBmysql.cs)（:147 DateTime ToString、:149-151 JToken.FromObject、:298 Convert Zero Datetime）
- parser 入口：[Tester.cs:139](../../DCT_data_import/ReadAndImport/Tester.cs)、[FailPin.cs:127](../../DCT_data_import/ReadAndImport/FailPin.cs)、[UiStatus.cs:105](../../DCT_data_import/ReadAndImport/UiStatus.cs)、[RecoveryRate.cs:163](../../DCT_data_import/ReadAndImport/RecoveryRate.cs)、[RawData.cs:154](../../DCT_data_import/ReadAndImport/RawData.cs)、[MultiSpecRawData.cs:265](../../DCT_data_import/ReadAndImport/MultiSpecRawData.cs)、[TsmcIeda.cs:121](../../DCT_data_import/ReadAndImport/TsmcIeda.cs)
- [packages.config](../../DCT_data_import/packages.config)（MySql.Data 9.4.0）
- 相關文件：[CONCERNS.md](CONCERNS.md)（R5 / S1-S4）、[TESTING.md](TESTING.md)（現有 R5 樁）、[STACK.md](STACK.md)、[DCT_data_import.Tests/README.md](../../DCT_data_import.Tests/README.md)

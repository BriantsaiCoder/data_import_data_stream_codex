# Phase 0：升級前安全網補完

> .NET Framework 4.6.2 → .NET 8 升級的**第一階段**。本檔是可遵循的實作計畫。
> 上游策略：[docs/codebase/NET8_UPGRADE_TEST_STRATEGY.md](../codebase/NET8_UPGRADE_TEST_STRATEGY.md)（root cause / capture-don't-assert 紀律 / seam 機制的權威來源）。
> 下游：完成本階段（net462 安全網全綠 + 基準值已 capture）才得進 [phase-1-migration.md](phase-1-migration.md)。**這是硬性順序，非建議**（策略檔 §1）。

## 為什麼有 Phase 0

net462→net8 的四類差異是**靜默**的：編譯過、不丟例外，只是數值/字串/時間悄悄變了，污染寫進 MySQL 後 process 回切救不回（見 Phase 1 的 L3 rollback）。對策唯一：**升級前先在 net462 把實跑值「拍照」存成基準，升級後同一批測試在 net8 再跑、逐值 diff = 回歸訊號。**

現有測試（`DCT_data_import.Tests/`）已覆蓋四個 root cause 的 **BCL 單腿**：

| 既有測試 | root cause | 覆蓋 |
|---|---|---|
| `Big5DecodeTests` | A | big5/cp950 解碼可用性（硬斷言，net8 閘門） |
| `SpecialFloatParseTests` | B | `double.TryParse` 對 `-1.#IND`/`1.#QNAN` 等舊 token（孤立 BCL 腿） |
| `DoubleToStringFormatTests` | C | `double.ToString()` G15 vs 最短往返（孤立 BCL 腿） |
| `DateTimeParserCultureTests` | D | `CustomizeDateTimeParser` 無 culture 解析漂移（孤立腿） |

**Phase 0 補的是「孤立 BCL 腿」與「真實 production 方法分支」之間的缺口**——B 和 C 在生產碼裡是**合成**的（同一個 `ValidateAndConvertStatisticValue` 先 parse 再 format 寫進 SQL literal），孤立測試抓不到合成後的分支行為（whitespace→0、parse 失敗→0 並寫 log）。這 5 個 task 把缺口補滿。

## 範圍與風險

- **Phase 0 全是加法且零 production 改動**：只新增 capture 測試檔 + 文件，不動任何生產碼。P0-1 不去 seam `FileProcess` 私有方法（避免其 empty/parse-fail 分支觸發 `WriteToLog`→`C:\temp` 的 CI 不必要副作用），改以鏡像 live 分支 + 生產同款 overload 的純 BCL 重現 capture。
- **Rollback**：本階段無 production 行為風險。任一 task 出錯 = `git revert` 該 test/doc commit 即可，master 的 net462 真相完全不受影響。
- **本機限制**：mac 無法編譯測試專案（CS0012 facade 坑）。**net462 真實基準以 CI（windows-latest）的 capture step detailed log 為權威來源，不可在本機臆測或手寫期望值**。

## Trait 紀律（CI 三種處置，務必正確標註）

| Category | 測試 | CI 處置 |
|---|---|---|
| `CaptureBaseline` | P0-1 / P0-2 的 capture 測試（emit-only，經 `ITestOutputHelper` 印值、**不硬斷言**；**P0-3 依 YAGNI 已裁掉、非 gate**，見下節） | 排除綠燈門檻，由專屬 capture step（`--filter Category=CaptureBaseline --logger "console;verbosity=detailed"`）收 net462 實跑值 |
| （無 trait） | `Big5DecodeTests`（P0-4） | 綠燈門檻內，必須通過；net8 未註冊 provider 會先紅 = P1-6 閘門 |
| `ByDesignRed` | 兩條 `_R5`（與本次升級無關） | `--filter Category!=ByDesignRed` 排除 |

> ⚠️ **P0-4（Big5DecodeTests）不得帶 `CaptureBaseline` 也不得帶 `ByDesignRed`**——它是 pass/fail 硬閘門，不是 capture。

---

## 任務清單

### P0-1：`ValidateAndConvertStatisticValue` 合成捕捉（B+C 同一函式）★最高優先

**為什麼**：`FileProcess.ValidateAndConvertStatisticValue`（[FileProcess.cs:1515](../../DCT_data_import/FileAccess/FileProcess.cs#L1515)，private，呼叫端 :259/:561）是 root cause **B（parse）與 C（format）唯一合成點**：
- B 腿（:1527）：用 culture-less `double.TryParse(string, out)`，受 `CurrentCulture` 影響（NLS vs ICU）。**Windows-CRT token `-1.#IND`/`1.#QNAN`/`1.#INF` 兩框架皆 `TryParse=false`→回 0**（CI 實測，見策略檔 §9；先前「net462→NaN/Inf」假設已被推翻）。真正漂移面在 culture 敏感的數字（如千分位/小數歧義 `"1,5"`），故 capture 同時 pin 這些 token 與 locale 數字的實跑值當基準。
- C 腿：回傳的 double 存進 `row[paramName]`（:265/:567），最終於 :617 經 boxed-double `item.ToString()`（CurrentCulture）序列化進 SQL literal——net462 G15 vs net8 最短往返 = INSERT 字串不同。

孤立的 `SpecialFloatParse`/`DoubleToStringFormat` 只測 BCL 單腿，**抓不到這個 production 方法的 whitespace→0、parse 失敗→0 並寫 log 的分支**。

**Seam（零 production 改動）**：**不** seam 私有 `ValidateAndConvertStatisticValue`。直接呼叫真實方法會觸發其 empty/parse-fail 分支的 `WriteToLog`→`C:\temp` 副作用（CI 不必要）；故測試**鏡像兩條 live 分支**（whitespace/null→0、Trim 後 2-arg `TryParse` 成功回值否則 0）+ 生產同款 2-arg overload，純 BCL 重現。`FileProcess.cs:1515` 維持 `private`、`FileProcess.cs` 完全不動。
> ⚠️ 與草案差異：早期計畫採 `private`→`internal` seam，落地時改為零 production 改動的鏡像分支（避免 `WriteToLog` 副作用、master net462 真相不受任何觸碰）。`InternalsVisibleTo` 仍在 `AssemblyInfo.cs:22`，本 task 未使用。

**步驟**：
1. 新增 `DCT_data_import.Tests/ValidateAndConvertStatisticValueTests.cs`（**不改 production**）：
   - `private static double Mirror(string)` 鏡像 `FileProcess.cs:1520-1533` 兩條 live 分支。
   - `[Theory]` + `[Trait("Category","CaptureBaseline")]`，emit-only。
   - 用 production 同款 overload（**no-style** `double.TryParse(s, out d)`，不是 `NumberStyles.Float`）。
   - pin `CurrentCulture`：`en-US` / `zh-TW` / `Invariant` 各跑。
   - 輸入：特殊 token `{-1.#IND, 1.#QNAN, 1.#INF, -1.#INF, NaN, Infinity, 1E400}`、locale 數字 `{"1,5", "1.5"}`、`"  3.14  "`（trim）、`""`、`null`、`"abc"`。
   - 每列同時印**回傳 double 的 bit pattern**（`BitConverter.DoubleToInt64Bits`）**與 `.ToString()` 渲染**，B 腿值與 C 腿 literal 一列記齊。
2. CI capture step 收 net462 基準（不手寫期望值）。

**驗證**：net462 build+test 綠（capture 測試不進綠燈門檻不會變紅）；CI capture step log 出現本檔每列輸出；P0-1 的輸出涵蓋 B+C 合成。
**Rollback**：revert 單一 test 檔即可（無 production 改動需還原）。

---

### P0-2：DateTime parse+format round-trip 捕捉（含 hh-vs-HH 12h 基準）

**為什麼**：生產碼 [FileProcess.cs:804-806](../../DCT_data_import/FileAccess/FileProcess.cs#L804) 先用 culture-less `DateTime.TryParse(cell, out)` 再 `.ToString("yyyy-MM-dd HH:mm:ss")`。**format 腿本身低漂移**（明確數字格式、無月名故 NLS-vs-ICU 不適用），但**上游 parse 腿中漂移**：net462-vs-net8 對模糊日期字串的 parse 成功/失敗可能翻轉，決定寫 datetime literal 還是 `'null'` fallback（:810）。

附帶：`CustomizeDateTimeParser` 在 :60/:65 用 `hh`（12 小時、無 `tt`），把 `13:00`→`01:00` 寫進 SQL——**兩框架皆同、不漂移，但是潛在 correctness bug**，capture 下來避免升級 diff 被誤算到它頭上。

**步驟**：
1. 新增 `DCT_data_import.Tests/DateTimeRoundTripCaptureTests.cs`，`[Trait("Category","CaptureBaseline")]`，emit-only，pin culture `en-US`/`zh-TW`/`Invariant`：
   - **(1) parse+format round-trip**（鏡像 :804-806）：輸入 `{"2022-06-06 13:08:22", "06/07/2022", "2022/6/6", "", "NA", "6-7-2022"}` 過 `DateTime.TryParse(s, out dt)`（與生產同 overload）再 format，印 `(parsedOk, literal 或 "null")`。
   - **(2) hh-vs-HH 基準**：固定值含下午時間（如 `2022-06-06 13:08:22`），印 `.ToString("yyyy-MM-dd HH:mm:ss")` 與 `.ToString("yyyy-MM-dd hh:mm:ss")` 兩者，釘住 12h 截斷。
2. 在 capture XML doc 註明 `hh` 為 **flagged pre-existing bug，升級 task 內不修**（CLAUDE.md：不順手改）。

> **Seam 注意**：不要去 seam 私有 SQL-builder（:806）。`DateTime.TryParse`+`ToString` 是 public BCL 組合，加上既有 public `CustomizeDateTimeParser`（已有 culture capture 測試），純對 BCL 重現即可，零 production 改動。

**驗證**：CI capture step log 出現 round-trip 與 hh/HH 兩組輸出；XML doc 標註 hh 為既有 bug。
**Rollback**：revert 單一 test 檔。

---

### P0-3：`DBmysql` DateTime→JSON 格式捕捉（最低優先，可時間盒內裁掉）

**為什麼**：[DBmysql.cs:147](../../DCT_data_import/MySqlApi/DBmysql.cs#L147) `dateTime.ToString("yyyy-MM-dd HH:mm:ss")` 進 `JObject`。值來自 MySql.Data 查詢結果**非字串 parse**，故無上游 parse 漂移；format 本身明確數字、`HH` 24h 無截斷——**Tier-1 三個 datetime 目標中最低漂移**。真正 library 依賴的是 DB→DateTime materialization（MySql.Data 9.4.0），那是 integration scope。

**步驟**：
1. 新增小型 capture（`[Trait("Category","CaptureBaseline")]`，emit-only），對固定 `DateTime` 實例（`{2022-06-06 13:08:22, 2021-12-31 23:59:59, DateTimeKind 變化}`）跑 `.ToString("yyyy-MM-dd HH:mm:ss")`，pin culture，**不連真實 DB**。
2. 把 DB-row→DateTime 轉換列為 **P0-5 的 integration smoke item**（非 unit capture）。

> **Ponytail / YAGNI**：本 task 自評零漂移、純格式契約。若 Phase 0 時間吃緊，**可裁掉**，把 DateTime materialization 完全交給 P0-5 的 manual smoke——不影響升級安全性。保留它只為文件完整。

**驗證**：capture log 有格式輸出。**Rollback**：revert 單一 test 檔。

---

### P0-4：確認 big5 provider 閘門（`Big5DecodeTests` 為 net8 紅綠閘）

**為什麼**：net8 預設 provider **不含 big5(950)**，9 個 active 資料讀取 site（見 Phase 1 P1-6）首次 `GetEncoding("big5")` 即丟。`Big5DecodeTests` 是**唯一硬斷言**：net462 必綠（內建 950），net8 未註冊 `CodePagesEncodingProvider` 前先紅 → **P1-6 的紅綠閘門**。

**步驟**（本 task 多為確認，非新碼）：
1. 確認 `Big5DecodeTests` **不帶任何 trait**（不是 `CaptureBaseline`、不是 `ByDesignRed`）→ 進綠燈門檻當硬閘門。
2. 確認它斷言真實 big5 CSV bytes round-trip 解出正確繁中（非僅 `GetEncoding` 不丟）。
3. 文件記明它的雙重角色：net462 綠燈基線 + net8 升級閘門（P1-6 由紅轉綠 = provider 註冊生效）。

**驗證**：`dotnet test --filter "Category!=ByDesignRed"` 含 `Big5DecodeTests` 且綠。
**Rollback**：N/A（確認性 task）。

---

### P0-5：機器相依 / integration 風險登記為例外 + 上線後 manual smoke 清單

**為什麼**：有些路徑**無穩定基準可 capture**，硬寫 characterization 只會抓到每台機器/每框架枚舉順序都不同的非決定值——純浪費。這些要**登記為例外**並轉成上線後人工 smoke，不是丟著不管。

**登記項**：
1. **`macid` dead code ×8**（`Tester.cs:27-28`, `FailPin.cs:28-29`, `UiStatus.cs:27-28`, `MultiSpecRawData.cs:121-122`, `RecoveryRate.cs:27-28`, `TsmcIeda.cs:31-32 & :206-207`, `RawData.cs:31-32`）：`NetworkInterface[0].GetPhysicalAddress()` 賦值後**從不讀取**（已驗證 dead code）。值被丟棄故 net8 枚舉來源改變不影響輸出。唯一 live 風險：nics 為空時 `IndexOutOfRange`（net8 一致或更好，會丟例外非靜默漂移）。
   - 處置：**不 capture**；登記為例外（機器相依、非決定）。dead code 依 CLAUDE.md **提及但不刪**（非升級 task 範圍）。empty-NIC 列為 manual smoke。
2. **MySql.Data 9.4.0 DateTime materialization**（DB row→DateTime，P0-3 上游）：integration scope，需真實 MySQL + 升級後 driver。登記為 manual smoke，非 unit capture。
3. **ASCII StreamReader ×2**（`ImportData.cs:28`, `TsmcIeda.cs:55`，`new StreamReader(responseStream)` 無 encoding）：已驗證讀的是 **FTP 目錄列表（ASCII 檔名）非 big5 CSV 內容**，UTF-8 預設兩框架對 ASCII 一致。**不加 big5、不 capture**，登記為「reviewed — 非 big5 資料路徑、無漂移」。

**上線後 manual smoke 清單**（A3 已由使用者回報 Windows PC pass；此處保留歷史紀錄）：
- [x] 各 importer 啟動不因 empty-NIC 丟非預期例外；`nics[0]` 空集合風險保持顯性。
- [x] MySql.Data 9.4.0 從真實 DB 撈出的 DateTime 字串格式與 net462 一致（抽樣比對）。
- [x] FTP 目錄列表解析（檔名 pattern match）在 net8 結果與 net462 一致。
- [x] big5 CSV 內容端到端解碼正確。
- [x] worker-hang 故障注入逼出 supervisor 路徑，確認 net8 不走 `Thread.Abort` PNSE 失敗路徑。

**步驟**：更新 `docs/codebase/NET8_UPGRADE_TEST_STRATEGY.md`（或本目錄新增 `exceptions-and-smoke.md`）記錄上述例外與 smoke 清單；同步修策略檔 §3/§7 的 doc-rot（P0-B「AverageOfSumSquare downstream capture pending」其實已完成於 `SpecialFloatParseTests.cs:62`）。

**驗證**：文件含三類例外 + smoke 清單；策略檔 doc-rot 已修。
**Rollback**：revert doc commit。

---

## Phase 0 完成定義（Phase 1 的硬 gate）

全部成立才可進 Phase 1：

1. ✅ P0-1 / P0-2 的 `CaptureBaseline` 測試已寫（**P0-3 依 YAGNI 裁掉、不作 gate**，見 P0-3 節），CI windows-latest capture step **跑出 net462 實跑基準並存成 log artifact**（權威基準，非手寫）。
2. ✅ `Big5DecodeTests`（P0-4）在 net462 綠、無 trait、是硬閘門。
3. ✅ P0-5 的例外與 manual smoke 清單已登記、策略檔 doc-rot 已修。
4. ✅ master 上有一個**綠的安全網 commit**（含上述測試 + 文件）——這是 Phase 1 切 TargetFramework 前的基準線，否則升級後分不清「自己改壞」還是「接手即壞」。

> 安全網未綠 **不得**進遷移（策略檔 §1 硬性順序）。

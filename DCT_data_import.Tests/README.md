# DCT_data_import.Tests

本專案含兩組測試:**R5 回歸樁**(釘住既有 bug)與 **net8 升級前的遷移敏感 capture 基準**。三類 `[Trait("Category", ...)]` 對映 CI 三種處置:

| Category | 測試 | CI 處置 |
|----------|------|---------|
| (無 trait) | `ComputeImportResult` happy-path、`ImportDecision` 派工、`Big5DecodeTests` | 綠燈門檻內,必須通過 |
| `ByDesignRed` | 兩條 `_R5` | 排除(`Category!=ByDesignRed`),故意紅燈 |
| `CaptureBaseline` | `SpecialFloatParseTests` / `DoubleToStringFormatTests` / `DateTimeParserCultureTests` | 排除綠燈門檻,改由專屬 capture step 以 detailed logger 收集 net462 實跑值 |

---

## 第一組:R5 回歸樁

釘住 `docs/codebase/CONCERNS.md` **R5**(CheckStatus 加權和的脆弱契約)的 regression test。

## 為什麼存在

`DbAccess.UpdateDbKeyImportStatus` 用加權和
`importResult = 8*recoveryRate + 4*tester + 2*testResult + failPin`
組出與 `db_key.check_status` 比對的 bitmask。公式**假設每個分量只回 0/1**,但匯入函式實際回 `0/1/2/3`。任一回 2/3 會讓加權和溢位、污染高位 bit,使 `importResult == check_status`(`DbAccess.cs:205`)恆 false → 部分成功一律被誤判失敗 + 寄信。

為了能在不改修法的前提下釘住此 bug,把原本 inline 的加權和抽成純函式
`DbAccess.ComputeImportResult(recoveryRate, tester, testResult, failPin)`(行為與原公式完全相同),再對它寫測試。

## 測試一覽

| 測試 | 目前狀態 | 說明 |
|------|----------|------|
| `ComputeImportResult_MatchesBitmask_WhenComponentsAreZeroOrOne` | 🟢 GREEN | happy path:分量皆 0/1 時加權和=各 bit 疊加。任何正確修法都應維持。 |
| `ComputeImportResult_StaysWithinValidFourBitMask_WhenAComponentReturnsTwo_R5` | 🔴 **RED(by design)** | 合法 check_status 是 4-bit mask(0..15);分量回 2 時結果=17 溢出 → 證明污染。 |
| `ComputeImportResult_DoesNotConflateDistinctComponentStates_R5` | 🔴 **RED(by design)** | `testResult=2` 與 `tester=1` 加權和都=4、無法區分 → 證明資訊遺失。 |

兩個 `_R5` 測試斷言「正確契約」,**在 R5 修正前 by-design 失敗**。任一合理修法(成功才 set bit、或把分量正規化為 0/1)都會讓它們轉 GREEN——屆時即代表 R5 已修。

> ⚠️ 這是**故意保留的紅燈**,不是壞掉的建置。

兩條 `_R5` 測試已標 `[Trait("Category", "ByDesignRed")]`,CI(`.github/workflows/ci.yml`)以 `--filter "Category!=ByDesignRed"` 排除,故 CI 綠 ≠ R5 已修。**本機要看紅燈請跑不帶 filter 的完整測試**;R5 修好後移除這兩條的 trait(或讓它們自然轉綠)即重新納入 CI。

## 如何執行(Windows)

> 本測試目標框架 net462。**R5 紅綠燈已在 macOS 驗證**:以 net8.0 + xUnit 跑同段加權和邏輯,得 **6 綠(happy-path Theory)/ 2 紅(`_R5` by-design)**,與下表一致。net462 測試專案本身在 mac 以 `FrameworkPathOverride`→Mono 編譯時尚有 `System.Runtime` facade 小坑(CS0012)未解,故**實跑 net462 測試專案仍以 Windows / Mono 為準**;主專案(非測試)已實測可在 macOS 零錯誤編譯。

主專案用 `packages.config`、本測試專案用 PackageReference。先確保主專案的 NuGet 套件已還原(Visual Studio 會自動還原;或 `nuget restore DCT_data_import.sln`),再:

```powershell
# 方式一:CLI(需安裝 .NET SDK)
dotnet test DCT_data_import.Tests\DCT_data_import.Tests.csproj

# 方式二:Visual Studio
# 開 DCT_data_import.sln → Test Explorer → Run All
```

預期結果:6 個 `[InlineData]` 案例 + happy-path 全綠,兩個 `_R5` 測試紅燈。

---

## 第二組:net8 升級前的遷移敏感 capture 基準

策略全文見 [`docs/codebase/NET8_UPGRADE_TEST_STRATEGY.md`](../docs/codebase/NET8_UPGRADE_TEST_STRATEGY.md)。核心:net462→net8 有四類**靜默**行為差異,升級前先在 net462 把實跑值「拍照」存成基準,升級後同檔再跑、比對輸出差異 = 回歸訊號。

| 測試檔 | root cause | capture 的是什麼 |
|--------|-----------|------------------|
| `Big5DecodeTests` | **A** | big5/cp950 解碼。**唯一硬斷言**(net462 必綠),升級後 net8 未註冊 `CodePagesEncodingProvider` 會先紅 → 升級閘門訊號。 |
| `SpecialFloatParseTests` | **B** | `double.TryParse` 對 `-1.#IND`/`1.#QNAN`/`1.#INF` 等舊式 token 的解析語意,及其流入 `AverageOfSumSquare` 的下游後果。 |
| `DoubleToStringFormatTests` | **C** | `double.ToString()` 預設格式(net462 G15 vs net8 最短往返)。直接特性化 BCL 即等價覆蓋 `FileProcess` 兩處轉換點,**免抽 seam、不動 production 程式**。 |
| `DateTimeParserCultureTests` | **D** | `FileProcess.CustomizeDateTimeParser` 無 culture 的 `DateTime.TryParse` 在 en-US/zh-TW/Invariant 下的漂移(NLS vs ICU)。 |

### capture-don't-assert 紀律

除 `Big5DecodeTests` 外,`CaptureBaseline` 全屬 **emit-only**(經 `ITestOutputHelper` 印值、不硬斷言)。原因:net462 真實值在 CI(windows-latest)跑出來前不可臆測;期望值由「實際輸出」決定,即使出乎意料也照貼。本機(mac)無法編譯測試專案(CS0012),**net462 真實基準以 CI capture step 的 detailed log 為權威來源**。

### 已知 emit-only 的非決定性

`DateTimeParserCultureTests`:解析失敗時 `CustomizeDateTimeParser` 回 `DateTime.Now`(非決定性)。判讀:回傳含輸入年份(如 `2022-`)= 解析成功;回傳為今天日期 = 落入 Now fallback(該 culture 解析失敗)。故只 capture、不斷言。

# DCT_data_import.Tests

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

> 本測試目標框架 net462,**無法在 macOS/Linux 上 build/run**;本檔由 macOS 環境撰寫,紅綠燈尚未在本機驗證,需於 Windows 上確認。

主專案用 `packages.config`、本測試專案用 PackageReference。先確保主專案的 NuGet 套件已還原(Visual Studio 會自動還原;或 `nuget restore DCT_data_import.sln`),再:

```powershell
# 方式一:CLI(需安裝 .NET SDK)
dotnet test DCT_data_import.Tests\DCT_data_import.Tests.csproj

# 方式二:Visual Studio
# 開 DCT_data_import.sln → Test Explorer → Run All
```

預期結果:6 個 `[InlineData]` 案例 + happy-path 全綠,兩個 `_R5` 測試紅燈。

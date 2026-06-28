# DCT_data_import.Tests

本測試專案已是 `net8.0-windows` single target。A4 後所有測試都屬正常綠燈門檻；原本的 `CaptureBaseline` emit-only 角色已結束，相關測試已改成 net8 行為硬斷言。

## 測試範圍

| 類別 | 測試 | CI 處置 |
|------|------|---------|
| R5 回歸 | `CheckStatusWeightedSumTests` | 必須通過 |
| 遷移/執行期契約 | Big5 provider、Thread supervisor、App.config、DryRun、ImportDecision | 必須通過 |
| net8 行為釘樁 | Special float parse、double formatting、DateTime parser、statistic value conversion | 必須通過 |
| SPC 回歸 | `CalculateSpcTests` | 必須通過 |
| Parser / helper 特性化 | Parser characterization、`FileContentFormat.Compare*()`、`FileProcess` helper | 必須通過 |
| MySQL driver golden master | `MySqlDataDateTimeRoundTripTests` | 無 connection string 時 skipped；有設定時必須通過 |

目前不使用 `ByDesignRed` 或 `CaptureBaseline` filter；若未來新增 by-design red 測試，需同步更新 CI 與本檔。

## R5 回歸樁

`DbAccess.UpdateDbKeyImportStatus` 以加權和組出與 `db_key.check_status` 比對的 bitmask：

```text
importResult = 8*recoveryRate + 4*tester + 2*testResult + failPin
```

公式只在每個分量都是 `0/1` 時安全，但匯入函式實際回 `0/1/2/3`。R5 修法把分量正規化為 `Result == 1 ? 1 : 0`，成功才設位，失敗碼 `2/3` 不貢獻 bit。測試也明確擋下 `Math.Min(x, 1)` 這種會把失敗碼誤當成功的修法。

## net8 行為釘樁

這些測試源自 net462→net8 的 migration probes。A4 後不再 capture 兩套框架輸出，而是固定斷言目前 net8 行為：

- `Big5DecodeTests`: codepage 950 / big5 provider smoke。
- `SpecialFloatParseTests`: legacy float token、`NaN`、`Infinity`、`1E400` 解析語意。
- `DoubleToStringFormatTests`: net8 shortest round-trippable double formatting。
- `DateTimeParserCultureTests`: `CustomizeDateTimeParser` 在指定 cultures 下的 net8 結果。
- `MySqlDataDateTimeRoundTripTests`: opt-in 真 MySQL `DATETIME` / `DATETIME(6)` / zero-date round-trip；設定 `DCT_MYSQL_GOLDEN_MASTER_CONNECTION_STRING` 後才會連線，使用 temporary table，不需匯入資料檔。
- `ValidateAndConvertStatisticValueTests`: parse + format 合成點的 CurrentCulture 行為。
- `CalculateSpcTests`: `AverageOfSumSquare` 的 pass/fail 篩選、無有效值 fallback、負 variance guard。

## 如何執行

```powershell
dotnet restore DCT_data_import.sln
dotnet build DCT_data_import.sln --configuration Release --no-restore
dotnet test DCT_data_import.Tests\DCT_data_import.Tests.csproj --configuration Release --no-build
```

MySQL DATETIME golden master 是 opt-in；本機可先建立空測試 schema，再用下列環境變數執行同一個 test command：

```powershell
$env:DCT_MYSQL_GOLDEN_MASTER_CONNECTION_STRING="Server=localhost;Port=3306;Database=dct_data_import_test;Uid=root;Pwd=***;SslMode=None;AllowPublicKeyRetrieval=True;Convert Zero Datetime=True"
```

macOS 可作 build/test evidence；完整服務 runtime smoke 仍需 Windows + 實際或影子 MySQL/FTP 環境。
2026-06-28 已在 macOS 直接 build 測試專案，未再重現舊 CS0012 facade 編譯問題。

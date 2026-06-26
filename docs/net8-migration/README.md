# .NET 8 升級計畫

DCT_data_import 從 .NET Framework 4.6.2 升級到 **.NET 8（`net8.0-windows`）** 的可遵循實作計畫。

## 順序（硬性，非建議）

```
Phase 0 安全網全綠 + net462 基準已 capture  ──gate──►  Phase 1 遷移本體
```

升級動作（切 TargetFramework）在 **Phase 1**。Phase 0 不做任何框架變更，只補升級前必須先在 net462 拍照的測試基準——**安全網未綠不得進 Phase 1**（策略檔 §1）。

## 文件

| 檔 | 內容 |
|---|---|
| [phase-0-safety-net.md](phase-0-safety-net.md) | 升級前安全網補完：P0-1~P0-5（合成 capture、DateTime round-trip、big5 閘門確認、例外登記） |
| [phase-1-migration.md](phase-1-migration.md) | 遷移本體：P1-1~P1-8 + CI（P1-1b）+ dry-run 影子開關（P1-7b，6 chokepoint gate）+ 5 項決策待確認 + **分層 rollback（L0~L3）+ cutover runbook** |
| [../codebase/NET8_UPGRADE_TEST_STRATEGY.md](../codebase/NET8_UPGRADE_TEST_STRATEGY.md) | 上游策略（root cause / capture-don't-assert / seam）——權威來源 |

## 進場前須拍板（Phase 1 開頭，未回覆採預設）

App.config 保留 · Thread.Abort 最小修 · 雙 TFM 過渡 · 影子模式範圍（**dry-run 開關＝正式 task P1-7b，6 chokepoint gate**）· MySql.Data pin 9.4.0。細節見 phase-1 的「決策待確認」表。

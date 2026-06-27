# .NET 8 遷移 — 剩餘開發 backlog

> 產生於 PR #4（commit `4c83125`）squash-merge 進 master 後，A4 後已更新為 net8-only 收尾狀態。本檔保留歷史序列與後續 backlog，給後續每一個新 session 接手用。
> 治理約束見 [memory/net8-upgrade-constraints]、[phase-1-migration.md](phase-1-migration.md)、根目錄 `CLAUDE.md`。

## 已完成（commit #4，**歷史紀錄，不要重做**）

| 項 | 證據 |
|---|---|
| 雙 TFM `net462;net8.0-windows`（SDK-style、PackageReference） | `DCT_data_import.csproj` |
| Thread.Abort 最小修（P1-4）、Assembly.CodeBase（P1-5） | source |
| big5 CodePages provider 註冊（P1-6） | `Program.cs:33` |
| DryRun/shadow 開關 + 6 個 chokepoint gate（P1-7b） | `Common/RuntimeMode.cs` + DBmysql/ImportData/EmailModels/NotificationService |
| S3 帳密遮罩（不再印明文 PASSWORD） | `Program.cs:35-37`（只印 set/unset） |
| MySql.Data 9.4.0 pin（Q5）、System.Private.Uri 4.3.2 transitive-pin（3 CVE） | csproj |
| NuGetAudit SCA CI gate（moderate+ 阻擋） | `ci.yml:31-32` |
| golden-master 逐值 diff（79 match / 8 良性 divergence，gate PASS） | [golden-master-diff-P1-8.md](golden-master-diff-P1-8.md) |
| ThreadSupervisor / ImportDecision / DryRun / AppConfigContract 回歸測試 | `DCT_data_import.Tests/` |
| net462 CaptureBaseline CI capture step（emit-only） | `ci.yml:40-41` |

---

## 後續已合併（PR #6–#9，**不要重做**）

| 項 | 證據 |
|---|---|
| **B / R5** check_status 加權和溢位修（`ComputeImportResult` 正規化 `Result == 1 ? 1 : 0`） | PR #6 `0a3ab92` |
| **A0** cutover 前置：RID/SelfContained、CI net8 capture + golden-master fail-on-diff（A4 後已退場）、rollback runbook L2/L3、dct.sql 影子 schema wiring | PR #7 `11305eb` |
| **C / S4** SMTP IP/sender → App.config（ConfigurationManager TryParse 雙 TFM） | PR #8 `7e6d6ba` |
| **NI-3** 收件人空清單回明確設定錯誤 | PR #9 `f15eb8e` |

> A0 註：「golden-master 期望值固化成 committed const」子項曾由跨 TFM 即時逐值 diff gate 取代；A4 砍掉 net462 後，CI 已改為 single net8 hard assertions，相關 diff 腳本退場。rollback runbook 的「乾演練一次」併入 A1（需環境）。

---

## Stream A — Cutover 收尾（受治理序列，**需 Windows runtime**）

> 這是把服務真正切到 net8 上線、再砍 net462 的序列。A1-A3 需要 Windows / 非 prod 或 production-like 環境；A4 已於使用者回報 A1-A3 Windows PC pass 後執行。

### A0 — Cutover 前置 ✅ **已完成（PR #7 `11305eb`，不需 Windows）**
- [x] **self-contained publish**：csproj `RuntimeIdentifier=win-x64` / `SelfContained`（取代散落 runbook 文字）。
- [x] **CI net8 capture + fail-on-diff（P1-1b，歷史）**：曾以雙 TFM capture + diff gate 守護 cutover；A4 後已退場，CI 改為 single net8 hard assertions。
- [~] **golden-master 期望值固化**：**由上方跨 TFM 即時 diff gate 取代**，不再需要固化 committed const。
- [x] **rollback runbook L2/L3**：[phase-1-migration.md] 已補可執行步驟（「乾演練一次」併入 A1，需環境）。
- [x] **shadow schema init 接 dct.sql**：[phase-1-migration.md:449] step 5 已引 `sql/dct.sql` 建影子 schema。

### A1 — Q4 dry-run 影子驗證（**需 Windows + 非 prod 環境 + 與 net462 prod 平行資料**）
- [x] 使用者回報 Windows PC 已順利 pass A1。
- [x] 影子跑 **≥1 個完整營運週期**（Tester / UiStatus / TSMC 三條 thread 都至少輪一圈），全程 DryRun=true，DB 留 snapshot/binlog。
- [x] 觀察 **Family B（1E400 / special float）** 是否在真實資料出現。

### A2 — Production cutover（**需 Windows，blocked on A1 綠燈**）
- [x] 使用者回報 Windows PC 已順利 pass A2。
- [x] 停 net462 → publish self-contained net8 → 啟動 → DB snapshot/binlog 留存（cutover step 6，[phase-1-migration.md:353-357]）。

### A3 — Cutover 期人工 smoke（**需真 MySQL/FTP + net8 runtime**）
- [x] 使用者回報 Windows PC 已順利 pass A3。
- [x] 5 條 P0-5 手動 smoke（[exceptions-and-smoke.md:41-44]）：空 NIC IndexOutOfRange、MySql.Data 9.4.0 DATETIME materialization、FTP 目錄列表解析、big5 CSV 端到端、worker-hang 故障注入。

### A4 — 砍 net462 + 文件同步（**已完成**）
- [x] csproj 回 single `net8.0-windows`、移除 net462-only 套件（ConfigurationManager 9.x / Unsafe / Tasks.Extensions / ReferenceAssemblies）。`App.config` 已無 binding redirects。
- [x] 移除 source/test 內 net8/net462 腳手架：`Program.cs`、`ReadWriteINIfile.cs`、`EncodingTestBootstrap.cs`、`AppConfigContractTests.cs`。
- [x] CaptureBaseline emit-only → net8 硬斷言。
- [x] 移除未使用的跨 TFM CI diff script。
- [x] **文件同步**：`docs/codebase/`、`CLAUDE.md`、根 `AGENTS.md`、`DCT_data_import.Tests/README.md`、`專案架構報告.md`、`專案架構視覺化.html`。

---

## Stream B — R5 加權和 bug（**✅ 已完成 2026-06-27，`fix/r5-checkstatus`**）

- [x] `DbAccess.ComputeImportResult` 分量正規化:**用戶規格決定（2026-06-27）= `Result == 1 ? 1 : 0`**（成功才設位,失敗碼 2/3 與缺席同視為 0;**明確排除 `Math.Min(x,1)`**——它會把 2/3 映成 1、反把失敗當成功）。單點 root-cause guard,0/1 輸入行為不變、僅修正 ≥2 溢位（`DbAccess.cs:160`）。
- [x] `CheckStatusWeightedSumTests.cs`:2 條原 `_R5` by-design RED 轉綠 + 新增第 3 條判別測試（`_FailureCodeContributesNoBit_DistinctFromSuccess_R5`，擋 `Math.Min` 誤修）→ 移除 `ByDesignRed` trait → 重納 CI 綠燈門檻。A4 前 full suite 已擴至 199 綠。
- [x] 文件同步:`CONCERNS.md` R5 標記已修復、`docs/codebase/TESTING.md`、`NET8_UPGRADE_TEST_STRATEGY.md`、`DCT_data_import.Tests/README.md`、`CLAUDE.md` 關鍵約束 #3。
- [x] B PR 收尾品質閘已完成：review / verification / PR squash merge 已結束；後續新工作依各自分支重新執行收尾閘。

---

## Stream C — 安全債（**僅剩 S2**；S4 已完成 PR #8、S1 維持不動、S3 已完成）

- ~~**S1**：App.config 明文 DB/FTP 帳密~~ → **用戶決定（2026-06-27）：維持不動**，既有已知債明確不處理。**不得新增/擴大/搬移**該段帳密（連 env var 化也不做）。
- [x] **S2（HIGH，已完成 A/PR-1 + A/PR-2）**：SQL 全字串串接零參數化（`FileProcess`/`DbAccess`/`TsmcIeda`）→ 已改參數化（Dapper 具名參數）。已完成 `DbAccess` / `TsmcIeda` / identifier chokepoint / `FileProcess.Import*` 批次 INSERT values。完整計畫見 [S2-SQL-PARAMETERIZATION-PLAN.md](S2-SQL-PARAMETERIZATION-PLAN.md)、交接見 [HANDOFF-S2.md](HANDOFF-S2.md)。
- [x] ~~**S4（MEDIUM）**~~ → **✅ 已完成 PR #8 `7e6d6ba`**：SMTP IP/sender 已移到 App.config（ConfigurationManager TryParse 雙 TFM）；內網 relay 暫不需 auth/TLS、理由已記錄。

---

## Stream D — Tier 3 測試廣度網（**可獨立開始；R1 傘下**）

- [ ] 6 個 parser characterization 測試（需 `InternalsVisibleTo` 給 3 個 private `FileRead*`）。
- [ ] `FileContentFormat.Compare*()` 測試。
- [x] `CalculateSPC.AverageOfSumSquare` 3 情境測試。
- [ ] `FileProcess` helper（`ConvertEmptyToDefaultString`、`AddColumnForDataset` round-9）測試。
- [ ] MySql.Data DATETIME driver round-trip golden master（需真 MySQL）。
- [ ] coverage 工具（coverlet）+ 門檻。
- [ ] mac CS0012 facade 編譯問題（測試專案）。

---

## Stream E — 修正/現代化（多數延後或機會性；**現代化需決策者確認後才動**）

> CLAUDE.md：「不主動現代化」。E 類除 R2/R3 屬 correctness/維運外，D2-D5/P1-P3 動前須確認。

- [ ] **R2（MEDIUM）**：SPC 負根號 NaN guard。
- [ ] **R3（MEDIUM）**：hardcoded `C:\temp` log 路徑 → 可設定（net8 cutover 可能觸發）。
- [ ] **R4（LOW）**：log rotation（optional）。
- [ ] **D3（MEDIUM）**：dead code 清理（`Program.cs` TEST CASE 區塊、`DbAccess` 舊邏輯）。
- [ ] **D4（MEDIUM）**：`TsmcIeda` self-contained 路徑收斂。
- [ ] **P1（MEDIUM）**：O(n²) SQL 字串累加 → StringBuilder/batch。
- [ ] **D2/D5/P2/P3（LOW/deferred）**：型別更名、命名不一致、fake async（非目標）、手動 GC.Collect。
- [ ] 觀測性（metrics/tracing/APM、error tracking）、DB migration 工具（LOW，外部 owner）。

---

## 實作規劃 — 每項「最小改動 / 原始設計」兩案（待用戶逐項確認）

> 規劃紀律：每項先用 **ponytail 階梯**（要不要存在 → 重用既有 → stdlib/native → 既裝依賴 → 一行 → 才到最小新 code）取最高可行 rung，蓄意簡化標 `ponytail:` 註記。
> **不可偷懶區（最小改動不得鬆綁）**：修 bug 先寫 failing regression test；高風險（auth/migration/security）附 rollback；SQL 參數化（injection）；改高扇入共用檔前 `deps-check`；部署前 `*-release-verification` + `dependency-security-scan`。
> **完成狀態以上方各 Stream 章節 + 「後續已合併」為準**（A0 / B / S4 / S2 / A1-A4 已完成或使用者回報通過）；本表為當初「兩案」決策的理由存檔，非 live 清單。

| 項 | 最小改動案（建議預設 ★ 多在此） | 原始設計案 | 建議 + trade-off |
|---|---|---|---|
| **A0 self-contained publish** | csproj 加 `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` + `<SelfContained>true</SelfContained>`，或 publish 命令帶 `-r win-x64 --self-contained`。不寫 pubxml。 | 建 `Properties/PublishProfiles/win-x64.pubxml` + single-file/trimming + 版本戳記 | ★最小。pubxml 只是 VS GUI 糖，CLI 帶參即可；single-file/trim 對 net8-windows + MySql.Data 有風險，不值得。 |
| **A0 CI net8 capture + fail-on-diff** | ci.yml 複製 net462 capture step 改 `-f net8.0-windows`，加 shell diff 比兩份輸出、非零 exit 1；容忍清單讀 golden-master-diff doc。 | 寫專屬比對工具/測試（結構化 diff + 容忍清單）整進測試專案 | ★最小。dump+diff 夠擋回歸；容忍清單已存在於 doc，腳本引用即可。 |
| **A0 golden-master 固化** | net462 capture 值貼成測試 `const` 期望、capture 測試改 `Assert.Equal`。 | 引 Verify/ApprovalTests snapshot 框架管 received/approved | ★最小。既有測試無 snapshot 框架，引入是新依賴 + 學習成本；固定值手貼夠用。 |
| **A0 rollback runbook L2/L3** | phase-1-migration.md 補可執行步驟 + 跑一次乾演練記錄。純文件。 | 自動化 rollback script（資料夾切換 + DB 還原）+ 演練 | ★最小（cutover 一次性，腳本化效益低）。**安全區：乾演練不可省。** |
| **A0 dct.sql wiring** | runbook 加一行「影子環境先跑 sql/dct.sql 建 schema」+ 連結。 | 建 migration 工具管 schema 版本 | ★最小。一次性影子環境；migration 工具屬 Stream E LOW，別在此引。 |
| **A1-A3 影子/cutover/smoke** | 用既有 DryRun 機制 + 既有 capture 跑，不新增工具。流程紀律題，非選型。 | — | 沿用既有機制；無兩案。 |
| **A4 砍 TFM + #if + 文件** | csproj 刪 net462 分支 + 對應 `#if`；文件就地改字串。機械式。 | 加 single-file/trim/AOT 評估 + 文件重寫 | ★最小。A4 是清理非再設計；現代化評估屬 Stream E，別混入。 |
| **B R5 加權和** ✅ | **已採最小改動案（2026-06-27）**:`ComputeImportResult` 一處正規化 `Result == 1 ? 1 : 0`（**非 `Math.Min`**——它會把失敗碼 2/3 當成功）再加權;3 條 `_R5` 轉綠。單點 root-cause guard。 | 重設計 check_status：enum/flags 型別取代裸 int 加權和、各分量明確語意、改所有讀寫點 | 規格確認＝「分量該 0/1」→ 採★最小單點正規化（已完成）。原始設計（enum/flags 重設計）若日後揭露加權和是錯抽象,屬 `mp-improve-codebase-architecture` 另立項。 |
| **C / S1 明文帳密** | ~~（已descope）~~ | ~~（已descope）~~ | **用戶決定：維持不動，不處理。** App.config 帳密為接受的既有債；不得新增/擴大/搬移。 |
| **C / S2 SQL 參數化** | 既裝 **Dapper 具名參數**逐一改現有 INSERT/SELECT/UPDATE，簽名不變、字串改 `@param`。 | 抽 repository 層 + 參數化 + 整合測試 | ★最小（Dapper 已在用）。抽 repository 屬現代化、非本債目標。**安全區：參數化不可省；動 DBmysql/FileProcess/TsmcIeda 前 `deps-check`。** |
| **C / S4 SMTP** | IP/sender 移到 App.config key；評估是否需 auth（內網 relay 不需則記錄理由）。 | 認證 SMTP + TLS + 重試 | ★最小先消 hardcode；auth/TLS 視環境。 |
| **D 測試廣度** | capture-don't-assert characterization，沿用既有 xUnit + capture 模式，`InternalsVisibleTo` 開私有方法，無新框架。 | 引 AutoFixture/Bogus/FluentAssertions/coverlet 門檻 + property-based | ★最小。既有測試零這些依賴；characterization 釘行為不需資料生成器；coverlet 可選非阻塞。 |
| **E R2/R3/D3/D4/P1…** | 逐項最小：R2 一個 NaN guard；R3 log 路徑改讀 config；D3 刪 dead code；P1 改 StringBuilder。 | 對應子系統重構 | 多數延後；**D2/D5/P2/P3 等現代化動前須決策者確認**（CLAUDE.md「不主動現代化」）。 |

# 任務交接 — DCT_data_import / C/S2 SQL 參數化(HIGH 安全債)

> 給新 session 或 codex 用。**整份貼進去即可開工**;需要全細節時讀本檔 §「先讀這些」列的權威來源。
> 產生時 master HEAD = `afdc7c4`。執行前先 `git log --oneline -3` 確認 HEAD 未漂移。

---

## 你是誰、做什麼
接手 .NET Framework 4.6.2 → .NET 8(雙 TFM `net462;net8.0-windows`)半導體測試 ETL 批次服務的
**C/S2 — 消除 SQL 全字串串接、改參數化已完成 A/PR-1 + A/PR-2**。其餘已完成或延後(見最末)。
專案根:`DCT_data_import_data_stream_codex`。

## 先讀這些(權威來源,勿憑記憶)
1. `docs/net8-migration/S2-SQL-PARAMETERIZATION-PLAN.md` ← **S2 完整計畫**:注入面逐站盤點 + 致能改動 + 測試紀律 + 範圍分期。照它做,勿重跑證據蒐集。
2. `docs/net8-migration/REMAINING-WORK.md` ← 全 backlog 與每項雙案(C/S2 在 line 112)
3. `docs/codebase/CONCERNS.md`(S2 列 HIGH)、根 `CLAUDE.md`(關鍵約束 #2/#4/#5)
4. `DCT_data_import.Tests/README.md`(R5 已示範「先寫 failing test 再修」模式)

## 綁定決策(不可推翻,違反屬 bug)
- **最小改動案**(用戶全域選擇):用既裝 Dapper 2.1.66 具名參數逐站把 `'"+v+"'` 改 `@param`,簽名不變、不抽 repository、不動 fake-async 風格。原始設計案(抽 repo 層)不取。
- **S1 維持不動**:App.config 明文 DB/FTP 帳密是接受的既有債,不處理、不搬移、不 env-var 化。S2 過程**不得新增/擴大/擴散**該段帳密。
- **安全紅線**:絕不把 DB/FTP 帳密或 SQL 全文印到 console/log;DryRun guard 訊息只能說 skipped,不印 SQL/帳密;新 config key 只進範例(key name)、值走外部。
- **NEVER force-push master**;只在 feature 分支用 `--force-with-lease`。

## S2 致能改動(計畫 §1,已驗證乾淨)
參數化管線其實**已存在於** `DBmysql.Excute_mysql_cmd(cmd, mode, object parameters)`(把 parameters 傳給 Dapper),只是被上層截斷。三步打通:
1. `DbApi/DbObject.cs:58` — `Execute_query` DTO 加 `public object Parameters { get; set; }`
2. `DbApi/DatabaseService.cs:47` — 改成 `DB.Excute_mysql_cmd(Execute_query.Query, mode, Execute_query.Parameters)`
3. 逐站:`'"+v+"'` → `@p` + `Parameters = new { p = v }`;批次 INSERT 站已用 `DynamicParameters` 逐列具名

## 三種注入機制(別混用)
- **值內插 → Dapper `@param`**:`DbAccess.cs` 186/230-233/270/315-318、`FileProcess` 批次 INSERT 迴圈(112-147/201-207/313/387-420/615-617/690-718)+ `ExecuteInsert`(1349 的 values)、`TsmcIeda.cs` 274/314/323/326 已完成。**複雜度集中在 `FileProcess`(~1500 行、最高扇入)的批次 values 累加迴圈，已於 A/PR-2 收斂**。
- **識別碼注入(MySQL 不能 `@param` 表/欄名)→ 白名單/字元驗證**:`FileProcess.cs:33`、`FileProcess.cs:1349`(tableName/columns)、`DatabaseService.cs:107`。
- **DataTable.Select 記憶體內篩選(Dapper 管不到)→ 單引號跳脫或 LINQ**:`TsmcIeda.cs:105/175`。

## 執行紀律(逐項照做)
1. 動工前對 `FileProcess.cs` / `DbAccess.cs` / `DBmysql.cs` / `TsmcIeda.cs` 跑 **deps-check**(高扇入必跑)。
2. 開分支 `fix/s2-sql-injection`(絕不直接動 master)。
3. **先寫 failing 接縫測試(紅)再改**:斷言改後 `Execute_query.Query` 含 `@p` 佔位符、`Parameters` 帶原值;一條塞 `'; DROP TABLE db_key;--` 證明惡意值進 `Parameters` 不進 SQL 文字;識別碼站一條擋非法表名。SQL 路徑無 DB 不能端到端(mac 無 MySQL、net462 測試只在 CI windows-latest),靠接縫測試 + 既有 golden-master CI gate 把關行為等價。
4. 實作後品質閘**依序**:`code-simplifier` → `dotnet-code-reviewer` → `backend-release-verification` → `dependency-security-scan`。全綠才往下。
5. **全綠後才手動 invoke `finishing-a-development-branch`** 做 squash PR(勿讓 executing/subagent 自動觸發 finishing)。
6. **Merge gate**:CI 綠**且**等 Copilot 非阻塞 review(開 PR 後約 2-3 分鐘才異步出來,開 PR 當下空是延遲不是「無」)。有未處理建議走 `receiving-code-review`(採納或有據 pushback + 在 thread 回覆)才 merge。merge 後自動刪分支。
7. 同步更新 `CONCERNS.md` 的 S2 標記 + `S2-SQL-PARAMETERIZATION-PLAN.md` 狀態(doc rot 視同 bug)。

## 範圍分期結果
- **A(★推薦)2 PR 風險二分，已完成**:PR-1 低風險群(DbAccess 4 值站 + TsmcIeda 值站&DataTable.Select + 識別碼白名單);PR-2 單獨攻 `FileProcess` 批次 INSERT。
- **B 1 PR 一次到底**(blast radius 最大)。
- **C 逐站最小步 3+ PR**(最保守最慢)。

## 約定/風格
Conventional Commits zh-TW(commit 首行中 ≤30);4 空格縮排、Allman 大括號;繁中註解為主;namespace 與資料夾不對齊是既有現象勿「修正」;async 全 `.GetAwaiter().GetResult()` 勿改真 async。

## 已完成,**不要重做**
雙 TFM 升級本體(PR #4)、A0 cutover 前置(PR #7=`11305eb`)、R5 加權和溢位修(PR #6=`0a3ab92`)、S3 帳密遮罩、S4 SMTP→App.config(PR #8=`7e6d6ba`)、NI-3 收件人空清單明確錯誤(PR #9=`f15eb8e`)。

## 其餘 backlog(本次不做,僅供脈絡)
A1-A4 cutover 需 Windows runtime + 影子/prod 環境,A4 強治理延後;Stream D 測試廣度網與 Stream E(R2/R3/D2-D5/P1-P3 + 現代化)延後,現代化動前需決策者確認。完整清單見 `REMAINING-WORK.md`。

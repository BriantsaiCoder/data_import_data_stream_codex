# C/S2 — SQL 參數化（HIGH）實作計畫

> 本檔把 S2 的注入面盤點 + 致能改動 + 測試紀律 + 範圍分期一次釘死，給接手 S2 的新 session/codex 用，**避免重跑證據蒐集**。
> 權威背景：[CONCERNS.md S2](../codebase/CONCERNS.md)、[REMAINING-WORK.md Stream C](REMAINING-WORK.md)、根 `CLAUDE.md` 關鍵約束 #4。
> 狀態：**A/PR-1 已完成**（DbAccess 4 值站 + TsmcIeda 值站/DataTable.Select + identifier chokepoint）；**A/PR-2 已完成**（FileProcess 批次 INSERT 值參數化）。

## 1. 致能改動（兩案共用；已驗證乾淨）

參數化管線**其實已存在於 `DBmysql.Excute_mysql_cmd(string cmd, string mode, object parameters)`**（line 51；把 `parameters` 傳給 Dapper `connection.Query`/`connection.Execute`，line 136/201），只是被上層截斷：

1. `Execute_query` DTO 加 `public object Parameters { get; set; }` — `DbApi/DbObject.cs:58`
2. `DatabaseService.ExecuteSqlAsync` 把它接通 — `DbApi/DatabaseService.cs:47`
   `return DB.Excute_mysql_cmd(Execute_query.Query, mode);`
   → `return DB.Excute_mysql_cmd(Execute_query.Query, mode, Execute_query.Parameters);`
3. 逐站：`"'" + v + "'"` → `"@p"` + `Parameters = new { p = v }`；批次站用 `DynamicParameters` 逐列具名

簽名不變、呼叫端不需大改 → 對齊「沿用既有風格、不現代化」。

## 2. 注入面盤點（已逐站驗證 2026-06-27）

### 2a. 值內插 → Dapper `@param`（injection 主面）

| 站點 | SQL | 外部可注入值 |
|---|---|---|
| `DbAccess.cs:186` | `SELECT id, check_status FROM db_key WHERE db_key='" + dbKey + "'` | `dbKey`（✅ A/PR-1 已改 `@dbKey`） |
| `DbAccess.cs:230-233` | `UPDATE db_key SET …,remark='" + remark + "' WHERE db_key='" + dbKey + "'` | `remark`、`dbKey`（✅ A/PR-1 已改 `@remark` / `@dbKey`；其餘狀態值也一併參數化） |
| `DbAccess.cs:270` | `SELECT id, check_status FROM db_key_ui_status WHERE db_key='" + dbKey + "'` | `dbKey`（✅ A/PR-1 已改 `@dbKey`） |
| `DbAccess.cs:315-318` | `UPDATE db_key_ui_status SET …,remark='" + remark + "' WHERE db_key='" + dbKey + "'` | `remark`、`dbKey`（✅ A/PR-1 已改 `@remark` / `@dbKey`） |
| `FileProcess.cs:33` | `SELECT … FROM {表名} WHERE db_key=…` | db_key 值（✅ A/PR-1 已改 `@dbKey`；表名見 2b） |
| `FileProcess.cs` 批次 INSERT 迴圈：`112-147`、`201-207`、`313`、`387-420`、`615-617`、`690-718` | CSV 欄位值 `values += "'"+v+"',"` 累加多列 `VALUES (…),(…)` | CSV 全欄位（✅ A/PR-2 已改 `DynamicParameters`，SQL text 只保留 placeholders / `NULL` / `0` literal） |
| `FileProcess.cs:1349` `ExecuteInsert` | `"INSERT INTO " + tableName + "(" + columns + ") VALUES (" + values + ");"` | `values`（✅ A/PR-1 已打通 optional `Parameters`；✅ A/PR-2 已讓 FileProcess 批次 values 傳入 `DynamicParameters`） |
| `TsmcIeda.cs:274/314/323/326` | INSERT 帶 IedaTitle/IedaContent 檔案資料 | IEDA 檔內容（✅ A/PR-1 已改 DynamicParameters） |

### 2b. 識別碼注入 → 白名單/驗證（**MySQL 無法用 `@param` 參數化表/欄名**）

| 站點 | 注入點 |
|---|---|
| `FileProcess.cs:33` | SELECT 的表名（✅ A/PR-1 已加 tableName identifier guard） |
| `FileProcess.cs:1349` | `ExecuteInsert` 的 `tableName` + `columns`（✅ A/PR-1 已加 chokepoint validation；columns 允許現有 schema 的 backtick 欄名） |
| `DatabaseService.cs:107` | `CheckDatabaseAndTableExists` 的 `tableName`（✅ A/PR-1 已改為 Dapper value parameter） |

> 處理：對已知 schema 表/欄名做白名單比對或字元驗證（如只允許 `[A-Za-z0-9_]`），**不可** `@param`。多數 tableName 為內部衍生，真實風險低於值站，但仍需守。

### 2c. DataTable.Select → 跳脫或 LINQ（記憶體內篩選，**非 DB SQL，Dapper 管不到**）

| 站點 | 篩選 |
|---|---|
| `TsmcIeda.cs:105` | `_lotMappingDt.Select("tsmc_lot='" + dr["lot_id"] + "'")`（lot_id 來自 IEDA 檔；✅ A/PR-1 已做單引號跳脫） |
| `TsmcIeda.cs:175` | `…Select("…='" + aseLot + "'")`（aseLot 參數；✅ A/PR-1 已做單引號跳脫） |

> 處理：對值內的單引號跳脫（DataTable filter 語法用 `''` 跳脫），或改 LINQ-to-DataTable 型別化篩選。真實 injection 風險最低。

## 3. 威脅模型脈絡（影響優先序、不改「要不要修」）

資料源為內部 FTP / 測試機產生的 CSV 與 `db_key` 旗標表。此處參數化的**主要效益是正確性**（乾淨處理含 `'` 等特殊字元、擺脫脆弱的 `DBmysql.FilterSqlCommand` 正則 OK 繃）+ 縱深防禦，而非阻擋線上攻擊者。但 CONCERNS 標 HIGH、且列「安全區不可偷懶」，**參數化不可省**。

## 4. 測試紀律（HIGH：先紅後綠）

SQL 路徑無 DB 不可端到端跑（mac 無 MySQL、net462 測試僅在 CI windows-latest）。改以**建構接縫單元測試**（無需 DB）：

- 斷言改後 `Execute_query.Query` 含 `@p` 佔位符、且 `Parameters` 帶原值；
- 一條塞 `'; DROP TABLE db_key;--` 證明惡意值進 `Parameters`、**不**進 SQL 文字（injection 中和證明）；
- 識別碼站：一條白名單擋掉非法表名的測試。

外加既有 golden-master CI gate（net462↔net8 逐值 diff）把關行為等價。**Rollback**：revert PR；雙 TFM net462 不受影響。

## 5. 雙案 + 範圍分期（待用戶確認）

- **★ 最小改動案（用戶全域選擇、推薦）**：用既裝 Dapper 逐站改 `@param` + 致能改動；識別碼站白名單；DataTable.Select 另跳脫。不抽 repository、不動 fake-async 風格。
- **原始設計案**（不取）：抽 repository 層 + 型別化查詢 + Testcontainers 整合測試 — 屬現代化、非本債目標。

**範圍分期（用戶已選 A）**：
- **A（★推薦）2 PR 風險二分，已完成**：PR-1 低風險群（DbAccess 4 值站 + TsmcIeda 值站&DataTable.Select + 識別碼白名單）；PR-2 單獨攻高扇入 FileProcess 批次 INSERT 值參數化，單獨 review/rollback。平衡覆蓋與安全。
- **B 1 PR 一次到底**：三機制一個 PR。ceremony 最少、最快，但 FileProcess 批次混進大 diff，blast radius 最大。
- **C 逐站最小步（3+ PR）**：值參數化 → 識別碼白名單 → DataTable.Select 各自 PR。最保守、最慢。

## 6. 接手執行順序（用戶選定範圍後）

1. `deps-check` 跑 `FileProcess.cs` / `DbAccess.cs` / `DBmysql.cs` / `TsmcIeda.cs`（高扇入，動前必跑）。
2. 開分支 `fix/s2-sql-injection`（never 直接動 master）。
3. 先寫 §4 failing 接縫測試（紅）→ 致能改動 + 逐站改 → 轉綠。
4. 品質閘序列：code-simplifier → dotnet-code-reviewer → backend-release-verification + dependency-security-scan。
5. 全綠後才手動 invoke finishing-a-development-branch 做 squash PR。
6. **Merge gate**：CI 綠 + 等 Copilot async review（開 PR 後 2-3 分鐘）出來、未處理建議走 receiving-code-review 後才 merge。
7. 同步更新 `CONCERNS.md` S2 標記、本檔狀態。

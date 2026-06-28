# D3 DbAccess Dead Code Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove only the confirmed D3 `DbAccess` dead code path and its stale test coverage while preserving the `Program.cs` `////TEST CASE` comment block.

**Architecture:** This is deletion-only cleanup. The active importer flow keeps using `SelectDataCountInDays`, `SelectDbKey`, `UpdateDbKeyImportStatus`, `UpdateDbKeyUiStatusImportStatus`, and `SelectFailDbKeyFromFile`; those are not dead code. The only removable method path is `SelectFailDbKeyResult` and `BuildFailDbKeyResultQuery`, whose production reference is inside a commented-out UI Status notification block.

**Tech Stack:** .NET 8 `net8.0-windows`, C#, xUnit, existing Dapper query object helpers. No new abstraction, dependency, or tooling.

---

## Scope Guard

- Keep `DCT_data_import/Program.cs:44-93` (`////TEST CASE`) exactly as-is.
- Do not touch R4 log rotation, D4 `TsmcIeda` self-contained path cleanup, D2/D5/P2/P3, observability, or DB migration tooling.
- Do not change SQL behavior, notification behavior, import status behavior, or file handling.
- Do not add replacement APIs for deleted code.
- If implementation starts from a clean `master` where `git log -1 --oneline` is not the expected `b174baa fix(db): 補齊殘留 SQL 參數化 (#21)`, record the actual HEAD in the closeout ledger before editing.

## Files

- Modify: `DCT_data_import/DbApi/DbAccess.cs`
  - Delete `SelectFailDbKeyResult`.
  - Delete `BuildFailDbKeyResultQuery`.
  - Optionally delete only obsolete commented backup lines inside still-active methods.
- Modify: `DCT_data_import/Program.cs`
  - Keep `////TEST CASE` block untouched.
  - Delete only the commented-out UI Status notification block that references `SelectFailDbKeyResult`.
- Modify: `DCT_data_import.Tests/SqlParameterizationTests.cs`
  - Delete tests that only cover `BuildFailDbKeyResultQuery`.
- Do not create new source or test files.

### Task 1: Confirm The Dead Code Boundary

**Files:**
- Inspect: `DCT_data_import/Program.cs`
- Inspect: `DCT_data_import/DbApi/DbAccess.cs`
- Inspect: `DCT_data_import.Tests/SqlParameterizationTests.cs`

- [ ] **Step 1: Confirm clean branch state**

Run:

```bash
git status --short --branch
git log -1 --oneline
```

Expected:

```text
## master...origin/master
```

Record the actual `git log -1 --oneline` value. Do not fail solely because it is `5665721 feat(logging): 新增匯入成功紀錄檔`; this repo was clean at that HEAD during planning.

- [ ] **Step 2: Confirm `////TEST CASE` is preserved scope**

Run:

```bash
rg -n "////TEST CASE|SelectFailDbKeyResult|BuildFailDbKeyResultQuery" DCT_data_import DCT_data_import.Tests
```

Expected before edits:

```text
DCT_data_import/Program.cs:44:                ////TEST CASE
DCT_data_import/Program.cs:595:            //        List<DbKeyObject> failDbKeyObject = dbAccess.SelectFailDbKeyResult(DatabaseService , "ui_status");
DCT_data_import/DbApi/DbAccess.cs:372:        public List<DbKeyObject> SelectFailDbKeyResult(DatabaseService DatabaseService, string mode = "")
DCT_data_import/DbApi/DbAccess.cs:420:        internal static Execute_query BuildFailDbKeyResultQuery(string mode, long threshold)
DCT_data_import.Tests/SqlParameterizationTests.cs:64:        public void BuildFailDbKeyResultQuery_UsesSharedThresholdParameter(string mode, string expectedTable)
DCT_data_import.Tests/SqlParameterizationTests.cs:77:        public void BuildFailDbKeyResultQuery_RejectsUnsupportedMode()
```

### Task 2: Delete The Uncalled DbAccess Failure Query Path

**Files:**
- Modify: `DCT_data_import/DbApi/DbAccess.cs`

- [ ] **Step 1: Remove `SelectFailDbKeyResult`**

Delete this whole method from `DCT_data_import/DbApi/DbAccess.cs`:

```csharp
public List<DbKeyObject> SelectFailDbKeyResult(DatabaseService DatabaseService, string mode = "")
{
    List<DbKeyObject> dbKeyObject = new List<DbKeyObject>();
    WriteToLog writeToLog = new WriteToLog();
    // 檢查資料庫和相關資料表是否存在
    string tableName = mode == "tester" ? "db_key" : "db_key_ui_status";
    if (!DatabaseService.CheckDatabaseAndTableExists(tableName))
    {
        writeToLog.WriteErrorLog($"資料庫或資料表 {tableName} 不存在");
        return new List<DbKeyObject>();
    }
    string remark = string.Empty;
    long nowTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
    //long threeHourAgoTimeStamp = nowTimeStamp - 10800;  // 3小時=10800秒  3小時前
    long threeHourAgoTimeStamp = nowTimeStamp - 1200;  // 20分鐘前
    //long threeHourAgoTimeStamp = nowTimeStamp + 10800;  // 3小時=10800秒  3小時後
    try
    {
        Execute_query execute_query = BuildFailDbKeyResultQuery(mode, threeHourAgoTimeStamp);
        Execute_query_response response = DatabaseService.ExecuteSqlAsync(execute_query, "select").GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(response.Error))
        {
            writeToLog.WriteErrorLog($"SQL Query: {execute_query.Query}");
            writeToLog.WriteErrorLog($"Error: {response.Error}");
            writeToLog.WriteErrorLog("SELECT `db_key` error! ");
        }
        for (int i = 0; i < response.Data.Count; i++)
        {
            //Console.WriteLine(response.data[i]["db_key"].ToString());
            remark = (response.Data[i]["check_status"].ToString() == "0") ? "未更新check status" : response.Data[i]["remark"].ToString();
            // Safe parsing for id field
            if (!int.TryParse(response.Data[i]["id"]?.ToString(), out int id))
            {
                writeToLog.WriteInfoLog($"SelectFailDbKeyResult() invalid id value at row {i}: {response.Data[i]["id"]}, skipping row");
                continue;
            }
            dbKeyObject.Add(new DbKeyObject(id, response.Data[i]["db_key"].ToString(), remark));
        }
    }
    catch (Exception ex)
    {
        writeToLog.WriteErrorLog("SelectFailDbKeyResult() error:" + ex.Message);
        Console.WriteLine(ex.ToString());
        return dbKeyObject;
    }
    return dbKeyObject;
}
```

- [ ] **Step 2: Remove `BuildFailDbKeyResultQuery`**

Delete this whole helper from `DCT_data_import/DbApi/DbAccess.cs`:

```csharp
internal static Execute_query BuildFailDbKeyResultQuery(string mode, long threshold)
{
    string sql;
    if (mode == "tester")
    {
        sql = @"SELECT id, db_key, check_status, remark FROM `db_key` WHERE `mail`=0 AND `import_status`=0 AND datetime <= @threshold" +
                  @" union ALL
                        SELECT id, db_key, check_status, remark FROM `db_key` WHERE `mail`= 0 AND `import_status`>=2 AND datetime <= @threshold";
    }
    else if (mode == "ui_status")
    {
        sql = @"SELECT id, db_key, check_status, remark FROM `db_key_ui_status` WHERE `mail`=0 AND `import_status`=0 AND datetime <= @threshold" +
                  @" union ALL
                        SELECT id, db_key, check_status, remark FROM `db_key_ui_status` WHERE `mail`= 0 AND `import_status` >=2 AND datetime <= @threshold";
    }
    else
    {
        throw new ArgumentException("Unsupported db_key mode", nameof(mode));
    }

    return new Execute_query
    {
        Query = sql,
        Parameters = new { threshold }
    };
}
```

- [ ] **Step 3: Run a focused compile check**

Run:

```bash
dotnet build DCT_data_import.sln --configuration Release --no-restore /p:UseAppHost=false
```

Expected:

```text
Build succeeded.
0 Error(s)
```

If this fails because a real active caller still references either method, stop and revert only Task 2 edits.

### Task 3: Remove Stale Tests For The Deleted Helper

**Files:**
- Modify: `DCT_data_import.Tests/SqlParameterizationTests.cs`

- [ ] **Step 1: Delete tests that target the deleted helper**

Remove these two tests:

```csharp
[Theory]
[InlineData("tester", "`db_key`")]
[InlineData("ui_status", "`db_key_ui_status`")]
public void BuildFailDbKeyResultQuery_UsesSharedThresholdParameter(string mode, string expectedTable)
{
    const long threshold = 987654321;

    Execute_query query = DbAccess.BuildFailDbKeyResultQuery(mode, threshold);

    Assert.Contains(expectedTable, query.Query);
    Assert.Equal(2, query.Query.Split("@threshold").Length - 1);
    Assert.DoesNotContain(threshold.ToString(), query.Query);
    Assert.Equal(threshold, AnonymousParameterValue(query.Parameters, "threshold"));
}

[Fact]
public void BuildFailDbKeyResultQuery_RejectsUnsupportedMode()
{
    ArgumentException ex = Assert.Throws<ArgumentException>(() =>
        DbAccess.BuildFailDbKeyResultQuery("unknown", 987654321));

    Assert.Equal("mode", ex.ParamName);
}
```

- [ ] **Step 2: Run the affected test file**

Run:

```bash
dotnet test DCT_data_import.Tests/DCT_data_import.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SqlParameterizationTests" -m:1 /p:UseAppHost=false
```

Expected:

```text
Failed: 0
```

### Task 4: Remove The Commented UI Status Notification Reference

**Files:**
- Modify: `DCT_data_import/Program.cs`

- [ ] **Step 1: Keep the `////TEST CASE` block untouched**

Before editing `Program.cs`, verify the block still exists:

```bash
rg -n "////TEST CASE" DCT_data_import/Program.cs
```

Expected:

```text
DCT_data_import/Program.cs:44:                ////TEST CASE
```

- [ ] **Step 2: Delete only the commented UI Status notification block**

Remove the commented block under `ImportUiStatusMode` that starts with:

```csharp
#region 寄信通報
//if (DateTime.Now.TimeOfDay.Hours == 0 || DateTime.Now.TimeOfDay.Hours == 12)
```

and ends with:

```csharp
//}
#endregion
```

Do not edit the `////TEST CASE` block in `Main`.

- [ ] **Step 3: Verify deleted references and preserved TEST CASE**

Run:

```bash
rg -n "////TEST CASE|SelectFailDbKeyResult|BuildFailDbKeyResultQuery" DCT_data_import DCT_data_import.Tests
```

Expected after edits:

```text
DCT_data_import/Program.cs:44:                ////TEST CASE
```

### Task 5: Optional Comment-Only Cleanup Inside DbAccess Active Methods

**Files:**
- Modify: `DCT_data_import/DbApi/DbAccess.cs`

- [ ] **Step 1: Remove only obsolete commented-out backup lines**

Remove these exact stale comment lines if they still exist:

```csharp
//int importResult = 4 * tester + 2 * testResult + failPin;
//if (importResult < int.Parse(checkStatus))
//{
//    importStatus = "0";
//}
/*else */
//importStatus = (importResult.ToString() == checkStatus) ? "1" : "2";
//if (importResult < int.Parse(checkStatus))
//{
//    importStatus = "0";
//}
/*else*/
//// 寫入寄信暫存檔
//writeToLog.WriteToMailTemp(dbKey + "," + dbKey);
```

Keep explanatory comments that describe current behavior, including the `ComputeImportResult` remarks and `== 1 ? 1 : 0` rationale.

- [ ] **Step 2: Skip this task if the diff becomes noisy**

Run:

```bash
git diff --stat
```

Expected: only a small deletion-heavy diff. If this task makes the diff harder to review, revert only Task 5 and continue with Tasks 2-4.

### Task 6: Required Verification And Review Gate

**Files:**
- Verify all modified files.

- [ ] **Step 1: Self-simplification check**

Run:

```bash
git diff --stat
rg -n "class .*Factory|interface I.*|new .*Provider|PackageReference" DCT_data_import DCT_data_import.Tests
```

Expected summary:

```text
Only deletion cleanup in existing files.
No new abstraction, dependency, or tooling introduced by this branch.
```

- [ ] **Step 2: Diff self-review**

Run:

```bash
git diff --check
git diff -- DCT_data_import/DbApi/DbAccess.cs DCT_data_import/Program.cs DCT_data_import.Tests/SqlParameterizationTests.cs
```

Expected:

```text
git diff --check returns no output.
Diff preserves Program.cs ////TEST CASE and removes only confirmed dead D3 code.
```

- [ ] **Step 3: Full relevant build/test**

Run:

```bash
dotnet build DCT_data_import.sln --configuration Release --no-restore /p:UseAppHost=false
dotnet test DCT_data_import.Tests/DCT_data_import.Tests.csproj --configuration Release --no-build -m:1 /p:UseAppHost=false
```

Expected:

```text
Build succeeded.
Failed: 0
```

- [ ] **Step 4: dotnet-code-reviewer gate**

Run the available dotnet code-review gate against the final diff. Record:

```text
Reviewer type: dotnet-code-reviewer
Agent id: write the concrete id shown by the reviewer tool, or write "unavailable: tool did not expose an agent id"
Final finding summary: write "no blocking findings" or list the blocking finding titles
```

If the reviewer tool is unavailable, do not invent a pass. Record the unavailable reason and complete a manual review focused on accidental behavior changes.

### Task 7: PR Preflight And Postflight If Requested

**Files:**
- No additional file edits required.

- [ ] **Step 1: PR Preflight Ledger before opening PR**

Before creating a PR, display:

```text
PR Preflight Ledger
Self-simplification: PASS/FAIL with evidence
Diff self-review: PASS/FAIL with evidence
Relevant verification: exact commands and result summary
Review gate: reviewer type, agent id or unavailable reason, final finding summary
PR / CI / review status: branch, push status, CI not started yet
Residual risks: any behavior or environment risk
```

- [ ] **Step 2: PR Postflight Ledger after opening PR**

After creating a PR, display:

```text
PR Postflight Ledger
Self-simplification: PASS/FAIL with evidence
Diff self-review: PASS/FAIL with evidence
Relevant verification: exact commands and result summary
Review gate: reviewer type, agent id or unavailable reason, final finding summary
PR / CI / review status: PR URL, CI status, review status
Residual risks: any behavior or environment risk
```

## Self-Review

- Spec coverage: D3 dead code cleanup is covered; `////TEST CASE` is explicitly preserved; R4, D4, D2/D5/P2/P3, observability, and DB migration are out of scope.
- Placeholder scan: No banned placeholder tokens remain. Runtime evidence fields use fixed wording for available and unavailable cases.
- Type consistency: Deleted method names are consistently `SelectFailDbKeyResult` and `BuildFailDbKeyResultQuery`; active method names are preserved.

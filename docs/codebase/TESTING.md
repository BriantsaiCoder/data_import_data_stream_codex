# Testing

> 現況：本專案有一個 SDK-style `net8.0-windows` xUnit 測試專案。原本用於 net462→net8 的 capture-only 遷移測試，在 A4 後已改為 net8 行為硬斷言，正常 CI 不再排除 `CaptureBaseline`。

## Core Sections (Required)

### 1) Test Frameworks

- Test project: `DCT_data_import.Tests` (`net8.0-windows`, xUnit).
- Test SDK: `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio`.
- Current suite covers:
  - R5 `ComputeImportResult` bitmask regression.
  - Import decision / thread supervisor / dry-run / App.config contract.
  - Big5 provider smoke.
  - net8-specialized characterization for special floating-point parse, double formatting, DateTime parsing, and statistic value conversion.
  - SPC `AverageOfSumSquare` pass/fail filtering, empty-value fallback, and negative-variance guard.
  - Parser characterization, `FileContentFormat.Compare*()`, and `FileProcess` helper behavior.

### 2) Test Organization

- Test files live under `DCT_data_import.Tests/`.
- Tests are mostly focused regression or characterization tests around high-risk migration and ETL contracts.
- Production code still has no broad importer/FTP/MySQL integration test harness; live DB/FTP validation remains manual or environment-dependent.

### 3) Mocking and Fixtures

- No mocking framework is installed.
- Most tests use pure functions, small in-memory `DataTable` fixtures, or config-file inspection.
- Testability obstacles remain:
  - Direct `new DBmysql()` and static `Program` config fields.
  - FTP/file-system side effects in importer base flow.
  - Process-wide MySQL connection manager state.

### 4) Coverage and CI

- Coverage tool / threshold: none.
- CI: `.github/workflows/ci.yml` on `windows-latest` runs:
  - `dotnet restore ... -p:NuGetAudit=true -p:NuGetAuditMode=all`
  - `dotnet build ... --configuration Release --no-restore`
  - `dotnet test ... --configuration Release --no-build`
- Latest macOS verification after R1 parser work and CS0012 cleanup: `dotnet build DCT_data_import.Tests/DCT_data_import.Tests.csproj --configuration Release --no-restore /p:UseAppHost=false` passed with 0 warnings / 0 errors, and `dotnet test ... /p:UseAppHost=false` passed `223` tests. Runtime smoke still belongs on Windows.

### 5) Evidence

- `DCT_data_import.Tests/DCT_data_import.Tests.csproj`
- `DCT_data_import.Tests/README.md`
- `.github/workflows/ci.yml`
- `docs/codebase/CONCERNS.md` R1 / R5

## Extended Sections (Optional)

### Recommended Next Test Work

1. MySQL/FTP integration smoke on a controlled Windows environment.
2. MySql.Data DATETIME round-trip golden master with a real MySQL instance.
3. Coverage tooling only after the high-value seams above are stable.

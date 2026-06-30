# Repository Guidelines

## Project Structure & Module Organization

This repository contains a .NET 8 (`net8.0-windows`) console ETL app for DCT data import.

- `DCT_data_import/`: main application source.
- `DCT_data_import/Program.cs`: process entry point and thread orchestration.
- `DCT_data_import/ReadAndImport/`: importer flows for supported data formats.
- `DCT_data_import/FileAccess/`: CSV format contracts, file handling, and DB insert bridge.
- `DCT_data_import/DbApi/` and `DCT_data_import/MySqlApi/`: database access and Dapper/MySQL execution.
- `DCT_data_import/Common/`: logging, notification, SPC, and shared models.
- `DCT_data_import.Tests/`: xUnit regression and characterization tests.
- `docs/codebase/`: architecture, stack, testing, integration, convention, and risk documentation.

## Build, Test, and Development Commands

Use Windows for production-like runtime verification. The solution can build and run tests with the .NET 8 SDK.

```powershell
dotnet restore DCT_data_import.sln
dotnet build DCT_data_import.sln --configuration Release --no-restore
dotnet test DCT_data_import.Tests\DCT_data_import.Tests.csproj --configuration Release --no-build
dotnet publish DCT_data_import\DCT_data_import.csproj --configuration Release --runtime win-x64 --self-contained true
DCT_data_import\bin\Release\net8.0-windows\DCT_data_import.exe
```

CI runs on `windows-latest` and uses the same restore/build/test flow. The app still depends on Windows runtime behavior (`kernel32.dll` INI P/Invoke, `C:\temp` logs, FTP/MySQL environment access), so macOS/Linux checks are build/test evidence only.

## Coding Style & Naming Conventions

Follow existing C# style: 4-space indentation, Allman braces, `using` directives at the top, PascalCase for types/methods/files, and camelCase for locals. Namespaces are aligned to their folders (commit `23e9c73`, enforced by `NamespaceConventionTests`); keep new files aligned and do not reintroduce drift. Keep comments short and useful, preferably matching the surrounding Traditional Chinese documentation style.

## Implementation Discipline

Prefer the smallest correct change that follows existing project patterns. Before adding new code, check whether the repo already has a helper, pattern, or installed dependency that covers the need. Avoid speculative abstractions, new dependencies, boilerplate, and unrelated refactors.

For bug fixes, trace the shared flow and callers before editing. Fix the root cause at the common point when feasible, and add the smallest relevant regression test or verification that would fail if the issue returns.

## Testing Guidelines

Tests use xUnit in `DCT_data_import.Tests/` and target `net8.0-windows`. Name tests by behavior, for example `ComputeImportResult_MatchesBitmask_WhenComponentsAreZeroOrOne`. The former migration capture tests now assert net8 behavior directly; normal CI runs the full suite without `ByDesignRed` or `CaptureBaseline` filters.

## Commit & Pull Request Guidelines

Git history uses Conventional Commits, often in zh-TW, such as `fix(importer): ...`, `test(db): ...`, and `docs: ...`. Keep commits scoped and avoid bundling unrelated formatting. Pull requests should state purpose, affected modules, verification commands, and any linked issue or risk from `docs/codebase/CONCERNS.md`.

## Security & Configuration Tips

`DCT_data_import/App.config` contains sensitive DB/FTP settings. Do not add new secrets, screenshots, or logs with credentials. Prefer externalized configuration for new secret values, and mask credentials in console or log output.

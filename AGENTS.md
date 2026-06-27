# Repository Guidelines

## Project Structure & Module Organization

This repository contains a .NET Framework 4.6.2 console ETL app for DCT data import.

- `DCT_data_import/`: main application source.
- `DCT_data_import/Program.cs`: process entry point and thread orchestration.
- `DCT_data_import/ReadAndImport/`: importer flows for supported data formats.
- `DCT_data_import/FileAccess/`: CSV format contracts, file handling, and DB insert bridge.
- `DCT_data_import/DbApi/` and `DCT_data_import/MySQL_api/`: database access and Dapper/MySQL execution.
- `DCT_data_import/Common/`: logging, notification, SPC, and shared models.
- `DCT_data_import.Tests/`: xUnit regression tests and test notes.
- `docs/codebase/`: architecture, stack, testing, integration, convention, and risk documentation.

## Build, Test, and Development Commands

Use Windows or a Visual Studio Developer Command Prompt for the full workflow.

```powershell
nuget restore DCT_data_import.sln
msbuild DCT_data_import.sln /p:Configuration=Release /m
DCT_data_import\bin\Release\DCT_data_import.exe
dotnet test DCT_data_import.Tests\DCT_data_import.Tests.csproj --configuration Release --no-build --filter "Category!=ByDesignRed"
```

`nuget restore` is required because the main project uses `packages.config`. The test project is SDK-style `net462`, but CI still builds on `windows-latest`.

## Coding Style & Naming Conventions

Follow existing C# style: 4-space indentation, Allman braces, `using` directives at the top, PascalCase for types/methods/files, and camelCase for locals. Do not reorganize namespaces just to match folders; this repo intentionally contains legacy namespace drift. Keep comments short and useful, preferably matching the surrounding Traditional Chinese documentation style.

## Implementation Discipline

Prefer the smallest correct change that follows existing project patterns. Before adding new code, check whether the repo already has a helper, pattern, or installed dependency that covers the need. Avoid speculative abstractions, new dependencies, boilerplate, and unrelated refactors.

For bug fixes, trace the shared flow and callers before editing. Fix the root cause at the common point when feasible, and add the smallest relevant regression test or verification that would fail if the issue returns.

## Testing Guidelines

Tests use xUnit in `DCT_data_import.Tests/`. Name tests by behavior, for example `ComputeImportResult_MatchesBitmask_WhenComponentsAreZeroOrOne`. Two R5 pinning tests are intentionally marked `[Trait("Category", "ByDesignRed")]`; exclude them for normal green CI runs and read `DCT_data_import.Tests/README.md` before changing that contract.

## Commit & Pull Request Guidelines

Git history uses Conventional Commits, often in zh-TW, such as `fix(importer): ...`, `test(db): ...`, and `docs: ...`. Keep commits scoped and avoid bundling unrelated formatting. Pull requests should state purpose, affected modules, verification commands, and any linked issue or risk from `docs/codebase/CONCERNS.md`.

## Security & Configuration Tips

`DCT_data_import/App.config` contains sensitive DB/FTP settings. Do not add new secrets, screenshots, or logs with credentials. Prefer externalized configuration for new secret values, and mask credentials in console or log output.

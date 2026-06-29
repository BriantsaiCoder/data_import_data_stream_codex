# Technology Stack

## Core Sections (Required)

### 1) Runtime Summary

| Area | Value | Evidence |
|------|-------|----------|
| Primary language | C# | `DCT_data_import/**/*.cs` |
| Runtime + version | .NET 8 `net8.0-windows` console `Exe` | `DCT_data_import/DCT_data_import.csproj` (`<TargetFramework>net8.0-windows</TargetFramework>`) |
| Package manager | NuGet PackageReference via SDK-style projects | `DCT_data_import/DCT_data_import.csproj`, `DCT_data_import.Tests/DCT_data_import.Tests.csproj` |
| Module/build system | .NET SDK / MSBuild SDK-style solution | `DCT_data_import.sln`, `.github/workflows/ci.yml` |

### 2) Production Frameworks and Dependencies

| Dependency | Version | Role in system | Evidence |
|------------|---------|----------------|----------|
| Dapper | 2.1.66 | Lightweight ORM; `DBmysql` uses `Query` / `Execute` with optional parameters | `DCT_data_import.csproj`, `MySqlApi/DBmysql.cs` |
| MySql.Data | 9.4.0 | MySQL ADO.NET driver | `DCT_data_import.csproj`, `MySqlApi/DBmysql.cs` |
| Newtonsoft.Json | 13.0.3 | Legacy `JArray`/`JObject` response shape | `DCT_data_import.csproj`, `DbApi/DbObject.cs` |
| System.Configuration.ConfigurationManager | 8.0.1 | Reads `App.config` appSettings / connectionStrings | `DCT_data_import.csproj`, `Program.cs` |
| System.Text.Encoding.CodePages | 8.0.0 | Enables Big5 / codepage 950 decoding on .NET 8 | `DCT_data_import.csproj`, `Program.cs`, `EncodingTestBootstrap.cs` |
| System.Private.Uri | 4.3.2 | Transitive CVE pin for NuGetAudit on SDK 8 restore graph | `DCT_data_import.csproj` |

### 3) Development Toolchain

| Tool | Purpose | Evidence |
|------|---------|----------|
| .NET SDK 8.0.x | Restore, build, test, publish | `.github/workflows/ci.yml` |
| GitHub Actions CI | `windows-latest` restore/build/test + NuGetAudit | `.github/workflows/ci.yml` |
| No linter / formatter config | No `.editorconfig`, StyleCop, or analyzer policy found | repo root |

### 4) Key Commands

```powershell
dotnet restore DCT_data_import.sln
dotnet build DCT_data_import.sln --configuration Release --no-restore
dotnet test DCT_data_import.Tests\DCT_data_import.Tests.csproj --configuration Release --no-build
dotnet publish DCT_data_import\DCT_data_import.csproj --configuration Release --runtime win-x64 --self-contained true
```

Runtime smoke should be done on Windows because the service uses Windows-specific INI P/Invoke and writes logs under `C:\temp`, and real operation needs FTP/MySQL access.

### 5) Environment and Config

- Config sources: `DCT_data_import/App.config` (`appSettings` + `connectionStrings`).
- Environment selection: runtime IPv4 detection maps known production IPs to `Prod`; all other hosts use `Dev` (`Program.GetEnvironment()`).
- Data source selection: `ImportSource=Ftp` by default; `ImportSource=Local` reads from `LocalImportRoot` with `LocalSuccessAction`.
- Required OS env vars: none.
- Runtime limits: Windows-oriented service runtime (`kernel32.dll` INI P/Invoke, hardcoded `C:\temp` log root, FTP/MySQL).

### 6) Evidence

- `DCT_data_import/DCT_data_import.csproj`
- `DCT_data_import.Tests/DCT_data_import.Tests.csproj`
- `DCT_data_import/App.config`
- `.github/workflows/ci.yml`
- `DCT_data_import/Program.cs`

# Technology Stack

## Core Sections (Required)

### 1) Runtime Summary

| Area | Value | Evidence |
|------|-------|----------|
| Primary language | C# | 全部原始碼為 `.cs`（`DCT_data_import/**/*.cs`） |
| Runtime + version | .NET Framework 4.6.2（Console / `Exe`） | `DCT_data_import/DCT_data_import.csproj:8`（`<OutputType>Exe</OutputType>`）、`:11`（`<TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>`） |
| Package manager | NuGet（舊式 `packages.config`，非 SDK-style PackageReference） | `DCT_data_import/packages.config`、`DCT_data_import/DCT_data_import.csproj`（含逐一 `<Reference HintPath=...>`） |
| Module/build system | MSBuild / Visual Studio 2017（Format Version 12.00, VS 15） | `DCT_data_import.sln` |

### 2) Production Frameworks and Dependencies

| Dependency | Version | Role in system | Evidence |
|------------|---------|----------------|----------|
| Dapper | 2.1.66 | 輕量 ORM；`DBmysql` 以 `connection.Query` / `connection.Execute` 執行 SQL | `packages.config:3`、`MySQL_api/DBmysql.cs:4,128,193` |
| MySql.Data | 9.4.0 | MySQL ADO.NET 驅動；`MySqlConnection` 直連資料庫 | `packages.config:4`、`MySQL_api/DBmysql.cs:5,71` |
| Newtonsoft.Json | 13.0.3 | 查詢結果序列化為 `JArray`/`JObject`（`Execute_query_response.Data`） | `packages.config:5`、`DbApi/DbObject.cs:1,64`、`MySQL_api/DBmysql.cs:6,129` |
| System.Configuration.ConfigurationManager | 9.0.7 | 讀取 `App.config` 的 `AppSettings` / `ConnectionStrings` | `packages.config:6`、`Program.cs:3,19-26` |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | 相依傳遞（assembly binding redirect） | `packages.config:7`、`App.config`（`assemblyBinding`） |
| System.Threading.Tasks.Extensions | 4.6.3 | 相依傳遞（`Task`/`ValueTask` 支援） | `packages.config:8` |

> 注意：`.csproj` 內的 `<Reference>` assembly 版本（如 Dapper `2.0.0.0`、ConfigurationManager `9.0.0.7`、Unsafe `6.0.3.0`）為 assembly 強名稱版本，與 `packages.config` 的 NuGet 套件版本不同步顯示，屬正常現象。以 `packages.config` 為套件版本權威來源。

### 3) Development Toolchain

| Tool | Purpose | Evidence |
|------|---------|----------|
| MSBuild / Visual Studio 2017 | 建置（Windows 標準路徑；mac 亦可編譯，見 §4 註） | `DCT_data_import.sln`、`.csproj` 非 SDK-style |
| 無 linter / formatter 設定 | [TODO] 未發現 `.editorconfig`、`StyleCop`、`.globalconfig` | `docs/codebase/.codebase-scan.txt`（no lint/format config detected） |
| GitHub Actions CI | `.github/workflows/ci.yml`：windows-latest build + test，push/PR to master 觸發 | `.github/workflows/ci.yml` |

### 4) Key Commands

```bash
# 下列為 Windows / Visual Studio 標準全流程（含實跑 exe）。mac 可改用 dotnet build + FrameworkPathOverride→Mono 4.6.2-api 僅做編譯驗證（見下方註）。
# 還原 NuGet 套件（packages.config 模式）
nuget restore DCT_data_import.sln

# 建置（Visual Studio Developer Command Prompt）
msbuild DCT_data_import.sln /p:Configuration=Release

# 執行（產物為 console exe）
DCT_data_import\bin\Release\DCT_data_import.exe

# 測試：dotnet test DCT_data_import.Tests（SDK-style net462 xUnit；R5 回歸樁）
# Lint：  [TODO] 無 linter 設定
```

> ⚠ 本知識萃取在 macOS 上進行。後續已實測:主專案（net462）在 macOS 以 `dotnet build` + `FrameworkPathOverride`→Mono `4.6.2-api` 參考組件**零錯誤編譯**（`./packages` 經 NuGet 還原）；genuinely Windows-only 的是**執行期**（P/Invoke / `C:\temp` / FTP / MySQL）。測試專案在該 override 下尚有 `System.Runtime` facade 小坑（CS0012）未解；R5 紅綠燈另以 net8.0 + xUnit 重現（6 綠 / 2 紅）。

### 5) Environment and Config

- Config sources：`DCT_data_import/App.config`（`appSettings` + `connectionStrings` + `assemblyBinding`）
- 環境選擇：執行期自動偵測本機 IPv4，命中正式 IP 則為 `Prod`，否則 `Dev`（`Program.cs:18` → `GetEnvironment()`）。`HOST/USER/PASSWORD/PORT/DATABASE` 以 `$"{Environment}Host"` 等鍵動態組合讀取（`Program.cs:19-23`）。
- Required env vars：無（不使用 OS 環境變數；全部設定來自 `App.config`）。
- 部署/執行限制：Windows-only（依賴 `kernel32.dll` P/Invoke 讀寫 INI，見 `FileAccess/ReadWriteINIfile.cs:10-13`；log 路徑 hardcoded `C:\temp\...`，見 `Common/WriteToLog.cs:29`）。

### 6) Evidence

- `DCT_data_import/DCT_data_import.csproj`
- `DCT_data_import/packages.config`
- `DCT_data_import/App.config`
- `DCT_data_import/Program.cs:18-26`
- `docs/codebase/.codebase-scan.txt`

## Extended Sections (Optional)

- 完整 assembly binding redirect 清單見 `App.config` 的 `<runtime><assemblyBinding>` 區段（含 `System.Runtime.CompilerServices.Unsafe`、`System.Buffers`、`System.Memory` 等傳遞相依重導向）。

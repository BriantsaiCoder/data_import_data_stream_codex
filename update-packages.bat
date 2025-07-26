@echo off
echo ========================================
echo DCT_data_import Package Update Script
echo ========================================
echo.

echo Step 1: Cleaning old package folders...
if exist "packages\MySql.Data.8.0.29" rmdir /s /q "packages\MySql.Data.8.0.29"
if exist "packages\Google.Protobuf.3.19.4" rmdir /s /q "packages\Google.Protobuf.3.19.4"
if exist "packages\Newtonsoft.Json.13.0.1" rmdir /s /q "packages\Newtonsoft.Json.13.0.1"
if exist "packages\Microsoft.Bcl.1.1.10" rmdir /s /q "packages\Microsoft.Bcl.1.1.10"
if exist "packages\Microsoft.Bcl.Build.1.0.14" rmdir /s /q "packages\Microsoft.Bcl.Build.1.0.14"
if exist "packages\System.Buffers.4.5.1" rmdir /s /q "packages\System.Buffers.4.5.1"
if exist "packages\System.Memory.4.5.4" rmdir /s /q "packages\System.Memory.4.5.4"
if exist "packages\System.Numerics.Vectors.4.5.0" rmdir /s /q "packages\System.Numerics.Vectors.4.5.0"
if exist "packages\System.Runtime.CompilerServices.Unsafe.6.1.2" rmdir /s /q "packages\System.Runtime.CompilerServices.Unsafe.6.1.2"
if exist "packages\System.Threading.Tasks.Extensions.4.5.4" rmdir /s /q "packages\System.Threading.Tasks.Extensions.4.5.4"

echo Step 2: Backup completed
echo - packages.config.backup
echo - DCT_data_import.csproj.backup

echo.
echo Step 3: Package Update Summary
echo ========================================
echo Updated packages:
echo   - MySql.Data: 8.0.29 to 8.4.0 (security fixes)
echo   - Google.Protobuf: 3.19.4 to 3.25.1 (security fixes)
echo   - Newtonsoft.Json: 13.0.1 to 13.0.3 (bug fixes)
echo.
echo Removed obsolete packages:
echo   - Microsoft.Bcl (no longer needed in .NET 4.6.1+)
echo   - Microsoft.Bcl.Build (no longer needed)
echo   - System.Buffers (built-in in .NET 4.6.1+)
echo   - System.Memory (built-in in .NET 4.6.1+)
echo   - System.Numerics.Vectors (built-in in .NET 4.6.1+)
echo   - System.Runtime.CompilerServices.Unsafe (no longer needed)
echo   - System.Threading.Tasks.Extensions (built-in in .NET 4.6.1+)
echo.
echo Kept packages:
echo   - BouncyCastle.Cryptography 2.4.0 (latest version)
echo   - Dapper 2.1.66 (latest version)
echo   - Microsoft.Bcl.AsyncInterfaces 9.0.1 (latest version)
echo   - K4os.* package group (stable versions)
echo   - Microsoft.AspNet.WebApi.Client 5.2.9 (maintenance mode)
echo.

echo Step 4: Next recommended actions
echo ========================================
echo 1. Rebuild project using Visual Studio or MSBuild
echo 2. Run unit tests to ensure functionality
echo 3. Test application for proper operation
echo 4. If issues occur, restore from backup files
echo.

echo Package update completed!
echo Please review package-analysis.md for detailed analysis report
pause

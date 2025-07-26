@echo off
echo ========================================
echo DCT_data_import 編譯驗證腳本
echo ========================================
echo.

echo 檢查 MSBuild 是否可用...
where msbuild >nul 2>&1
if %errorlevel% neq 0 (
    echo 錯誤: 找不到 MSBuild，請確保已安裝 Visual Studio 或 Build Tools
    echo 您可以使用以下命令來定位 MSBuild:
    echo "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    pause
    exit /b 1
)

echo.
echo 正在清理專案...
msbuild DCT_data_import.sln /t:Clean /p:Configuration=Debug

echo.
echo 正在還原套件...
nuget restore DCT_data_import.sln 2>nul
if %errorlevel% neq 0 (
    echo 注意: NuGet CLI 不可用，將嘗試使用 MSBuild 還原
    msbuild DCT_data_import.sln /t:Restore /p:Configuration=Debug
)

echo.
echo 正在編譯專案...
msbuild DCT_data_import.sln /p:Configuration=Debug /p:Platform="Any CPU"

if %errorlevel% equ 0 (
    echo.
    echo ✅ 編譯成功！
    echo 所有套件更新都相容於現有程式碼
) else (
    echo.
    echo ❌ 編譯失敗！
    echo 請檢查編譯錯誤訊息，可能需要調整程式碼或還原套件版本
    echo.
    echo 如需還原，請執行:
    echo copy DCT_data_import\packages.config.backup DCT_data_import\packages.config
    echo copy DCT_data_import\DCT_data_import.csproj.backup DCT_data_import\DCT_data_import.csproj
)

echo.
pause

# 檢查 NuGet 套件版本的 PowerShell 腳本

$packages = @(
    "BouncyCastle.Cryptography",
    "Dapper",
    "MySql.Data",
    "Google.Protobuf",
    "Newtonsoft.Json",
    "Microsoft.AspNet.WebApi.Client",
    "Microsoft.Bcl.AsyncInterfaces"
)

Write-Host "正在檢查套件版本..." -ForegroundColor Yellow

foreach ($package in $packages) {
    try {
        Write-Host "`n檢查套件: $package" -ForegroundColor Cyan

        # 使用 Invoke-RestMethod 查詢 NuGet API
        $url = "https://api.nuget.org/v3-flatcontainer/$package/index.json"
        $response = Invoke-RestMethod -Uri $url -ErrorAction Stop

        if ($response.versions) {
            $latest = $response.versions[-1]  # 最後一個版本通常是最新的
            Write-Host "  最新版本: $latest" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "  無法取得版本資訊: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n檢查完成!" -ForegroundColor Yellow

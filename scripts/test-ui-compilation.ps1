# UI编译测试脚本
Write-Host "开始测试UI项目编译..." -ForegroundColor Green

# 切换到UI项目目录
Set-Location "src/GravityDamAnalysis.UI"

# 清理之前的编译结果
Write-Host "清理之前的编译结果..." -ForegroundColor Yellow
dotnet clean

# 恢复NuGet包
Write-Host "恢复NuGet包..." -ForegroundColor Yellow
dotnet restore

# 编译项目
Write-Host "编译UI项目..." -ForegroundColor Yellow
$result = dotnet build

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ UI项目编译成功！" -ForegroundColor Green
    Write-Host "所有XAML文件已成功编译" -ForegroundColor Green
} else {
    Write-Host "❌ UI项目编译失败！" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
}

# 返回根目录
Set-Location "../.."

Write-Host "测试完成！" -ForegroundColor Green 
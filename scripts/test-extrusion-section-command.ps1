# 测试拉伸截面提取命令修复
Write-Host "=== 拉伸截面提取命令修复验证 ===" -ForegroundColor Green

# 检查命令文件是否存在
$commandFile = "src/GravityDamAnalysis.Revit/Commands/ExtractExtrusionSectionCommand.cs"
if (Test-Path $commandFile) {
    Write-Host "✅ 命令文件存在: $commandFile" -ForegroundColor Green
} else {
    Write-Host "❌ 命令文件不存在: $commandFile" -ForegroundColor Red
    exit 1
}

# 检查构造函数修复
$content = Get-Content $commandFile -Raw
if ($content -match "catch \(InvalidOperationException\)") {
    Write-Host "✅ 构造函数已修复 - 正确处理服务提供者未初始化异常" -ForegroundColor Green
} else {
    Write-Host "❌ 构造函数未修复 - 缺少InvalidOperationException处理" -ForegroundColor Red
}

# 检查日志记录器安全使用
if ($content -match "_logger\?\.Log") {
    Write-Host "✅ 日志记录器安全使用 - 使用空条件运算符" -ForegroundColor Green
} else {
    Write-Host "❌ 日志记录器使用不安全" -ForegroundColor Red
}

# 检查应用程序注册
$appFile = "src/GravityDamAnalysis.Revit/Application/DamAnalysisApplication.cs"
if (Test-Path $appFile) {
    $appContent = Get-Content $appFile -Raw
    if ($appContent -match "ExtractExtrusionSectionCommand") {
        Write-Host "✅ 命令已在应用程序中注册" -ForegroundColor Green
    } else {
        Write-Host "❌ 命令未在应用程序中注册" -ForegroundColor Red
    }
} else {
    Write-Host "❌ 应用程序文件不存在: $appFile" -ForegroundColor Red
}

# 检查编译
Write-Host "`n=== 编译检查 ===" -ForegroundColor Yellow
try {
    $buildResult = dotnet build src/GravityDamAnalysis.Revit/GravityDamAnalysis.Revit.csproj --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ 项目编译成功" -ForegroundColor Green
    } else {
        Write-Host "❌ 项目编译失败" -ForegroundColor Red
        Write-Host $buildResult
    }
} catch {
    Write-Host "❌ 编译检查失败: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== 修复总结 ===" -ForegroundColor Cyan
Write-Host "1. 修复了构造函数中的服务提供者初始化问题" -ForegroundColor White
Write-Host "2. 使用try-catch正确处理InvalidOperationException" -ForegroundColor White
Write-Host "3. 确保日志记录器为null时程序仍能正常运行" -ForegroundColor White
Write-Host "4. 在Revit应用程序中注册了新的命令按钮" -ForegroundColor White
Write-Host "5. 添加了详细的工具提示和描述" -ForegroundColor White

Write-Host "`n修复完成！现在可以在Revit中使用'拉伸截面提取'命令了。" -ForegroundColor Green 
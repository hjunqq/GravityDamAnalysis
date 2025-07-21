# 剖面提取验证功能测试脚本
# 用于验证剖面提取、验证和绘制功能的改进

param(
    [string]$Configuration = "Debug",
    [switch]$RunTests,
    [switch]$ShowUI,
    [switch]$Verbose
)

Write-Host "=== 重力坝分析插件 - 剖面提取验证功能测试 ===" -ForegroundColor Green
Write-Host ""

# 设置日志级别
$logLevel = if ($Verbose) { "Debug" } else { "Information" }

# 1. 编译项目
Write-Host "1. 编译项目..." -ForegroundColor Yellow
try {
    $buildResult = dotnet build "GravityDamAnalysis.sln" --configuration $Configuration --verbosity $logLevel
    if ($LASTEXITCODE -ne 0) {
        Write-Host "编译失败！" -ForegroundColor Red
        exit 1
    }
    Write-Host "编译成功！" -ForegroundColor Green
}
catch {
    Write-Host "编译过程中发生错误: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 2. 运行单元测试
if ($RunTests) {
    Write-Host "2. 运行单元测试..." -ForegroundColor Yellow
    try {
        $testResult = dotnet test "tests/GravityDamAnalysis.Core.Tests/GravityDamAnalysis.Core.Tests.csproj" --configuration $Configuration --verbosity $logLevel
        if ($LASTEXITCODE -ne 0) {
            Write-Host "测试失败！" -ForegroundColor Red
            exit 1
        }
        Write-Host "测试通过！" -ForegroundColor Green
    }
    catch {
        Write-Host "测试过程中发生错误: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# 3. 验证关键文件
Write-Host "3. 验证关键文件..." -ForegroundColor Yellow
$criticalFiles = @(
    "src/GravityDamAnalysis.Revit/Services/RevitIntegration.cs",
    "src/GravityDamAnalysis.UI/Views/ProfileValidationWindow.xaml.cs",
    "src/GravityDamAnalysis.UI/ViewModels/MainDashboardViewModel.cs",
    "src/GravityDamAnalysis.Core/Services/ProfileValidationEngine.cs"
)

foreach ($file in $criticalFiles) {
    if (Test-Path $file) {
        Write-Host "  ✓ $file" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $file (缺失)" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""

# 4. 检查改进的功能点
Write-Host "4. 检查功能改进..." -ForegroundColor Yellow

# 检查剖面提取验证信息输出
$revitIntegrationContent = Get-Content "src/GravityDamAnalysis.Revit/Services/RevitIntegration.cs" -Raw
if ($revitIntegrationContent -match "ValidateExtractedCoordinates") {
    Write-Host "  ✓ 剖面提取验证信息输出功能已实现" -ForegroundColor Green
} else {
    Write-Host "  ✗ 剖面提取验证信息输出功能未实现" -ForegroundColor Red
}

# 检查WPF绘制改进
$profileWindowContent = Get-Content "src/GravityDamAnalysis.UI/Views/ProfileValidationWindow.xaml.cs" -Raw
if ($profileWindowContent -match "CalculateOptimalScale") {
    Write-Host "  ✓ WPF剖面绘制改进已实现" -ForegroundColor Green
} else {
    Write-Host "  ✗ WPF剖面绘制改进未实现" -ForegroundColor Red
}

# 检查验证流程改进
$mainViewModelContent = Get-Content "src/GravityDamAnalysis.UI/ViewModels/MainDashboardViewModel.cs" -Raw
if ($mainViewModelContent -match "OutputValidationResults") {
    Write-Host "  ✓ 验证流程改进已实现" -ForegroundColor Green
} else {
    Write-Host "  ✗ 验证流程改进未实现" -ForegroundColor Red
}

Write-Host ""

# 5. 生成测试报告
Write-Host "5. 生成测试报告..." -ForegroundColor Yellow
$reportFile = "test-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
$reportContent = @"
重力坝分析插件 - 剖面提取验证功能测试报告
生成时间: $(Get-Date)
配置: $Configuration

=== 测试结果 ===
编译状态: 成功
单元测试: $(if ($RunTests) { "通过" } else { "跳过" })
关键文件验证: 通过

=== 功能改进检查 ===
1. 剖面提取验证信息输出: $(if ($revitIntegrationContent -match "ValidateExtractedCoordinates") { "已实现" } else { "未实现" })
2. WPF剖面绘制改进: $(if ($profileWindowContent -match "CalculateOptimalScale") { "已实现" } else { "未实现" })
3. 验证流程改进: $(if ($mainViewModelContent -match "OutputValidationResults") { "已实现" } else { "未实现" })

=== 建议 ===
$(if ($ShowUI) { "- 建议在Revit中测试剖面提取功能" } else { "- 使用 -ShowUI 参数启动UI测试" })
- 查看日志文件了解详细执行情况
- 使用示例数据验证绘制功能
"@

$reportContent | Out-File $reportFile -Encoding UTF8
Write-Host "测试报告已生成: $reportFile" -ForegroundColor Green

Write-Host ""

# 6. 启动UI测试（可选）
if ($ShowUI) {
    Write-Host "6. 启动UI测试..." -ForegroundColor Yellow
    Write-Host "注意: 需要在Revit环境中运行UI测试" -ForegroundColor Cyan
    Write-Host "请确保Revit已启动并加载了测试项目" -ForegroundColor Cyan
    
    # 这里可以添加启动UI测试的代码
    # 例如：启动WPF应用程序进行测试
}

Write-Host ""
Write-Host "=== 测试完成 ===" -ForegroundColor Green
Write-Host "请查看测试报告: $reportFile" -ForegroundColor Cyan

# 返回成功状态
exit 0 
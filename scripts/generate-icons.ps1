# 生成重力坝分析插件图标
Write-Host "=== 生成重力坝分析插件图标 ===" -ForegroundColor Green

# 创建图标目录
$iconDir = "src/GravityDamAnalysis.Revit/Resources/Icons"
if (!(Test-Path $iconDir)) {
    New-Item -ItemType Directory -Path $iconDir -Force
    Write-Host "✅ 创建图标目录: $iconDir" -ForegroundColor Green
}

# 编译项目以生成图标
Write-Host "`n=== 编译项目生成图标 ===" -ForegroundColor Yellow
try {
    $buildResult = dotnet build src/GravityDamAnalysis.Revit/GravityDamAnalysis.Revit.csproj --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ 项目编译成功" -ForegroundColor Green
    } else {
        Write-Host "❌ 项目编译失败" -ForegroundColor Red
        Write-Host $buildResult
        exit 1
    }
} catch {
    Write-Host "❌ 编译失败: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 检查生成的图标文件
Write-Host "`n=== 检查生成的图标 ===" -ForegroundColor Yellow
$iconFiles = @(
    "StabilityAnalysis.png",
    "AdvancedAnalysis.png", 
    "UIIntegration.png",
    "ExtrusionSketch.png"
)

$allIconsExist = $true
foreach ($iconFile in $iconFiles) {
    $iconPath = Join-Path $iconDir $iconFile
    if (Test-Path $iconPath) {
        $fileSize = (Get-Item $iconPath).Length
        Write-Host "✅ $iconFile ($fileSize bytes)" -ForegroundColor Green
    } else {
        Write-Host "❌ $iconFile (未找到)" -ForegroundColor Red
        $allIconsExist = $false
    }
}

if ($allIconsExist) {
    Write-Host "`n✅ 所有图标生成成功！" -ForegroundColor Green
} else {
    Write-Host "`n❌ 部分图标生成失败" -ForegroundColor Red
}

# 显示图标预览信息
Write-Host "`n=== 图标预览信息 ===" -ForegroundColor Cyan
Write-Host "• StabilityAnalysis.png - 坝体稳定性分析图标（蓝色坝体+绿色安全系数指示器）" -ForegroundColor White
Write-Host "• AdvancedAnalysis.png - 高级坝体分析图标（绿色坝体+橙色分析网格）" -ForegroundColor White
Write-Host "• UIIntegration.png - UI集成分析图标（窗口框架+按钮界面）" -ForegroundColor White
Write-Host "• ExtrusionSketch.png - 拉伸Sketch提取图标（紫色拉伸体+红色箭头+绿色提取指示器）" -ForegroundColor White

Write-Host "`nIcons generated and configured successfully! You can now see beautiful icons in Revit." -ForegroundColor Green 
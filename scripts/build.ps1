# 重力坝分析插件构建脚本
# Build script for Gravity Dam Analysis Plugin

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

Write-Host "======================================" -ForegroundColor Green
Write-Host "重力坝稳定性分析插件构建脚本" -ForegroundColor Green
Write-Host "Gravity Dam Analysis Plugin Build Script" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green

$ErrorActionPreference = "Stop"

# 设置路径
$SolutionPath = "GravityDamAnalysis.sln"
$OutputDir = "bin\$Configuration"
$RevitPluginDir = "$env:USERPROFILE\AppData\Roaming\Autodesk\Revit\Addins\2025"

Write-Host "配置: $Configuration" -ForegroundColor Yellow
Write-Host "平台: $Platform" -ForegroundColor Yellow

# 清理之前的构建
Write-Host "正在清理之前的构建文件..." -ForegroundColor Cyan
try {
    dotnet clean $SolutionPath --configuration $Configuration --verbosity minimal
    Write-Host "✓ 清理完成" -ForegroundColor Green
} catch {
    Write-Host "✗ 清理失败: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 还原NuGet包
Write-Host "正在还原NuGet包..." -ForegroundColor Cyan
try {
    dotnet restore $SolutionPath --verbosity minimal
    Write-Host "✓ 包还原完成" -ForegroundColor Green
} catch {
    Write-Host "✗ 包还原失败: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 构建解决方案
Write-Host "正在构建解决方案..." -ForegroundColor Cyan
try {
    dotnet build $SolutionPath --configuration $Configuration --platform $Platform --no-restore --verbosity minimal
    Write-Host "✓ 构建完成" -ForegroundColor Green
} catch {
    Write-Host "✗ 构建失败: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 运行测试
Write-Host "正在运行单元测试..." -ForegroundColor Cyan
try {
    dotnet test --configuration $Configuration --no-build --verbosity minimal --logger:"console;verbosity=normal"
    Write-Host "✓ 测试完成" -ForegroundColor Green
} catch {
    Write-Host "✗ 测试失败: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 复制插件文件到Revit插件目录
Write-Host "正在部署插件文件..." -ForegroundColor Cyan

$RevitProjectPath = "src\GravityDamAnalysis.Revit"
$RevitOutputPath = "$RevitProjectPath\bin\$Platform\$Configuration\net8.0"

if (Test-Path $RevitOutputPath) {
    # 确保Revit插件目录存在
    if (!(Test-Path $RevitPluginDir)) {
        New-Item -ItemType Directory -Path $RevitPluginDir -Force | Out-Null
    }

    # 复制插件文件
    $FilesToCopy = @(
        "GravityDamAnalysis.Revit.dll",
        "GravityDamAnalysis.Core.dll",
        "GravityDamAnalysis.Calculation.dll",
        "GravityDamAnalysis.Infrastructure.dll",
        "GravityDamAnalysis.Reports.dll"
    )

    foreach ($file in $FilesToCopy) {
        $sourcePath = Join-Path $RevitOutputPath $file
        if (Test-Path $sourcePath) {
            Copy-Item $sourcePath $RevitPluginDir -Force
            Write-Host "  ✓ 已复制: $file" -ForegroundColor White
        } else {
            Write-Host "  ⚠ 文件不存在: $file" -ForegroundColor Yellow
        }
    }

    # 复制manifest文件
    $manifestSource = "$RevitProjectPath\Resources\manifest.addin"
    if (Test-Path $manifestSource) {
        Copy-Item $manifestSource $RevitPluginDir -Force
        Write-Host "  ✓ 已复制: manifest.addin" -ForegroundColor White
    }

    # 复制配置文件
    $configSource = "$RevitProjectPath\Resources\appsettings.json"
    if (Test-Path $configSource) {
        Copy-Item $configSource $RevitPluginDir -Force
        Write-Host "  ✓ 已复制: appsettings.json" -ForegroundColor White
    }

    Write-Host "✓ 插件部署完成" -ForegroundColor Green
} else {
    Write-Host "✗ 找不到Revit插件输出目录: $RevitOutputPath" -ForegroundColor Red
}

# 创建发布包
Write-Host "正在创建发布包..." -ForegroundColor Cyan

$PackageDir = "Package"
if (Test-Path $PackageDir) {
    Remove-Item $PackageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $PackageDir -Force | Out-Null

# 复制所有必要文件到发布包
if (Test-Path $RevitOutputPath) {
    Copy-Item "$RevitOutputPath\*.dll" $PackageDir -Force
    Copy-Item "$RevitProjectPath\Resources\manifest.addin" $PackageDir -Force
    Copy-Item "$RevitProjectPath\Resources\appsettings.json" $PackageDir -Force
    Copy-Item "Usage_Guide.md" $PackageDir -Force
    Copy-Item "README.md" $PackageDir -Force -ErrorAction SilentlyContinue
    
    Write-Host "✓ 发布包创建完成: $PackageDir" -ForegroundColor Green
}

Write-Host "======================================" -ForegroundColor Green
Write-Host "构建完成!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host "插件已部署到: $RevitPluginDir" -ForegroundColor Cyan
Write-Host "发布包位置: $PackageDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "请重启Revit以加载更新的插件。" -ForegroundColor Yellow 
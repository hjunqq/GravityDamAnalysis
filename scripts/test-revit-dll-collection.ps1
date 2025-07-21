# Revit DLL收集测试脚本
# 解决collect-all-dependencies.ps1无法找到Revit项目DLL的问题

param(
    [string]$Config = "Debug",
    [string]$Platform = "x64",
    [string]$TargetDir = "bin\test-collected"
)

Write-Host "=== Revit DLL收集测试脚本 ===" -ForegroundColor Cyan
Write-Host "配置: $Config" -ForegroundColor Gray
Write-Host "平台: $Platform" -ForegroundColor Gray

# 创建测试目录
if (Test-Path $TargetDir) {
    Remove-Item $TargetDir -Recurse -Force
}
New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

# 智能路径检测函数
function Find-ProjectBuildPath {
    param(
        [string]$ProjectName,
        [string]$Config,
        [string]$Platform,
        [string]$TargetFramework
    )
    
    $basePath = "src\$ProjectName\bin"
    Write-Host "搜索项目: $ProjectName" -ForegroundColor Yellow
    
    # 可能的构建路径（按优先级排序）
    $possiblePaths = @(
        "$basePath\x64\$Config\$TargetFramework",      # x64 特定平台
        "$basePath\$Config\$TargetFramework",          # AnyCPU 或默认
        "$basePath\Release\$TargetFramework",          # Release 备用
        "$basePath\Debug\$TargetFramework",            # Debug 备用
        "$basePath\x64\Release\$TargetFramework",      # x64 Release 备用
        "$basePath\x64\Debug\$TargetFramework"         # x64 Debug 备用
    )
    
    foreach ($path in $possiblePaths) {
        Write-Host "  检查路径: $path" -ForegroundColor Gray
        if (Test-Path $path) {
            $dllPath = Join-Path $path "$ProjectName.dll"
            if (Test-Path $dllPath) {
                Write-Host "  ✓ 找到DLL: $dllPath" -ForegroundColor Green
                return $path
            } else {
                Write-Host "  - 路径存在但无DLL文件" -ForegroundColor Yellow
            }
        } else {
            Write-Host "  - 路径不存在" -ForegroundColor Gray
        }
    }
    
    # 如果都没找到，尝试递归搜索
    Write-Host "  进行递归搜索..." -ForegroundColor Yellow
    if (Test-Path $basePath) {
        $foundDlls = Get-ChildItem -Path $basePath -Filter "$ProjectName.dll" -Recurse -ErrorAction SilentlyContinue
        if ($foundDlls) {
            Write-Host "  找到以下DLL文件:" -ForegroundColor Yellow
            foreach ($dll in $foundDlls) {
                $relativePath = $dll.DirectoryName.Replace((Get-Location).Path, "").TrimStart('\')
                Write-Host "    - $relativePath\$($dll.Name)" -ForegroundColor Cyan
            }
            return $foundDlls[0].DirectoryName
        }
    }
    
    Write-Host "  ✗ 未找到项目输出" -ForegroundColor Red
    return $null
}

# 测试Revit项目
$revitProjectName = "GravityDamAnalysis.Revit"
$revitFramework = "net8.0-windows"

Write-Host ""
Write-Host "=== 测试Revit项目DLL收集 ===" -ForegroundColor Cyan

$revitBuildPath = Find-ProjectBuildPath -ProjectName $revitProjectName -Config $Config -Platform $Platform -TargetFramework $revitFramework

if ($revitBuildPath) {
    Write-Host ""
    Write-Host "成功找到Revit项目构建路径: $revitBuildPath" -ForegroundColor Green
    
    # 收集DLL
    $dllFile = Join-Path $revitBuildPath "$revitProjectName.dll"
    if (Test-Path $dllFile) {
        Copy-Item $dllFile $TargetDir -Force
        $fileInfo = Get-Item $dllFile
        Write-Host "✓ 已收集: $revitProjectName.dll ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
    }
    
    # 收集PDB
    $pdbFile = Join-Path $revitBuildPath "$revitProjectName.pdb"
    if (Test-Path $pdbFile) {
        Copy-Item $pdbFile $TargetDir -Force
        $fileInfo = Get-Item $pdbFile
        Write-Host "✓ 已收集: $revitProjectName.pdb ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
    }
    
    # 收集addin文件
    $addinSource = "src\GravityDamAnalysis.Revit\GravityDamAnalysis.addin"
    if (Test-Path $addinSource) {
        Copy-Item $addinSource $TargetDir -Force
        Write-Host "✓ 已收集: GravityDamAnalysis.addin" -ForegroundColor Green
    }
    
} else {
    Write-Host ""
    Write-Host "✗ 无法找到Revit项目的构建输出" -ForegroundColor Red
    Write-Host ""
    Write-Host "建议的解决方案:" -ForegroundColor Yellow
    Write-Host "1. 确保已构建项目: dotnet build 或在Visual Studio中构建" -ForegroundColor Gray
    Write-Host "2. 检查项目配置是否为 $Config" -ForegroundColor Gray
    Write-Host "3. 检查平台设置是否为 $Platform" -ForegroundColor Gray
    Write-Host "4. 如果使用Visual Studio，确保选择了正确的解决方案配置" -ForegroundColor Gray
}

# 测试其他项目（用于对比）
Write-Host ""
Write-Host "=== 测试其他项目DLL收集 ===" -ForegroundColor Cyan

$otherProjects = @(
    @{Name = "GravityDamAnalysis.Core"; Framework = "net8.0"},
    @{Name = "GravityDamAnalysis.UI"; Framework = "net8.0-windows"}
)

foreach ($project in $otherProjects) {
    $projectPath = Find-ProjectBuildPath -ProjectName $project.Name -Config $Config -Platform $Platform -TargetFramework $project.Framework
    if ($projectPath) {
        Write-Host "✓ $($project.Name): $projectPath" -ForegroundColor Green
    } else {
        Write-Host "✗ $($project.Name): 未找到" -ForegroundColor Red
    }
}

# 显示结果
Write-Host ""
Write-Host "=== 收集结果 ===" -ForegroundColor Cyan
$collectedFiles = Get-ChildItem $TargetDir -ErrorAction SilentlyContinue
if ($collectedFiles) {
    Write-Host "已收集的文件:" -ForegroundColor Green
    foreach ($file in $collectedFiles) {
        Write-Host "  - $($file.Name) ($([math]::Round($file.Length/1KB, 1))KB)" -ForegroundColor White
    }
} else {
    Write-Host "未收集到任何文件" -ForegroundColor Red
}

Write-Host ""
Write-Host "测试完成！收集的文件位于: $TargetDir" -ForegroundColor Cyan 
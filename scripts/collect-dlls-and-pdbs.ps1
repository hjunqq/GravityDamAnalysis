# 重力坝分析插件 DLL & PDB 收集脚本
param(
    [string]$Config = "x64\Debug",
    [string]$TargetDir = "bin\collected",
    [switch]$IncludeDependencies,
    [switch]$Deploy,
    [string]$RevitVersion = "2026"
)

Write-Host "? 收集项目 DLL 和 PDB 文件..." -ForegroundColor Cyan
Write-Host "配置: $Config" -ForegroundColor Gray
Write-Host "输出目录: $TargetDir" -ForegroundColor Gray

# 创建目标目录
if (Test-Path $TargetDir) {
    Remove-Item $TargetDir -Recurse -Force
}
New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

# 项目输出路径配置
$ProjectPaths = @{
    "GravityDamAnalysis.Revit" = "src\GravityDamAnalysis.Revit\bin\$Config\net8.0"
    "GravityDamAnalysis.Core" = "src\GravityDamAnalysis.Core\bin\$Config\net8.0"
    "GravityDamAnalysis.Calculation" = "src\GravityDamAnalysis.Calculation\bin\$Config\net8.0"
    "GravityDamAnalysis.Infrastructure" = "src\GravityDamAnalysis.Infrastructure\bin\$Config\net8.0"
    "GravityDamAnalysis.Reports" = "src\GravityDamAnalysis.Reports\bin\$Config\net8.0"
    "GravityDamAnalysis.UI" = "src\GravityDamAnalysis.UI\bin\$Config\net8.0-windows"
}

# 需要收集的项目文件
$RequiredProjects = @(
    "GravityDamAnalysis.Revit",
    "GravityDamAnalysis.Core", 
    "GravityDamAnalysis.Calculation",
    "GravityDamAnalysis.Infrastructure",
    "GravityDamAnalysis.Reports"
)

# 可选项目文件
$OptionalProjects = @(
    "GravityDamAnalysis.UI"
)

Write-Host "? 收集项目 DLL 和 PDB 文件..." -ForegroundColor Yellow

$collectedFiles = @()
$missingFiles = @()
$totalSize = 0

foreach ($project in $RequiredProjects) {
    $sourcePath = $ProjectPaths[$project]
    
    if (Test-Path $sourcePath) {
        # 收集 DLL 文件
        $dllFile = Join-Path $sourcePath "$project.dll"
        if (Test-Path $dllFile) {
            Copy-Item $dllFile $TargetDir -Force
            $fileInfo = Get-Item $dllFile
            $collectedFiles += @{Name = "$project.dll"; Size = $fileInfo.Length; Type = "DLL"}
            $totalSize += $fileInfo.Length
            Write-Host "  ? $project.dll ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
        } else {
            $missingFiles += "$project.dll"
            Write-Host "  ? 找不到: $project.dll" -ForegroundColor Red
        }
        
        # 收集 PDB 文件
        $pdbFile = Join-Path $sourcePath "$project.pdb"
        if (Test-Path $pdbFile) {
            Copy-Item $pdbFile $TargetDir -Force
            $fileInfo = Get-Item $pdbFile
            $collectedFiles += @{Name = "$project.pdb"; Size = $fileInfo.Length; Type = "PDB"}
            $totalSize += $fileInfo.Length
            Write-Host "  ? $project.pdb ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
        } else {
            $missingFiles += "$project.pdb"
            Write-Host "  ??  找不到: $project.pdb" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ? 项目输出目录不存在: $sourcePath" -ForegroundColor Red
        $missingFiles += "$project (整个项目)"
    }
}

# 收集可选项目
Write-Host "? 收集可选项目文件..." -ForegroundColor Yellow
foreach ($project in $OptionalProjects) {
    $sourcePath = $ProjectPaths[$project]
    
    if (Test-Path $sourcePath) {
        $dllFile = Join-Path $sourcePath "$project.dll"
        if (Test-Path $dllFile) {
            Copy-Item $dllFile $TargetDir -Force
            $fileInfo = Get-Item $dllFile
            $collectedFiles += @{Name = "$project.dll"; Size = $fileInfo.Length; Type = "DLL"}
            $totalSize += $fileInfo.Length
            Write-Host "  ? $project.dll ($([math]::Round($fileInfo.Length/1KB, 1))KB) [可选]" -ForegroundColor Cyan
        }
        
        $pdbFile = Join-Path $sourcePath "$project.pdb"
        if (Test-Path $pdbFile) {
            Copy-Item $pdbFile $TargetDir -Force
            $fileInfo = Get-Item $pdbFile
            $collectedFiles += @{Name = "$project.pdb"; Size = $fileInfo.Length; Type = "PDB"}
            $totalSize += $fileInfo.Length
            Write-Host "  ? $project.pdb ($([math]::Round($fileInfo.Length/1KB, 1))KB) [可选]" -ForegroundColor Cyan
        }
    }
}

# 收集配置文件
Write-Host ""
Write-Host "=== 开始收集配置文件 ===" -ForegroundColor Magenta
Write-Host "? 收集配置文件..." -ForegroundColor Yellow

# 重新定义配置文件数组，确保没有空元素
$configFilesToCollect = @(
    @{Source = "src\GravityDamAnalysis.Revit\Resources\manifest.addin"; Name = "manifest.addin"},
    @{Source = "src\GravityDamAnalysis.Revit\Resources\appsettings.json"; Name = "appsettings.json"}
)

foreach ($config in $configFilesToCollect) {
    Write-Host "  检查配置文件: $($config.Name)" -ForegroundColor Cyan
    Write-Host "    源路径: $($config.Source)" -ForegroundColor Gray
    if (Test-Path $config.Source) {
        Write-Host "    ? 文件存在，开始复制..." -ForegroundColor Green
        $targetPath = Join-Path $TargetDir $config.Name
        Write-Host "    目标路径: $targetPath" -ForegroundColor Gray
        Copy-Item $config.Source $targetPath -Force
        $fileInfo = Get-Item $config.Source
        $collectedFiles += @{Name = $config.Name; Size = $fileInfo.Length; Type = "CONFIG"}
        $totalSize += $fileInfo.Length
        Write-Host "  ? $($config.Name) ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
    } else {
        Write-Host "  ??  找不到: $($config.Source)" -ForegroundColor Yellow
    }
}

# 收集第三方依赖（如果需要）
if ($IncludeDependencies) {
    Write-Host "? 收集第三方依赖..." -ForegroundColor Yellow
    
    $mainProjectPath = $ProjectPaths["GravityDamAnalysis.Revit"]
    if (Test-Path $mainProjectPath) {
        $excludePatterns = @(
            "GravityDamAnalysis.*",
            "Microsoft.Win32.*",
            "System.*", 
            "Windows.*",
            "RevitAPI*"
        )
        
        $dependencyFiles = Get-ChildItem -Path $mainProjectPath -Filter "*.dll" | Where-Object {
            $shouldExclude = $false
            foreach ($pattern in $excludePatterns) {
                if ($_.Name -like $pattern) {
                    $shouldExclude = $true
                    break
                }
            }
            return -not $shouldExclude
        }
        
        foreach ($dep in $dependencyFiles) {
            Copy-Item $dep.FullName $TargetDir -Force
            $collectedFiles += @{Name = $dep.Name; Size = $dep.Length; Type = "DEPENDENCY"}
            $totalSize += $dep.Length
            Write-Host "  ? $($dep.Name) ($([math]::Round($dep.Length/1KB, 1))KB) [依赖]" -ForegroundColor Magenta
        }
    }
}

# 生成收集报告
Write-Host "? 生成收集报告..." -ForegroundColor Yellow

$reportPath = Join-Path $TargetDir "collection-report.txt"
$report = "重力坝分析插件文件收集报告`n"
$report += "====================================`n"
$report += "收集时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n"
$report += "配置: $Config`n"
$report += "目标目录: $TargetDir`n"
$report += "包含依赖: $IncludeDependencies`n`n"
$report += "收集的文件:`n"
$report += "====================================`n"

$dllFiles = $collectedFiles | Where-Object { $_.Type -eq "DLL" }
$pdbFiles = $collectedFiles | Where-Object { $_.Type -eq "PDB" }
$configFiles = $collectedFiles | Where-Object { $_.Type -eq "CONFIG" }
$depFiles = $collectedFiles | Where-Object { $_.Type -eq "DEPENDENCY" }

$report += "`nDLL 文件 ($($dllFiles.Count) 个):`n"
foreach ($file in $dllFiles) {
    $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
}

$report += "`nPDB 文件 ($($pdbFiles.Count) 个):`n"
foreach ($file in $pdbFiles) {
    $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
}

$report += "`n配置文件 ($($configFiles.Count) 个):`n"
foreach ($file in $configFiles) {
    $report += "  - $($file.Name)`n"
}

if ($depFiles.Count -gt 0) {
    $report += "`n第三方依赖 ($($depFiles.Count) 个):`n"
    foreach ($file in $depFiles) {
        $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
    }
}

if ($missingFiles.Count -gt 0) {
    $report += "`n缺失的文件 ($($missingFiles.Count) 个):`n"
    foreach ($file in $missingFiles) {
        $report += "  - $file`n"
    }
}

$report += "`n统计信息:`n"
$report += "  总文件数: $($collectedFiles.Count)`n"
$report += "  总大小: $([math]::Round($totalSize/1KB, 1))KB`n"

Set-Content -Path $reportPath -Value $report -Encoding UTF8

# 部署到Revit（可选）
if ($Deploy) {
    Write-Host "? 部署到Revit $RevitVersion..." -ForegroundColor Yellow
    
    $revitPath = "$env:APPDATA\Autodesk\Revit\Addins\$RevitVersion"
    $pluginDir = Join-Path $revitPath "GravityDamAnalysis"
    
    if (-not (Test-Path $pluginDir)) {
        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    }
    
    # 复制所有文件到Revit目录
    Copy-Item "$TargetDir\*" $pluginDir -Force
    
    # 复制addin文件到Revit根目录
    $addinFile = Join-Path $TargetDir "GravityDamAnalysis.addin"
    if (Test-Path $addinFile) {
        Copy-Item $addinFile $revitPath -Force
    }
    
    Write-Host "  ? 已部署到: $pluginDir" -ForegroundColor Green
}

# 显示完成信息
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "                   收集完成！" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "输出目录: $TargetDir" -ForegroundColor Cyan
Write-Host "收集的文件: $($collectedFiles.Count) 个" -ForegroundColor White
Write-Host "  - DLL 文件: $($dllFiles.Count) 个" -ForegroundColor White
Write-Host "  - PDB 文件: $($pdbFiles.Count) 个" -ForegroundColor White
Write-Host "  - 配置文件: $($configFiles.Count) 个" -ForegroundColor White
if ($depFiles.Count -gt 0) {
    Write-Host "  - 依赖文件: $($depFiles.Count) 个" -ForegroundColor White
}
Write-Host "总大小: $([math]::Round($totalSize/1KB, 1))KB" -ForegroundColor White

if ($missingFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "??  缺失文件: $($missingFiles.Count) 个" -ForegroundColor Yellow
    Write-Host "详细信息请查看: $reportPath" -ForegroundColor Yellow
}

if ($Deploy) {
    Write-Host "? 已部署到Revit $RevitVersion" -ForegroundColor Green
}

Write-Host ""
Write-Host "使用说明:" -ForegroundColor Yellow
Write-Host "  - 查看详细报告: Get-Content '$reportPath'" -ForegroundColor Gray
Write-Host "  - 包含依赖: .\collect-dlls-and-pdbs.ps1 -IncludeDependencies" -ForegroundColor Gray
Write-Host "  - 直接部署: .\collect-dlls-and-pdbs.ps1 -Deploy" -ForegroundColor Gray
Write-Host "  - Release版本: .\collect-dlls-and-pdbs.ps1 -Config Release" -ForegroundColor Gray 
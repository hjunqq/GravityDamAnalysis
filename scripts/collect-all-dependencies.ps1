# 重力坝分析插件完整依赖收集脚本
# 合并项目文件收集和 NuGet 依赖项复制功能
param(
    [string]$Config = "Debug",
    [string]$TargetDir = "bin\collected",
    [switch]$IncludeNuGetDependencies,
    [switch]$Deploy,
    [string]$RevitVersion = "2026",
    [string]$Platform = "AnyCPU"
)

Write-Host "? 重力坝分析插件依赖收集脚本" -ForegroundColor Cyan
Write-Host "配置: $Config" -ForegroundColor Gray
Write-Host "平台: $Platform" -ForegroundColor Gray
Write-Host "输出目录: $TargetDir" -ForegroundColor Gray
Write-Host "包含NuGet依赖: $IncludeNuGetDependencies" -ForegroundColor Gray

# 创建目标目录
if (Test-Path $TargetDir) {
    Remove-Item $TargetDir -Recurse -Force
}
New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

# 智能检测构建路径的函数
function Get-ProjectBuildPath {
    param(
        [string]$ProjectName,
        [string]$Config,
        [string]$Platform,
        [string]$TargetFramework
    )
    
    $basePath = "src\$ProjectName\bin"
    
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
        if (Test-Path $path) {
            Write-Host "    找到构建路径: $path" -ForegroundColor Green
            return $path
        }
    }
    
    Write-Host "    未找到有效的构建路径，尝试过的路径:" -ForegroundColor Red
    foreach ($path in $possiblePaths) {
        Write-Host "      - $path" -ForegroundColor Gray
    }
    
    return $null
}

# 项目配置和目标框架
$ProjectConfigs = @{
    "GravityDamAnalysis.Revit" = @{Name = "GravityDamAnalysis.Revit"; Framework = "net8.0-windows"}
    "GravityDamAnalysis.Core" = @{Name = "GravityDamAnalysis.Core"; Framework = "net8.0"}
    "GravityDamAnalysis.Calculation" = @{Name = "GravityDamAnalysis.Calculation"; Framework = "net8.0"}
    "GravityDamAnalysis.Infrastructure" = @{Name = "GravityDamAnalysis.Infrastructure"; Framework = "net8.0"}
    "GravityDamAnalysis.Reports" = @{Name = "GravityDamAnalysis.Reports"; Framework = "net8.0"}
    "GravityDamAnalysis.UI" = @{Name = "GravityDamAnalysis.UI"; Framework = "net8.0-windows"}
}

# 动态构建项目路径映射
Write-Host "正在检测项目构建路径..." -ForegroundColor Cyan
$ProjectPaths = @{}
foreach ($projectKey in $ProjectConfigs.Keys) {
    $projectConfig = $ProjectConfigs[$projectKey]
    $detectedPath = Get-ProjectBuildPath -ProjectName $projectConfig.Name -Config $Config -Platform $Platform -TargetFramework $projectConfig.Framework
    
    if ($detectedPath) {
        $ProjectPaths[$projectKey] = $detectedPath
        Write-Host "  ? $projectKey -> $detectedPath" -ForegroundColor Green
    } else {
        Write-Host "  ? $projectKey -> 未找到构建输出" -ForegroundColor Red
    }
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

# NuGet 依赖项定义
$requiredNuGetDependencies = @(
    @{Package = "microsoft.extensions.dependencyinjection.abstractions"; Version = "8.0.0"; Dll = "Microsoft.Extensions.DependencyInjection.Abstractions.dll"},
    @{Package = "microsoft.extensions.dependencyinjection"; Version = "8.0.0"; Dll = "Microsoft.Extensions.DependencyInjection.dll"},
    @{Package = "microsoft.extensions.logging.abstractions"; Version = "8.0.0"; Dll = "Microsoft.Extensions.Logging.Abstractions.dll"},
    @{Package = "microsoft.extensions.logging"; Version = "8.0.0"; Dll = "Microsoft.Extensions.Logging.dll"},
    @{Package = "microsoft.extensions.configuration.abstractions"; Version = "8.0.0"; Dll = "Microsoft.Extensions.Configuration.Abstractions.dll"},
    @{Package = "microsoft.extensions.configuration"; Version = "8.0.0"; Dll = "Microsoft.Extensions.Configuration.dll"},
    @{Package = "microsoft.extensions.configuration.json"; Version = "8.0.0"; Dll = "Microsoft.Extensions.Configuration.Json.dll"},
    @{Package = "microsoft.extensions.options"; Version = "8.0.0"; Dll = "Microsoft.Extensions.Options.dll"},
    @{Package = "microsoft.extensions.primitives"; Version = "8.0.0"; Dll = "Microsoft.Extensions.Primitives.dll"},
    @{Package = "serilog"; Version = "3.1.1"; Dll = "Serilog.dll"},
    @{Package = "serilog.extensions.logging"; Version = "8.0.0"; Dll = "Serilog.Extensions.Logging.dll"},
    @{Package = "serilog.sinks.file"; Version = "5.0.0"; Dll = "Serilog.Sinks.File.dll"},
    @{Package = "system.text.encodings.web"; Version = "8.0.0"; Dll = "System.Text.Encodings.Web.dll"},
    @{Package = "system.text.json"; Version = "8.0.0"; Dll = "System.Text.Json.dll"}
)

# NuGet 包缓存路径
$nugetCachePaths = @(
    "$env:USERPROFILE\.nuget\packages",
    "$env:ProgramFiles(x86)\Microsoft SDKs\NuGetPackages"
)

$collectedFiles = @()
$missingFiles = @()
$totalSize = 0

# ================================
# 步骤1: 收集项目 DLL 和 PDB 文件
# ================================
Write-Host ""
Write-Host "? 步骤1: 收集项目 DLL 和 PDB 文件..." -ForegroundColor Yellow

foreach ($project in $RequiredProjects) {
    Write-Host "  正在处理项目: $project" -ForegroundColor Cyan
    
    $sourcePath = $ProjectPaths[$project]
    
    if ($sourcePath -and (Test-Path $sourcePath)) {
        Write-Host "    使用路径: $sourcePath" -ForegroundColor Gray
        
        # 收集 DLL 文件
        $dllFile = Join-Path $sourcePath "$project.dll"
        if (Test-Path $dllFile) {
            Copy-Item $dllFile $TargetDir -Force
            $fileInfo = Get-Item $dllFile
            $collectedFiles += @{Name = "$project.dll"; Size = $fileInfo.Length; Type = "DLL"; Category = "Project"}
            $totalSize += $fileInfo.Length
            Write-Host "    ? $project.dll ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
        } else {
            $missingFiles += "$project.dll"
            Write-Host "    ? 找不到DLL: $dllFile" -ForegroundColor Red
        }
        
        # 收集 PDB 文件
        $pdbFile = Join-Path $sourcePath "$project.pdb"
        if (Test-Path $pdbFile) {
            Copy-Item $pdbFile $TargetDir -Force
            $fileInfo = Get-Item $pdbFile
            $collectedFiles += @{Name = "$project.pdb"; Size = $fileInfo.Length; Type = "PDB"; Category = "Project"}
            $totalSize += $fileInfo.Length
            Write-Host "    ? $project.pdb ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
        } else {
            $missingFiles += "$project.pdb"
            Write-Host "    ?  找不到PDB: $pdbFile" -ForegroundColor Yellow
        }
    } else {
        Write-Host "    ? 项目构建路径未找到或不存在" -ForegroundColor Red
        if ($sourcePath) {
            Write-Host "    尝试的路径: $sourcePath" -ForegroundColor Gray
        } else {
            Write-Host "    路径映射未设置" -ForegroundColor Gray
        }
        
        $missingFiles += "$project (构建路径未找到)"
        
        # 尝试手动搜索可能的位置
        Write-Host "    尝试搜索其他可能的位置..." -ForegroundColor Yellow
        try {
            $searchBase = "src\$project\bin"
            if (Test-Path $searchBase) {
                $foundDlls = Get-ChildItem -Path $searchBase -Filter "$project.dll" -Recurse -ErrorAction SilentlyContinue
                
                if ($foundDlls) {
                    Write-Host "    找到以下可能的DLL文件:" -ForegroundColor Yellow
                    foreach ($dll in $foundDlls) {
                        $relativePath = $dll.FullName.Replace((Get-Location).Path, "").TrimStart('\')
                        Write-Host "      - $relativePath" -ForegroundColor Gray
                    }
                    
                    # 使用第一个找到的DLL
                    $bestMatch = $foundDlls[0]
                    Write-Host "    使用: $($bestMatch.FullName)" -ForegroundColor Green
                    Copy-Item $bestMatch.FullName $TargetDir -Force
                    $collectedFiles += @{Name = "$project.dll"; Size = $bestMatch.Length; Type = "DLL"; Category = "Project"}
                    $totalSize += $bestMatch.Length
                    
                    # 尝试找对应的PDB
                    $pdbPath = $bestMatch.FullName.Replace(".dll", ".pdb")
                    if (Test-Path $pdbPath) {
                        Copy-Item $pdbPath $TargetDir -Force
                        $pdbInfo = Get-Item $pdbPath
                        $collectedFiles += @{Name = "$project.pdb"; Size = $pdbInfo.Length; Type = "PDB"; Category = "Project"}
                        $totalSize += $pdbInfo.Length
                        Write-Host "    ? 同时找到PDB文件" -ForegroundColor Green
                    }
                }
            }
        } catch {
            Write-Host "    搜索过程中出现错误: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

# 收集可选项目
Write-Host ""
Write-Host "收集可选项目文件..." -ForegroundColor Yellow
foreach ($project in $OptionalProjects) {
    $sourcePath = $ProjectPaths[$project]
    
    if (Test-Path $sourcePath) {
        $dllFile = Join-Path $sourcePath "$project.dll"
        if (Test-Path $dllFile) {
            Copy-Item $dllFile $TargetDir -Force
            $fileInfo = Get-Item $dllFile
            $collectedFiles += @{Name = "$project.dll"; Size = $fileInfo.Length; Type = "DLL"; Category = "Optional"}
            $totalSize += $fileInfo.Length
            Write-Host "  ? $project.dll ($([math]::Round($fileInfo.Length/1KB, 1))KB) [可选]" -ForegroundColor Cyan
        }
        
        $pdbFile = Join-Path $sourcePath "$project.pdb"
        if (Test-Path $pdbFile) {
            Copy-Item $pdbFile $TargetDir -Force
            $fileInfo = Get-Item $pdbFile
            $collectedFiles += @{Name = "$project.pdb"; Size = $fileInfo.Length; Type = "PDB"; Category = "Optional"}
            $totalSize += $fileInfo.Length
            Write-Host "  ? $project.pdb ($([math]::Round($fileInfo.Length/1KB, 1))KB) [可选]" -ForegroundColor Cyan
        }
    }
}

# ================================
# 步骤2: 收集配置文件
# ================================
Write-Host ""
Write-Host "? 步骤2: 收集配置文件..." -ForegroundColor Yellow

$configFilesToCollect = @(
    @{Source = "src\GravityDamAnalysis.Revit\Resources\manifest.addin"; Name = "manifest.addin"},
    @{Source = "src\GravityDamAnalysis.Revit\Resources\appsettings.json"; Name = "appsettings.json"}
)

foreach ($config in $configFilesToCollect) {
    Write-Host "  检查配置文件: $($config.Name)" -ForegroundColor Cyan
    if (Test-Path $config.Source) {
        $targetPath = Join-Path $TargetDir $config.Name
        Copy-Item $config.Source $targetPath -Force
        $fileInfo = Get-Item $config.Source
        $collectedFiles += @{Name = $config.Name; Size = $fileInfo.Length; Type = "CONFIG"; Category = "Configuration"}
        $totalSize += $fileInfo.Length
        Write-Host "  ? $($config.Name) ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
    } else {
        Write-Host "  ??  找不到: $($config.Source)" -ForegroundColor Yellow
        $missingFiles += $config.Source
    }
}

# ================================
# 步骤3: 收集 NuGet 依赖项
# ================================
if ($IncludeNuGetDependencies) {
    Write-Host ""
    Write-Host "? 步骤3: 收集 NuGet 依赖项..." -ForegroundColor Yellow
    
    $nugetCopiedCount = 0
    $nugetMissingCount = 0
    
    foreach ($dep in $requiredNuGetDependencies) {
        $found = $false
        
        foreach ($cachePath in $nugetCachePaths) {
            if (-not (Test-Path $cachePath)) { continue }
            
            # 尝试不同可能的 DLL 路径
            $possiblePaths = @(
                "$cachePath\$($dep.Package)\$($dep.Version)\lib\net8.0\$($dep.Dll)",
                "$cachePath\$($dep.Package)\$($dep.Version)\lib\netstandard2.0\$($dep.Dll)",
                "$cachePath\$($dep.Package)\$($dep.Version)\lib\netstandard2.1\$($dep.Dll)",
                "$cachePath\$($dep.Package)\$($dep.Version)\lib\net6.0\$($dep.Dll)"
            )
            
            foreach ($sourcePath in $possiblePaths) {
                if (Test-Path $sourcePath) {
                    $targetPath = Join-Path $TargetDir $dep.Dll
                    Copy-Item $sourcePath $targetPath -Force
                    $fileInfo = Get-Item $sourcePath
                    $collectedFiles += @{Name = $dep.Dll; Size = $fileInfo.Length; Type = "DLL"; Category = "NuGet"}
                    $totalSize += $fileInfo.Length
                    Write-Host "  ? $($dep.Dll) ($([math]::Round($fileInfo.Length/1KB, 1))KB) [NuGet]" -ForegroundColor Magenta
                    $nugetCopiedCount++
                    $found = $true
                    break
                }
            }
            
            if ($found) { break }
        }
        
        if (-not $found) {
            Write-Host "  ? $($dep.Dll)" -ForegroundColor Red
            $missingFiles += $dep.Dll
            $nugetMissingCount++
        }
    }
    
    Write-Host "  NuGet 依赖收集完成: 成功 $nugetCopiedCount 个，缺失 $nugetMissingCount 个" -ForegroundColor Gray
}

# ================================
# 步骤4: 生成收集报告
# ================================
Write-Host ""
Write-Host "? 步骤4: 生成收集报告..." -ForegroundColor Yellow

$reportPath = Join-Path $TargetDir "collection-report.txt"
$report = "重力坝分析插件文件收集报告`n"
$report += "==========================================`n"
$report += "收集时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n"
$report += "配置: $Config`n"
$report += "平台: $Platform`n"
$report += "目标目录: $TargetDir`n"
$report += "包含NuGet依赖: $IncludeNuGetDependencies`n`n"

# 按类别分组文件
$projectDlls = $collectedFiles | Where-Object { $_.Type -eq "DLL" -and $_.Category -eq "Project" }
$projectPdbs = $collectedFiles | Where-Object { $_.Type -eq "PDB" -and $_.Category -eq "Project" }
$optionalDlls = $collectedFiles | Where-Object { $_.Type -eq "DLL" -and $_.Category -eq "Optional" }
$optionalPdbs = $collectedFiles | Where-Object { $_.Type -eq "PDB" -and $_.Category -eq "Optional" }
$configFiles = $collectedFiles | Where-Object { $_.Type -eq "CONFIG" }
$nugetDlls = $collectedFiles | Where-Object { $_.Type -eq "DLL" -and $_.Category -eq "NuGet" }

$report += "收集的文件:`n"
$report += "==========================================`n"

$report += "`n项目 DLL 文件 ($($projectDlls.Count) 个):`n"
foreach ($file in $projectDlls) {
    $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
}

$report += "`n项目 PDB 文件 ($($projectPdbs.Count) 个):`n"
foreach ($file in $projectPdbs) {
    $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
}

if ($optionalDlls.Count -gt 0) {
    $report += "`n可选 DLL 文件 ($($optionalDlls.Count) 个):`n"
    foreach ($file in $optionalDlls) {
        $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
    }
}

if ($optionalPdbs.Count -gt 0) {
    $report += "`n可选 PDB 文件 ($($optionalPdbs.Count) 个):`n"
    foreach ($file in $optionalPdbs) {
        $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
    }
}

$report += "`n配置文件 ($($configFiles.Count) 个):`n"
foreach ($file in $configFiles) {
    $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
}

if ($nugetDlls.Count -gt 0) {
    $report += "`nNuGet 依赖文件 ($($nugetDlls.Count) 个):`n"
    foreach ($file in $nugetDlls) {
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
$report += "  总大小: $([math]::Round($totalSize/1KB, 1))KB ($([math]::Round($totalSize/1MB, 2))MB)`n"

Set-Content -Path $reportPath -Value $report -Encoding UTF8

# ================================
# 步骤5: 部署到 Revit（可选）
# ================================
if ($Deploy) {
    Write-Host ""
    Write-Host "? 步骤5: 部署到 Revit $RevitVersion..." -ForegroundColor Yellow
    
    $revitPath = "$env:APPDATA\Autodesk\Revit\Addins\$RevitVersion"
    $pluginDir = Join-Path $revitPath "GravityDamAnalysis"
    
    if (-not (Test-Path $pluginDir)) {
        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    }
    
    # 复制所有文件到 Revit 目录
    Copy-Item "$TargetDir\*" $pluginDir -Force
    
    # 复制 addin 文件到 Revit 插件目录
    $addinFile = Join-Path $TargetDir "manifest.addin"
    if (Test-Path $addinFile) {
        $addinTargetFile = Join-Path $revitPath "GravityDamAnalysis.addin"
        Copy-Item $addinFile $addinTargetFile -Force
        Write-Host "  ? 清单文件已部署: $addinTargetFile" -ForegroundColor Green
    }
    
    Write-Host "  ? 插件已部署到: $pluginDir" -ForegroundColor Green
}

# ================================
# 显示完成信息
# ================================
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "                   收集完成！" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "输出目录: $TargetDir" -ForegroundColor Cyan
Write-Host "收集的文件: $($collectedFiles.Count) 个" -ForegroundColor White

# 按类别显示统计
$dllCount = ($collectedFiles | Where-Object { $_.Type -eq "DLL" }).Count
$pdbCount = ($collectedFiles | Where-Object { $_.Type -eq "PDB" }).Count
$configCount = ($collectedFiles | Where-Object { $_.Type -eq "CONFIG" }).Count

Write-Host "  - DLL 文件: $dllCount 个" -ForegroundColor White
Write-Host "  - PDB 文件: $pdbCount 个" -ForegroundColor White
Write-Host "  - 配置文件: $configCount 个" -ForegroundColor White

if ($IncludeNuGetDependencies) {
    $nugetCount = ($collectedFiles | Where-Object { $_.Category -eq "NuGet" }).Count
    Write-Host "  - NuGet 依赖: $nugetCount 个" -ForegroundColor White
}

Write-Host "总大小: $([math]::Round($totalSize/1KB, 1))KB ($([math]::Round($totalSize/1MB, 2))MB)" -ForegroundColor White

if ($missingFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "??  缺失文件: $($missingFiles.Count) 个" -ForegroundColor Yellow
    Write-Host "详细信息查看: $reportPath" -ForegroundColor Yellow
}

if ($Deploy) {
    Write-Host ""
    Write-Host "? 已部署到 Revit $RevitVersion" -ForegroundColor Green
}

Write-Host ""
Write-Host "使用说明:" -ForegroundColor Yellow
Write-Host "  - 查看详细报告: Get-Content '$reportPath'" -ForegroundColor Gray
Write-Host "  - 包含NuGet依赖: .\collect-all-dependencies.ps1 -IncludeNuGetDependencies" -ForegroundColor Gray
Write-Host "  - 直接部署: .\collect-all-dependencies.ps1 -Deploy" -ForegroundColor Gray
Write-Host "  - Release版本: .\collect-all-dependencies.ps1 -Config Release" -ForegroundColor Gray
Write-Host "  - x64平台: .\collect-all-dependencies.ps1 -Platform x64" -ForegroundColor Gray
Write-Host "  - 完整收集+部署: .\collect-all-dependencies.ps1 -IncludeNuGetDependencies -Deploy" -ForegroundColor Gray 
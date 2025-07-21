# �����ӷ���������������ռ��ű�
# �ϲ���Ŀ�ļ��ռ��� NuGet ������ƹ���
param(
    [string]$Config = "Debug",
    [string]$TargetDir = "bin\collected",
    [switch]$IncludeNuGetDependencies,
    [switch]$Deploy,
    [string]$RevitVersion = "2026",
    [string]$Platform = "AnyCPU"
)

Write-Host "? �����ӷ�����������ռ��ű�" -ForegroundColor Cyan
Write-Host "����: $Config" -ForegroundColor Gray
Write-Host "ƽ̨: $Platform" -ForegroundColor Gray
Write-Host "���Ŀ¼: $TargetDir" -ForegroundColor Gray
Write-Host "����NuGet����: $IncludeNuGetDependencies" -ForegroundColor Gray

# ����Ŀ��Ŀ¼
if (Test-Path $TargetDir) {
    Remove-Item $TargetDir -Recurse -Force
}
New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

# ���ܼ�⹹��·���ĺ���
function Get-ProjectBuildPath {
    param(
        [string]$ProjectName,
        [string]$Config,
        [string]$Platform,
        [string]$TargetFramework
    )
    
    $basePath = "src\$ProjectName\bin"
    
    # ���ܵĹ���·���������ȼ�����
    $possiblePaths = @(
        "$basePath\x64\$Config\$TargetFramework",      # x64 �ض�ƽ̨
        "$basePath\$Config\$TargetFramework",          # AnyCPU ��Ĭ��
        "$basePath\Release\$TargetFramework",          # Release ����
        "$basePath\Debug\$TargetFramework",            # Debug ����
        "$basePath\x64\Release\$TargetFramework",      # x64 Release ����
        "$basePath\x64\Debug\$TargetFramework"         # x64 Debug ����
    )
    
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            Write-Host "    �ҵ�����·��: $path" -ForegroundColor Green
            return $path
        }
    }
    
    Write-Host "    δ�ҵ���Ч�Ĺ���·�������Թ���·��:" -ForegroundColor Red
    foreach ($path in $possiblePaths) {
        Write-Host "      - $path" -ForegroundColor Gray
    }
    
    return $null
}

# ��Ŀ���ú�Ŀ����
$ProjectConfigs = @{
    "GravityDamAnalysis.Revit" = @{Name = "GravityDamAnalysis.Revit"; Framework = "net8.0-windows"}
    "GravityDamAnalysis.Core" = @{Name = "GravityDamAnalysis.Core"; Framework = "net8.0"}
    "GravityDamAnalysis.Calculation" = @{Name = "GravityDamAnalysis.Calculation"; Framework = "net8.0"}
    "GravityDamAnalysis.Infrastructure" = @{Name = "GravityDamAnalysis.Infrastructure"; Framework = "net8.0"}
    "GravityDamAnalysis.Reports" = @{Name = "GravityDamAnalysis.Reports"; Framework = "net8.0"}
    "GravityDamAnalysis.UI" = @{Name = "GravityDamAnalysis.UI"; Framework = "net8.0-windows"}
}

# ��̬������Ŀ·��ӳ��
Write-Host "���ڼ����Ŀ����·��..." -ForegroundColor Cyan
$ProjectPaths = @{}
foreach ($projectKey in $ProjectConfigs.Keys) {
    $projectConfig = $ProjectConfigs[$projectKey]
    $detectedPath = Get-ProjectBuildPath -ProjectName $projectConfig.Name -Config $Config -Platform $Platform -TargetFramework $projectConfig.Framework
    
    if ($detectedPath) {
        $ProjectPaths[$projectKey] = $detectedPath
        Write-Host "  ? $projectKey -> $detectedPath" -ForegroundColor Green
    } else {
        Write-Host "  ? $projectKey -> δ�ҵ��������" -ForegroundColor Red
    }
}

# ��Ҫ�ռ�����Ŀ�ļ�
$RequiredProjects = @(
    "GravityDamAnalysis.Revit",
    "GravityDamAnalysis.Core", 
    "GravityDamAnalysis.Calculation",
    "GravityDamAnalysis.Infrastructure",
    "GravityDamAnalysis.Reports"
)

# ��ѡ��Ŀ�ļ�
$OptionalProjects = @(
    "GravityDamAnalysis.UI"
)

# NuGet �������
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

# NuGet ������·��
$nugetCachePaths = @(
    "$env:USERPROFILE\.nuget\packages",
    "$env:ProgramFiles(x86)\Microsoft SDKs\NuGetPackages"
)

$collectedFiles = @()
$missingFiles = @()
$totalSize = 0

# ================================
# ����1: �ռ���Ŀ DLL �� PDB �ļ�
# ================================
Write-Host ""
Write-Host "? ����1: �ռ���Ŀ DLL �� PDB �ļ�..." -ForegroundColor Yellow

foreach ($project in $RequiredProjects) {
    Write-Host "  ���ڴ�����Ŀ: $project" -ForegroundColor Cyan
    
    $sourcePath = $ProjectPaths[$project]
    
    if ($sourcePath -and (Test-Path $sourcePath)) {
        Write-Host "    ʹ��·��: $sourcePath" -ForegroundColor Gray
        
        # �ռ� DLL �ļ�
        $dllFile = Join-Path $sourcePath "$project.dll"
        if (Test-Path $dllFile) {
            Copy-Item $dllFile $TargetDir -Force
            $fileInfo = Get-Item $dllFile
            $collectedFiles += @{Name = "$project.dll"; Size = $fileInfo.Length; Type = "DLL"; Category = "Project"}
            $totalSize += $fileInfo.Length
            Write-Host "    ? $project.dll ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
        } else {
            $missingFiles += "$project.dll"
            Write-Host "    ? �Ҳ���DLL: $dllFile" -ForegroundColor Red
        }
        
        # �ռ� PDB �ļ�
        $pdbFile = Join-Path $sourcePath "$project.pdb"
        if (Test-Path $pdbFile) {
            Copy-Item $pdbFile $TargetDir -Force
            $fileInfo = Get-Item $pdbFile
            $collectedFiles += @{Name = "$project.pdb"; Size = $fileInfo.Length; Type = "PDB"; Category = "Project"}
            $totalSize += $fileInfo.Length
            Write-Host "    ? $project.pdb ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
        } else {
            $missingFiles += "$project.pdb"
            Write-Host "    ?  �Ҳ���PDB: $pdbFile" -ForegroundColor Yellow
        }
    } else {
        Write-Host "    ? ��Ŀ����·��δ�ҵ��򲻴���" -ForegroundColor Red
        if ($sourcePath) {
            Write-Host "    ���Ե�·��: $sourcePath" -ForegroundColor Gray
        } else {
            Write-Host "    ·��ӳ��δ����" -ForegroundColor Gray
        }
        
        $missingFiles += "$project (����·��δ�ҵ�)"
        
        # �����ֶ��������ܵ�λ��
        Write-Host "    ���������������ܵ�λ��..." -ForegroundColor Yellow
        try {
            $searchBase = "src\$project\bin"
            if (Test-Path $searchBase) {
                $foundDlls = Get-ChildItem -Path $searchBase -Filter "$project.dll" -Recurse -ErrorAction SilentlyContinue
                
                if ($foundDlls) {
                    Write-Host "    �ҵ����¿��ܵ�DLL�ļ�:" -ForegroundColor Yellow
                    foreach ($dll in $foundDlls) {
                        $relativePath = $dll.FullName.Replace((Get-Location).Path, "").TrimStart('\')
                        Write-Host "      - $relativePath" -ForegroundColor Gray
                    }
                    
                    # ʹ�õ�һ���ҵ���DLL
                    $bestMatch = $foundDlls[0]
                    Write-Host "    ʹ��: $($bestMatch.FullName)" -ForegroundColor Green
                    Copy-Item $bestMatch.FullName $TargetDir -Force
                    $collectedFiles += @{Name = "$project.dll"; Size = $bestMatch.Length; Type = "DLL"; Category = "Project"}
                    $totalSize += $bestMatch.Length
                    
                    # �����Ҷ�Ӧ��PDB
                    $pdbPath = $bestMatch.FullName.Replace(".dll", ".pdb")
                    if (Test-Path $pdbPath) {
                        Copy-Item $pdbPath $TargetDir -Force
                        $pdbInfo = Get-Item $pdbPath
                        $collectedFiles += @{Name = "$project.pdb"; Size = $pdbInfo.Length; Type = "PDB"; Category = "Project"}
                        $totalSize += $pdbInfo.Length
                        Write-Host "    ? ͬʱ�ҵ�PDB�ļ�" -ForegroundColor Green
                    }
                }
            }
        } catch {
            Write-Host "    ���������г��ִ���: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

# �ռ���ѡ��Ŀ
Write-Host ""
Write-Host "�ռ���ѡ��Ŀ�ļ�..." -ForegroundColor Yellow
foreach ($project in $OptionalProjects) {
    $sourcePath = $ProjectPaths[$project]
    
    if (Test-Path $sourcePath) {
        $dllFile = Join-Path $sourcePath "$project.dll"
        if (Test-Path $dllFile) {
            Copy-Item $dllFile $TargetDir -Force
            $fileInfo = Get-Item $dllFile
            $collectedFiles += @{Name = "$project.dll"; Size = $fileInfo.Length; Type = "DLL"; Category = "Optional"}
            $totalSize += $fileInfo.Length
            Write-Host "  ? $project.dll ($([math]::Round($fileInfo.Length/1KB, 1))KB) [��ѡ]" -ForegroundColor Cyan
        }
        
        $pdbFile = Join-Path $sourcePath "$project.pdb"
        if (Test-Path $pdbFile) {
            Copy-Item $pdbFile $TargetDir -Force
            $fileInfo = Get-Item $pdbFile
            $collectedFiles += @{Name = "$project.pdb"; Size = $fileInfo.Length; Type = "PDB"; Category = "Optional"}
            $totalSize += $fileInfo.Length
            Write-Host "  ? $project.pdb ($([math]::Round($fileInfo.Length/1KB, 1))KB) [��ѡ]" -ForegroundColor Cyan
        }
    }
}

# ================================
# ����2: �ռ������ļ�
# ================================
Write-Host ""
Write-Host "? ����2: �ռ������ļ�..." -ForegroundColor Yellow

$configFilesToCollect = @(
    @{Source = "src\GravityDamAnalysis.Revit\Resources\manifest.addin"; Name = "manifest.addin"},
    @{Source = "src\GravityDamAnalysis.Revit\Resources\appsettings.json"; Name = "appsettings.json"}
)

foreach ($config in $configFilesToCollect) {
    Write-Host "  ��������ļ�: $($config.Name)" -ForegroundColor Cyan
    if (Test-Path $config.Source) {
        $targetPath = Join-Path $TargetDir $config.Name
        Copy-Item $config.Source $targetPath -Force
        $fileInfo = Get-Item $config.Source
        $collectedFiles += @{Name = $config.Name; Size = $fileInfo.Length; Type = "CONFIG"; Category = "Configuration"}
        $totalSize += $fileInfo.Length
        Write-Host "  ? $($config.Name) ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
    } else {
        Write-Host "  ??  �Ҳ���: $($config.Source)" -ForegroundColor Yellow
        $missingFiles += $config.Source
    }
}

# ================================
# ����3: �ռ� NuGet ������
# ================================
if ($IncludeNuGetDependencies) {
    Write-Host ""
    Write-Host "? ����3: �ռ� NuGet ������..." -ForegroundColor Yellow
    
    $nugetCopiedCount = 0
    $nugetMissingCount = 0
    
    foreach ($dep in $requiredNuGetDependencies) {
        $found = $false
        
        foreach ($cachePath in $nugetCachePaths) {
            if (-not (Test-Path $cachePath)) { continue }
            
            # ���Բ�ͬ���ܵ� DLL ·��
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
    
    Write-Host "  NuGet �����ռ����: �ɹ� $nugetCopiedCount ����ȱʧ $nugetMissingCount ��" -ForegroundColor Gray
}

# ================================
# ����4: �����ռ�����
# ================================
Write-Host ""
Write-Host "? ����4: �����ռ�����..." -ForegroundColor Yellow

$reportPath = Join-Path $TargetDir "collection-report.txt"
$report = "�����ӷ�������ļ��ռ�����`n"
$report += "==========================================`n"
$report += "�ռ�ʱ��: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n"
$report += "����: $Config`n"
$report += "ƽ̨: $Platform`n"
$report += "Ŀ��Ŀ¼: $TargetDir`n"
$report += "����NuGet����: $IncludeNuGetDependencies`n`n"

# ���������ļ�
$projectDlls = $collectedFiles | Where-Object { $_.Type -eq "DLL" -and $_.Category -eq "Project" }
$projectPdbs = $collectedFiles | Where-Object { $_.Type -eq "PDB" -and $_.Category -eq "Project" }
$optionalDlls = $collectedFiles | Where-Object { $_.Type -eq "DLL" -and $_.Category -eq "Optional" }
$optionalPdbs = $collectedFiles | Where-Object { $_.Type -eq "PDB" -and $_.Category -eq "Optional" }
$configFiles = $collectedFiles | Where-Object { $_.Type -eq "CONFIG" }
$nugetDlls = $collectedFiles | Where-Object { $_.Type -eq "DLL" -and $_.Category -eq "NuGet" }

$report += "�ռ����ļ�:`n"
$report += "==========================================`n"

$report += "`n��Ŀ DLL �ļ� ($($projectDlls.Count) ��):`n"
foreach ($file in $projectDlls) {
    $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
}

$report += "`n��Ŀ PDB �ļ� ($($projectPdbs.Count) ��):`n"
foreach ($file in $projectPdbs) {
    $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
}

if ($optionalDlls.Count -gt 0) {
    $report += "`n��ѡ DLL �ļ� ($($optionalDlls.Count) ��):`n"
    foreach ($file in $optionalDlls) {
        $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
    }
}

if ($optionalPdbs.Count -gt 0) {
    $report += "`n��ѡ PDB �ļ� ($($optionalPdbs.Count) ��):`n"
    foreach ($file in $optionalPdbs) {
        $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
    }
}

$report += "`n�����ļ� ($($configFiles.Count) ��):`n"
foreach ($file in $configFiles) {
    $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
}

if ($nugetDlls.Count -gt 0) {
    $report += "`nNuGet �����ļ� ($($nugetDlls.Count) ��):`n"
    foreach ($file in $nugetDlls) {
        $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
    }
}

if ($missingFiles.Count -gt 0) {
    $report += "`nȱʧ���ļ� ($($missingFiles.Count) ��):`n"
    foreach ($file in $missingFiles) {
        $report += "  - $file`n"
    }
}

$report += "`nͳ����Ϣ:`n"
$report += "  ���ļ���: $($collectedFiles.Count)`n"
$report += "  �ܴ�С: $([math]::Round($totalSize/1KB, 1))KB ($([math]::Round($totalSize/1MB, 2))MB)`n"

Set-Content -Path $reportPath -Value $report -Encoding UTF8

# ================================
# ����5: ���� Revit����ѡ��
# ================================
if ($Deploy) {
    Write-Host ""
    Write-Host "? ����5: ���� Revit $RevitVersion..." -ForegroundColor Yellow
    
    $revitPath = "$env:APPDATA\Autodesk\Revit\Addins\$RevitVersion"
    $pluginDir = Join-Path $revitPath "GravityDamAnalysis"
    
    if (-not (Test-Path $pluginDir)) {
        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    }
    
    # ���������ļ��� Revit Ŀ¼
    Copy-Item "$TargetDir\*" $pluginDir -Force
    
    # ���� addin �ļ��� Revit ���Ŀ¼
    $addinFile = Join-Path $TargetDir "manifest.addin"
    if (Test-Path $addinFile) {
        $addinTargetFile = Join-Path $revitPath "GravityDamAnalysis.addin"
        Copy-Item $addinFile $addinTargetFile -Force
        Write-Host "  ? �嵥�ļ��Ѳ���: $addinTargetFile" -ForegroundColor Green
    }
    
    Write-Host "  ? ����Ѳ���: $pluginDir" -ForegroundColor Green
}

# ================================
# ��ʾ�����Ϣ
# ================================
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "                   �ռ���ɣ�" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "���Ŀ¼: $TargetDir" -ForegroundColor Cyan
Write-Host "�ռ����ļ�: $($collectedFiles.Count) ��" -ForegroundColor White

# �������ʾͳ��
$dllCount = ($collectedFiles | Where-Object { $_.Type -eq "DLL" }).Count
$pdbCount = ($collectedFiles | Where-Object { $_.Type -eq "PDB" }).Count
$configCount = ($collectedFiles | Where-Object { $_.Type -eq "CONFIG" }).Count

Write-Host "  - DLL �ļ�: $dllCount ��" -ForegroundColor White
Write-Host "  - PDB �ļ�: $pdbCount ��" -ForegroundColor White
Write-Host "  - �����ļ�: $configCount ��" -ForegroundColor White

if ($IncludeNuGetDependencies) {
    $nugetCount = ($collectedFiles | Where-Object { $_.Category -eq "NuGet" }).Count
    Write-Host "  - NuGet ����: $nugetCount ��" -ForegroundColor White
}

Write-Host "�ܴ�С: $([math]::Round($totalSize/1KB, 1))KB ($([math]::Round($totalSize/1MB, 2))MB)" -ForegroundColor White

if ($missingFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "??  ȱʧ�ļ�: $($missingFiles.Count) ��" -ForegroundColor Yellow
    Write-Host "��ϸ��Ϣ�鿴: $reportPath" -ForegroundColor Yellow
}

if ($Deploy) {
    Write-Host ""
    Write-Host "? �Ѳ��� Revit $RevitVersion" -ForegroundColor Green
}

Write-Host ""
Write-Host "ʹ��˵��:" -ForegroundColor Yellow
Write-Host "  - �鿴��ϸ����: Get-Content '$reportPath'" -ForegroundColor Gray
Write-Host "  - ����NuGet����: .\collect-all-dependencies.ps1 -IncludeNuGetDependencies" -ForegroundColor Gray
Write-Host "  - ֱ�Ӳ���: .\collect-all-dependencies.ps1 -Deploy" -ForegroundColor Gray
Write-Host "  - Release�汾: .\collect-all-dependencies.ps1 -Config Release" -ForegroundColor Gray
Write-Host "  - x64ƽ̨: .\collect-all-dependencies.ps1 -Platform x64" -ForegroundColor Gray
Write-Host "  - �����ռ�+����: .\collect-all-dependencies.ps1 -IncludeNuGetDependencies -Deploy" -ForegroundColor Gray 
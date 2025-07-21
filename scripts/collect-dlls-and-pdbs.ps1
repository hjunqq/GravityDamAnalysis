# �����ӷ������ DLL & PDB �ռ��ű�
param(
    [string]$Config = "x64\Debug",
    [string]$TargetDir = "bin\collected",
    [switch]$IncludeDependencies,
    [switch]$Deploy,
    [string]$RevitVersion = "2026"
)

Write-Host "? �ռ���Ŀ DLL �� PDB �ļ�..." -ForegroundColor Cyan
Write-Host "����: $Config" -ForegroundColor Gray
Write-Host "���Ŀ¼: $TargetDir" -ForegroundColor Gray

# ����Ŀ��Ŀ¼
if (Test-Path $TargetDir) {
    Remove-Item $TargetDir -Recurse -Force
}
New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

# ��Ŀ���·������
$ProjectPaths = @{
    "GravityDamAnalysis.Revit" = "src\GravityDamAnalysis.Revit\bin\$Config\net8.0"
    "GravityDamAnalysis.Core" = "src\GravityDamAnalysis.Core\bin\$Config\net8.0"
    "GravityDamAnalysis.Calculation" = "src\GravityDamAnalysis.Calculation\bin\$Config\net8.0"
    "GravityDamAnalysis.Infrastructure" = "src\GravityDamAnalysis.Infrastructure\bin\$Config\net8.0"
    "GravityDamAnalysis.Reports" = "src\GravityDamAnalysis.Reports\bin\$Config\net8.0"
    "GravityDamAnalysis.UI" = "src\GravityDamAnalysis.UI\bin\$Config\net8.0-windows"
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

Write-Host "? �ռ���Ŀ DLL �� PDB �ļ�..." -ForegroundColor Yellow

$collectedFiles = @()
$missingFiles = @()
$totalSize = 0

foreach ($project in $RequiredProjects) {
    $sourcePath = $ProjectPaths[$project]
    
    if (Test-Path $sourcePath) {
        # �ռ� DLL �ļ�
        $dllFile = Join-Path $sourcePath "$project.dll"
        if (Test-Path $dllFile) {
            Copy-Item $dllFile $TargetDir -Force
            $fileInfo = Get-Item $dllFile
            $collectedFiles += @{Name = "$project.dll"; Size = $fileInfo.Length; Type = "DLL"}
            $totalSize += $fileInfo.Length
            Write-Host "  ? $project.dll ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
        } else {
            $missingFiles += "$project.dll"
            Write-Host "  ? �Ҳ���: $project.dll" -ForegroundColor Red
        }
        
        # �ռ� PDB �ļ�
        $pdbFile = Join-Path $sourcePath "$project.pdb"
        if (Test-Path $pdbFile) {
            Copy-Item $pdbFile $TargetDir -Force
            $fileInfo = Get-Item $pdbFile
            $collectedFiles += @{Name = "$project.pdb"; Size = $fileInfo.Length; Type = "PDB"}
            $totalSize += $fileInfo.Length
            Write-Host "  ? $project.pdb ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
        } else {
            $missingFiles += "$project.pdb"
            Write-Host "  ??  �Ҳ���: $project.pdb" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ? ��Ŀ���Ŀ¼������: $sourcePath" -ForegroundColor Red
        $missingFiles += "$project (������Ŀ)"
    }
}

# �ռ���ѡ��Ŀ
Write-Host "? �ռ���ѡ��Ŀ�ļ�..." -ForegroundColor Yellow
foreach ($project in $OptionalProjects) {
    $sourcePath = $ProjectPaths[$project]
    
    if (Test-Path $sourcePath) {
        $dllFile = Join-Path $sourcePath "$project.dll"
        if (Test-Path $dllFile) {
            Copy-Item $dllFile $TargetDir -Force
            $fileInfo = Get-Item $dllFile
            $collectedFiles += @{Name = "$project.dll"; Size = $fileInfo.Length; Type = "DLL"}
            $totalSize += $fileInfo.Length
            Write-Host "  ? $project.dll ($([math]::Round($fileInfo.Length/1KB, 1))KB) [��ѡ]" -ForegroundColor Cyan
        }
        
        $pdbFile = Join-Path $sourcePath "$project.pdb"
        if (Test-Path $pdbFile) {
            Copy-Item $pdbFile $TargetDir -Force
            $fileInfo = Get-Item $pdbFile
            $collectedFiles += @{Name = "$project.pdb"; Size = $fileInfo.Length; Type = "PDB"}
            $totalSize += $fileInfo.Length
            Write-Host "  ? $project.pdb ($([math]::Round($fileInfo.Length/1KB, 1))KB) [��ѡ]" -ForegroundColor Cyan
        }
    }
}

# �ռ������ļ�
Write-Host ""
Write-Host "=== ��ʼ�ռ������ļ� ===" -ForegroundColor Magenta
Write-Host "? �ռ������ļ�..." -ForegroundColor Yellow

# ���¶��������ļ����飬ȷ��û�п�Ԫ��
$configFilesToCollect = @(
    @{Source = "src\GravityDamAnalysis.Revit\Resources\manifest.addin"; Name = "manifest.addin"},
    @{Source = "src\GravityDamAnalysis.Revit\Resources\appsettings.json"; Name = "appsettings.json"}
)

foreach ($config in $configFilesToCollect) {
    Write-Host "  ��������ļ�: $($config.Name)" -ForegroundColor Cyan
    Write-Host "    Դ·��: $($config.Source)" -ForegroundColor Gray
    if (Test-Path $config.Source) {
        Write-Host "    ? �ļ����ڣ���ʼ����..." -ForegroundColor Green
        $targetPath = Join-Path $TargetDir $config.Name
        Write-Host "    Ŀ��·��: $targetPath" -ForegroundColor Gray
        Copy-Item $config.Source $targetPath -Force
        $fileInfo = Get-Item $config.Source
        $collectedFiles += @{Name = $config.Name; Size = $fileInfo.Length; Type = "CONFIG"}
        $totalSize += $fileInfo.Length
        Write-Host "  ? $($config.Name) ($([math]::Round($fileInfo.Length/1KB, 1))KB)" -ForegroundColor Green
    } else {
        Write-Host "  ??  �Ҳ���: $($config.Source)" -ForegroundColor Yellow
    }
}

# �ռ������������������Ҫ��
if ($IncludeDependencies) {
    Write-Host "? �ռ�����������..." -ForegroundColor Yellow
    
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
            Write-Host "  ? $($dep.Name) ($([math]::Round($dep.Length/1KB, 1))KB) [����]" -ForegroundColor Magenta
        }
    }
}

# �����ռ�����
Write-Host "? �����ռ�����..." -ForegroundColor Yellow

$reportPath = Join-Path $TargetDir "collection-report.txt"
$report = "�����ӷ�������ļ��ռ�����`n"
$report += "====================================`n"
$report += "�ռ�ʱ��: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n"
$report += "����: $Config`n"
$report += "Ŀ��Ŀ¼: $TargetDir`n"
$report += "��������: $IncludeDependencies`n`n"
$report += "�ռ����ļ�:`n"
$report += "====================================`n"

$dllFiles = $collectedFiles | Where-Object { $_.Type -eq "DLL" }
$pdbFiles = $collectedFiles | Where-Object { $_.Type -eq "PDB" }
$configFiles = $collectedFiles | Where-Object { $_.Type -eq "CONFIG" }
$depFiles = $collectedFiles | Where-Object { $_.Type -eq "DEPENDENCY" }

$report += "`nDLL �ļ� ($($dllFiles.Count) ��):`n"
foreach ($file in $dllFiles) {
    $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
}

$report += "`nPDB �ļ� ($($pdbFiles.Count) ��):`n"
foreach ($file in $pdbFiles) {
    $report += "  - $($file.Name) ($([math]::Round($file.Size/1KB, 1))KB)`n"
}

$report += "`n�����ļ� ($($configFiles.Count) ��):`n"
foreach ($file in $configFiles) {
    $report += "  - $($file.Name)`n"
}

if ($depFiles.Count -gt 0) {
    $report += "`n���������� ($($depFiles.Count) ��):`n"
    foreach ($file in $depFiles) {
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
$report += "  �ܴ�С: $([math]::Round($totalSize/1KB, 1))KB`n"

Set-Content -Path $reportPath -Value $report -Encoding UTF8

# ����Revit����ѡ��
if ($Deploy) {
    Write-Host "? ����Revit $RevitVersion..." -ForegroundColor Yellow
    
    $revitPath = "$env:APPDATA\Autodesk\Revit\Addins\$RevitVersion"
    $pluginDir = Join-Path $revitPath "GravityDamAnalysis"
    
    if (-not (Test-Path $pluginDir)) {
        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    }
    
    # ���������ļ���RevitĿ¼
    Copy-Item "$TargetDir\*" $pluginDir -Force
    
    # ����addin�ļ���Revit��Ŀ¼
    $addinFile = Join-Path $TargetDir "GravityDamAnalysis.addin"
    if (Test-Path $addinFile) {
        Copy-Item $addinFile $revitPath -Force
    }
    
    Write-Host "  ? �Ѳ���: $pluginDir" -ForegroundColor Green
}

# ��ʾ�����Ϣ
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "                   �ռ���ɣ�" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "���Ŀ¼: $TargetDir" -ForegroundColor Cyan
Write-Host "�ռ����ļ�: $($collectedFiles.Count) ��" -ForegroundColor White
Write-Host "  - DLL �ļ�: $($dllFiles.Count) ��" -ForegroundColor White
Write-Host "  - PDB �ļ�: $($pdbFiles.Count) ��" -ForegroundColor White
Write-Host "  - �����ļ�: $($configFiles.Count) ��" -ForegroundColor White
if ($depFiles.Count -gt 0) {
    Write-Host "  - �����ļ�: $($depFiles.Count) ��" -ForegroundColor White
}
Write-Host "�ܴ�С: $([math]::Round($totalSize/1KB, 1))KB" -ForegroundColor White

if ($missingFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "??  ȱʧ�ļ�: $($missingFiles.Count) ��" -ForegroundColor Yellow
    Write-Host "��ϸ��Ϣ��鿴: $reportPath" -ForegroundColor Yellow
}

if ($Deploy) {
    Write-Host "? �Ѳ���Revit $RevitVersion" -ForegroundColor Green
}

Write-Host ""
Write-Host "ʹ��˵��:" -ForegroundColor Yellow
Write-Host "  - �鿴��ϸ����: Get-Content '$reportPath'" -ForegroundColor Gray
Write-Host "  - ��������: .\collect-dlls-and-pdbs.ps1 -IncludeDependencies" -ForegroundColor Gray
Write-Host "  - ֱ�Ӳ���: .\collect-dlls-and-pdbs.ps1 -Deploy" -ForegroundColor Gray
Write-Host "  - Release�汾: .\collect-dlls-and-pdbs.ps1 -Config Release" -ForegroundColor Gray 
# Copy NuGet Dependencies Script
# This script manually copies required NuGet dependencies from the package cache

param(
    [string]$TargetDir = "bin\collected",
    [switch]$Deploy
)

Write-Host "Copying NuGet dependencies from package cache..." -ForegroundColor Cyan

# NuGet package cache locations
$nugetCachePaths = @(
    "$env:USERPROFILE\.nuget\packages",
    "$env:ProgramFiles(x86)\Microsoft SDKs\NuGetPackages"
)

# Required dependencies with their package names and versions
$requiredDependencies = @(
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

# Create target directory if it doesn't exist
if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
}

$copiedCount = 0
$missingCount = 0

foreach ($dep in $requiredDependencies) {
    $found = $false
    
    foreach ($cachePath in $nugetCachePaths) {
        if (-not (Test-Path $cachePath)) { continue }
        
        # Try different possible paths for the DLL
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
                Write-Host "  [OK] $($dep.Dll)" -ForegroundColor Green
                $copiedCount++
                $found = $true
                break
            }
        }
        
        if ($found) { break }
    }
    
    if (-not $found) {
        Write-Host "  [MISSING] $($dep.Dll)" -ForegroundColor Red
        $missingCount++
    }
}

Write-Host "Dependency copy completed!" -ForegroundColor Green
Write-Host "Copied: $copiedCount files" -ForegroundColor White
Write-Host "Missing: $missingCount files" -ForegroundColor Yellow

if ($missingCount -gt 0) {
    Write-Host ""
    Write-Host "Some dependencies are missing. You may need to:" -ForegroundColor Yellow
    Write-Host "1. Run 'dotnet restore' to ensure packages are downloaded" -ForegroundColor White
    Write-Host "2. Check if package versions match your project references" -ForegroundColor White
}

# Deploy to Revit if requested
if ($Deploy) {
    Write-Host ""
    Write-Host "Deploying to Revit..." -ForegroundColor Cyan
    
    $revitPath = "$env:APPDATA\Autodesk\Revit\Addins\2025"
    $pluginDir = Join-Path $revitPath "GravityDamAnalysis"
    
    if (-not (Test-Path $pluginDir)) {
        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    }
    
    # Copy all files to Revit directory
    Copy-Item "$TargetDir\*" $pluginDir -Force
    
    Write-Host "Dependencies deployed to: $pluginDir" -ForegroundColor Green
} 
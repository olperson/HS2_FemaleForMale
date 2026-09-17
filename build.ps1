[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$GameRoot,
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
$project = Join-Path $projectDir 'HS2_FemaleForMale.Studio.csproj'
$outputDir = Join-Path $projectDir 'bin\Release\Studio'
$dllName = 'HS2_FemaleForMale.Studio.dll'
$builtDll = Join-Path $outputDir $dllName

if (-not (Test-Path -LiteralPath $GameRoot -PathType Container)) {
    throw "Game directory does not exist: $GameRoot"
}
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).ProviderPath
$GameRoot = $GameRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
Get-Command dotnet -ErrorAction Stop | Out-Null

# Validate every referenced assembly before starting the build.
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$managedDir = Join-Path $GameRoot 'StudioNEOV2_Data\Managed'
foreach ($reference in $projectXml.Project.ItemGroup.Reference) {
    if ($null -eq $reference) { continue }
    $dependency = ([string]$reference.HintPath).Replace('$(HS2GameRoot)', $GameRoot).Replace('$(StudioManagedDir)', $managedDir)
    if (-not (Test-Path -LiteralPath $dependency -PathType Leaf)) {
        throw "Missing local build dependency: $dependency"
    }
}

& dotnet build $project -c Release -o $outputDir "-p:HS2GameRoot=$GameRoot"
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed. No plugin was installed.'
}
if (-not (Test-Path -LiteralPath $builtDll -PathType Leaf)) {
    throw "Build output is missing: $builtDll"
}
Write-Host "Built: $builtDll"

# Installation is opt-in; normal builds never write into the game directory.
if ($Install) {
    $running = @(Get-Process -Name 'HoneySelect2', 'HoneySelect2VR', 'StudioNEOV2' -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) {
        throw 'Save and close the game and Studio before installing. Build output is available above.'
    }
    $pluginDir = Join-Path $GameRoot 'BepInEx\plugins\Codex'
    $installedDll = Join-Path $pluginDir $dllName
    if (Test-Path -LiteralPath $installedDll -PathType Leaf) {
        $backupDir = Join-Path $GameRoot ('BepInEx\cache\HS2_FemaleForMale\PluginBackups\' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
        Copy-Item -LiteralPath $installedDll -Destination $backupDir
        Write-Host "Previous version backed up to: $backupDir"
    }
    New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    Copy-Item -LiteralPath $builtDll -Destination $installedDll -Force
    Write-Host "Installed: $installedDll"
}

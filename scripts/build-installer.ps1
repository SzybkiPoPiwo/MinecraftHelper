param(
    [string]$Version = "1.0.0",
    [ValidateSet("win-x64", "win-x86")]
    [string]$Rid = "win-x64",
    [bool]$SelfContained = $true,
    [switch]$SingleFile,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

function Resolve-IsccPath {
    if ($env:ISCC_PATH -and (Test-Path $env:ISCC_PATH)) {
        return $env:ISCC_PATH
    }

    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) {
            return $path
        }
    }

    $fromPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    throw "Nie znaleziono Inno Setup Compiler (ISCC.exe). Zainstaluj Inno Setup 6 lub ustaw zmienną ISCC_PATH."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "MinecraftHelper\MinecraftHelper.csproj"
$issPath = Join-Path $repoRoot "Installer\MinecraftHelper.iss"
$publishDir = Join-Path $repoRoot ("artifacts\publish\" + $Rid)
$installerOutputDir = Join-Path $repoRoot "artifacts\installer"

if ($Clean) {
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    if (Test-Path $installerOutputDir) { Remove-Item $installerOutputDir -Recurse -Force }
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutputDir -Force | Out-Null

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }
$publishSingleFileValue = if ($SingleFile) { "true" } else { "false" }
$includeNativeSelfExtractValue = if ($SingleFile) { "true" } else { "false" }
$includeAllContentSelfExtractValue = if ($SingleFile) { "true" } else { "false" }

Write-Host "Publikowanie aplikacji ($Rid, self-contained=$selfContainedValue, single-file=$publishSingleFileValue)..." -ForegroundColor Cyan
dotnet publish $projectPath `
    -c Release `
    -r $Rid `
    --self-contained $selfContainedValue `
    -p:PublishSingleFile=$publishSingleFileValue `
    -p:IncludeNativeLibrariesForSelfExtract=$includeNativeSelfExtractValue `
    -p:IncludeAllContentForSelfExtract=$includeAllContentSelfExtractValue `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

$publishedExe = Join-Path $publishDir "MinecraftHelper.exe"
if (Test-Path $publishedExe) {
    Write-Host "Sprawdź ręcznie przed instalatorem: $publishedExe" -ForegroundColor Yellow
}

$isccPath = Resolve-IsccPath
Write-Host "Budowanie instalatora przez ISCC: $isccPath" -ForegroundColor Cyan

& $isccPath `
    "/DAppVersion=$Version" `
    "/DPublishDir=$publishDir" `
    "/DInstallerOutputDir=$installerOutputDir" `
    $issPath

$installerFile = Join-Path $installerOutputDir ("MinecraftHelper-Setup-" + $Version + ".exe")
if (Test-Path $installerFile) {
    Write-Host "Gotowe: $installerFile" -ForegroundColor Green
} else {
    Write-Host "Instalator zbudowany. Sprawdź folder: $installerOutputDir" -ForegroundColor Yellow
}

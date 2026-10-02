param(
    [string]$Version = "1.1.8",
    [ValidateSet("win-x64", "win-x86")]
    [string]$Rid = "win-x64",
    [bool]$SelfContained = $true,
    [switch]$SingleFile,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

function Resolve-IsccPath {
    if (-not [string]::IsNullOrWhiteSpace($env:ISCC_PATH) -and [IO.File]::Exists($env:ISCC_PATH)) {
        return [IO.Path]::GetFullPath($env:ISCC_PATH)
    }

    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        [IO.Path]::Combine($env:LOCALAPPDATA, "Programs", "Inno Setup 6", "ISCC.exe")
    )

    foreach ($path in $candidates) {
        if ([IO.File]::Exists($path)) {
            return [IO.Path]::GetFullPath($path)
        }
    }

    foreach ($directory in ($env:PATH -split [IO.Path]::PathSeparator)) {
        if ([string]::IsNullOrWhiteSpace($directory)) {
            continue
        }

        $path = [IO.Path]::Combine($directory.Trim(), "ISCC.exe")
        if ([IO.File]::Exists($path)) {
            return [IO.Path]::GetFullPath($path)
        }
    }

    throw "Nie znaleziono Inno Setup Compiler (ISCC.exe). Zainstaluj Inno Setup 6 lub ustaw zmienna ISCC_PATH."
}

function Assert-ChildPath([string]$Parent, [string]$Child) {
    $parentPrefix = [IO.Path]::GetFullPath($Parent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $childPath = [IO.Path]::GetFullPath($Child)
    if (-not $childPath.StartsWith($parentPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Odmowa operacji poza katalogiem repozytorium: $childPath"
    }
}

$repoRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, ".."))
$projectPath = [IO.Path]::Combine($repoRoot, "MinecraftHelper", "MinecraftHelper.csproj")
$issPath = [IO.Path]::Combine($repoRoot, "Installer", "MinecraftHelper.iss")
$publishDir = [IO.Path]::Combine($repoRoot, "artifacts", "publish", $Rid)
$installerOutputDir = [IO.Path]::Combine($repoRoot, "artifacts", "installer")
Assert-ChildPath $repoRoot $publishDir
Assert-ChildPath $repoRoot $installerOutputDir

if ($Clean) {
    if ([IO.Directory]::Exists($publishDir)) { [IO.Directory]::Delete($publishDir, $true) }
    if ([IO.Directory]::Exists($installerOutputDir)) { [IO.Directory]::Delete($installerOutputDir, $true) }
}

[void][IO.Directory]::CreateDirectory($publishDir)
[void][IO.Directory]::CreateDirectory($installerOutputDir)

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }
$publishSingleFileValue = if ($SingleFile) { "true" } else { "false" }
$includeNativeSelfExtractValue = if ($SingleFile) { "true" } else { "false" }
$includeAllContentSelfExtractValue = if ($SingleFile) { "true" } else { "false" }

[Console]::WriteLine("Publikowanie aplikacji ($Rid, self-contained=$selfContainedValue, single-file=$publishSingleFileValue)...")
dotnet publish $projectPath `
    -c Release `
    -r $Rid `
    --self-contained $selfContainedValue `
    -p:Version=$Version `
    -p:AssemblyVersion="$Version.0" `
    -p:FileVersion="$Version.0" `
    -p:InformationalVersion=$Version `
    -p:PublishSingleFile=$publishSingleFileValue `
    -p:IncludeNativeLibrariesForSelfExtract=$includeNativeSelfExtractValue `
    -p:IncludeAllContentForSelfExtract=$includeAllContentSelfExtractValue `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish zakonczyl sie kodem $LASTEXITCODE."
}

$publishedExe = [IO.Path]::Combine($publishDir, "MinecraftHelper.exe")
if ([IO.File]::Exists($publishedExe)) {
    [Console]::WriteLine("Sprawdz recznie przed instalatorem: $publishedExe")
}

$isccPath = Resolve-IsccPath
[Console]::WriteLine("Budowanie instalatora przez ISCC: $isccPath")

& $isccPath `
    "/DAppVersion=$Version" `
    "/DPublishDir=$publishDir" `
    "/DInstallerOutputDir=$installerOutputDir" `
    $issPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC zakonczyl sie kodem $LASTEXITCODE."
}

$installerFile = [IO.Path]::Combine($installerOutputDir, "MinecraftHelper-Setup-" + $Version + ".exe")
if ([IO.File]::Exists($installerFile)) {
    [Console]::WriteLine("Gotowe: $installerFile")
} else {
    throw "ISCC zakonczyl prace bez oczekiwanego pliku: $installerFile"
}

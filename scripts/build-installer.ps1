param(
    [string]$Version = "2.0",
    [ValidateSet("win-x64")]
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

    throw "Nie znaleziono Inno Setup Compiler (ISCC.exe). Zainstaluj Inno Setup 6 albo ustaw ISCC_PATH."
}

function Assert-ChildPath([string]$Parent, [string]$Child) {
    $parentPrefix = [IO.Path]::GetFullPath($Parent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $childPath = [IO.Path]::GetFullPath($Child)
    if (-not $childPath.StartsWith($parentPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Odmowa operacji poza katalogiem repozytorium: $childPath"
    }
}

function Publish-App(
    [string]$ProjectPath,
    [string]$OutputDirectory,
    [string]$InformationalSuffix,
    [string]$ExpectedExe
) {
    [void][IO.Directory]::CreateDirectory($OutputDirectory)
    [Console]::WriteLine("Publikowanie $ExpectedExe...")

    $selfContainedValue = if ($SelfContained) { "true" } else { "false" }
    $singleFileValue = if ($SingleFile) { "true" } else { "false" }
    dotnet publish $ProjectPath `
        -c Release `
        -r $Rid `
        --self-contained $selfContainedValue `
        -p:Version=$Version `
        -p:AssemblyVersion="$Version.0" `
        -p:FileVersion="$Version.0" `
        -p:InformationalVersion="$Version-$InformationalSuffix" `
        -p:PublishSingleFile=$singleFileValue `
        -p:IncludeNativeLibrariesForSelfExtract=$singleFileValue `
        -p:IncludeAllContentForSelfExtract=$singleFileValue `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $OutputDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish $ExpectedExe failed with exit code $LASTEXITCODE."
    }

    $exePath = [IO.Path]::Combine($OutputDirectory, $ExpectedExe)
    if (-not [IO.File]::Exists($exePath)) {
        throw "Publish did not create the expected file: $exePath"
    }
    [Console]::WriteLine("Manual test target: $exePath")
}

$repoRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, ".."))
$autoClickerProject = [IO.Path]::Combine($repoRoot, "MinecraftHelper.Pvp", "MinecraftHelper.Pvp.csproj")
$minerProject = [IO.Path]::Combine($repoRoot, "MinecraftHelper", "MinecraftHelper.csproj")
$issPath = [IO.Path]::Combine($repoRoot, "Installer", "MinecraftHelper.iss")
$publishRoot = [IO.Path]::Combine($repoRoot, "artifacts", "publish")
$autoClickerPublishDir = [IO.Path]::Combine($publishRoot, "autoclicker", $Rid)
$minerPublishDir = [IO.Path]::Combine($publishRoot, "miner", $Rid)
$installerOutputDir = [IO.Path]::Combine($repoRoot, "artifacts", "installer")

Assert-ChildPath $repoRoot $publishRoot
Assert-ChildPath $repoRoot $installerOutputDir

if ($Clean) {
    if ([IO.Directory]::Exists($publishRoot)) { [IO.Directory]::Delete($publishRoot, $true) }
    if ([IO.Directory]::Exists($installerOutputDir)) { [IO.Directory]::Delete($installerOutputDir, $true) }
}

[void][IO.Directory]::CreateDirectory($installerOutputDir)
Publish-App $autoClickerProject $autoClickerPublishDir "autoclicker" "MinecraftHelper.AutoClicker.exe"
Publish-App $minerProject $minerPublishDir "miner" "MinecraftHelper.Miner.exe"

$isccPath = Resolve-IsccPath
[Console]::WriteLine("Building the combined installer with: $isccPath")
& $isccPath `
    "/DAppVersion=$Version" `
    "/DAutoClickerPublishDir=$autoClickerPublishDir" `
    "/DMinerPublishDir=$minerPublishDir" `
    "/DInstallerOutputDir=$installerOutputDir" `
    $issPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE."
}

$installerFile = [IO.Path]::Combine($installerOutputDir, "MinecraftHelper-Setup-$Version.exe")
if (-not [IO.File]::Exists($installerFile)) {
    throw "ISCC did not create the expected file: $installerFile"
}

$stream = [IO.File]::OpenRead($installerFile)
try {
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha256.ComputeHash($stream)
    }
    finally {
        $sha256.Dispose()
    }
}
finally {
    $stream.Dispose()
}
$hash = [BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant()
[Console]::WriteLine("Gotowe: $installerFile")
[Console]::WriteLine("SHA-256: $hash")

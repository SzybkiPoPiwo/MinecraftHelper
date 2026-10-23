param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
$repoRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, ".."))
$projects = @(
    "EditionSplit",
    "MinerIsolation",
    "MiningStop",
    "PvpMouseTracker",
    "PvpQuickActions",
    "AutoWater"
)

foreach ($name in $projects) {
    $project = [IO.Path]::Combine($repoRoot, "tests", $name, "$name.csproj")
    $arguments = @("run", "--project", $project, "-c", $Configuration)
    if ($NoRestore) { $arguments += "--no-restore" }
    [Console]::WriteLine("Test: $name")
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Test $name failed with exit code $LASTEXITCODE."
    }
}
[Console]::WriteLine("Wszystkie zestawy testow zakonczone poprawnie.")

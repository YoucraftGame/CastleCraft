[CmdletBinding()]
param(
    [string]$ClientVersionDir = 'D:\新版客户端\.minecraft\versions\Fabulously Optimized',
    [switch]$Reinstall
)
$ErrorActionPreference = 'Stop'
$projectDir = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$sourceJar = Join-Path $projectDir 'build\hunter-choice-release-source\build\skycraft-0.1.2.jar'
$expectedHash = '8fe0dd35396840e9d03ade6e7d6b2d0323944eb8ee054fd452f2e6c6540b20ff'
$clientDir = (Resolve-Path -LiteralPath $ClientVersionDir).Path
$modsDir = (Resolve-Path -LiteralPath (Join-Path $clientDir 'mods')).Path
$descriptor = Join-Path $clientDir ((Split-Path -Leaf $clientDir) + '.json')
if (-not (Test-Path -LiteralPath $descriptor -PathType Leaf)) { throw 'Selected PCL instance has no version descriptor.' }
$version = Get-Content -LiteralPath $descriptor -Raw -Encoding UTF8 | ConvertFrom-Json
$mcVersion = $version.clientVersion
if (-not $mcVersion) { $mcVersion = $version.inheritsFrom }
if (-not $mcVersion) { $mcVersion = $version.id }
if ($mcVersion -ne '26.3') { throw "This repair requires Minecraft 26.3. Selected instance declares: $mcVersion" }
if ($version.mainClass -notlike 'net.fabricmc.loader*') { throw 'Selected instance is not a Fabric client.' }
if ($version.javaVersion -and $version.javaVersion.majorVersion -lt 25) { throw 'Selected instance requires an older Java runtime. This repair requires Java 25.' }
$loader = @($version.libraries | Where-Object { $_.name -like 'net.fabricmc:fabric-loader:*' })
if ($loader.Count -eq 0) { throw 'Fabric Loader dependency missing.' }
$loaderVersion = ($loader[0].name -split ':')[-1]
if ($loaderVersion -notmatch '^\d+\.\d+\.\d+$' -or [version]$loaderVersion -lt [version]'0.19.5') { throw "Fabric Loader >=0.19.5 required. Selected: $loaderVersion" }
$targetJar = [IO.Path]::GetFullPath((Join-Path $modsDir 'skycraft-0.1.2.jar'))
if ((Split-Path -Parent $targetJar) -ne $modsDir) { throw 'Invalid destination path.' }
function Get-Digest([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Get-OtherMods {
    $result = [ordered]@{}
    foreach ($item in (Get-ChildItem -LiteralPath $modsDir -Filter '*.jar' -File | Sort-Object Name)) {
        if ($item.Name -ne 'skycraft-0.1.2.jar') { $result[$item.Name] = Get-Digest $item.FullName }
    }
    return $result
}
function Get-Settings {
    $result = [ordered]@{}
    foreach ($relative in @('options.txt', 'config\lithium.properties')) {
        $path = Join-Path $clientDir $relative
        if (Test-Path -LiteralPath $path -PathType Leaf) { $result[$relative] = Get-Digest $path }
    }
    return $result
}
function Test-SameMap($A, $B) {
    if ($A.Count -ne $B.Count) { return $false }
    foreach ($key in $A.Keys) { if (-not $B.Contains($key) -or $A[$key] -ne $B[$key]) { return $false } }
    return $true
}
if (-not (Test-Path -LiteralPath $sourceJar -PathType Leaf)) { throw "Missing built bridge: $sourceJar" }
if ((Get-Digest $sourceJar) -ne $expectedHash) { throw 'Built bridge differs from the reviewed repair. Build/deployment manifest must be updated first.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($sourceJar)
try {
    $entry = $zip.GetEntry('fabric.mod.json')
    if ($null -eq $entry) { throw 'fabric.mod.json missing.' }
    $reader = New-Object IO.StreamReader($entry.Open())
    try { $metadata = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($metadata.id -ne 'skycraft' -or $metadata.version -ne '0.1.2' -or $metadata.depends.minecraft -ne '~26.3') { throw 'Unexpected bridge identity or Minecraft version.' }
    foreach ($name in @('CastlePoisonMixin', 'CastleEnchantmentPredicateMixin')) {
        if ($null -eq $zip.GetEntry("dev/skycraft/mixin/$name.class")) { throw "Missing repaired class: $name" }
    }
} finally { $zip.Dispose() }
$beforeHash = $null
if (Test-Path -LiteralPath $targetJar -PathType Leaf) { $beforeHash = Get-Digest $targetJar }
$modsBefore = Get-OtherMods
$settingsBefore = Get-Settings
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$backupDir = $null
$action = 'verified_already_installed'
if ($Reinstall -or $beforeHash -ne $expectedHash) {
    $running = @(Get-Process -Name java,javaw -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) { throw ('Close Minecraft normally before installation. No process was closed. Java PIDs: ' + (($running | ForEach-Object Id) -join ', ')) }
    $backupDir = Join-Path $projectDir "backups\pcl-equipment-$stamp"
    New-Item -ItemType Directory -Path $backupDir | Out-Null
    if ($null -ne $beforeHash) { Copy-Item -LiteralPath $targetJar -Destination (Join-Path $backupDir 'skycraft-0.1.2.jar') }
    foreach ($relative in $settingsBefore.Keys) {
        Copy-Item -LiteralPath (Join-Path $clientDir $relative) -Destination (Join-Path $backupDir ([IO.Path]::GetFileName($relative)))
    }
    $pre = [ordered]@{ client=$clientDir; source=$sourceJar; target=$targetJar; before_sha256=$beforeHash; after_sha256=$expectedHash; other_mods=$modsBefore; settings=$settingsBefore }
    $pre | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $backupDir 'before-install.json') -Encoding UTF8
    $running = @(Get-Process -Name java,javaw -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) { throw 'Java started during preparation. Installation stopped before writing.' }
    try {
        Copy-Item -LiteralPath $sourceJar -Destination $targetJar -Force
        if ((Get-Digest $targetJar) -ne $expectedHash) { throw 'Installed bridge hash mismatch.' }
        if (-not (Test-SameMap $modsBefore (Get-OtherMods))) { throw 'Other Minecraft mods changed during installation.' }
        if (-not (Test-SameMap $settingsBefore (Get-Settings))) { throw 'Minecraft settings changed during installation.' }
        $action = 'installed'
    } catch {
        $failure = $_
        if ($null -ne $beforeHash) {
            Copy-Item -LiteralPath (Join-Path $backupDir 'skycraft-0.1.2.jar') -Destination $targetJar -Force
            if ((Get-Digest $targetJar) -ne $beforeHash) { throw 'Rollback verification failed.' }
        } elseif (Test-Path -LiteralPath $targetJar -PathType Leaf) {
            if ((Split-Path -Parent ([IO.Path]::GetFullPath($targetJar))) -ne $modsDir) { throw 'Rollback destination is outside selected mods directory.' }
            Remove-Item -LiteralPath $targetJar -Force
        }
        throw $failure
    }
}
$proof = [ordered]@{
    checked_at=(Get-Date).ToString('o'); action=$action; client=$clientDir; installed=$targetJar
    sha256=(Get-Digest $targetJar); expected_sha256=$expectedHash; backup=$backupDir
    other_mods_unchanged=(Test-SameMap $modsBefore (Get-OtherMods)); other_mod_count=$modsBefore.Count
    settings_unchanged=(Test-SameMap $settingsBefore (Get-Settings)); settings_count=$settingsBefore.Count
    gameplay_verified=$false; host_not_modified=$true; game_not_launched=$true
}
$evidence = Join-Path $projectDir 'build\pcl-equipment-deployment.json'
$proof | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidence -Encoding UTF8
if ($backupDir) { $proof | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $backupDir 'verification.json') -Encoding UTF8 }
Write-Host "PCL DEPLOY PASS: $action"
Write-Host "Client: $clientDir"
Write-Host "Bridge SHA256: $($proof.sha256)"
Write-Host "Unchanged other mods: $($proof.other_mod_count); settings: $($proof.settings_count)"
Write-Host "Evidence: $evidence"
if ($backupDir) { Write-Host "Backup: $backupDir" }
Write-Host 'No game launched. Gameplay verification is pending.'

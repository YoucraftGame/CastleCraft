[CmdletBinding()]
param([switch]$Apply)
$ErrorActionPreference='Stop'
$checkpointDir=$PSScriptRoot
$manifestPath=Join-Path $checkpointDir 'checkpoint.json'
if(-not(Test-Path -LiteralPath $manifestPath)){throw 'Run the Restore-Artifacts.ps1 inside a saved checkpoint.'}
$checkpoint=Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$checksums=Get-Content -LiteralPath (Join-Path $checkpointDir 'checksums.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($property in $checksums.PSObject.Properties){
 $file=Join-Path $checkpointDir $property.Name
 if(-not(Test-Path -LiteralPath $file -PathType Leaf) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $property.Value){throw "Checkpoint integrity failed: $($property.Name)"}
}
$projectDir=(Resolve-Path -LiteralPath (Join-Path $checkpointDir '..\..')).Path
$gameDir=(Resolve-Path -LiteralPath (Join-Path $projectDir '..')).Path
$hostTarget=Join-Path $gameDir 'BepInEx\plugins\CastleCraft\CastleCraft.Passthrough.dll'
$guestTarget='D:\新版客户端\.minecraft\versions\Fabulously Optimized\mods\skycraft-0.1.2.jar'
foreach($artifact in $checkpoint.artifacts){
 $resolved=[IO.Path]::GetFullPath($artifact.installed)
 if($artifact.backup_name -eq 'host.dll'){$allowed=[IO.Path]::GetFullPath($hostTarget)}
 elseif($artifact.backup_name -eq 'bridge.jar'){$allowed=[IO.Path]::GetFullPath($guestTarget)}
 elseif($artifact.backup_name -eq 'gpu-native.dll'){$allowed=[IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $hostTarget) 'CastleCraft.GpuBridge.dll'))}
 else{throw 'Unknown artifact; no restore attempted.'}
 if($resolved -ne $allowed){throw 'Checkpoint target differs from approved game/PCL directory.'}
}
Write-Host "Checkpoint verified: $($checkpoint.id)"
Write-Host "Restore build: $($checkpoint.build)"
Write-Host 'Only checkpoint-listed host DLL, PCL bridge JAR and optional GPU native DLL would be restored. Source, saves and user settings are untouched.'
if(-not $Apply){Write-Host 'Preview only. To explicitly restore, close both games normally and run this command with -Apply.';return}
$running=@(Get-Process -Name Lost_Castle,java,javaw -ErrorAction SilentlyContinue)
if($running.Count -gt 0){throw 'Close games normally. No process was stopped.'}
$backupDir=Join-Path $projectDir ('backups\checkpoint-rollback-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backupDir | Out-Null
$latest=Join-Path $projectDir 'build\latest-deployment.json'
Copy-Item -LiteralPath $latest -Destination (Join-Path $backupDir 'previous-deployment.json')
$oldHashes=@{}
foreach($artifact in $checkpoint.artifacts){
 if(-not(Test-Path -LiteralPath $artifact.installed -PathType Leaf)){throw 'Current installed artifact missing; no rollback writes attempted.'}
 $oldHashes[$artifact.backup_name]=(Get-FileHash -LiteralPath $artifact.installed -Algorithm SHA256).Hash
 Copy-Item -LiteralPath $artifact.installed -Destination (Join-Path $backupDir $artifact.backup_name)
}
try{
 if(@(Get-Process -Name Lost_Castle,java,javaw -ErrorAction SilentlyContinue).Count -gt 0){throw 'Game started during preparation.'}
 foreach($artifact in $checkpoint.artifacts){
  $source=Join-Path $checkpointDir ('artifacts\'+$artifact.backup_name)
  Copy-Item -LiteralPath $source -Destination $artifact.installed -Force
  if((Get-FileHash -LiteralPath $artifact.installed -Algorithm SHA256).Hash.ToLowerInvariant() -ne $artifact.after_sha256){throw 'Restored artifact hash mismatch.'}
 }
 $restored=Get-Content -LiteralPath (Join-Path $checkpointDir 'evidence\installed-deployment.json') -Raw -Encoding UTF8 | ConvertFrom-Json
 $restored | Add-Member -NotePropertyName restored_from_checkpoint -NotePropertyValue $checkpoint.id -Force
 $restored | Add-Member -NotePropertyName restored_at -NotePropertyValue (Get-Date).ToString('o') -Force
 $restored | Add-Member -NotePropertyName restore_backup -NotePropertyValue $backupDir -Force
 $restored | Add-Member -NotePropertyName current_source_matches_restored_build -NotePropertyValue $false -Force
 $restored | Add-Member -NotePropertyName gameplay_verified -NotePropertyValue $false -Force
 $restored | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $latest -Encoding UTF8
 $restored | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $backupDir 'restored-deployment.json') -Encoding UTF8
}catch{
 $failure=$_
 foreach($artifact in $checkpoint.artifacts){
  Copy-Item -LiteralPath (Join-Path $backupDir $artifact.backup_name) -Destination $artifact.installed -Force
  if((Get-FileHash -LiteralPath $artifact.installed -Algorithm SHA256).Hash -ne $oldHashes[$artifact.backup_name]){throw 'Rollback of failed restore could not be verified.'}
 }
 Copy-Item -LiteralPath (Join-Path $backupDir 'previous-deployment.json') -Destination $latest -Force
 throw $failure
}
Write-Host "RESTORE PASS: $($checkpoint.build)"
Write-Host "Previous installed build backed up: $backupDir"
Write-Host 'Source and settings were not restored. Gameplay acceptance is still pending.'

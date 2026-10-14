param([Parameter(Mandatory=$true)][string]$LostCastlePath)
$ErrorActionPreference='Stop'
$taskGame=(Resolve-Path -LiteralPath $LostCastlePath).Path
if(!(Test-Path -LiteralPath (Join-Path $taskGame 'Lost_Castle.exe'))){throw 'Choose the directory containing Lost_Castle.exe.'}
if(Get-Process Lost_Castle -ErrorAction SilentlyContinue){throw 'Exit Lost Castle before installation.'}
$taskCore=Join-Path $taskGame 'BepInEx/core/BepInEx.dll'
if(Test-Path -LiteralPath $taskCore){if([Reflection.AssemblyName]::GetAssemblyName($taskCore).Version.Major -ne 5){throw 'This package requires BepInEx 5 Mono.'}}
else{Expand-Archive -LiteralPath (Join-Path $PSScriptRoot 'Dependencies/BepInEx-5.4.23.5.zip') -DestinationPath $taskGame}
$taskRecords=Join-Path $env:LOCALAPPDATA 'CastleCraft/install-backups'
New-Item -ItemType Directory -Path $taskRecords -Force | Out-Null
$taskRecord=Join-Path $taskRecords 'latest-install.json'
if(Test-Path -LiteralPath $taskRecord){$taskOld=Get-Content -LiteralPath $taskRecord -Raw | ConvertFrom-Json;if($taskOld.game -eq $taskGame){throw 'An install record already exists. Run Uninstall.ps1 to restore it before installing this package.'}}
$taskBackup=Join-Path $taskRecords ([Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $taskBackup | Out-Null
$taskEntries=@()
$taskCopies=@(@{source='LostCastle/BepInEx/plugins/CastleCraft/CastleCraft.Passthrough.dll';target='BepInEx/plugins/CastleCraft/CastleCraft.Passthrough.dll'})
foreach($taskFile in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Launcher') -File){$taskCopies+=@{source=('Launcher/'+$taskFile.Name);target=('CastleCraftRuntime/'+$taskFile.Name)}}
foreach($taskCopy in $taskCopies){
 $taskDestination=Join-Path $taskGame $taskCopy.target
 $taskSaved=Join-Path $taskBackup $taskCopy.target
 $taskExisting=Test-Path -LiteralPath $taskDestination
 if($taskExisting){New-Item -ItemType Directory -Path (Split-Path -Parent $taskSaved) -Force | Out-Null;Copy-Item -LiteralPath $taskDestination -Destination $taskSaved}
 $taskEntries+=@{path=$taskCopy.target;existed=$taskExisting;backup=$taskSaved}
 # Record before each write so a partial installation can also be rolled back.
 @{game=$taskGame;backup=$taskBackup;files=$taskEntries} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $taskRecord -Encoding UTF8
 New-Item -ItemType Directory -Path (Split-Path -Parent $taskDestination) -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskCopy.source) -Destination $taskDestination -Force
}
Write-Output "Installed CastleCraft development build. Backup: $taskBackup"

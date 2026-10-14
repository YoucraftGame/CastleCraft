param([Parameter(Mandatory=$true)][string]$LostCastlePath)
$ErrorActionPreference='Stop'
$taskGame=(Resolve-Path -LiteralPath $LostCastlePath).Path
if(Get-Process Lost_Castle -ErrorAction SilentlyContinue){throw 'Exit Lost Castle before uninstalling.'}
$taskRecord=Join-Path $env:LOCALAPPDATA 'CastleCraft/install-backups/latest-install.json'
if(!(Test-Path -LiteralPath $taskRecord)){throw 'No installer record. For manual installation, remove only the CastleCraft plugin and your bridge jar.'}
$taskInstall=Get-Content -LiteralPath $taskRecord -Raw | ConvertFrom-Json
if($taskInstall.game -ne $taskGame){throw 'The installation record belongs to another game directory.'}
foreach($taskFile in $taskInstall.files){
 $taskTarget=[IO.Path]::GetFullPath((Join-Path $taskGame $taskFile.path))
 if(!$taskTarget.StartsWith($taskGame.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid destination in installation record.'}
 if($taskFile.existed){Copy-Item -LiteralPath $taskFile.backup -Destination $taskTarget -Force}
 elseif(Test-Path -LiteralPath $taskTarget){Remove-Item -LiteralPath $taskTarget}
}
Move-Item -LiteralPath $taskRecord -Destination (Join-Path $taskInstall.backup 'uninstalled-record.json')
Write-Output 'CastleCraft files restored/removed. Loaders, other mods, configuration and saves were retained.'

param([Parameter(Mandatory=$true)][string]$LostCastlePath,[Parameter(Mandatory=$true)][string]$MinecraftPath)
$ErrorActionPreference='Stop'
$taskGame=(Resolve-Path -LiteralPath $LostCastlePath).Path
$taskMinecraft=(Resolve-Path -LiteralPath $MinecraftPath).Path
$taskLauncher=Join-Path $PSScriptRoot 'Launcher/CastleCraftLauncher.exe'
if(!(Test-Path -LiteralPath (Join-Path $taskGame 'BepInEx/plugins/CastleCraft/CastleCraft.Passthrough.dll'))){throw 'Install the CastleCraft host plugin first.'}
if(Get-Process Lost_Castle -ErrorAction SilentlyContinue){throw 'An existing castle process is running; close that session before starting another.'}
Start-Process -FilePath $taskLauncher -ArgumentList @('--host',('"'+$taskGame+'"'),'--minecraft',('"'+$taskMinecraft+'"')) -WindowStyle Hidden

param([Parameter(Mandatory=$true)][string]$LostCastlePath)
$ErrorActionPreference='Stop'
$taskGame=(Resolve-Path -LiteralPath $LostCastlePath).Path
if(!(Test-Path -LiteralPath (Join-Path $taskGame 'Lost_Castle.exe'))){throw 'Choose the directory containing Lost_Castle.exe.'}
if(!(Test-Path -LiteralPath (Join-Path $taskGame 'BepInEx/plugins/CastleCraft/CastleCraft.Passthrough.dll'))){throw 'Install the CastleCraft host plugin first.'}
if(Get-Process Lost_Castle -ErrorAction SilentlyContinue){throw 'Close the existing castle session first.'}
$taskRuntime=Join-Path $env:LOCALAPPDATA 'CastleCraft'
$taskSession=Join-Path $taskRuntime 'sessions/manual'
New-Item -ItemType Directory -Path $taskSession -Force | Out-Null
Set-Content -LiteralPath (Join-Path $taskRuntime 'active-session.txt') -Value $taskSession -Encoding UTF8
$taskPrevious=$env:CASTLECRAFT_SESSION
try{
 $env:CASTLECRAFT_SESSION=$taskSession
 $taskProcess=Start-Process -FilePath (Join-Path $taskGame 'Lost_Castle.exe') -WorkingDirectory $taskGame -ArgumentList '-screen-fullscreen','0','-screen-width','960','-screen-height','600' -WindowStyle Hidden -PassThru
 Set-Content -LiteralPath (Join-Path $taskSession 'castle.pid') -Value $taskProcess.Id
}finally{$env:CASTLECRAFT_SESSION=$taskPrevious}
Write-Output 'Host mapping: Local\CastleCraft_v3_manual. Start the matching Fabric instance in your launcher.'

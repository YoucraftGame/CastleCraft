param([Parameter(Mandatory=$true)][string]$MinecraftInstancePath)
$ErrorActionPreference='Stop'
$taskInstance=(Resolve-Path -LiteralPath $MinecraftInstancePath).Path
$taskMods=Join-Path $taskInstance 'mods'
New-Item -ItemType Directory -Path $taskMods -Force | Out-Null
$taskBackup=Join-Path $env:LOCALAPPDATA ('CastleCraft/client-install-backups/'+[Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $taskBackup | Out-Null
$taskJar=Join-Path $taskMods 'skycraft-0.1.2.jar'
if(Test-Path -LiteralPath $taskJar){Copy-Item -LiteralPath $taskJar -Destination (Join-Path $taskBackup 'previous-skycraft.jar')}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Minecraft/mods/skycraft-0.1.2.jar') -Destination $taskJar -Force
if(!(Get-ChildItem -LiteralPath $taskMods -Filter 'fabric-api*.jar' -File)){Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Minecraft/mods/fabric-api-0.161.0+26.3.jar') -Destination $taskMods}
Write-Output "Installed bridge into $taskMods. Existing settings and mods retained. Restart this instance from your launcher. Backup: $taskBackup"

[CmdletBinding()]
param([switch]$Apply)
$ErrorActionPreference='Stop'
$projectDir=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$icd='C:\Windows\System32\DriverStore\FileRepository\nv_dispi.inf_amd64_adc55ecfca814224\nv-vk64.json'
$driver=Join-Path (Split-Path -Parent $icd) 'nvoglv64.dll'
if(-not(Test-Path -LiteralPath $icd) -or -not(Test-Path -LiteralPath $driver)){throw 'Reviewed installed NVIDIA driver changed; no registry writes.'}
$signature=Get-AuthenticodeSignature -LiteralPath $driver
if($signature.Status -ne 'Valid'){throw 'Installed driver signature invalid; no registry writes.'}
$base=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,[Microsoft.Win32.RegistryView]::Registry64)
$sub='SOFTWARE\Khronos\Vulkan\Drivers'
$key=$base.OpenSubKey($sub,$false)
$exists=$null -ne $key
$hadValue=$exists -and @($key.GetValueNames()) -contains $icd
$prior=if($hadValue){$key.GetValue($icd)}else{$null}
$kind=if($hadValue){$key.GetValueKind($icd).ToString()}else{$null}
if($key){$key.Dispose()}
$plan=@{key='HKLM64\'+$sub;value=$icd;newType='DWord';newValue=0;keyExisted=$exists;valueExisted=$hadValue;oldValue=$prior;oldKind=$kind;icdSha256=(Get-FileHash -LiteralPath $icd -Algorithm SHA256).Hash;driverSha256=(Get-FileHash -LiteralPath $driver -Algorithm SHA256).Hash;scope='One installed signed NVIDIA ICD registration; no driver replacement, other values untouched';applied=$false}
$planPath=Join-Path $projectDir 'build\vulkan-registration-plan.json'
$plan | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $planPath -Encoding UTF8
if(-not $Apply){$base.Dispose();Write-Host 'PREPARED ONLY: existing signed NVIDIA Vulkan ICD will be registered as DWORD0. No registry change.';return}
if(@(Get-Process -Name Lost_Castle,java,javaw -ErrorAction SilentlyContinue).Count -gt 0){throw 'Exit games normally first; no registry change.'}
$backup=Join-Path $projectDir ('backups\vulkan-registration-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backup | Out-Null
$plan | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $backup 'before.json') -Encoding UTF8
$key=$base.CreateSubKey($sub)
try{
 $key.SetValue($icd,0,[Microsoft.Win32.RegistryValueKind]::DWord)
 if($key.GetValue($icd) -ne 0){throw 'Registration readback failed.'}
 $plan.applied=$true;$plan.backup=$backup
 $plan | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $backup 'applied.json') -Encoding UTF8
 $plan | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $planPath -Encoding UTF8
 Write-Host 'REGISTRATION PASS: one existing signed NVIDIA driver registered. Vulkan runtime verification still required.'
}catch{
 if($hadValue){$key.SetValue($icd,$prior,[Microsoft.Win32.RegistryValueKind]::$kind)}else{$key.DeleteValue($icd,$false)}
 throw
}finally{$key.Dispose();$base.Dispose()}

# Run once as administrator. Installs signed PawnIO and starts only the RGB backend with elevation.
$ErrorActionPreference = 'Stop'
$rgbPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (!$rgbPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this script as administrator to install PawnIO and register the RAM lighting backend.' }
Start-Transcript -Path (Join-Path $PSScriptRoot 'build\ram-setup.log') -Force | Out-Null
$rgbBackend = Join-Path $PSScriptRoot 'dist\backend\OpenRGB.exe'
$rgbInstaller = Join-Path $PSScriptRoot 'build\PawnIO_setup.exe'
if (!(Test-Path -LiteralPath $rgbBackend)) { throw 'Build RGBABY first so dist\backend\OpenRGB.exe exists.' }
if (!(Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Services\PawnIO')) {
    if (!(Test-Path -LiteralPath $rgbInstaller)) { throw 'Download the official signed installer from https://pawnio.eu to build\PawnIO_setup.exe first.' }
    $rgbSignature = Get-AuthenticodeSignature -LiteralPath $rgbInstaller
    if ($rgbSignature.Status -ne 'Valid' -or $rgbSignature.SignerCertificate.Subject -notmatch 'CN=namazso.eu(?:,|$)') { throw 'PawnIO installer signature is not valid or is not from the expected publisher.' }
    $rgbInstall = Start-Process -FilePath $rgbInstaller -ArgumentList '-install -silent' -WindowStyle Hidden -PassThru
    $rgbInstall.WaitForExit()
    if ($rgbInstall.ExitCode -eq 3010) { throw 'PawnIO requires a Windows restart. Restart when convenient, then run this setup again.' }
    if ($rgbInstall.ExitCode -ne 0) { throw "PawnIO installation failed: $($rgbInstall.ExitCode)" }
}
$rgbIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$rgbTaskName = 'RGBABY OpenRGB ' + $rgbIdentity.User.Value
$rgbArguments = '--server --server-host 127.0.0.1 --server-port 6742 --startminimized --config "' + (Join-Path (Split-Path $rgbBackend) 'rgbaby-config') + '"'
$rgbAction = New-ScheduledTaskAction -Execute $rgbBackend -Argument $rgbArguments -WorkingDirectory (Split-Path $rgbBackend)
$rgbTrigger = New-ScheduledTaskTrigger -AtLogOn -User $rgbIdentity.Name
$rgbTaskPrincipal = New-ScheduledTaskPrincipal -UserId $rgbIdentity.Name -LogonType Interactive -RunLevel Highest
$rgbTaskSettings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew
$rgbExisting = Get-ScheduledTask -TaskName $rgbTaskName -ErrorAction SilentlyContinue
if ($rgbExisting -and ($rgbExisting.Actions.Execute -ne $rgbBackend)) { throw 'A different backend uses this task name; no task was replaced.' }
Register-ScheduledTask -TaskName $rgbTaskName -Action $rgbAction -Trigger $rgbTrigger -Principal $rgbTaskPrincipal -Settings $rgbTaskSettings -Description 'RGBABY local OpenRGB engine with PawnIO access for Corsair RAM.' -Force | Out-Null
# Stop only our own portable backend, so the new process can acquire RAM and the local port.
foreach ($rgbProcess in (Get-Process -Name OpenRGB -ErrorAction SilentlyContinue)) {
    if ($rgbProcess.Path -eq $rgbBackend) { $rgbProcess.Kill(); $rgbProcess.WaitForExit() }
}
Start-ScheduledTask -TaskName $rgbTaskName
Write-Output 'RAM backend configured for this user at sign-in. Open RGBABY and Rescan, then choose and save the RAM color.'
Stop-Transcript | Out-Null

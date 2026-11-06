param([string]$AssemblyPath=(Join-Path (Split-Path $PSScriptRoot -Parent) 'dist\RGBABY.exe'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom($AssemblyPath)
$type=$assembly.GetType('Rgbaby.MainForm')
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$path=Join-Path (Split-Path $PSScriptRoot -Parent) ('build\corrupt-profile-'+[guid]::NewGuid()+'.json')
$form=$null
try {
    [IO.File]::WriteAllText($path,'{broken-but-preserve-this')
    $before=[IO.File]::ReadAllBytes($path)
    $form=[Activator]::CreateInstance($type,[object[]]@($false,$false,[string]$path))
    $event=[Windows.Forms.FormClosingEventArgs]::new([Windows.Forms.CloseReason]::UserClosing,$false)
    $type.GetMethod('OnFormClosing',$flags).Invoke($form,[object[]]@($event.PSObject.BaseObject)) | Out-Null
    if([Convert]::ToBase64String($before) -ne [Convert]::ToBase64String([IO.File]::ReadAllBytes($path))){throw 'Corrupt profile was overwritten on close'}
    $form.Dispose();$form=$null
    'PASS corrupt profile survives ordinary close without hardware work'
    $directory=Join-Path (Split-Path $PSScriptRoot -Parent) 'build'
    $form=[Activator]::CreateInstance($type,[object[]]@($false,$false,[string]$directory))
    $saved=$type.GetMethod('SaveProfile',$flags).Invoke($form,@())
    if($saved -or [string]::IsNullOrEmpty($type.GetField('lastSaveError',$flags).GetValue($form))){throw 'Save error was not surfaced'}
    $log=$type.GetField('log',$flags).GetValue($form)
    if($log.Text -notmatch 'NOT saved'){throw 'Save failure missing from log'}
    'PASS save failure returns failure and exposes actual error for close-time notice'
} finally {if($null -ne $form){$form.Dispose()};if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}

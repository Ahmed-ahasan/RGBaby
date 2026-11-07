param([string]$AssemblyPath=(Join-Path (Split-Path $PSScriptRoot -Parent) 'dist\RGBABY.exe'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom($AssemblyPath)
$type=$assembly.GetType('Rgbaby.TrayApplicationContext')
if($null -eq $type){throw 'RGBABY must have a tray lifetime that starts without showing the console'}
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$form=[Activator]::CreateInstance($assembly.GetType('Rgbaby.MainForm'),[object[]]@($true))
$context=$null
try {
    $context=[Activator]::CreateInstance($type,[object[]]@($form))
    [Windows.Forms.Application]::DoEvents()
    $tray=$type.GetField('tray',$flags).GetValue($context)
    if($form.Visible -or $form.ShowInTaskbar -or -not $form.IsHandleCreated -or -not $tray.Visible){throw 'Launch must have a tray icon and hidden window handle, without a taskbar window'}
    'PASS launch stays hidden with a live window handle for wake notifications'
    $click=[Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,0,0,0)
    $tray.GetType().GetMethod('OnMouseClick',$flags).Invoke($tray,[object[]]@($click.PSObject.BaseObject)) | Out-Null
    [Windows.Forms.Application]::DoEvents()
    if(-not $form.Visible -or -not $form.ShowInTaskbar){throw 'Single tray click must open the console'}
    $strips=$form.GetType().GetField('strips',$flags).GetValue($form)
    $strips[0].Intensity.Value=37
    $form.Close();[Windows.Forms.Application]::DoEvents()
    if($form.Visible -or $form.IsDisposed -or $form.ShowInTaskbar -or -not $tray.Visible){throw 'Close must hide to tray without disposing the console'}
    $tray.ContextMenuStrip.Items[0].PerformClick();[Windows.Forms.Application]::DoEvents()
    if(-not $form.Visible -or $strips[0].Intensity.Value -ne 37){throw 'Open menu must restore the same staged controls'}
    'PASS tray click and Open reopen the console; Close preserves staged controls'
    $form.WindowState=[Windows.Forms.FormWindowState]::Maximized
    $form.WindowState=[Windows.Forms.FormWindowState]::Minimized
    [Windows.Forms.Application]::DoEvents()
    if($form.Visible -or $form.ShowInTaskbar){throw 'Minimize must hide to tray'}
    $tray.ContextMenuStrip.Items[0].PerformClick();[Windows.Forms.Application]::DoEvents()
    if(-not $form.Visible -or $form.WindowState -ne [Windows.Forms.FormWindowState]::Maximized){throw 'Reopen must restore the pre-minimize window state'}
    'PASS minimize hides to tray and restores maximized placement'
    $tray.ContextMenuStrip.Items[2].PerformClick();[Windows.Forms.Application]::DoEvents()
    if(-not $form.IsDisposed -or $tray.Visible){throw 'Exit must dispose the console and remove the tray icon'}
    'PASS Exit shuts down and removes the tray icon'
} finally {if($null -ne $context){$context.Dispose()};$form.Dispose()}

# Exercise exit before the console has ever been opened, including the path
# that waits for an in-flight lighting update to finish.
$form=[Activator]::CreateInstance($assembly.GetType('Rgbaby.MainForm'),[object[]]@($true,$true))
$context=[Activator]::CreateInstance($type,[object[]]@($form))
try {
    $tray=$type.GetField('tray',$flags).GetValue($context)
    $form.GetType().GetField('applying',$flags).SetValue($form,$true)
    $tray.ContextMenuStrip.Items[2].PerformClick()
    if($form.IsDisposed -or -not $tray.Visible -or -not $form.GetType().GetField('closeAfterApply',$flags).GetValue($form)){throw 'Exit must wait for an active apply, even if the console was never opened'}
    $form.GetType().GetField('applying',$flags).SetValue($form,$false)
    $form.Close()
    if(-not $form.IsDisposed -or $tray.Visible){throw 'Deferred Exit must finish without requiring the console to be shown'}
    'PASS hidden Exit waits for active writes and then shuts down'
} finally {$context.Dispose();$form.Dispose()}

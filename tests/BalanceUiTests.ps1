param([string]$AssemblyPath=(Join-Path (Split-Path $PSScriptRoot -Parent) 'dist\RGBABY.exe'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom($AssemblyPath)
$form=[Activator]::CreateInstance($assembly.GetType('Rgbaby.MainForm'),[object[]]@($true))
try {
    $form.Opacity=0;$form.ShowInTaskbar=$false;$form.Show();[Windows.Forms.Application]::DoEvents()
    $strips=$form.GetType().GetField('strips',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($form)
    if($null -eq $strips[1].BalanceButton -or $null -eq $strips[2].BalanceButton){throw 'Motherboard and mouse balance controls are missing'}
    $strips[1].Intensity.Value=67;$strips[2].Intensity.Value=39
    $timer=New-Object Windows.Forms.Timer
    $timer.Interval=100
    $timer.Add_Tick({
        $dialog=@([Windows.Forms.Application]::OpenForms | Where-Object {$_.GetType().Name -eq 'BalanceDialog'})[0]
        if($null -ne $dialog){$timer.Stop();$dialog.Green.Value=70;$dialog.AcceptButton.PerformClick()}
    })
    $timer.Start();$strips[1].BalanceButton.PerformClick();$timer.Dispose()
    if($strips[1].State.GreenBalance -ne 70 -or $strips[2].State.GreenBalance -ne 70 -or $strips[0].State.GreenBalance -ne 100){throw 'Balance leaked between devices or was not staged'}
    if($strips[1].State.Brightness -ne 67 -or $strips[2].State.Brightness -ne 39){throw 'Balance changed intensity'}
    $timer=New-Object Windows.Forms.Timer;$timer.Interval=100
    $timer.Add_Tick({
        $dialog=@([Windows.Forms.Application]::OpenForms | Where-Object {$_.GetType().Name -eq 'BalanceDialog'})[0]
        if($null -ne $dialog){$timer.Stop();$dialog.Green.Value=40;$dialog.CancelButton.PerformClick()}
    })
    $timer.Start();$strips[1].BalanceButton.PerformClick();$timer.Dispose()
    if($strips[1].State.GreenBalance -ne 70){throw 'Cancel changed staged balance'}
    $strips[0].Editor.SetColor([Drawing.Color]::Yellow)
    $form.GetType().GetField('syncColors',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($form).PerformClick()
    if($strips[1].State.GreenBalance -ne 70 -or $strips[1].State.Brightness -ne 67){throw 'Shared hue reset calibration/intensity'}
    if($strips[1].State.OutputColor.R -ne 171 -or $strips[1].State.OutputColor.G -ne 120 -or $strips[1].State.OutputColor.B -ne 0){throw 'Wrong calibrated yellow output'}
    'PASS independent balance, intensity preservation, cancel, shared hue and calibrated yellow'
} finally {$form.Dispose()}

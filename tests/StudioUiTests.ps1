param([string]$AssemblyPath=(Join-Path (Split-Path $PSScriptRoot -Parent) 'dist\RGBABY.exe'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom($AssemblyPath)
$form=[Activator]::CreateInstance($assembly.GetType('Rgbaby.MainForm'),[object[]]@($true))
try {
    $form.Opacity=0;$form.ShowInTaskbar=$false;$form.Show();[Windows.Forms.Application]::DoEvents()
    $flags=[Reflection.BindingFlags]'Instance,NonPublic'
    $strips=$form.GetType().GetField('strips',$flags).GetValue($form)
    if($strips.Count -ne 4 -or $strips[3].Kind.ToString() -ne 'Ram'){throw 'RAM lighting controls are missing'}
    $strips[0].Intensity.Value=21;$strips[1].Intensity.Value=67;$strips[2].Intensity.Value=90
    $strips[0].Editor.SetColor([Drawing.Color]::Yellow)
    $form.GetType().GetField('syncColors',$flags).GetValue($form).PerformClick()
    if($strips[0].State.Brightness -ne 21 -or $strips[1].State.Brightness -ne 67 -or $strips[2].State.Brightness -ne 90){throw 'Color sync changed intensity'}
    foreach($s in $strips){if($s.State.Color.ToArgb() -ne [Drawing.Color]::Yellow.ToArgb()){throw 'Color sync did not propagate'}}
    $strips[1].Editor.SetColor([Drawing.Color]::Red);$strips[2].Editor.SetColor([Drawing.Color]::Blue)
    $form.GetType().GetField('syncBrightness',$flags).GetValue($form).PerformClick()
    if($strips[1].State.Color.ToArgb() -ne [Drawing.Color]::Red.ToArgb() -or $strips[2].State.Color.ToArgb() -ne [Drawing.Color]::Blue.ToArgb()){throw 'Brightness sync changed colors'}
    foreach($s in $strips){if($s.State.Brightness -ne 21){throw 'Brightness sync did not propagate'}}
    if($strips[1].State.GreenBalance -ne 65 -or $strips[2].State.GreenBalance -ne 70){throw 'Sync reset calibration'}
    'PASS independent color and brightness sync preserves calibration'
    $knob=$strips[0].Intensity;$knob.Value=50
    $knob.GetType().GetMethod('OnKeyDown',$flags).Invoke($knob,[object[]]@(([Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Up)).PSObject.BaseObject)) | Out-Null
    if($knob.Value -ne 51 -or $strips[0].State.Brightness -ne 51){throw 'Knob arrow input failed'}
    $knob.Value=500;if($knob.Value -ne 100){throw 'Knob maximum failed'};$knob.Value=-20;if($knob.Value -ne 0){throw 'Knob minimum failed'}
    $knob.Value=50
    $knob.GetType().GetMethod('OnMouseDown',$flags).Invoke($knob,[object[]]@(([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,40,70,0)).PSObject.BaseObject)) | Out-Null
    $knob.GetType().GetMethod('OnMouseMove',$flags).Invoke($knob,[object[]]@(([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,0,40,50,0)).PSObject.BaseObject)) | Out-Null
    $knob.GetType().GetMethod('OnMouseUp',$flags).Invoke($knob,[object[]]@(([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,40,50,0)).PSObject.BaseObject)) | Out-Null
    if($knob.Value -ne 70){throw 'Knob drag failed'}
    'PASS knob keyboard, drag and bounded intensity'
    $form.Size=New-Object Drawing.Size(900,650);[Windows.Forms.Application]::DoEvents()
    if(-not $form.Controls[0].VerticalScroll.Visible -or -not $form.Controls[0].HorizontalScroll.Visible){throw 'Small-window scrolling missing'}
    function FindPictures($control){if($control -is [Windows.Forms.PictureBox]){throw 'Interior artwork was not removed'};foreach($child in $control.Controls){FindPictures $child}}
    FindPictures $form
    'PASS small-window access and no interior artwork'
} finally {$form.Dispose()}

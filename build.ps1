param([string]$OutputName = 'RGBABY.exe')
$ErrorActionPreference = 'Stop'
if ([IO.Path]::GetFileName($OutputName) -ne $OutputName -or -not $OutputName.EndsWith('.exe')) { throw 'OutputName must be an executable filename' }
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'dist') -Force | Out-Null
$rgbCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$rgbSources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $rgbCompiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /out:"$PSScriptRoot\dist\$OutputName" /win32manifest:"$PSScriptRoot\src\app.manifest" /win32icon:"$PSScriptRoot\Images\Gemini_Generated_Image_lpg9yqlpg9yqlpg9.ico" /resource:"$PSScriptRoot\Images\Gemini_Generated_Image_lpg9yqlpg9yqlpg9.ico",app.ico /resource:"$PSScriptRoot\research\k636-keymap.json",keymap.json /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $rgbSources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed' }
$rgbBackend = Join-Path $PSScriptRoot 'runtime\OpenRGB\OpenRGB Windows 64-bit'
if (Test-Path -LiteralPath $rgbBackend) {
    New-Item -ItemType Directory -Path "$PSScriptRoot\dist\backend" -Force | Out-Null
    foreach ($rgbFile in (Get-ChildItem -LiteralPath $rgbBackend -Recurse -File | Where-Object { $_.FullName -notmatch '\\rgbaby-config\\' })) {
        $rgbRelative=$rgbFile.FullName.Substring($rgbBackend.Length).TrimStart('\')
        $rgbTarget=Join-Path "$PSScriptRoot\dist\backend" $rgbRelative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($rgbTarget)) -Force | Out-Null
        if (!(Test-Path -LiteralPath $rgbTarget) -or (Get-FileHash -LiteralPath $rgbFile.FullName).Hash -ne (Get-FileHash -LiteralPath $rgbTarget).Hash) {
            Copy-Item -LiteralPath $rgbFile.FullName -Destination $rgbTarget -Force
        }
    }
}
Write-Output "Built $PSScriptRoot\dist\$OutputName"

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'build') -Force | Out-Null
$rgbCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $rgbCompiler /nologo /target:exe /out:"$PSScriptRoot\build\ProtocolTests.exe" "$PSScriptRoot\src\Protocol.cs" "$PSScriptRoot\tests\ProtocolTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& "$PSScriptRoot\build\ProtocolTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Keyboard tests failed' }
& $rgbCompiler /nologo /target:exe /out:"$PSScriptRoot\build\LightingTests.exe" /reference:System.Drawing.dll "$PSScriptRoot\src\LightingState.cs" "$PSScriptRoot\src\OpenRgb.cs" "$PSScriptRoot\tests\LightingTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Lighting test compilation failed' }
Push-Location $PSScriptRoot
try { & "$PSScriptRoot\build\LightingTests.exe" } finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw 'Lighting tests failed' }
& $rgbCompiler /nologo /target:exe /out:"$PSScriptRoot\build\SettingsTests.exe" /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\src\LightingState.cs" "$PSScriptRoot\src\Settings.cs" "$PSScriptRoot\src\StartupRestore.cs" "$PSScriptRoot\tests\SettingsTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Settings test compilation failed' }
Push-Location $PSScriptRoot
try { & "$PSScriptRoot\build\SettingsTests.exe" } finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw 'Settings tests failed' }
& $rgbCompiler /nologo /target:exe /out:"$PSScriptRoot\build\ResumeTests.exe" /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\src\LightingState.cs" "$PSScriptRoot\src\Settings.cs" "$PSScriptRoot\src\StartupRestore.cs" "$PSScriptRoot\src\ResumeRestore.cs" "$PSScriptRoot\tests\ResumeTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Resume test compilation failed' }
& "$PSScriptRoot\build\ResumeTests.exe"
exit $LASTEXITCODE

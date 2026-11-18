# Builds MidiLink.exe with the C# compiler built into Windows (no Visual Studio or downloads needed).
$ErrorActionPreference = 'Stop'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$root = $PSScriptRoot
New-Item -ItemType Directory -Force "$root\dist" | Out-Null
& $csc /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 `
    /out:"$root\dist\MidiLink.exe" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    "$root\src\*.cs"
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "Built $root\dist\MidiLink.exe"

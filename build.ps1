$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$framework = Split-Path -Parent $compiler
$destination = Join-Path $PSScriptRoot 'portable'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$arguments = @('/nologo','/utf8output','/target:winexe','/optimize+',('/out:' + (Join-Path $destination 'H9BatteryShowcase.exe')),('/win32manifest:' + (Join-Path $PSScriptRoot 'src\app.manifest')))
foreach ($reference in @('System.dll','System.Core.dll','System.Management.dll','System.Web.Extensions.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')) { $arguments += '/reference:' + (Join-Path $framework $reference) }
foreach ($file in @('Reader.cs','DesktopLayer.cs','Showcase.cs')) { $arguments += Join-Path $PSScriptRoot ('src\' + $file) }
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output 'Built portable\H9BatteryShowcase.exe'

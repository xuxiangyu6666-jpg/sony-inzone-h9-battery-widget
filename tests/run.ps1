$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$temp = Join-Path ([IO.Path]::GetTempPath()) ('H9WidgetTests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    $exe = Join-Path $temp 'ReaderTests.exe'
    & $compiler /nologo /utf8output /target:exe ('/out:' + $exe) /reference:System.Management.dll /reference:System.Web.Extensions.dll (Join-Path $root 'src\Reader.cs') (Join-Path $PSScriptRoot 'ReaderTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
    & $exe (Join-Path $temp 'result.json')
    if ($LASTEXITCODE -ne 0) { throw 'Parser tests failed' }
} finally {
    foreach ($name in @('ReaderTests.exe','result.json')) { $file = Join-Path $temp $name; if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
    Remove-Item -LiteralPath $temp
}

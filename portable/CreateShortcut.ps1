$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'H9BatteryShowcase.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'H9BatteryShowcase.exe was not found beside this script.' }
$shell = New-Object -ComObject WScript.Shell
try {
    $shortcut = $shell.CreateShortcut((Join-Path $PSScriptRoot 'Start H9 Battery Widget.lnk'))
    try {
        $shortcut.TargetPath = $exe
        $shortcut.WorkingDirectory = $PSScriptRoot
        $shortcut.Description = 'INZONE H9 Battery Widget'
        $shortcut.Save()
    } finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null }
} finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null }
Write-Output 'Created Start H9 Battery Widget.lnk in this folder. No startup setting was changed.'

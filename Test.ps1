param([switch]$NativeChecks,[string]$BaselineImage,[string]$Python='python')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $root
try {
    New-Item -ItemType Directory -Path Evidence -Force | Out-Null
    $common=@('Plugin/DriveEngine.cs','Plugin/NativeRuntime.cs','Plugin/NativeCompatibility.cs','Plugin/NativeDrivePolicy.cs','Plugin/SessionStats.cs','Plugin/DefenseChoice.cs','Plugin/Diagnostics.cs')
    function Run-Check([string]$Name) {
        $output=[IO.Path]::GetFullPath((Join-Path $root ('Evidence/'+$Name+'.exe')))
        $sources=@($common | ForEach-Object {[IO.Path]::GetFullPath($_.Replace('/','\'))})
        $testSource=[IO.Path]::GetFullPath(('Tools/'+$Name+'.cs').Replace('/','\'))
        & $compiler /nologo /platform:x64 /reference:System.IO.Compression.dll "/out:$output" @sources $testSource
        if($LASTEXITCODE -ne 0){throw "Compilation failed: $Name"}
        & $output
        if($LASTEXITCODE -ne 0){throw "Check failed: $Name"}
    }
    foreach($name in @('DriveChecks','NativeDriveChecks','DefenseChecks','OpponentChecks','SessionStatsChecks','ScrimmageChecks','ModeChecks','StartChecks','DiagnosticsChecks')){Run-Check $name}
    if($NativeChecks) {
        Run-Check 'ExportHooks'
        & $Python Tools/test_hooks.py
        if($LASTEXITCODE -ne 0){throw 'Native hook checks failed'}
        if($BaselineImage) {
            & $Python Tools/native_tools.py fixtures $BaselineImage
            if($LASTEXITCODE -ne 0){throw 'Native reference fixture generation failed'}
            Run-Check 'CompatibilityChecks'
        }
    }
} finally {Pop-Location}

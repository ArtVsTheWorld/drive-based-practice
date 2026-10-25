param([string]$MmcRoot=$env:MMC_ROOT)
$ErrorActionPreference='Stop'
if(!$MmcRoot){throw 'Pass -MmcRoot with your MMC folder, or set MMC_ROOT.'}
foreach($name in @('FrostyCore.dll','FrostySdk.dll')){if(!(Test-Path -LiteralPath (Join-Path $MmcRoot $name))){throw "Missing $name in MmcRoot"}}
$projectRoot=Split-Path -Parent $PSScriptRoot
$versionSource=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AssemblyInfo.cs') -Raw
if($versionSource -notmatch 'PluginVersion\("([^"]+)"\)'){throw 'Plugin version missing'}
$version=$Matches[1]
$release=Join-Path $projectRoot ("Release\SituationalPractice-"+$version)
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path $release -Force | Out-Null
$runtime=Join-Path $release 'SituationalPractice.Runtime.exe'
$runtimePdb=Join-Path $release 'SituationalPractice.Runtime.pdb'
& $compiler /nologo /target:winexe /platform:x64 /debug:pdbonly "/pdb:$runtimePdb" /reference:System.IO.Compression.dll "/out:$runtime" (Join-Path $PSScriptRoot 'DriveEngine.cs') (Join-Path $PSScriptRoot 'NativeRuntime.cs') (Join-Path $PSScriptRoot 'NativeCompatibility.cs') (Join-Path $PSScriptRoot 'NativeDrivePolicy.cs') (Join-Path $PSScriptRoot 'PracticeRunner.cs') (Join-Path $PSScriptRoot 'SessionStats.cs') (Join-Path $PSScriptRoot 'DefenseChoice.cs') (Join-Path $PSScriptRoot 'Diagnostics.cs')
if($LASTEXITCODE -ne 0){throw 'Runtime compilation failed'}
& $compiler /nologo /target:library /platform:x64 /reference:System.IO.Compression.dll "/out:$(Join-Path $release 'SituationalPractice.dll')" "/resource:$runtime,SituationalPractice.Runtime.exe" "/resource:$runtimePdb,SituationalPractice.Runtime.pdb" "/reference:$(Join-Path $MmcRoot 'FrostyCore.dll')" "/reference:$(Join-Path $MmcRoot 'FrostySdk.dll')" (Join-Path $PSScriptRoot 'AssemblyInfo.cs') (Join-Path $PSScriptRoot 'PracticeAction.cs') (Join-Path $PSScriptRoot 'Diagnostics.cs')
if($LASTEXITCODE -ne 0){throw 'MMC plugin compilation failed'}



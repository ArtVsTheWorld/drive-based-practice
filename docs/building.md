# Building

Use 64-bit Windows with .NET Framework 4.x and a matching MMC Mod Manager install.
The build uses MMC's `FrostyCore.dll` and `FrostySdk.dll`. They stay on your computer
and are not included in this repository. No extra DLL goes in the release ZIP.

```powershell
.\Build.ps1 -MmcRoot "C:\Path\To\MMC"
.\Test.ps1
.\Package.ps1
```

You can set `MMC_ROOT` instead of passing `-MmcRoot`. Build output goes into
`Release/`; test output goes into `Evidence/`. Both folders are ignored by Git.

The package command checks the DLL version and makes a ZIP containing only the
plugin DLL and a short `README.txt`. It does not install or launch anything.

`Test.ps1` checks drive rules, CPU play choices, Stats, Scrimmage, first-snap
counting, first-down line ownership and diagnostics.
These tests use local test data and do not open or patch the game.

For generated native hooks and compatibility checks:

```powershell
python -m pip install -r requirements-dev.txt
.\Test.ps1 -NativeChecks -BaselineImage "private/working.pe.bin"
```

Without `-BaselineImage`, the native hook tests still run with mocks, but the
region compatibility fixture checks are skipped. Fixtures are generated locally
from a verified capture and are never committed. GitHub runs the SDK-free managed
checks and checks that the native manifest matches the source.

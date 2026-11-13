# Expanded Practice by Ace

Optional drive and scrimmage practice for supported offline Dynasty and main-menu Practice. Other COT modules are not required.

- Choose Default Practice, Drive Practice or Scrimmage in the native pause-menu Mode control
- Drive Practice carries downs, distance, field position and score between plays and returns to play selection
- Scrimmage supports possession changes and scoring/turnover flow, including returns before the next possession
- Choose supported starting positions, switch sides and return to Default Practice
- Session statistics track supported practice results without adding them to Dynasty season records
- Supported first-down markers, drive resets and opponent/defensive selection policies remain included
- The verified Dynasty Extensions pregame bridge avoids the confirmed controller-hook conflict while preserving hook ownership
- Settings/logs/diagnostics and verified runtime files use %LOCALAPPDATA%\Ace\DynastyCOT\ExpandedPractice across MMC copies
- Legacy data is copied without deleting originals; existing AppData preferences win and conflicts are archived
- Copy ExpandedPractice.dll into Plugins, or let COT install that same DLL; support prepares automatically
- Session ownership prevents duplicate practice helpers; payload hashes are verified before startup
- Native fingerprints and offline eligibility remain enforced; unsupported owners are rejected
- No arbitrary online modes or replacement of normal competitive simulation is included
- Current companion modules share the supported practice/controller contracts; older modified versions may conflict

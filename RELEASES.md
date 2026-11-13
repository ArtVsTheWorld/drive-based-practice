## Private candidate 0.8.23 (not published)

# ExpandedPractice 0.8.23

Changes since public 0.8.21

- Avoids the confirmed Dynasty Extensions pregame controller-hook conflict on the supported layout
- Settings, diagnostics and verified runtime files now use shared AppData, including when MMC is installed in a protected folder
- Keeps Default, Drive and Scrimmage modes, session statistics and existing native safety checks

Only the exact supported Dynasty Extensions/controller layout is covered. Check both practice entry routes after a clean restart.

Install or update: close MMC/game, back up the previous DLL and replace it in Plugins. Keep settings and history. Restore that DLL to roll back.

---

# Expanded Practice 0.8.21 - standalone release

Compared with the last published standalone release, **v0.8.18**.

- **A clearer name:** Drive-Based Practice is now Expanded Practice.
- **Main-menu Practice support:** use Default Practice, Drive Practice or Scrimmage in supported offline main-menu Practice as well as Dynasty Practice. Native offline/practice eligibility remains checked.
- **Updated native compatibility:** the runtime uses the current supported game layout, with exact bridge handling for current Extra Settings and the COT practice contracts. Unknown hook owners are still rejected.
- **Play-selection transitions:** the current native flow completes requested picker transitions before the next pre-snap step, including supported possession/reset situations.
- **Verified runtime and better reports:** a matching helper is SHA256-checked before launch; game-session duplicate ownership, launch/exit observation and diagnostic reports remain enabled.
- **One-DLL download:** put `ExpandedPractice.dll` in Plugins. The matching runtime helper is embedded and prepared automatically. Existing module data is retained.

Default Practice, full-drive downs/distance, Scrimmage possession changes, scores, session stats, re-spotting, side switching and the first-down line are retained features. They are not all new in 0.8.21. Kicking sequences and a normal running game clock are not supported practice features.

Move old `SituationalPractice.dll` outside Plugins and keep its data. Disable COT's Expanded Practice module if using the standalone beside COT. Use matched current modules: older practice helpers may not satisfy Beyond the Field's exact hook contract or Extra Settings' controller bridge.

Validation: the practice regression suites passed, including drive/defense/opponent/session/scrimmage/mode/start/first-down/diagnostics checks. The shared practice contract passed 5,009 ownership/mutation checks, and all-four package/coexistence fixtures passed. The helper matches COT 1.0.2 byte-for-byte. **Ace reported all four standalone releases verified working on November 9, 2026.** No live game was patched during these tests.

Source comparison: old tag `v0.8.18`, commit `0b3998949a63c842f02d0f05da4c5f66ecb22ad0`; current maintained source is pinned in `source-release.json`. Checked changes include `NativeRuntime.cs`, `NativeCompatibility.cs`, `DynastyControllerCompatibility.cs`, `PracticeRunner.cs`, `PracticeAction.cs` and `RuntimeFiles.cs`. Core drive/scoring policies are retained rather than advertised as newly introduced.

Stable promotion: the ZIP and DLL are unchanged from the tested preview. Enclosed preview labels describe the build-time status. Acceptance is user-reported for the tested setup; other game/overhaul combinations are not automatically certified.

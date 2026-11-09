# Expanded Practice by Ace

Practice a drive or a scrimmage without entering a full game. Works in supported **offline Dynasty Practice and main-menu Practice**; no other COT module is required.

- Keep **Default Practice**, use **Drive Practice**, or choose **Scrimmage** from the native pause-menu Mode control. Ordinary practice remains the default.
- Drive Practice carries **downs, distance, field position and score** between plays. It brings you back to play selection instead of making every snap a fresh disconnected rep.
- Scrimmage handles **possession changes** and the supported scoring/turnover flow. Interceptions and fumble returns can continue before the next possession.
- Choose a supported **starting spot**, switch sides, and use the module's practice controls rather than editing your Dynasty save.
- Session statistics track the supported practice plays and results. They are practice-session data, not added Dynasty season statistics or official film grades.
- The existing first-down/drive reset and opponent/defensive selection policies stay included. Switching modes uses their supported play-selection flow; returning to Default Practice remains available.
- The runtime helper has its own game-session ownership guard. A duplicate helper exits instead of starting a second practice controller.
- Existing settings/log/diagnostic paths stay under `Plugins/SituationalPractice`. The plugin verifies the embedded helper against its compiled-in SHA256 before launching it, with a read-only lease held through process creation.
- Native scope checks, code fingerprints, main-menu practice eligibility and controller ownership checks are preserved. Unknown or changed owners are not overwritten.
- Current Beyond the Field recognizes the exact supported practice hook bodies; current Extra Settings has the compatible controller bridge. Different old/modified helpers can be rejected by those contracts.
- This does not enable drives in arbitrary online modes or replace the normal game simulation. Gameday Atmosphere deliberately does not apply its stadium crowd behavior to Practice.
- This is a new standalone packaging preview. The gameplay helper matches released COT 1.0.2, but clean-launch testing of the new standalone wrapper remains necessary.

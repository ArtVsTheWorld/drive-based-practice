# After an EA update

The current code targets the October 13, 2026 native layout. A game update can move
functions, globals, vtables and jump tables by different amounts. A single address
offset is not enough.

1. Keep an unmodified decrypted capture of the working build under `private/`.
2. Launch the updated game without native plugins. Let it reach the menus.
3. Capture only its code/read-only sections. This command opens the game read-only:

```powershell
python Tools/native_tools.py capture --pid <game-pid> --output private/new.pe.bin
python Tools/native_tools.py verify private/new.pe.bin
python Tools/native_tools.py map private/old.pe.bin private/new.pe.bin --output private/address-map.json
```

The map is a list of suggestions. It does not change source or patch the game.
Unique instruction matches include paired RIP-relative globals and call targets.
Unresolved or cloned functions need manual review. Use the disassembler as needed:

```powershell
python Tools/native_tools.py disasm private/new.pe.bin 0x123456 --length 0x200
```

4. Review every native reference in `docs/native-layout.json`, not just hook entries.
   Check instruction boundaries, preserved registers, return addresses, constant
   values, object offsets, vtable slots, jump tables and mode/side flags.
5. Update the source files listed below and the corresponding tests. Recalculate
   only the contracts whose new code you have inspected. Keep third-party hook
   exclusions narrow. Never disable compatibility checks just to make a build load.
6. Run `python Tools/export_layout.py`, review its diff, build, run tests, and package.
7. Test a fresh launch in the actual game with other plugins disabled, then repeat
   with the supported plugin combination. Check settings/stats, re-entry and exits.
8. Bump the version, update the short release README, commit, tag, and attach the
   two-file ZIP to a GitHub release. GitHub tests do not replace gameplay testing.

Captures, native reference binaries, SDK files, saves and logs stay local. They are
ignored by Git. Python inspection tools use `requirements-dev.txt` (Capstone).

## Source map

- `Plugin/NativeCompatibility.cs`: required region lengths and SHA-256 fingerprints.
- `Plugin/NativeRuntime.cs`: hook entries, replayed instructions, native calls and globals.
- `Plugin/DefenseChoice.cs`: playbook/personnel access and CPU selection.
- `Plugin/SessionStats.cs`: native stats events and menu fields.
- `Plugin/PracticeAction.cs`, `PracticeRunner.cs`, `Diagnostics.cs`: launch and reporting.
- `NativeDrivePolicy.cs` and `DriveEngine.cs`: drive/scoring rules; keep these unchanged for a relocation-only update.

The first practice snap initializes the session. Test both modes, both user sides,
timeouts, Choose New Play, re-spotting, turnovers/returns, mode changes and Stats.
The yellow first-down line and kicking sequences are still unsupported.

Call Sheet compatibility excludes only the five-byte calls at `0x35ff6ef` and
`0x7b7e964` in the current layout. The surrounding code and owned hook sites remain
checked. Re-establish those exclusions from the new code after an update.

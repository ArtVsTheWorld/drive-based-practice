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

Fresh practice starts with gameplay hooks inactive and only the mode selector
available. The native menu scope is separate from the drive enable flag. Test
all three modes, both user sides, timeouts, Choose New Play, re-spotting,
turnovers/returns, mode changes and Stats. Returning to default practice must
restore the native HUD flags and two-sided picker; entering a custom mode must
finish selection once, with no penalty loop. When both native play-manager
readiness bytes at +0x50c and +0x50d are 1, mode changes resume native preplay
with the existing cached plays. Do not reopen a picker from the pause-menu flow.
Choose New Play remains available. All custom-mode changes use this same entry.
Native mode
entry is applied at the safe preplay or play-selection update on the game thread.
If no complete cached pair exists, custom-mode entry waits for initialization stage 2 before opening
the picker; stages 0 and 1 can clear choices made too early. The pending UI
request lives at offset 12448. The 64-bit refresh-helper address at 12392 must
never overlap the 32-bit opponent-possession tag at 12028. Test calls through
the full refresh address with both possession tags, including an address above 4 GB.
The mode-entry thunk captures the pre-snap situation and completion counter in
the shared block at offset 12400. It publishes the applied command only after
opening the custom picker, or returning to native preplay for Default Practice.
Keep that order: the worker must retain a first result that
finishes before it observes the acknowledgement. Attach/recovery must also use
the safe pre-snap entry, rather than waiting for an end-of-play reset.
`NativeFirstDownLine` uses the practice FieldLineProcessor visibility override
at +0x61. Resolve it through `[image+0xf8abf80]+0xea28`, check the practice vtable
at 0xb474928 and first-down color enum 26 or fourth-down enum 35 at +0x20.
Invalid color 44 can appear while the slot is rebuilt. Keep processor identity
separate from the color check so cleanup still recognizes an owned processor.
Practice deliberately skips
the normal first-down display check in 0x35bfd70; +0x61 enables the native line.
The game clears the override during play transitions, so maintain it while a
custom drive is active. Restore only the currently owned processor when leaving
the mode; never write a saved pointer after the processor changes. Goal-to-go
must retain the painted goal line without an extra yellow line. Test re-entry,
mode changes, both possession directions, first downs, losses and re-spotting.
Kicking sequences are still unsupported.

Call Sheet compatibility excludes only the five-byte calls at `0x35ff6ef` and
`0x7b7e964` in the current layout. The surrounding code and owned hook sites remain
checked. Re-establish those exclusions from the new code after an update.

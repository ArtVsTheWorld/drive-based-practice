# Drive-Based Practice

By Ace. Offline Dynasty practice for College Football 27.

Keep normal practice, or switch to drives and scrimmage with ball movement, downs,
the in-game scoreboard and session stats.

## Install

Download the ZIP from [Releases](https://github.com/ArtVsTheWorld/drive-based-practice/releases).
Close the game and MMC, copy `SituationalPractice.dll` into MMC's `Plugins` folder,
then restart MMC and launch the game through it.

Enter Dynasty practice. **Default Practice** is selected each time you enter.
It works like the game's regular practice: the ball returns to the same spot,
and you select the plays for both sides.

**No initialization play is required in any mode.** Choose a drive mode from the
pause menu, select your play, and start. The first snap counts toward the drive
and session stats.

## Modes

- **Default Practice:** the game's normal practice, with its usual ball resets and
  play selection. Custom scoring, drive tracking and CPU play selection are off.
- **Drive Practice:** repeated drives from your chosen spot. Offensive touchdowns
  score 6, defensive turnovers 4, fourth-down stops 2, and return touchdowns 6 total.
- **Scrimmage:** possession changes after scores and turnovers. Touchdowns score 6
  and safeties 2. Turnovers and stops have no bonus points.

Use **Pause > Mode** to cycle through Default Practice, Drive Practice and Scrimmage.
Switching modes clears scores and stats. In the two drive modes, choose your side's
play each snap and let the CPU choose the other side. **Stats** shows session totals,
**Flip Sides** resets the drive and keeps totals, and **Re-spot Ball** starts a fresh
drive at the chosen spot. Returning to Default Practice restores normal practice
play selection.

Mode changes keep the current plays when both teams have already selected one.
The first snap in a drive mode counts normally. Use Choose New Play from the
pause menu to change plays before that snap.

Drive Practice and Scrimmage enable the game's yellow first-down line. There is
no game clock, and kicking sequences are not supported. Logs and reports are in
`Plugins/SituationalPractice`.

## Source

Current version: **0.8.13 beta**. Fourth-down line visibility and resuming the
current plays after a mode change are being tested.

[Build and test](docs/building.md) | [Updating after an EA patch](docs/updating.md)

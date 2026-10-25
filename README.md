# Drive-Based Practice

By Ace. Offline Dynasty practice for College Football 27.

Practice becomes a series of drives with ball movement, downs, the in-game
scoreboard and session stats. Play offense or defense, choose your play each snap,
and let the CPU call the other side.

## Install

Download the ZIP from [Releases](https://github.com/ArtVsTheWorld/drive-based-practice/releases).
Close the game and MMC, copy `SituationalPractice.dll` into MMC's `Plugins` folder,
then restart MMC and launch the game through it.

Enter Dynasty practice and **run one initialization play**. It does not count toward
stats. The session starts when that play resets.

## Modes

- **Drive Practice:** repeated drives from your chosen spot. Offensive touchdowns
  score 6, defensive turnovers 4, fourth-down stops 2, and return touchdowns 6 total.
- **Scrimmage:** possession changes after scores and turnovers. Touchdowns score 6
  and safeties 2. Turnovers and stops have no bonus points.

Use **Pause > Mode** to switch. Switching modes clears scores and stats.
**Stats** shows session totals. **Flip Sides** resets the drive and keeps totals.
**Re-spot Ball** starts a fresh drive at the chosen spot.

There is no game clock or yellow first-down line. Kicking sequences are not
supported. Logs and reports are in `Plugins/SituationalPractice`.

## Source

Current version: **0.8.7 beta**. The failed Spring Training experiment is not included.

[Build and test](docs/building.md) | [Updating after an EA patch](docs/updating.md)

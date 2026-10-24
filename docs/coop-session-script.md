# Co-op session script

A test for two players (host and guest). Two game copies cannot run on one PC, so this is done with two people online. It takes about 20 minutes.

## Before you start

- Both players install the same build of this branch (`feature/coop-sim`), and the same Sailwind Coop (0.4.0).
- Both play in the same save, in the same port, with a candle bundle, a food stack and a crate of hooks ready (or make them as you go).
- Optional, alone first: run the co-op probe (see SKILLS.md, "Co-op probe") on one bundle of each kind. Any `[Probe] FAIL` is worth fixing before the session.
- Delete or move `BepInEx/LogOutput.log` before launching, so the log holds only this session.
- Say the step number out loud when you start each step. The log has no step markers, so note the time as well.

Each step says what to do and which lines should appear. Lines are from `[Coop]` in the log of the player named.

## Steps

| # | Do | Expect |
|---|----|--------|
| 1 | Host makes a candle bundle of 4, then puts it down. | Host: `Told the other players about a new …`. Guest: `Received … <id>`, then `Applied … 4 pieces`. Both see 4. |
| 2 | Guest picks it up and holds it. Host looks at it and presses the color key. | Host: nothing changes (it is held). No `Sent` line on the host. |
| 3 | Guest recolors the bundle they hold. | Guest: `Sent … 4 pieces`. Host: `Received … (held)`, then `Applied`. Host sees the new color. |
| 4 | Guest drops it. Host recolors it. | Host: `Sent`. Guest: `Applied`. Colors match. |
| 5 | Host and guest both take one piece off the same bundle at the same moment (count down 3, 2, 1, go). | Both: `Sent`, then `Received` and `Applied`. Then `Settled …` on the host and `was changed here too` on at least one side. The bundle ends at 2 on both screens (4 minus 2). |
| 6 | Guest picks up the bundle while the host combines it with another bundle. | One side may log `Another player picked up … a bundle we just combined. Ignored`. No duplicated bundle, no bundle missing. |
| 7 | Host takes one piece off a bundle and the guest picks that piece up. | Guest: `Another player picked up …`. The piece is one candle on both screens, not the whole bundle. |
| 8 | Guest stows a bundle in their inventory, then takes it out and drops it. | Host sees it vanish, then appear where dropped. No `UNKNOWN` ID in the checker. |
| 9 | Host hangs a food stack on a hook while the guest looks at it and presses the width key. | Width key refused for the guest while the host holds it. After the host lets go, both see the same hanging stack. |
| 10 | Host gathers loose hooks from a crate while the guest carries that crate. | Gather refused for the host. After the guest drops the crate it works, and the guest sees the new bundle. |
| 11 | Guest uses a piece up completely (burn a candle, eat a food portion). Host tries to pick up that same piece at the same moment. | Host: `Ignored` for a bundle used up in the last 30 seconds. No ghost item. |
| 12 | Guest holds a bundle and leaves the game. | Host: the bundle drops at the guest's last position. It is still one bundle with the right count. |

Steps 5 and 6 depend on timing. Try each three times.

## Afterwards

1. Both players keep their `LogOutput.log`, and the stall logs if the mod wrote any.
2. Run the checker on one PC with both logs:

```bash
py -3 scripts/coop_log_check.py host.log guest.log
```

3. Read the result:
   - `LOST` or `DIFFERS`: a bundle one player saw and the other did not, or counts that disagree. Write down the step.
   - `UNKNOWN`: a bundle packet named an ID that was not there.
   - `WARNING`, `ERROR`, `STALL`: read the line.
   - `MERGED` and `IGNORED` are notes, not faults. They show the race rules did their work.
   - Exit code 0 is clean, 1 is notes or warnings, 2 is faults.

## Known limits (not faults)

- Nothing else is known to end apart, other than a lost packet (below).
- A lost packet is not sent again. A rare `DIFFERS` after a lag spike may be this.

# Plan: a Sailwind Coop simulator for testing Bundle Up

Status: proposal (2026-10-23). Nothing here is built yet.

## Why

Every co-op bug so far (J-001, J-002, the held-by-other bugs, the stolen item ID, the freeze after a drop) was found by two people playing together. That is slow, and a fix can't be checked until both players are online again. Each of those bugs came from one of a few things Coop does, and all of them are written down in the journal. A simulator that does the same things lets us replay those bugs in a test and check new bundles before anyone plays.

## What Coop does that matters to us

These are the rules the simulator has to copy. Each one comes from the journal or from reading Coop's code.

| # | Coop behaviour | Where we learned it |
|---|---|---|
| C1 | Sends an item's full saved state only when a player joins. | J-001 |
| C2 | After that, sends `amount` only alongside a health change, and sets it on arrival without rebuilding anything. | J-001 |
| C3 | Never sends `extraValue4` (food's count). | J-002 |
| C4 | Passes custom packets on. A guest sends to the host, and the host passes the packet on to the others. Bundle Up uses type 249. Clothesline uses 241 to 248. | J-002 |
| C5 | While another player carries an item, Coop sets `item.held` to the **local** player's pointer and lists the item in `_remoteHeldItems`. | Held-by-other entry |
| C6 | When a player picks up an item the host doesn't know, `OnRemoteItemPickedUp` takes the **nearest item of the same prefab** and gives it the new ID. | Stolen-ID entry |
| C7 | Applies a remote pick-up or drop in order, on the main thread, inside its own update. | Freeze entry |
| C8 | Messages can arrive late, and their order relative to local actions is not fixed. A real link has latency. | Assumed. Check it against Coop's code. |

## Design

There are two layers. Layer 1 runs in `dotnet test` with no game. Layer 2 runs the real game.

### Layer 1: an in-process simulator (xunit)

Unity types can't be created in a test, so the simulator works on a plain model of the boat. It does not use the real `ShipItem`.

1. **Move the sync decisions into `Dizzy.FirewoodBundle.Core`.** This is pure C# that `CoopBridge` calls, so the game and the tests run the same code:
   - `BundleWire.Encode(state)` and `Decode(bytes)`: the packet 249 payload (count, layout, color, food state, `amount`, `health`).
   - `SyncRules`: when to send (after a rebuild, not while loading), what to refuse while `HeldByOther` (combine, take from, gather), and that a new piece announces itself (`Spawned`).
   - `BundleState`: the fields a bundle saves.
   `CoopBridge` keeps only the Unity and reflection glue.
2. **The model.**
   - `SimItem`: an ID, a prefab index, a position, `amount`, `health`, `extraValue4`, `held` and the state a bundle holds.
   - `SimPeer`: one player's copy of the world (a list of `SimItem`s) plus the Bundle Up logic from Core.
   - `SimNetwork`: one host and N guests, with a message queue per link. It obeys C1 to C8: the snapshot on join, `amount` only with health, no `extraValue4`, passing 249 on, the held pointer, taking the nearest prefab for an unknown ID, and delay and reordering.
3. **Control and checks.**
   - `net.Step()` and `net.Flush()` move messages along. A seeded `net.Shuffle(seed)` reorders the messages that are in flight. The seed is printed when a test fails, so the run can be repeated.
   - `net.AssertConverged()` checks that every peer has the same items, with the same ID, prefab, count and food state, and that no ID is on two items.
   - Faults to add: drop the link mid-message (`Disconnect`), late join (`Join` after N actions), and one packet lost.
4. **Bug mode.** Each rule (C1 to C8) can be turned off. A test can then show that our fix depends on the rule it claims to depend on.

### Layer 2: a two-instance smoke rig (the real game)

Some things the model can't catch: meshes, colliders, a stall in Unity. For those:

- Run two Sailwind instances on this PC from two copies of the pack (host and guest), connected over localhost. **Check first** that Coop allows a localhost or LAN connection and that Steam allows two instances. If it doesn't, use the Deck as the second player.
- A small debug plugin (`Dizzy.CoopProbe`, never shipped) reads a script of actions from a JSON file: spawn, bundle, take one, pick up, drop, hang, eat. It runs them on a timer and writes each peer's item list to `BepInEx/coop-probe-<peer>.json`.
- A Python script compares the two dumps with the same rules as `AssertConverged`, and also reads the stall log (`BundleUp-stall.log`).
- It runs the same scripts as the Layer 1 tests, so a case that fails in the game can be compared with the simulator.

## Build order

1. Move `BundleWire` and `SyncRules` into Core, with no change in behaviour. Check it by building and by a short co-op session (C1 to C3 still work).
2. Build `SimItem`, `SimPeer` and `SimNetwork` in the test project, with rules C1 to C6.
3. Write the regression tests (R1 to R6). Each must fail with its rule's fix turned off and pass with it on.
4. Add delay, reordering and faults (C7, C8), then the stress tests (S1 to S4).
5. Layer 2: the probe plugin and the compare script. Do this only once the localhost check passes.

## Test suite

Unless a case says otherwise, each one runs with one host (H) and one guest (G). Every case ends with `AssertConverged`.

### R: regressions, one for each co-op bug found so far

| ID | Case | Expected |
|---|---|---|
| R1 | G joins. H then bundles 3 candles. | G sees a bundle of 3, not 1 candle (J-001). |
| R2 | G joins. H bundles 4 sausages, then 3 pork chops. | G sees the right counts and food state, though `extraValue4` is never sent (J-002). |
| R3 | G carries a bundle. H tries to combine a log into it, take one from it and gather it. | All three are refused on H. G's bundle is unchanged (`HeldByOther`, C5). |
| R4 | H takes one log off a bundle. G picks up that new log. | The new log has its own ID. H's bundle keeps its ID and stays put (C6, `Spawned`). |
| R5 | Same as R4 with a stove log and a hook taken off a line. | Same result, for every place that creates a piece. |
| R6 | G drops a goat cheese bag that H can see. | The drop is applied once, with no loop of rebuilds or messages (the freeze entry). |

### F: one case for each kind of bundle

For each kind (firewood, candles, line, lure line, sausages, cheese, apples and other bags, pork and lamb meat bags, poles, round shot pyramids):

| ID | Case | Expected |
|---|---|---|
| F1 | Make a bundle on H. | G matches. |
| F2 | Make it on G. | H matches (the host passes the packet on). |
| F3 | Add one, take one, down to 1 (it becomes a single) and down to 0. | Every peer shows the same count at each step. At 1 it is a single on everyone, not a bundle of 1 (the meat bag bug at 1). |
| F4 | Hang it on a hook, then take it off. | The hung state matches. A hung food bundle of 1 stays a bundle. |
| F5 | Save on H, reload, and G joins. | The snapshot (C1) gives the same bundle. Nothing is sent twice while loading. |

### J: join and timing

| ID | Case | Expected |
|---|---|---|
| J1 | G joins with 50 bundles on the boat. | No 249 messages during the load (no flood). Everything matches after the snapshot. |
| J2 | H changes a bundle while G's join snapshot is in flight. | The change after the snapshot wins. |
| J3 | A second guest (G2) joins late. | G2 matches H and G. |
| J4 | G disconnects mid-message and joins again. | No duplicate items and no half-built bundles. |

### X: conflicts (two players, one bundle)

| ID | Case | Expected |
|---|---|---|
| X1 | H and G each take one off the same bundle in the same step. | The count drops by 2, and two new pieces have two IDs. Or one is refused. Never a count that differs between peers. |
| X2 | H combines into a bundle while G picks it up. | Either G holds the bigger bundle or the combine is refused. The log is never lost. |
| X3 | Two bundles of the same prefab sit side by side. G picks up a new piece next to them. | The ID goes to the piece, not to a bundle (C6). |
| X4 | Run X1 to X3 under 100 shuffle seeds. | They all converge. Print the seed of any failure. |

### C: crew mod together with Coop

| ID | Case | Expected |
|---|---|---|
| C-1 | The steward on H takes one piece off a food bundle. | G sees the bundle drop by 1 and sees the new piece, then sees it eaten. |
| C-2 | A lantern hand on H loads a candle from a bundle. | G sees the count drop by 1, not the whole bundle gone. |
| C-3 | The steward picks food while G carries that bundle. | The steward doesn't take it (`HeldByOther`). |

### S: stress

| ID | Case | Expected |
|---|---|---|
| S1 | 10,000 random actions across H, G and G2, with a fixed seed. | They converge at the end, and no ID is on two items. |
| S2 | S1 with 5% of packets lost. | Record what diverges. That tells us whether to resend state now and then. |
| S3 | S1 with each rule C1 to C8 turned off in turn. | Tells us which tests guard which rule. Any rule no test guards gets a new test. |
| S4 | Count the messages for 100 actions. | No more than one 249 per rebuild. |

## Layer 2 check (2026-10-23): two copies on one PC will not work

Coop 0.4.0 (`SailwindCoop.dll`) sends everything over Steam P2P (`SteamNetworking.SendP2PPacket`, `AcceptP2PSessionWithUser`) between Steam IDs, joined through a Steam lobby. It has no loopback, offline or fake-peer mode; its `Debug` classes are command and profiling tools. Two copies need two Steam accounts, and one account cannot run the game twice. The Deck does not help either, since it would be the same account.

So the rig as drawn is out. Two ways to still cover what the simulator cannot (Unity rebuilds, colliders, stalls):

- **One-game loopback probe.** A dev-only command in a single running game builds a packet from a real bundle with `BundleWire`, then hands it to `CoopBridge.Receive` as if from another player (it does not check for a session). It checks the apply path against the real rebuild code: count, colors, hanging, food state. It cannot test Coop's own sync.
- **Session log checker.** A script reads both players' BepInEx logs from a real session with Robby and checks that each `[Coop] Sent` line has a matching `[Coop] Received` on the other side, and lists stalls. It needs no new plugin, and covers Coop's real behaviour, but only when two people play.

## Open questions

- **C8:** confirm from Coop's code whether a guest's messages to the host can arrive out of order, and whether there is any resend.
- Is the Coop version in the October pack the one the journal describes? Pin the version in the simulator (`CoopRules.Version`).
- Layer 2: see the check above. Pick the probe, the log checker, or both.
- X1 and X2 have no agreed answer yet. The tests should first record what happens. Pick the rule after that.

## Findings from steps 3 and 4

The simulator runs these in `dotnet test` (`tests/Dizzy.FirewoodBundle.Tests/CoopSim`). Links keep their order, as a reliable connection does, but take turns at random.

- **R1 to R6, F, J:** pass, and each fix has a twin test that goes wrong with the fix off.
- **S1:** 10,000 actions, one at a time, across a host and two guests: the peers always agree.
- **S1b:** peers acting at the same moment disagree in about 2 of 20 runs. Last write wins, and the host sees changes in a different order from each guest.
- **S2:** with 5% of messages lost, 19 of 20 runs disagree. Nothing sends a lost change again.
- **S3:** with the bundle packet, the spawn announcement or the echo guard off, the stress run goes wrong. With the held check off it does not: the model has no harm to the holder's own copy, so only R3 guards that rule.
- **X1 (fixed, J-025):** two take-offs at once used to leave a piece too many. Packets now carry the count they changed from, and the other side adds the difference.
- **X2 (fixed, J-026):** a combine racing a pick-up used to give the bundle the other item's ID. The host now ignores a pick-up of a bundle it just combined away.
- **Still open:** concurrent play still ends in disagreement in about 3 of 20 runs, from other races (for example health and hanging changed at once). A lost message is still never sent again, and since J-025 a later change no longer heals it.
- **Not yet done:** the 5% loss run for X cases and Layer 2 (see the check above). X1 and X2 are fixed.

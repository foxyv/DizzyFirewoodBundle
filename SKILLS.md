# Working on Dizzy Bundle Up

How changes are recorded in this project. Follow this for every fix, whoever makes it.

## Dev journal

[docs/dev-journal.md](docs/dev-journal.md) records every fix, and every diagnostic change made while hunting a bug.

### When to add an entry

- **Add one:** whenever you change code to fix a problem, or add logging or tools to find one.
- **Skip it:** for new features, refactors and version bumps. Those go in the commit message and the release notes.

### How to write it

Add the entry at the end of the journal, with the next number (`J-011`, `J-012`, …). Never renumber or delete an entry.

```markdown
## J-011: <the problem, as the player saw it>

- **Date:** YYYY-MM-DD
- **Version:** <the version it ships in, or — if not yet>
- **Status:** Fixed | Untested | Superseded by J-0NN | Diagnostic | Open
- **Issue:** [#NN](https://github.com/foxyv/DizzyFirewoodBundle/issues/NN)   (leave the line out if there is none)

**Problem.** What the player saw, or what the log showed.

**Cause.** Why it happened: the class, method or field at fault. Say "not confirmed" if it is a suspect.

**Fix.** What changed and where.
```

- Write the cause from what the code or a log shows, not from a guess. If it's a guess, say so.
- **Status:** start a fix as **Untested**. Change it to **Fixed** only once it's confirmed in play. If a later entry replaces it, mark it **Superseded by J-0NN**.
- **Open:** a problem with no fix yet can have an **Open** entry recording what is known. Update it when the fix lands rather than adding a duplicate.

## Commits

Every commit that contains a fix names its journal entries. Put them in the last line of the message, before any `Co-Authored-By` line:

```
Fix bag click surface after building it in hand.

The hull collider kept the layer the bag had when it was built, so a bag
built while held could not be looked at once hung.

Journal: J-009
```

- **Several fixes:** list them all: `Journal: J-005, J-006`.
- **Same commit:** add or update the journal entry in the commit that contains the fix, so the two stay together.
- **Pull requests:** list the journal entries a pull request covers in its description.

## Releases

The release notes summarize the fixes in plain words for players. The journal keeps the technical detail. When a release ships, set the **Version** of each entry it contains.

## Measuring the shape of a piece

Do this before building a net, hull, collider or layout around a game mesh, and when a player says a net doesn't match the item or has gaps. The game's meshes can't be read while the game runs (`mesh.isReadable` is false), so the shape is measured offline from the asset files. Don't judge a fit by eye or by the bounding box.

### Steps

1. **Find the mesh.** `sw_assets.py prefab 39` shows the prefab's mesh name, its bounds centre and its half size. The thin axis is the one with the smallest half size. The pork chop is `porkchop` and is thin in y (2.7 cm half thickness).
2. **Measure it.** From the repo root:

   ```powershell
   py -3 scripts/measure_outline.py porkchop
   py -3 scripts/measure_outline.py porkchop --table 0.2034,0.1991,...
   py -3 scripts/measure_outline.py lamb_chop --csharp
   py -3 scripts/measure_outline.py porkchop --fixture PorkTriangles
   py -3 scripts/measure_outline.py porkchop --edges PorkEdges
   ```

   `--edges` prints the shape data that `MeatChopShapes.cs` holds. `--fixture` prints the flat triangles as C# test data. `tests/Dizzy.FirewoodBundle.Tests/ChopFixtures.cs` is made this way, so the tests measure the net against the real meat.

   It needs UnityPy and the `sailwind-modding` skill's `sw_assets.py`. Set `SW_ASSETS` to its path if it isn't in the default place. `--flat` names the thin axis (default y). `--cord` and `--gap` match `MeatNetThickness` and `MeatNetGap` (defaults 0.00375 and 0.004 metres).
3. **Read the result.** The script:
   - reads the mesh's points and triangles, turning UnityPy's x mirror back (checked against the mesh bounds);
   - drops the thin axis and marks every 0.5 mm cell of the flat shadow that a triangle covers;
   - walks outward from the middle of the mesh bounds, the same point the builder uses as the chop's centre, in 720 directions, and records the furthest covered cell. That is the **reach**;
   - with `--table`, lays the code's net over the reach as `MeatBagLayout.NetRadius` does (table value + cord + gap) and prints **net minus meat**: the least, average and most, how many directions put the net inside the meat (closer than one cord), how many are more than 1.5 cm off, and the runs of directions that are, in degrees;
   - builds the best table of the same size, where each slot is the furthest reach within half a slot either side, and prints the same figures for it. `--csharp` prints that table as an array.
4. **Judge it.**
   - The least should be about one cord plus the gap (0.8 cm). Anything lower is the net inside the meat.
   - A large most, or many directions more than 1.5 cm off, are the gaps a player sees.
   - If even the best table of that size has large gaps, the table is too coarse. Use more slots, or the mesh's outline points directly.
5. **Record it.** Quote the figures in the journal entry's Cause and Fix, and run the script again after changing the net.

### What it found for the meat bags

| | Least | Average | Most | Directions more than 1.5 cm off |
|---|---|---|---|---|
| Pork, 48-slot table (0.15.6) | 0.7 cm | 2.5 cm | 6.6 cm | 522 of 720 |
| Lamb, 48-slot table (0.15.6) | 0.7 cm | 4.7 cm | 12.1 cm | 600 |
| Pork, best 48 slots | 0.7 cm | 1.2 cm | 2.5 cm | 112 |
| Lamb, best 48 slots | 0.8 cm | 1.4 cm | 4.9 cm | 194 |
| Pork, 720 slots (now in the code) | 0.8 cm | 0.8 cm | 1.0 cm | 0 |
| Lamb, 720 slots (now in the code) | 0.8 cm | 0.9 cm | 1.9 cm | 3 |

The 48-slot table never went inside the meat, but it sat well off it. Even the best 48-slot table was 4.9 cm off in places on lamb, so the shape data is now each chop's exact silhouette (`MeatChopShapes`, made with `--edges`) and the table is made from it at 720 slots. The figures for 720 slots come from the script's grid, which is good to about 2 mm. The tests measure the exact shape.

### Limits

- It measures the mesh in the asset files, not what is drawn in the game. Check a fix in the game too.
- It looks down the thin side only. It says nothing about the end faces, the thickness or the cinch at the ends.
- The reach follows a notch cut into the edge down to its floor and bridges only where the edge doubles back over itself. Whether a net should follow a notch or stay taut across it is a design choice, not something this measures.
- If the builder changes the point it measures from (`mesh.bounds.center`), the numbers no longer apply.

## Deploying to a test pack

`deploy.py` copies only `Dizzy.FirewoodBundle.dll` and its `.pdb`. The mod also needs `Dizzy.FirewoodBundle.Core.dll`, which holds the layout math. After deploying, copy `Dizzy.FirewoodBundle.Core.dll` and its `.pdb` from `src/Dizzy.FirewoodBundle/bin/Release/` into both the pack's `plugins/Dizzy.FirewoodBundle` folder and the library's `extracted/Dizzy.FirewoodBundle` folder. A stale Core next to a new mod DLL makes the game fail the first time it reaches a missing method. Compare the two files' hashes to be sure.

## Co-op simulation and testing

Bundles are shared through Sailwind Coop by `CoopBridge`. Two copies of the game can't run on one PC (Coop uses Steam P2P and needs an account for each player), so co-op is tested in layers. Each catches what the one before it can't. Read `docs/coop-simulator-plan.md` for the plan and the findings.

| Layer | What it runs | Where | Catches |
|-------|--------------|-------|---------|
| Simulator | The sync rules against a model of Coop | `tests/Dizzy.FirewoodBundle.Tests/CoopSim` | Races, merge mistakes, wrong rules |
| Source tests | The mod's source, read as text | `CoopGuardTests`, `CoopSourceRuleTests` | A new code path that skips a co-op rule |
| Probe | The mod's real packet code on a real bundle, one game | `CoopProbe.cs` | Mistakes in the code and the game objects |
| Session | Two players, then the log checker | `docs/coop-session-script.md`, `scripts/coop_log_check.py` | Whether Coop itself behaves as modelled |

### Running the tests

```powershell
dotnet test tests/Dizzy.FirewoodBundle.Tests
dotnet test tests/Dizzy.FirewoodBundle.Tests --filter "FullyQualifiedName~RaceHunt" --logger "console;verbosity=detailed"
py -3 -m unittest test_coop_log_check   # from scripts/
```

The second command also prints every distinct way a random game still ends with the peers disagreeing, each cut down to a few acts.

### How the simulator works

- **Core holds the decisions.** Anything the sync decides without the game lives in `src/Dizzy.FirewoodBundle.Core/CoopSync.cs` so the tests can run it: `BundleState`, `BundleWire` (the packet; `Protocol` is 2), `StackCode`, `CountMerge` (a base count of `Settled`, 0, means the packet stands), `DestroyedMemory`, `SyncRules`. If a new rule can be written without Unity types, put it there.
- **The model.** `SimNetwork` is a host and guests in a star, with a message queue per link. `SimPeer` is one player's game, `SimItem` an item. `SimRules` has one switch for each fact about Coop or each fix. The facts come from the decompiled Coop 0.4.0: the join snapshot, `amount` sent only with health, the host passing messages on, a remote-held item having `held` set, a pick-up of an unknown ID taking the nearest item of the prefab, a guest asking the host to approve a pick-up, a leaver's items dropping where they were. Check them again when Coop updates.
- **Delivery.** `Settle` and `SettleInterleaved(seed)` deliver everything, the second with links taking turns at random (each link keeps its order, as a reliable connection does). `LoseMessages`, `DuplicateMessages`, `Disconnect` and `Join` cut it up.
- **Every fix has a twin.** A regression test shows the fix works. A twin runs the same play with its `SimRules` switch off and shows it goes wrong. A fix whose twin still passes is not tested.
- **Random play.** `Scenario.cs` makes a game as a list of acts (`Generate`), runs it (`Run`) and cuts a failing one down to the fewest acts that still fail (`Shrink`). `LongRandomGames_WithRejoins_AlwaysEndInAgreement` runs 500. A new bundle action gets an `Op` there.

### Source tests and their lists

- `CoopGuardTests` fails if a way to change a bundle never reaches `CoopBridge.HeldByOther`. The ways that only touch the player's own hand are listed with a reason.
- `CoopSourceRuleTests` fails if a piece is made from a bundle without `RegisterToSave` and `CoopBridge.Spawned`, if a count or tie color is written outside the two `WriteCount` methods (which note the count first), or if an item is destroyed without `CoopBridge.Destroyed`.
- Each has an exemption list with a reason for every entry, and a test that fails when an entry is out of date. To exempt something, say why in the list. Do not loosen the rule.

### When you add or change a way to change a bundle

1. Ask `CoopBridge.HeldByOther` before changing a bundle that might be in another player's hands.
2. Write the count through `WriteCount`, so `CoopBridge.CountWillChange` runs first.
3. Announce a new piece with `CoopBridge.Spawned`, after `RegisterToSave`.
4. Call `CoopBridge.Destroyed` before an item is destroyed.
5. Add a simulator test with a twin. If the fix is a rule, add a `SimRules` switch.
6. Add a journal entry (J-NNN, status Untested, "Found by" saying how). Say in "Not covered" what the test can't show.
7. If the packet format changes, raise `BundleWire.Protocol` and update the frozen bytes in `CoopSyncTests`. Players on different protocols ignore each other's bundle packets.

### Co-op probe (one game, no second player)

With Sailwind Coop installed, set `Co-op Probe Key` (section Developer, off by default) in the Bundle Up config. Hold or look at a bundle and press the key. `CoopProbe.cs` feeds it the packets the simulator uses, as if another player had sent them, through the real `ApplyState` and merge, and writes `[Probe] PASS` or `[Probe] FAIL` lines and a summary to `LogOutput.log`. It checks the wire round trip, applying a bundle's own state, another player's count, the J-025 merge, a settled packet standing over a local change, a count Coop set (J-032), and that the bundle is put back as it was. Nothing is sent. A held hangable bundle shifts for a moment, because the probe really rebuilds it at one piece fewer and back.

To get bundles to try it on, deploy to a test pack (see above) and spawn crates with the NANDCommand console command `SpawnItem <code>`. Open the crate, then press G on a loose piece (or loose hook) to pick up a bundle of at least 3.

| Item | Crate | Item | Crate |
|------|-------|------|-------|
| Firewood | 108 | Cheese | 7 |
| Lantern candles | 131 | Goat cheese | 8 |
| Sausages | 15 | Oranges | 213 |
| Fishing hooks (hook line) | 104 | Apples | 224 |
| Bananas | 17 | Pork | 16 |
| Dates | 2 | Lamb | 4 |

Wood poles and round shot need the Tent and Plank and Dangerous Waters mods. Numbers repeat between lists, so check the name against `List items`. Not yet probed: a bundle hanging on a hook rather than held, poles and shot.

### Checking a co-op session

Coop's own behaviour is only tested in a session with another player. Both need the same build of Bundle Up and the same Coop version. `docs/coop-session-script.md` is a 12-step script with the log lines to expect. After it, each player sends their `BepInEx/LogOutput.log`, and `BepInEx/BundleUp-stall.log` if it exists:

```powershell
py -3 scripts/coop_log_check.py HOST_LogOutput.log GUEST_LogOutput.log
py -3 scripts/coop_log_check.py HOST_LogOutput.log GUEST_LogOutput.log --stall HOST_BundleUp-stall.log GUEST_BundleUp-stall.log
```

It prints each problem (`LOST`, `DIFFERS`, `UNKNOWN`, `WARNING`, `ERROR`, `STALL`) and exits 1 if there is one. Put what it finds in the journal entry of the fix.

### What is still unproven

- J-025 to J-033 and J-035 have not been played in a real session. The simulator models Coop as read from its code.
- Coop sends health and amount in its own packets, which the host settles only through the mod (J-031).
- A lost bundle packet is never sent again, and a duplicated guest packet would count twice. Coop's links are reliable and send no duplicates, so neither is expected.
- A bundle hanging on a hook, wood poles and round shot have no probe result.

# Dev journal

One entry per fix, and per diagnostic change made while hunting a bug. Newest last. Each entry has an ID (`J-001`, `J-002`, …) that commits refer to. See [SKILLS.md](../SKILLS.md) for how to write one.

Entries start with the co-op work in October 2026. Earlier fixes are in the git history and the [release notes](https://github.com/foxyv/DizzyFirewoodBundle/releases).

Status is one of:

- **Fixed:** confirmed in play.
- **Untested:** built and deployed, not yet confirmed in play.
- **Superseded:** replaced by a later entry.
- **Diagnostic:** logging or tooling, not a fix.
- **Open:** the problem is known, with no fix yet.

---

## J-001: Bundles made after a player joins aren't shared

- **Date:** 2026-10-12
- **Version:** 0.12.1
- **Status:** Superseded by J-002
- **Issue:** [#27](https://github.com/foxyv/DizzyFirewoodBundle/issues/27)

**Problem.** In Sailwind Coop, a candle bundle made after the other player joined showed to them as one candle.

**Cause.** Sailwind Coop sends an item's full saved state only when a player joins. After that it sends `amount` only alongside a health change, and on arrival it sets the field without rebuilding anything.

**Fix.** Told Coop about our `amount` changes (`ItemSyncManager.OnLocalItemAmountChanged`), and rebuilt bundles in a postfix on `OnRemoteItemAmountChanged`. This covered firewood, candles and lines only. Food keeps its count in `extraValue4`, which Coop never sends.

## J-002: Food bundles aren't shared in co-op

- **Date:** 2026-10-14
- **Version:** 0.12.2
- **Status:** Untested. Partly seen working: guests' bundle messages arrive.
- **Issue:** [#27](https://github.com/foxyv/DizzyFirewoodBundle/issues/27)

**Problem.** A salted cheese bundle made by the host showed to Robby as one cheese.

**Cause.** As in J-001, and Coop has no message for `extraValue4`.

**Fix.** `CoopBridge` sends a bundle's whole state on a packet type of its own (249; Dizzy Clothesline takes 241 to 248). The state is count, layout, color, food state, `amount` and `health`. A guest sends to the host, which passes it on. It's sent whenever a bundle is rebuilt, but not while bundles load, so a join doesn't flood everyone. This replaces the J-001 approach.

## J-003: Bags on the ground lose their net color on load

- **Date:** 2026-10-14
- **Version:** 0.12.2
- **Status:** Untested

**Problem.** Found while reading the code: a fruit or goat cheese bag set down, not hung, loaded with the default net color.

**Cause.** `SausageStacks.Decode` set the color only for hanging stacks.

**Fix.** It sets the color for every stack.

## J-004: Timing and message logs for the co-op freeze

- **Date:** 2026-10-14
- **Version:** 0.12.3
- **Status:** Diagnostic
- **Issue:** [#29](https://github.com/foxyv/DizzyFirewoodBundle/issues/29)

**What.** The log now gets:

- a line for each bundle message sent and received (`[Coop] Sent …`, `[Coop] Received …`, marked `(held)` when the other player holds the item);
- `[Stall]` lines for any rebuild over 30 ms;
- `[Stall]` lines for any frame over half a second.

## J-005: You can change a bundle another player is holding

- **Date:** 2026-10-14
- **Version:** 0.12.4
- **Status:** Untested
- **Issue:** [#29](https://github.com/foxyv/DizzyFirewoodBundle/issues/29)

**Problem.** While Robby held a goat cheese bag, the host was offered "add to bundle" on it. Changing it made the two games disagree about what was in it, and was followed by a freeze and a crash.

**Cause.** To freeze the physics of an item another player carries, Coop sets `item.held` to the *local* player's pointer. Our checks took `held` to mean the local player was holding it, or missed the item when `held` was empty.

**Fix.** `CoopBridge.HeldByOther` asks Coop's `_remoteHeldItems` and compares `held.GetHeldItem()`. While another player holds an item, combining, taking from and gathering it are refused.

## J-006: The drop check runs on an item another player carries

- **Date:** 2026-10-14
- **Version:** 0.12.5
- **Status:** Untested
- **Issue:** [#29](https://github.com/foxyv/DizzyFirewoodBundle/issues/29)

**Problem.** `FirewoodDropOverlapPatch` ran every frame on the bag Robby carried, because of the borrowed `held` in J-005. This was a suspect for the freeze, not a confirmed cause.

**Fix.** The patch skips items `HeldByOther`. Also added log lines before and after Coop applies another player's pick-up or drop of a bundle.

## J-007: Pieces taken off a bundle by a guest vanish

- **Date:** 2026-10-14
- **Version:** 0.12.6
- **Status:** Untested

**Problem.** When Robby took logs off a bundle, they vanished, and the host's count didn't change. The host's log said the bundle "is not here".

**Cause.** A piece taken off is a new item, and nothing told Coop about it. When a player picks up an item the host doesn't know, `OnRemoteItemPickedUp` takes the nearest item of the same prefab and gives it the new ID. The nearest log was usually the bundle itself, so the host's bundle took the new piece's ID and followed Robby's hand.

**Fix.** Every place that creates a piece (taking one off, a stove log, a hook off a line) calls `ItemSyncManager.OnLocalItemSpawned` right after `RegisterToSave`, through `CoopBridge.Spawned`.

## J-008: Stall watchdog

- **Date:** 2026-10-15
- **Version:** 0.12.7
- **Status:** Diagnostic
- **Issue:** [#29](https://github.com/foxyv/DizzyFirewoodBundle/issues/29)

**What.** A background thread notices when the game stops for 3 seconds or more and writes `BepInEx/BundleUp-stall.log`. The report gives how long the game was stopped, the last place the game reached, and the last bundle work. The places are: before physics, after physics, `Update`, `LateUpdate`, rebuilding a bundle, and Coop applying a pick-up or drop.

## J-009: A hung bag can only be clicked near its top or bottom

- **Date:** 2026-10-15
- **Version:** 0.12.8
- **Status:** Untested

**Problem.** A hung goat cheese bag could only be clicked in a tiny spot. The log also showed `GoPointer: ERROR: POINTED AT HELD ITEM`.

**Cause.** `GoPointer.PickUpItem` moves a held item to layer 2 (Ignore Raycast), and the drop moves it back to 0. Both change only the item's own object. A bag's clickable surface is a child (`BundleColliderHull`, tagged `ItemSubcollider`) that `AddHull` put on the item's layer at the moment it was made:

- **Made while held** (adding to a bag in hand): it stayed on layer 2, so the look ray passed through the net. Only the small core collider in the bottom could be hit.
- **Made on the deck:** it stayed on layer 0 when picked up, so the holder's own aim hit it.

**Fix.** A `FollowLayer` component on the surface copies the item's layer every `LateUpdate`. This covers bags, plus sausage stacks and cheese wheels with a tree or pile hull.

## J-010: Game freezes when another player drops a goat cheese bag

- **Date:** 2026-10-14
- **Version:** —
- **Status:** Open
- **Issue:** [#29](https://github.com/foxyv/DizzyFirewoodBundle/issues/29)

**Problem.** Each time Robby dropped a goat cheese bag, the host's game froze. That includes a bag that existed before he joined. Firewood bundles he dropped didn't freeze it.

**What is known.**

- The freeze comes after Coop finishes applying the drop: the log shows `...applied: dropped goat cheese`, then `goat cheese(Clone): ExitBoat`, then nothing.
- Checked and ruled out: the game's `ExitBoat`, `HangableItem.DisconnectJoint` and Dizzy Clothesline's hang patch. None can loop on a bag that isn't hanging.
- The main suspect is the bag's convex hull collider, which firewood bundles don't have.
- J-008's watchdog report from the next freeze should say where it stops.

## J-011: The knot sits on the end of a wood pole bundle

- **Date:** 2026-10-19
- **Version:** 0.14.3
- **Status:** Untested
- **Issue:** [#32](https://github.com/foxyv/DizzyFirewoodBundle/issues/32)

**Problem.** On a tied bundle of Tent and Plank wood poles, the knot sat on the end of the poles instead of on the cord.

**Cause.** `CreateKnot` placed the knot at the average of the piece origins along the long axis. A pole's origin is one end. The cord is wrapped at the middle of the mesh.

**Fix.** The knot uses the center of the mesh bounds on the long axis, which is where the cord sits.

## J-012: A wood pole bundle does not fall

- **Date:** 2026-10-19
- **Version:** 0.14.3
- **Status:** Untested
- **Issue:** [#32](https://github.com/foxyv/DizzyFirewoodBundle/issues/32)

**Problem.** A tied wood pole bundle stayed in the air.

**Cause.** A pole is cloned from the painting prefab, so it starts `attached` and can keep that mesh collider. `ItemRigidbody` stays kinematic while `attached` is set, and while a non-convex mesh collider is present.

**Fix.** `TentPoles.Release` clears `attached`, removes the mesh colliders, turns gravity on, and wakes the body when the bundle is put down.

## J-013: Tent and Plank treats a pole bundle as one pole

- **Date:** 2026-10-19
- **Version:** 0.14.3
- **Status:** Untested
- **Issue:** [#32](https://github.com/foxyv/DizzyFirewoodBundle/issues/32)

**Problem.** Picking up a wood pole bundle made Tent and Plank offer length keys, lean, and the "wood pole" notice.

**Cause.** The bundle is still prefab 643, so `WoodPoles.IsPole` was true. `PoleHeldLook` does not ask `IsPole`, and it drew one pole over the bundle while held.

**Fix.** A prefix makes `IsPole` return false while the count is above one. `PoleHeldLook` is turned off for that same time, and turned back on when one pole is left.

## J-014: Dropping a round shot stack stalls the game

- **Date:** 2026-10-19
- **Version:** 0.14.3
- **Status:** Untested
- **Issue:** [#30](https://github.com/foxyv/DizzyFirewoodBundle/issues/30)

**Problem.** Every time a round shot stack was dropped, the game lagged badly.

**Cause.** Each ball had its own capsule, and a twin on the physics body. A full pyramid is hundreds of colliders waking up together.

**Fix.** The stack has one convex pyramid collider, a little inside the iron.

## J-015: The meat bag's net is a different shape from the meat and has gaps

- **Date:** 2026-10-21
- **Version:** 0.15.6
- **Status:** Untested

**Problem.** A pork or lamb bag's net didn't follow the meat, and was open in places.

**Cause.** Measured against the game's meshes with `scripts/measure_outline.py`:

- The net's size came from a table of 48 distances. It sat 0.7 to 6.6 cm off the pork (2.5 cm on average) and 0.7 to 12.1 cm off the lamb (4.7 cm on average).
- When the game let the builder read the mesh, it filled the table from the mesh's 32 outline points, which left 33 of 48 slots empty. The net then cut into the meat by up to 9 cm on pork and 15 cm on lamb. The game's own meshes can't be read, so this path was probably never used.
- The 14 strands in each family were spaced evenly by angle, not by distance round the net. The chop is long and narrow, so the openings were 1.5 to 1.8 times wider at the ends than at the sides.

**Fix.**

- The shape is each chop's exact silhouette (`MeatChopShapes`). The outline table is made from it at 720 slots, and the unreadable-mesh path is gone.
- `MeatNetPath` measures the net's line round the meat. Strands are placed by distance along it, 26 to a family, so the openings are even. Hoops use the same line.
- Each family's strands are joined into one object. A net is two cords and two hoops, not 28 strands and two hoops.
- New tests in `MeatNetFitTests` measure the net against the real meat. They failed before the fix and pass after it.

## J-016: The meat bag's strands miss the hoop round the bottom

- **Date:** 2026-10-21
- **Version:** 0.15.6
- **Status:** Untested

**Problem.** The strings of the net didn't meet the thick string round the bottom of the bag, which looked odd.

**Cause.** A strand was drawn as straight lines between 37 even heights, from the bottom of the net to the top. The hoops sit at the edges of the meat, which is not one of those heights, so a strand cut across the corner there. Over each end the net pulls in quickly, so the corner was big: in the tests a drawn strand was up to 55 mm off the net on a bag of 8 chops, and 12 mm on a bag of 2.

**Fix.** `MeatBagLayout.NetHeights` picks the heights. The edges of the meat are always one, and they are close together where the net pulls in over each end and where a strand winds quickly round the meat. Drawn strands are now within 2 mm of the net, and meet the hoops. Hoops have 160 steps, up from 96. `MeatNetFitTests` has the checks.

## J-017: A meat bag with one chop left looks like it has two

- **Date:** 2026-10-21
- **Version:** 0.15.6
- **Status:** Untested

**Problem.** Taking a chop off a hanging bag of two left a bag that looked as if it still had two.

**Cause.** `FirewoodBundleBuilder.Apply` skips any bundle with one piece left, unless its kind hangs. A meat bag isn't a hanging kind, so the rebuild was skipped and the old two-chop look stayed while the count said one.

**Fix.** A meat bag hanging from a lamp hook is rebuilt whatever its count, the same as a hanging bunch of sausages. The last chop comes off the hook on the next take, as before. There is no unit test: the rebuild is in the Unity-bound builder.

## J-018: The meat bag has no visible ring at the bottom

- **Date:** 2026-10-21
- **Version:** 0.15.6
- **Status:** Untested

**Problem.** After the bottom circle was made bigger, the string that held the net together at the bottom was gone.

**Cause.** The strands used to gather into a circle 1.2 cm across, where they bunched into what looked like a ring of string. With the circle set by `Net End Size` (2.5 cm), the strands end separately on it. Nothing in the code drew a ring there: the only rings were the two hoops at the edges of the meat and the loop at the top.

**Fix.** The builder draws a ring at the bottom, on the circle the strands end on, a little thicker than a strand. `MeatNetFitTests` checks that the ring and the strand ends are on a circle of the set size.

## J-019: A meat bag can't be picked up from a lamp hook

- **Date:** 2026-10-21
- **Version:** 0.15.6
- **Status:** Untested

**Problem.** Clicking a meat bag hanging from a lamp hook did nothing: it stayed on the hook.

**Cause.** Lifting a hanging bundle was meant to let the hook go. That check was for bundle kinds that hang. A meat bag hangs, but its kind (pork or lamb) isn't one that does, so the hook was never told to let go. `HangableItem.LateUpdate` puts a hung item back on its hook every frame, so it snapped back.

**Fix.** `FirewoodPieces.IsHungBundle` is true for a hanging kind or a meat bag on a hook. The pick-up patch and the look prompt use it.

## J-020: The strands of a meat bag don't meet at the top

- **Date:** 2026-10-21
- **Version:** 0.15.6
- **Status:** Untested

**Problem.** At the top of the bag the strings ended in a ring, short of the cord that runs up to the hook.

**Cause.** Over the top end the net pulled in to a circle 1.2 cm across, not to a point. The cord to the hook starts at the middle of that circle and is about 0.6 cm thick, so 52 strand ends stood around it and none touched it. `MeatNetFitTests` showed it: ends up to 1.2 cm from the cord on bags of 2 and 8 chops.

**Fix.** The top end is a point (`MeatBagLayout.TopEnd`). Every strand ends on the cord. The bottom keeps its ring.

## J-021: A meat bag set on a table sinks into it

- **Date:** 2026-10-21
- **Version:** 0.15.6
- **Status:** Untested

**Problem.** A bag put down on a table clipped through it.

**Cause.** The bag rests on a flat stand under the bottom chop (`BundleColliderMath.PileStand`). The net hangs lower than that: the cone below the chop, then the ring at the bottom, which is 0.8 cm below the meat by default (`Net Bottom Height`) and a cord thick besides. The ring sank about 1.4 cm into the table.

**Fix, first try.** `MeatBagLayout.FootY` is the underside of the ring. The builder adds it to the points the stand is worked out from, so the stand is as wide as the chop and reaches down to the ring. The bag rests on its bottom, whatever `Net Bottom Height` is set to. The test checks the stand's width and where it ends.

## J-022: The cord at the top of a meat bag stops short of the hook

- **Date:** 2026-10-21
- **Version:** 0.15.6
- **Status:** Untested

**Problem.** The cord running up from the bag didn't reach the place it hangs from.

**Cause.** It ended at the point a hung item is pinned at, the middle of the lamp hook's peg. The peg is thicker than the cord and hides the end. The game's lanterns, which hang from the same peg, rise 2.4 to 3.3 cm above that point with a handle over the top.

**Fix.** The cord rises `MeatBagLayout.HookReach`, 2 cm, above the pin: past the top of the peg, which is 1.55 cm. A test keeps it between the peg's top and what the lanterns reach.

**Second try (the first did not fix it in the game).** The stand was never what decided the height. When a held item is aimed at a table, the game puts its origin `furniturePlaceHeight` above the surface (`GoPointer.LateUpdate`, 0.15 m unless set). A meat bag hangs down from its origin, up to half a metre, so everything below the top chop went through the table. The builder now sets `furniturePlaceHeight` to the distance from the origin down to the foot, plus 1 cm (`MeatBagLayout.PlaceHeight`), and a single chop gets the prefab's value back.

## J-025: Two players taking a piece off one bundle at once make a piece too many

- **Date:** 2026-10-23
- **Version:** —
- **Status:** Untested
- **Found by:** the co-op simulator (`CoopScenarioTests`, X1). Not yet seen in the game.

**Problem.** If two players each take one piece off the same bundle before hearing of the other's change, the bundle ends with one piece fewer than it should. Four candles, two taken off: the bundle showed 3 and the two pieces made 5 in all.

**Cause.** Each bundle packet carried the new state only. Each player sent "3 left" and applied the other's "3 left", so the second take-off was lost.

**Fix.** The packet also carries the count the sender changed from and the count it made (`BundleState.BaseCount`, `Count`; protocol 2). A player whose own count differs from that base changed the bundle meanwhile, so adds the sender's difference to their own count (`CountMerge.Resolve`, never below one). The base is noted the first time a count is written in a frame (`CoopBridge.CountWillChange`). Players on protocol 1 and 2 ignore each other's bundle packets, as for any other version difference.

**Limit.** A lost packet is still never sent again, and a later change no longer corrects the count, because it is now added to it. Before, the next change healed a lost one.

## J-026: A bundle combined while another player picks it up gives its ID to a different bundle

- **Date:** 2026-10-23
- **Version:** —
- **Status:** Untested
- **Found by:** the co-op simulator (X2). Not yet seen in the game.

**Problem.** The host combines bundle B into bundle A while the guest picks B up. The two games then disagree about which item has which ID, and the pieces in A are shown under B's ID.

**Cause.** B is gone when the pick-up reaches the host. For an item it does not know, Coop takes the nearest item of the same prefab and gives it the new ID (as in J-007). That is A.

**Fix.** `FirewoodPieces.Consume`, which destroys a combined bundle, notes its ID (`CoopBridge.Destroyed`). For 30 seconds a pick-up of that ID is ignored (`DestroyedMemory`, checked in the patch on `OnRemoteItemPickedUp`). The guest's copy goes when it hears of the destroy. Only the combine path is covered; other places that destroy an item are not.

## J-027: A way to check two players' logs for bundles that went out of step

- **Date:** 2026-10-23
- **Version:** —
- **Status:** Diagnostic

**What.** `scripts/coop_log_check.py` reads the host's and the guest's `LogOutput.log` from one co-op session and reports bundles that were sent and never received, bundles whose last count differs between the two players, bundles sent to a player who does not have them, warnings and errors from Dizzy Bundle Up, and (with `--stall`) stall reports. It lists merges of simultaneous changes and ignored pick-ups as notes. `scripts/test_coop_log_check.py` tests it.

**Log change.** After applying a bundle from another player, the mod now logs `[Coop] Applied <name> <id>: <n> pieces.` The checker compares final counts with it. Both players need this build.

**Why.** Two copies of the game can't be run on one PC (Coop needs a Steam account for each player), so a real session with another player is the only test of Coop's own sync. This makes a session's result readable.

## J-028: You can change the color or width of a bundle another player is holding

- **Date:** 2026-10-23
- **Version:** —
- **Status:** Untested
- **Found by:** `CoopGuardTests`, which reads the source for ways to change a bundle that never ask `HeldByOther`. Not yet seen in the game.

**Problem.** In co-op, pointing at a bundle in the other player's hands and pressing the tie color key or the stack width key changed it. The bundle was rebuilt in their hands, as in J-005.

**Cause.** `CycleLookedAtColor` and `CycleLookedAtWidth` act on the bundle the pointer is held on or looking at. J-005 added the `HeldByOther` check to combine, take from and gather, but not to these two. A third case, `GatherLookedAtHook` (G on loose hooks in an open crate), did not ask whether another player was carrying the crate.

**Fix.** Each checks `CoopBridge.HeldByOther` and does nothing if another player holds the bundle (or the crate). `CoopGuardTests` fails if any other way to change a bundle skips the check; the ways that only touch the player's own hand are listed there with reasons. The simulator has tests that color and width are refused while held and allowed for the holder.

## J-029: Two players changing one bundle at once could end with different bundles

- **Date:** 2026-10-24
- **Version:** —
- **Status:** Untested
- **Found by:** the simulator's shrinker (`RaceHuntTests`). 53 of 400 random runs ended apart; 21 do now. Not yet seen in the game.

**Problem.** Every change sends the whole bundle. If the host and a guest changed the same bundle in the same moment (one recolors, one takes a piece off), each took the other's packet over its own and the two swapped. They stayed different.

**Cause.** The host passed a guest's packet on to the other guests as it was, and nobody decided which of two changes came last.

**Fix.** The host applies a guest's packet, then sends what the bundle now is to everyone, the sender included (`CoopBridge.Receive`; log line `[Coop] Settled <name> <id>: <n> pieces.`). The host's order of changes is the order on every screen. The packet carries a base count of 0, which `CountMerge.Resolve` takes to mean "this stands": the guest's own merge no longer applies to it. The packet format is unchanged (protocol 2).

**Not covered.** Health travels in Coop's own item update, which the host does not settle, so a health change at the same moment as a bundle change can still end apart. Existence races (a destroy or combine against a pick-up) are separate.

## J-030: A pick-up of an item another player used up gave its ID to a different item

- **Date:** 2026-10-24
- **Version:** —
- **Status:** Untested
- **Found by:** the simulator's shrinker (`RaceHuntTests`). Not yet seen in the game.

**Problem.** One player uses an item up (or combines it) while another picks it up. A third player then had one of their items renamed to the used-up item's ID, and the players disagreed about which bundle was which.

**Cause.** The third player heard of the destroy first, then of the pick-up of an item that is gone. For an item it does not know, Coop takes the nearest item of the same prefab and gives it the new ID (J-007). J-026 only remembered destroys made by this mod's own combine, on the player who made them.

**Fix.** The mod also remembers every destroy Coop reports, ours (`OnLocalItemDestroyed`) and other players' (`OnRemoteItemDestroyed`), for 30 seconds, and ignores a pick-up of such an ID.

**Not a bug.** The hunt also showed an item in two places after two players picked it up at once and dropped it in different spots. That was the simulator: Coop 0.4.0 has the guest ask the host (`ItemPickupRequest`), and the host denies it if anyone holds the item (`ItemPickupDenied`), so only one player holds it. The simulator now does the same, and no random run ends apart.

## J-031: A health change at the same moment as another change could end with different bundles

- **Date:** 2026-10-24
- **Version:** —
- **Status:** Untested
- **Found by:** the simulator's shrinker (`RaceHuntTests`). Not yet seen in the game.

**Problem.** One player's health change (a candle burning down, food spoiling) at the same moment as another player's change to the same bundle could leave the two with different health, or different counts and colors.

**Cause.** Health and amount travel in Coop's own packets (`ItemHealthChanged`, `ItemAmountChanged`), which the host only passes on. J-029 had the host settle bundle packets but not these.

**Fix.** After Coop's host applies a health or amount change to a bundle, the mod has the host settle the bundle too (`SettleAfterCoopUpdate`, a postfix on `OnRemoteItemHealthChanged` and `OnRemoteItemAmountChanged`). Nothing changes for other items.

**Also.** The simulator let a message the host passed on overtake the host's own earlier messages to the same guest, which a real connection can't do. It now keeps each link in order. That showed the remaining random failures were fewer than thought: 1 of 400 runs now ends apart (the double drop in J-030).

## J-032: A count change and a health change in one frame were counted twice by the other players

- **Date:** 2026-10-24
- **Version:** —
- **Status:** Untested
- **Found by:** the simulator's shrinker, once random play could include a guest dropping out and rejoining. The failing run had no rejoin in it. Not yet seen in the game.

**Problem.** A candle bundle changed in count and in health in the same frame (for example, a piece taken off while the candle burns down) ended with too many pieces on the other players' screens: 1 became 3 on the host and 5 on the guests.

**Cause.** A candle bundle keeps its count in `amount`, and Coop's health packet (`ItemHealthChanged`, then `ItemAmountChanged`) carries `amount`. So Coop set the new count on the other side first. The bundle packet came after, saw a count unlike the one it started from, took that for a change made there too, and added its own change on top (the J-025 merge).

**Fix.** After Coop applies a remote amount to a bundle, the mod remembers the count it set (`SetByCoop`). When a bundle packet arrives and the count is still that, no change was made locally, so the packet stands. A local change clears the note.

**Not covered.** A guest that joins in the same frame as the host changes a count gets the change twice (the snapshot has it, and the bundle packet adds it again). The window is one frame. It is a test, `KnownLimit_AJoinInTheSameFrameAsACountChange_CountsItTwice`.

## J-033: A guest joining in the same frame as a count change got the change twice

- **Date:** 2026-10-24
- **Version:** —
- **Status:** Untested
- **Found by:** the simulator's random play with guests dropping out and rejoining. Not yet seen in the game.

**Problem.** If a guest joined in the frame the host changed a bundle's count, the guest ended with the change twice (a count of 2 changed to 3 showed as 4).

**Cause.** The join snapshot already had the new count. The host's bundle packet followed and carried the change as a difference from the old count, which the guest added to what it already had (the J-025 merge).

**Fix.** The host's own bundle packets now carry a base count of 0, which `CountMerge.Resolve` takes as "this stands". The merge was only needed to keep two players' simultaneous changes both; since J-029 the host settles every guest change after the host's packet, so a guest has nothing left to merge on the host's packets. The guest's packets to the host still carry their base.

**Also.** A duplicated host packet is now harmless (a duplicated guest packet still counts twice, as before). 5000 random games of 60 steps, up to 6 acts at once, with rejoins, all end in agreement; 500 of them run as a test.

## J-034: A co-op probe that runs the real packet code in one game

- **Date:** 2026-10-24
- **Version:** —
- **Status:** Diagnostic

**What.** `CoopProbe.cs` adds a developer key (`Co-op Probe Key`, off by default). Held or looked at, a bundle gets the packets the simulator uses, as if another player had sent them, through the real `ApplyState` and merge. It logs `[Probe] PASS` or `FAIL` for each check and a summary, then puts the bundle back as it was. Nothing is sent.

**Why.** The simulator models Coop and the mod's rules, but not the mod's code or the game's objects. Two copies of the game can't run on one PC. This runs the mod's own packet code on a real bundle with no second player. It does not test Coop's own sync or the network.

**Result (2026-10-24).** Run in the game on the Dizzy Oct 2026 pack, single player, 14 runs on 8 kinds of bundle: firewood, candles, cheese, bananas, lamb, pork, dates and apples. All 11 checks passed every time (`ALL PASSED: 11 passed, 0 failed`), with no `FAIL` and no errors, and each bundle was put back as it was. This covers both ways a count is stored (candles in amount, food in a stack code) and the J-025, J-029/J-033 and J-032 rules on real game objects.

**Not tried yet.** Sausage stacks, hook lines, goat cheese, oranges, poles and shot. Hook lines use different code from the others and are the likeliest to differ. The probe does not test Coop's own sync, the host's settle step or the network; only a session with another player does.

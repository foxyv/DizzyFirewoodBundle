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

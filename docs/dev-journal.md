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

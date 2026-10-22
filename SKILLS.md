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

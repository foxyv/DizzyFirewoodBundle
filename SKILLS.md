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

# RAGE 2 Filelist

Hash-to-path mapping for RAGE 2 assets.

## Numbers at a glance

| Metric | Value |
|--------|-------|
| Entries shipped in `data/filelist.txt` | **103,428** |
| Sourced from | REDxEYE hash extractor (author of [ApexPredator](https://github.com/REDxEYE/APEX-PREDATOR)) |
| Resolved by us before REDxEYE's contribution | 36,695 (kept at `data/sources/filelist_we_built.txt`) |
| Gameplay corpus (46 `.arc`, `initial` + `supplemental`) | 39,519 hashes |
| Coverage of that gameplay corpus | **92.85%** (36,695 / 39,519) |
| Remaining gameplay orphans | 2,824 (engine-internal constants, no string in any accessible source) |
| Localization (`languages/eng`) | 45,739 hashes, separate universe |
| Union of everything present in the game | 85,258 hashes |
| Extra paths in the shipped filelist not present as files in our game copy | 66,733 |

The **92.85%** figure refers to the 39,519 hashes physically present in the 46 gameplay `.arc` files the toolkit extracts. The **103,428** figure is the full catalogue REDxEYE recovered, which is a superset: it includes DLC content, other locales, and paths referenced by the engine through embedded manifests (`.epe`, `.adf`, `.blo`) but not shipped as standalone files in every install.

Both numbers are correct. They describe different things.

## Files

- `data/filelist.txt` - the mapping currently shipped (103,428 entries)
- `data/sources/filelist_we_built.txt` - our pre-REDxEYE baseline (36,695 entries, historical)
- `methods.txt` - full documentation of every method we used to build our own filelist
- `verification.txt` - bit-a-bit verification report for the 36,695-entry baseline

## Format

Tab-separated: `<HEX16>\t<path>`

UTF-8 without BOM, LF line endings.

## Hash algorithm

MurmurHash3 x64 128-bit, seed=0, output h1 (first 64 bits).

Test vector: `hash("text/master_eng.stringlookup") = 8453EE3581F31F39`

## Notes on the 66,733 extra entries

Of the 103,428 entries in the shipped filelist, 66,733 do not correspond to any hash present in the `.tab` files of our reference game copy. Verified by cross-checking the filelist against every `.tab` in the install, including `languages/`.

These 66,733 entries fall into three categories:

1. **Content from REDxEYE's install not present in ours.** DLCs that our copy does not include, and locale files other than `eng` (English only here; other languages add up to ~40k hashes per locale).
2. **Engine-referenced paths.** Assets that the engine looks up by name from embedded manifests (`.epe`, `.adf`, `.blo`) but that are not present as standalone files. The path string exists inside the manifest; the asset itself does not exist in the archive.
3. **Content removed or renamed across patches.** Paths that existed in earlier builds of the game and are still present in the engine's asset registry, but were consolidated or renamed in later versions.

They are kept in the shipped filelist because they are useful as a reference catalogue and because some of them resolve to real assets when a different install (Steam complete, other locales) is used.

## Open question (future work)

We have not yet determined the exact proportion of category 2 vs category 3 above. A useful follow-up would be: extract strings from every `.epe`, `.adf`, and `.blo` manifest in the extracted corpus, hash every path-like string, and check how many of the 66,733 entries match. That would tell us how many of the extras are engine references vs content that simply is not in this install.

Not blocking. Not scheduled. Filed here for the record.

## Notes on the 2,824 gameplay orphans

Of our 39,519 gameplay hashes, 2,824 remain unrecovered. They are NOT reachable via static analysis - verified empirically by ~55 billion candidate tests across 15+ tools (2026-09-26 final sweep). The only remaining path would be a runtime hook on the engine hash function, which is impractical (requires playing the entire game across all biomes/DLCs, ~900 MB logs per 3 min of gameplay).

See `methods.txt` for the full list of methods used and dead ends.
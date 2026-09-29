# RAGE 2 Filelist

Hash-to-path mapping for RAGE 2 assets.

## Numbers at a glance

- **103,428 entries shipped** in `data/filelist.txt` - REDxEYE hash extractor output.
- **36,695 hashes we resolved ourselves** before REDxEYE's contribution (kept at `data/sources/filelist_we_built.txt`).
- **39,519 gameplay hashes** in the 46 `.arc` archives the toolkit extracts.
- **92.85% coverage** of that gameplay corpus (36,695 / 39,519 verified).
- **0 fabricated entries** - every hash verified with MurmurHash3 re-hash.
- Localization strings excluded (45,739 hashes, separate universe).

The 103,428-entry filelist is the fullest mapping available. The 92.85% figure is the coverage of *our* gameplay corpus - the assets the toolkit can actually extract and mod.

## Files

- `data/filelist.txt` - current shipped mapping (103,428 entries)
- `data/sources/filelist_we_built.txt` - our own pre-REDxEYE filelist (36,695 entries, historical)
- `methods.txt` - documentation of every method we used to build our own filelist
- `verification.txt` - bit-a-bit verification report for our 36,695-entry baseline

## Format

Tab-separated: `<HEX16>\t<path>`

UTF-8 without BOM, LF line endings.

## Hash algorithm

MurmurHash3 x64 128-bit, seed=0, output h1 (first 64 bits).

Test vector: `hash("text/master_eng.stringlookup") = 8453EE3581F31F39`

## Notes on the remaining 2,824 orphans

Of our 39,519 gameplay hashes, 2,824 remain unrecovered. They are NOT reachable via static analysis - verified empirically by ~55 billion candidate tests across 15+ tools (2026-09-26 final sweep). The only remaining path would be a runtime hook on the engine hash function, which is impractical (requires playing the entire game across all biomes/DLCs, ~900 MB logs per 3 min of gameplay).

See `methods.txt` for the full list of methods used and dead ends.
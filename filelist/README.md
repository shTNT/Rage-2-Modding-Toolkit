# RAGE 2 Filelist

Hash-to-path mapping for every asset in RAGE 2. Most complete filelist
to date - bit-a-bit verified, no fabricated entries.

## Coverage

- **36,695 / 39,519 = 92.85%** of gameplay hashes verified.
- **0 fabricated entries**, all verified with MurmurHash3 re-hash.
- Localization strings excluded (45,739 hashes, separate universe).

## Files

- `filelist.txt` - the hash-to-path mapping (36,695 entries)
- `methods.txt` - detailed documentation of every method used to build it
- `verification.txt` - bit-a-bit verification report

## Format

Tab-separated: `<HEX16>\\t<path>`

UTF-8 without BOM, LF line endings.

## Hash algorithm

MurmurHash3 x64 128-bit, seed=0, output h1 (first 64 bits).

Test vector: `hash("text/master_eng.stringlookup") = 8453EE3581F31F39`

## Notes on the remaining 2,824 orphans

The remaining unrecovered hashes are NOT reachable via static analysis.
Verified empirically by ~55 billion candidate tests across 15+ tools
(2026-09-26 final sweep). The only remaining path would be a runtime
hook on the engine hash function, which is impractical (requires
playing the entire game across all biomes/DLCs, ~900 MB logs per
3 min of gameplay).

See `methods.txt` for the full list of methods used and dead ends.


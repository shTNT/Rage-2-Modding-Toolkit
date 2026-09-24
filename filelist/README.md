# RAGE 2 Filelist

Hash-to-path mapping for every asset in RAGE 2. This is the most
complete filelist to date - bit-a-bit verified, no fabricated entries.

## Files

| File | Description |
|---|---|
| [`filelist.txt`](filelist.txt) | **35,999 unique hashes** in `<HEX16>    <path>` format. This is what you want. |
| [`verification.txt`](verification.txt) | Bit-a-bit re-hash report. Every entry is re-hashed from its path and confirmed to exist in a game `.tab`. |
| [`methods.txt`](methods.txt) | Every method that produced hashes, ranked by yield, plus a reproducibility section. |

## Coverage (gameplay assets, excluding localization)

| Universe | Hashes | Coverage |
|---|---|---|
| Initial | 14,459 / 16,385 | 88.25% |
| Supplemental | 21,540 / 23,134 | 93.11% |
| **Union** | **35,999 / 39,519** | **91.09%** |

Localization strings (45,739 unique hashes in a separate tree) are
excluded on purpose. They are not needed for asset modding and would
inflate the denominator 2.16x.

## Filelist format

    <HEX16>    <path>

- `HEX16` - 16-character uppercase hexadecimal hash
- `path`  - asset path as understood by the engine (lowercase, forward slashes)
- UTF-8 without BOM, LF line endings

Example lines:

    8453EE3581F31F39    text/master_eng.stringlookup
    E73A9076A24B7E8D    ai/tiles/43_37.navmeshc

## Hash algorithm

- MurmurHash3 x64 128-bit
- Input: path in UTF-8, lowercase, forward slashes, no prefix
- Seed: 0
- Output: `h1` (first 64-bit word of the 128-bit digest)
- Test vector: `hash("text/master_eng.stringlookup") = 8453EE3581F31F39`

If your hasher produces 0 matches, verify the test vector before
assuming your paths are wrong.

## Verification

Every entry in `filelist.txt` has been re-hashed from its stored path
and confirmed to exist in a game `.tab` file.

- **35,999 / 35,999** re-hash OK
- **0** mismatches
- **0** ghosts (hashes not present in any `.tab`)
- **Verdict: PASS**

See `verification.txt` for the full report with 12 systematic samples.

## Reproducibility

`methods.txt` documents every method that produced hashes, ranked by
yield, plus a reproducibility section explaining how each was
implemented. Anyone can rebuild this filelist from the source data
(DECA raw filelist + a RAGE 2 installation) by following that section.

## Future updates

This is the most complete filelist to date, but not necessarily the
last. New methods or new community sources may recover additional
hashes. If a future version is published, it will appear in this
directory and `methods.txt` will list the incremental additions.

## License

CC0 1.0 Universal (public domain dedication). No copyrighted game
assets - only hashes and path strings, which are functional identifiers.


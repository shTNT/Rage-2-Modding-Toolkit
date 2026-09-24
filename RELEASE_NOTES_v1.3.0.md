# RAGE 2 Modding Toolkit v1.3.0

**Released:** 2026-09-24
**SHA256:** E356BBCD388FD71F72A471583A5388BDEF24448D819889739F3DECF54D2B2137

## What's new

- **Supplemental universe support.** The toolkit now scans both
  `archives_win64/initial` and `archives_win64/supplemental`. Previous
  versions only touched the initial universe, missing ~23,000 additional
  hashes present in the DLC/supplemental archives.
- **Three-file filelist.** `filelist.txt` + `filelist_extra.txt` +
  `filelist_supplemental.txt`, all loaded with hash-level dedup.
- **Definitive filelist shipped in-repo.** `FILELIST_DEFINITIVO/`
  contains a bit-a-bit verified filelist with 35,999 unique hashes,
  the verification report, and the full method documentation.

## Coverage (gameplay assets, excluding localization)

| Universe | Hashes | Coverage |
|---|---|---|
| Initial | 14,459 / 16,385 | 88.25% |
| Supplemental | 21,540 / 23,134 | 93.11% |
| **Union** | **35,999 / 39,519** | **91.09%** |

Localization strings (45,739 unique hashes across all locales) are
excluded on purpose — they inflate the denominator 2.16x and are not
needed for asset modding.

## Verification

Every entry in `FILELIST_DEFINITIVO/filelist.txt` has been re-hashed
from its path (`MurmurHash3 x64 128-bit`, seed=0, low 64 bits of h1)
and confirmed to exist in a game `.tab` file. See
`FILELIST_DEFINITIVO/verification.txt`.

## Methods

`FILELIST_DEFINITIVO/methods.txt` documents every method that produced
hashes — ranked by yield — plus a reproducibility section explaining
how each was implemented.

## Hash algorithm

- MurmurHash3 x64 128-bit, seed=0, low 64 bits of h1
- Test vector: `hash("text/master_eng.stringlookup") = 8453EE3581F31F39`
- Additional test vectors in `FILELIST_DEFINITIVO/methods.txt`

## Requirements

- Windows 10/11 x64
- Your own RAGE 2 installation
- ~25 GB free disk space for full extraction

## Credits

- **DECA project** — community filelist (358,057 paths) + reference
  TAB parser that documented the multi-block compression layout.
- **PredatorCZ / ApexToolset** — ddscConvert, R2SmallArchive.
- **Microsoft / DirectXTex** — texconv.
- **rage-2-archive torrent** — cross-reference source.

## Legal

Unofficial tool. Not affiliated with Avalanche Studios, Bethesda
Softworks, RAD Game Tools, Epic Games, or their subsidiaries. Users
must own a legitimate copy of RAGE 2.
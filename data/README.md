# Filelists

Hash-to-path mappings used by RAGE2Toolkit to recover real file names from the .arc archives.

## Files

| File | Lines | Description |
|------|-------|-------------|
| filelist.txt | 11,714 | Base hash to path mappings |
| filelist_extra.txt | 2,498 | Extra mappings recovered via brute-force + cross-orphan manifest mining |
| filelist_raw.txt | 358,057 | Raw path database (DECA source) - input for brute-force tools |

## Coverage

Combined: 14,212 / 16,385 hashes (86.74%).

The toolkit loads filelist.txt first, then overlays filelist_extra.txt. To update coverage, replace filelist_extra.txt with a newer version.

## Sources

- filelist.txt / filelist_raw.txt: from the DECA project (https://github.com/kk49/deca).
- filelist_extra.txt: recovered by this project via MurmurHash3 brute-force + cross-orphan string mining (see CHANGELOG).

## Format

filelist.txt and filelist_extra.txt:

    <16-hex-digits>\t<path>

filelist_raw.txt: one path per line, no hash.

## Contributing

If you recover additional hash to path mappings, append them to filelist_extra.txt (one per line, tab-separated) and open a PR or share them in the DECA Discord #rage-2 channel.

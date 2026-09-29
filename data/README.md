# Filelist

`filelist.txt` - 103,428 hash-to-path entries mapping 64-bit asset hashes to their original engine paths.

## Numbers at a glance

| Metric | Value |
|--------|-------|
| Entries shipped | **103,428** |
| Sourced from | REDxEYE hash extractor (ApexPredator) |
| Our own resolved hashes | 36,695 (kept under `sources/filelist_we_built.txt`) |
| Gameplay corpus | 39,519 hashes (46 `.arc` files) |
| Our corpus coverage | **92.85%** |
| Remaining corpus orphans | 2,824 (engine-internal constants) |
| Localization excluded | 45,739 hashes (separate universe) |

The **103,428 entries** are the full asset catalog as reverse-engineered by REDxEYE (author of [ApexPredator](https://github.com/REDxEYE/APEX-PREDATOR)). They include DLCs, extra regions and localization paths that our toolkit does not extract but which are useful references for modders.

The **92.85% coverage** refers specifically to the 39,519 gameplay hashes actually present in the 46 `.arc` archives the toolkit extracts. This is what matters for modding: not every asset the game ships is reachable from `archives_win64/`.

## Files

| File | Contents |
|------|----------|
| `filelist.txt` | The hash-to-path mapping currently shipped (103,428 entries) |
| `sources/filelist_we_built.txt` | Our own resolved hashes pre-REDxEYE (36,695 entries, historical) |
| `sources/filelist_deca.txt` | DECA community baseline (11,714 entries) |
| `sources/filelist_extra.txt` | Initial-universe extras recovered by brute-force (2,745 entries) |
| `sources/filelist_supplemental.txt` | Supplemental universe / DLC (21,540 entries) |
| `sources/filelist_raw.txt` | DECA raw paths, no hashes (358,057 entries, source data) |

## Format

`<HEX16><TAB><path>` - one entry per line, UTF-8 without BOM, LF line endings.

## Contributing

If you recover additional hash-to-path mappings, append them to `filelist.txt` (one per line, tab-separated) and open a PR, or share them in the DECA Discord #rage-2 channel.
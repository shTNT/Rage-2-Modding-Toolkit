# Filelist

`filelist.txt` - hash-to-path mapping currently shipped with the toolkit.

## Numbers at a glance

| Metric | Value |
|--------|-------|
| Entries shipped | **103,428** |
| Sourced from | REDxEYE hash extractor (ApexPredator) |
| Resolved by us pre-REDxEYE | 36,695 (kept at `sources/filelist_we_built.txt`) |
| Gameplay corpus (46 `.arc`) | 39,519 hashes |
| Coverage of that corpus | **92.85%** |
| Remaining gameplay orphans | 2,824 |
| Localization (`languages/eng`) | 45,739 hashes (separate) |
| Entries not present as files in our install | 66,733 |

The 103,428-entry filelist is a superset: it includes paths from DLCs and locales not present in every install, plus paths referenced by engine manifests but not shipped as standalone files. The **92.85%** figure refers specifically to the gameplay corpus physically present in the 46 `.arc` archives the toolkit extracts.

## Files

| File | Contents |
|------|----------|
| `filelist.txt` | Current mapping (103,428 entries) |
| `sources/filelist_we_built.txt` | Our pre-REDxEYE baseline (36,695 entries, historical) |
| `sources/filelist_deca.txt` | DECA community baseline (11,714 entries) |
| `sources/filelist_extra.txt` | Initial-universe brute-force extras (2,745 entries) |
| `sources/filelist_supplemental.txt` | Supplemental universe / DLC (21,540 entries) |
| `sources/filelist_raw.txt` | DECA raw paths, no hashes (358,057 entries) |

## Format

`<HEX16><TAB><path>` - one entry per line, UTF-8 without BOM, LF line endings.

## Contributing

If you recover additional hash-to-path mappings, append them to `filelist.txt` (one per line, tab-separated) and open a PR, or share them in the DECA Discord #rage-2 channel.
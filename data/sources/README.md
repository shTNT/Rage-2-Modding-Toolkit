# Filelist sources

Preserves the original filelists merged into the unified
`../filelist.txt`. Kept for documentation and process integrity.
The toolkit only reads the unified file.

## Files

| File | Origin | Entries |
|---|---|---|
| `filelist_deca.txt` | DECA community filelist | 11,714 |
| `filelist_extra.txt` | Initial-universe extras (brute-force) | 2,745 |
| `filelist_supplemental.txt` | Supplemental universe (DLC) | 21,540 |
| `filelist_raw.txt` | DECA raw paths (source data, no hashes) | 358,057 |

## Why the split

The three hash-to-path filelists come from different sources and
stages of the project:

- `filelist_deca.txt` — community baseline (DECA).
- `filelist_extra.txt` — entries recovered by brute-force on the
  **initial** universe only.
- `filelist_supplemental.txt` — entries for the **supplemental**
  universe (DLC). Zero overlap with initial.

The unified `../filelist.txt` is the merge of all three, deduped by
hash. See `../../filelist/methods.txt` for the derivation.


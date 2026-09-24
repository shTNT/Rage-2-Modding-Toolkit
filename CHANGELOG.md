# CHANGELOG â€” RAGE2Toolkit

## [1.3.1] - 2026-09-24

### Changed
- Toolkit loads a single unified `data/filelist.txt` (36,126 hashes)
  instead of three separate files.
- Original sources preserved under `data/sources/` for documentation.


## [1.3.0] - 2026-09-24

### Added
- Supplemental universe support: toolkit enumerates both
  `archives_win64/initial` and `archives_win64/supplemental`.
- Three-file filelist loading: `filelist.txt`, `filelist_extra.txt`,
  `filelist_supplemental.txt`.
- `filelist/` folder:
  - `filelist.txt` â€” 36,126 unique hashes (91.41% gameplay coverage)
  - `verification.txt` â€” bit-a-bit re-hash report (0 mismatch, 0 ghost)
  - `methods.txt` â€” documentation of every method that produced hashes
- Release artifact `RAGE2TOOLKIT-v1.3.0.7z` + `.sha256`.

### Changed
- `LoadFilelist` now reads `filelist_supplemental.txt`.
- Extraction wizard walks both archive universes.
- Repack wizard can target any `.arc` in either universe.

### Coverage
- Initial:      88.25% (14,461 / 16,385)
- Supplemental: 93.11% (21,540 / 23,134)
- Union:        91.41% (36,126 / 39,519)

### Hash algorithm
- MurmurHash3 x64 128-bit, seed=0, low 64 bits of h1.
- Test vector: hash("text/master_eng.stringlookup") = 8453EE3581F31F39




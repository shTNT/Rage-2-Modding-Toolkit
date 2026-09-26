# CHANGELOG - RAGE2Toolkit

## [2.0.3] - 2026-09-26

### Added
- Asset browser tree reorganized into 4 levels: Category > Entity > Resource > Asset.
  Uses new `data/type_map_v2.json` with 12 semantic categories.
- Descriptive filenames on extract: `<descriptive>_<HASH16>.<ext>` instead of just
  `<HASH16>.<ext>`. Example: `ark_assault_dif_4E37BD8EAD14BEA2.atx1`.
- Convert dropdown in the Single Extract browser: PNG / DDS / OGG / WAV.
  Auto-selects a sensible default based on selected asset type.
- sRGB-aware conversion: `_dif`/`_emc` textures get `-srgbi -srgbo` flags; normal
  and mask maps use linear. Fixes washed-out albedo appearance.
- Automatic cleanup of intermediate files (`.ddsc`, `.avtx`, `.dds`, `__tmp_rev`)
  after successful conversion.
- Unknown extension fallback: when magic detection fails, use the extension
  from the filelist path instead of writing `.unknown`.
- Alphabetical sort after extract so output folder shows files in order.
- Classify coverage for `.modelc`, `.epe`, `.epeb`, `.epeo` (native model/entity).
- `.atx1..9` warning: flagged as raw BC1 mip blob without descriptor.

### Changed
- Filelist updated: 36,192 -> 36,695 hashes (91.58% -> 92.85% coverage).
  Added 503 new hashes via StringPathHunter + TargetedSweep + MegaHunter sweeps.
- `methods.txt` v2.0: full documentation of 26 methods used and 19 dead ends,
  including the empirical ceiling (~55B candidate tests, 3 hits total).
- README + verification.txt + data/README.md + docs/index.md refreshed with
  new coverage numbers.

### Fixed
- Crash on tree selection: `tree.AfterSelect` was attached to a null reference
  in the constructor. Now attached after the TreeView is instantiated.

### Compatibility
- Backward compatible with mods from v1.3.x and v2.0.x. Extracted files from
  older versions (`<HASH>.<ext>`) continue to work — the repack parser accepts
  both naming schemes.

## [2.0.2] - 2026-09-26

### Fixed
- Repack writer: emit block table sentinel `FFFFFFFF FFFFFFFF` at end
  and increment `block_count`. Without it the engine computes the file
  table offset one entry short and hangs on load.
- Repack writer: sort file entries by hash before serializing. The engine
  performs a binary search; unsorted entries made the archive unreadable.

Both bugs were invisible from SHA comparison and only surfaced during an
in-game test with a replaced `.atx1` texture (`game8.arc`).

## [2.0.1] - 2026-09-25

### Fixed
- SingleExtractForm search now matches by hash in addition to path.
- type_map.json regenerated with all 39,519 hashes (was truncated to 500 per game).

## [2.0.0] - 2026-09-25

Major release. Extract 10x faster, new parallel writer, asset validators, converters, and a redesigned drag & drop wizard. First release with verified round-trip determinism.
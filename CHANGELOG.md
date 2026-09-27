# Changelog

All notable changes to RAGE 2 Modding Toolkit.

---

## [2.0.5] - 2026-09-28

### Added

- Drift detection: `ArcLastWritten` dict in `mods.json` registers SHA256 after each write
- `--force` on install and uninstall now respects drift warnings
- AVTX manual build: preserves original header (128 bytes) + payload from texconv mip chain
- `IsColorSuffix()` helper for sRGB heuristic on conversion
- `MapDxgiToTexconv()` to preserve original texture format (BC1/BC3/BC5/BC7)
- `TryReadAvtxHeader()` parser for AVTX descriptors
- Docs: `docs/ROLLBACK.md` and `docs/FALSE_POSITIVES.md`
- Harness V4: `MASTER-TEST-RUNNER-V4.ps1` with 19 phases

### Fixed (CRITICAL)

- **DDSC texture crash in-game**: `ddscConvert` (bundled, Generation Zero upstream) spreads mips across three files (`.atx1` + `.atx2` + `.ddsc`). RAGE 2 expects a monolithic `.ddsc` with the full mip chain inside. The toolkit now builds the AVTX file manually: original header + full mip payload from texconv. This was causing the game to hang on save load after installing any `.ddsc` texture mod.
- `ObjectDisposedException` in `TryConvertAtx1ToEditable`: `BinaryWriter` was closing the underlying `FileStream` before `fs.Write()`. 236 of 236 `.atx1` conversions were failing silently before this fix.
- `atx1 -> DDS` was returning PNG (hardcoded `-ft png`)
- `_nrm` and `_mpm` textures were granulated (sRGB applied to linear data)
- Cleanup deleted `.atxN` files even when conversion failed (silent data loss)
- `mod.json` was case-sensitive; lowercase keys were ignored
- CLI parser off-by-one: `--force` at end of args was never read
- Collapse by priority didn't respect the original asset format
- `ddscConvert` side files (`.atx1`, `.atx2`) polluted the converted dir
- `UninstallEngine` did `RemoveById` before drift check, leaving the registry inconsistent

### Changed

- `TabFormat.cs` extracted from `Repack.cs` (was 835 lines mixed with legacy writer + old GUI)
- `__converted__` staging moved from source folder to `%TEMP%\RAGE2Toolkit_conv_<guid8>`
- `PendingStatus` enum replaces `bool Ok` + `string Status` mix
- `type_map.json` and `type_map_v2.json` removed. Only `type_map_v3.json` ships.
- `Diag.cs` (temporary debug logger) removed

### Breaking

- New monolithic `.ddsc` layout. Mods built with v2.0.4 or older that touch DDSC textures are incompatible and must be repacked with v2.0.5.
- `mods.json` now has an `ArcLastWritten` field (backward compatible; missing field treated as no baseline)

---

## [2.0.4] - 2026-09-26

### Added
- Semantic asset tree v3 (12 top-level categories, recursive schema, deep subfolders)
- Search by extension: `.ddsc`, `.atx1`, `*.ogg`, etc.
- Conversion preferences dialog (gear icon): resolution 1024/2048, format PNG/DDS, audio Keep/WAV/OGG
- Batch extraction `ExtractMany` (236 assets in ~5s vs minutes before)
- Smart extract prompt: Convert & Extract / Extract raw only / Cancel
- Post-move to per-entity folders (`ark_assault\` instead of flat)
- Dark dialogs for warnings and confirmations
- `.atx1..9` cleanup notice in final status
- `_conversion_log.txt` for conversion failures

### Fixed (CRITICAL)
- **Race condition #1**: `MemoryMappedViewAccessor` shared across threads caused random extraction failures (145/236). Fixed: one accessor per thread.
- **Race condition #2**: `tmpDir` shared across threads caused random conversion failures (61/128). Fixed: unique guid per call.
- **Silent data loss**: `.ddsc`/`.avtx` deleted even when conversion failed. Fixed: conditional cleanup.
- **Wrong extension**: `.hrmeshc`/`.meshc` exported as `.adf`. Fixed: filelist takes precedence over magic.
- **Residual folders**: `__atxtmp` left behind after conversion. Fixed: recursive delete.

### Changed
- Extract wizard: `Browse & pick manually` is now the primary action
- Back/Next buttons removed from the Extract wizard
- HomeForm buttons: FILE BROWSER & EXTRACTOR / MODDING

### Verified
- 6 game archives, 10,488 files, 0 extraction failures
- Round-trip byte-identical
- Multi-pass x5 deterministic (5/5 identical SHAs)
- Baseline 46/46 intact after all tests

---

## [2.0.3] - 2026-09-26

- Asset browser tree v2 (Category > Entity > Resource type > Asset)
- Descriptive filenames on extract (`ark_assault_dif_4E37BD8EAD14BEA2.atx1`)
- sRGB-aware texture conversion per PBR suffix
- Convert dropdown in browser (PNG / DDS / OGG / WAV)
- Extract cleanup of intermediate files
- Unknown extension fallback to filelist path
- `.modelc`, `.epe`, `.epeb`, `.epeo` recognized as native containers
- Alphabetical sort after extract
- Fixed crash on tree selection

---

## [2.0.2] - 2026-09-26

Repack writer hotfix. Two format-level bugs that were silently hanging the
game on load after installing a modified `.arc`.

- Block table sentinel missing (engine read the file table at the wrong offset)
- File entries not sorted by hash (engine binary search failed)

Verified: round-trip with zero changes produces a byte-identical `.tab`.

---

## [2.0.1] - 2026-09-25

- `SingleExtractForm` search by hash
- `type_map.json` regenerated with the full 39,519 hashes

---

## [2.0.0] - 2026-09-25

- Extractor 10x faster (46 archives / 39,519 files / 61.66 GB in ~108 s)
- Parallel writer (RepackerCore v5.2) with Oodle Kraken level 1
- 14 asset validators and 4 converters
- Redesigned drag & drop wizard
- Full filelist in a single `data/filelist.txt`

---

## [1.3.1] and earlier

See `release-notes/` in the repository.
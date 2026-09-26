# Changelog

All notable changes to RAGE 2 Modding Toolkit.

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
- Extract wizard: `Browse & pick manually` is now primary action (larger, top position)
- Auto-advance wizard steps (no manual Next)
- Back/Next buttons removed from Extract wizard
- Selection counter unified (checkbox mark != cart add)
- Remove button eliminated (only Clear Selection remains)
- HomeForm buttons: FILE BROWSER & EXTRACTOR / MODDING

### Verified
- 6 game archives, 10,488 files, 0 extraction failures
- Round-trip byte-identical
- Multi-pass x5 deterministic (5/5 identical SHAs)
- Baseline 46/46 intact after all tests

---

## [2.0.3] and earlier

See GitHub releases for historical changelogs.


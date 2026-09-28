# Changelog

All notable changes to RAGE 2 Modding Toolkit.

---

## [2.0.5.1] - 2026-09-28

Hotfix on top of v2.0.5. Same archive format, same mod format. No compatibility changes.

### Fixed

- **Extraction was silently skipping assets depending on the Resolution preference.** With `Resolution = 2048` selected, `.ddsc`/`.avtx` textures were extracted as raw bytes instead of being converted. With `Resolution = 1024`, `.atx1` textures were skipped. Conversion is now unconditional: every supported extension is converted through its natural path, regardless of preferences.
- **The UI still reported v2.0.4 in the window title and header.** The csproj version was never bumped between v2.0.4 and v2.0.5. Fixed.

### Changed

- The `Texture resolution` group was removed from `Conversion preferences`. It is no longer used.

---

## [2.0.5] - 2026-09-28

Full modding pipeline: Mod System backend + Mod Manager GUI + redesigned Repack wizard + critical DDSC fix.

This release turns the toolkit from "extractor + repacker" into a complete modding platform. Mods are now 1-5 MB `.zip` files instead of 700 MB `.arc` replacements.

### Added - Mod System

- `ModMetadata.cs`: `mod.json` schema 1 (name, version, author, description, license, dependencies, conflicts)
- `ModsStore.cs`: portable registry at `mods/mods.json` + `mods/library/` (original zips) + `mods/backups/` (original `.arc` files). No `%APPDATA%`, no registry keys.
- `ModValidator.cs`: per-asset validation, hash grouping, native-over-convertible priority
- `ModPackager.cs`: folder -> validated `.zip` with `mod.json` injected
- `ModInstaller.cs`: 6-phase install (READ, DETECT, CONFLICT, PRE_INSTALL, BUILD_TMP, COMMIT, REGISTER). Rollback on any failure.
- `ConflictChecker.cs`: hash-level conflict detection against installed mods
- `UninstallEngine.cs`: restores backups when no other mod touches the `.arc`; rebuilds with remaining mods otherwise; cleans up orphan backups
- `ModManagerForm.cs`: drag & drop `.zip`, list of installed mods, priority adjust, conflict panel, uninstall, `Restore All`

### Added - Repack wizard redesign

- 3-step wizard (was 5): Drop -> Scan -> Result
- Produces distributable `.zip` mods (1-5 MB) instead of full `.arc` replacements
- Asks for name + author via `ModMetaDialog` before packaging
- Collapses duplicate hashes by priority (`ddsc` > `atx1` > `atx2` > `dds` > `png`)

### Added - Drift detection

- `ArcLastWritten` dict in `mods.json` registers SHA256 after each write
- Install/uninstall refuses if the `.arc` was modified outside the ModManager
- `--force` overrides (both CLI and future GUI)
- `UninstallEngine` pre-checks drift before `RemoveById` to keep the registry consistent

### Added - Critical DDSC fix

- AVTX manual build: preserves original header (128 bytes) + full mip payload from texconv
- `IsColorSuffix()` helper for sRGB heuristic on conversion
- `MapDxgiToTexconv()` to preserve original texture format (BC1/BC3/BC5/BC7)
- `TryReadAvtxHeader()` parser for AVTX descriptors

### Added - Other

- Docs: `docs/ROLLBACK.md`, `docs/FALSE_POSITIVES.md`
- Harness V4: `MASTER-TEST-RUNNER-V4.ps1` with 19 phases

### Fixed

- **DDSC texture crash in-game**: `ddscConvert` spreads mips across three files. Now builds monolithic AVTX by hand. This was causing the game to hang on save load after any `.ddsc`/`.avtx` mod install.
- **`ObjectDisposedException` in `TryConvertAtx1ToEditable`**: `BinaryWriter` was closing the underlying `FileStream` before `fs.Write`. 236/236 `.atx1` conversions were failing silently.
- **`atx1 -> DDS` returned PNG**: hardcoded `-ft png`
- **`_nrm` and `_mpm` textures were granulated**: sRGB applied to linear data
- **Cleanup deleted `.atxN` files when conversion failed**: silent data loss
- **`mod.json` was case-sensitive**: lowercase keys ignored
- **CLI parser off-by-one**: `--force` at the end of args was never read
- **Collapse by priority ignored original format**: now uses `origExtByHash`
- **`ddscConvert` side files** (`.atx1`, `.atx2`) polluted the converted dir
- **`UninstallEngine` did `RemoveById` before drift check**: left the registry inconsistent
- **`type_map.json` and `type_map_v2.json` shipped stale fallbacks**: removed
- **`Diag.cs` temporary debug logger** wrote to a hardcoded dev path: removed

### Changed

- `TabFormat.cs` extracted from `Repack.cs` (was 835 lines mixed with legacy writer + old GUI)
- `__converted__` staging moved from source folder to `%TEMP%\RAGE2Toolkit_conv_<guid8>`
- `PendingStatus` enum replaces `bool Ok` + `string Status` mix
- Legacy `Repack.cs` (`Repacker` + `RepackForm`) deprecated

### Breaking

- New monolithic `.ddsc` layout. Mods built with v2.0.4 or older that touch DDSC textures must be rebuilt with v2.0.5.
- `mods.json` now has an `ArcLastWritten` field (backward compatible; missing field treated as no baseline)

### Verified in-game

- `.atx1` reskin (ark_assault dif) - visible in-game, no crash
- `.ddsc` reskin (ark_pistol dif) - visible in-game, no crash
- Baseline 46/46 intact after every test
- Harness V4: 19/19 PASS

---

## [2.0.4] - 2026-09-26

### Added
- Semantic asset tree v3 (12 top-level categories, recursive schema, deep subfolders)
- Search by extension: `.ddsc`, `.atx1`, `*.ogg`, etc.
- Conversion preferences dialog (gear icon)
- Batch extraction `ExtractMany` (236 assets in ~5s vs minutes before)
- Smart extract prompt: Convert & Extract / Extract raw only / Cancel
- Post-move to per-entity folders
- Dark dialogs for warnings and confirmations
- `_conversion_log.txt` for conversion failures

### Fixed (CRITICAL)
- **Race condition #1**: `MemoryMappedViewAccessor` shared across threads caused random extraction failures (145/236)
- **Race condition #2**: `tmpDir` shared across threads caused random conversion failures (61/128)
- **Silent data loss**: `.ddsc`/`.avtx` deleted even when conversion failed
- **Wrong extension**: `.hrmeshc`/`.meshc` exported as `.adf`
- **Residual folders**: `__atxtmp` left behind after conversion

### Verified
- 6 game archives, 10,488 files, 0 extraction failures
- Round-trip byte-identical, multi-pass x5 deterministic
- Baseline 46/46 intact

---

## [2.0.3] - 2026-09-26

- Asset browser tree v2 (Category > Entity > Resource type > Asset)
- Descriptive filenames on extract
- sRGB-aware texture conversion per PBR suffix
- Convert dropdown in browser
- Extract cleanup of intermediate files
- Unknown extension fallback to filelist path
- `.modelc`, `.epe`, `.epeb`, `.epeo` recognized as native containers
- Fixed crash on tree selection

---

## [2.0.2] - 2026-09-26

Repack writer hotfix. Two format-level bugs that silently hung the game on load.

- Block table sentinel missing
- File entries not sorted by hash

---

## [2.0.1] - 2026-09-25

- `SingleExtractForm` search by hash
- `type_map.json` regenerated with the full 39,519 hashes

---

## [2.0.0] - 2026-09-25

- Extractor 10x faster (46 archives / 39,519 files / 61.66 GB in ~108s)
- Parallel writer (RepackerCore v5.2)
- 14 asset validators and 4 converters
- Redesigned drag & drop wizard
- Full filelist in a single `data/filelist.txt`

---

## [1.3.1] and earlier

See `release-notes/` in the repository.
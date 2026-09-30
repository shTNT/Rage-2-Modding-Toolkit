# Changelog

All notable changes to RAGE 2 Modding Toolkit.

## [2.1.1] - 2026-09-30

### Fixed

- **Extract-all crash**: `ShowStats()` accessed `stepPanels[3]` which was never created (WizardExtract only builds 3 panels). Extraction and classification were already complete; the "Failed" dialog was a false positive while painting the summary. The wizard now advances to Step 4 and closes cleanly.
- **Wizard Extract freeze**: `BuildStep4()` was never called from the constructor and `GotoStep(3)` was missing at the end of `StartExtract`. Now shows the summary and "Finish".
- **Home layout**: bottom buttons (Browse RAGE2.exe / Check update / Select output folder / Open output folder) now form a symmetric 2x2 grid. The path label no longer gets truncated.
- **World Settings Editor**: STAGE is cleared on open (one-shot per session).

### Added

- **World Settings Editor multi-file session**: in-memory cache keyed by `(fileHash, ValueOffset)`. Edit multiple nodes across multiple files; a single SAVE builds one ZIP containing every affected `.rtpc`. A confirmation dialog lists the files before applying.
- **Wizard Extract**: removed the redundant "Extract all" button and the orphan "Pick what interests you." label. Rewrote the EXTRACT EVERYTHING hover text without jargon.
- **EXTRACT EVERYTHING** now matches the width of BROWSE & PICK MANUALLY (640px).

### Internal

- `Process.Start` now wrapped in `using` (6 sites: explorer + ddscConvert batch + RunTool).
- `TrimLogBox` capped at 5000 lines (defensive; was not the root cause).
- Diagnostic dumps in `StartExtract` (removable in a future version).

### Hotfix (2026-09-30, same day)

- **World Settings Editor**: the editor now clears `WORK\` on open and reloads it from `release\data\settings\` (bundled originals). Previously, edits from a previous session accumulated in `WORK\` and the editor showed them as "Current", so a modder who opened the editor after a previous session would see stale values instead of the game defaults, and any SAVE would bake those stale values into the ZIP. Now every session starts from the original game values.
- Asset `RAGE2TOOLKIT-v2.1.1.7z` replaced. SHA256 updated accordingly.
### Full Changelog

- https://github.com/shTNT/Rage-2-Modding-Toolkit/compare/v2.1.0...v2.1.1

## [2.1.0] - 2026-09-29

**World Settings Editor release.** The toolkit now includes a native C# settings editor that produces installable mod zips on SAVE. Wizard redesign for both Modding and Extract flows. 445/989 settings-editor hashes resolved.

### Added - World Settings Editor

- New native C# editor (replaces the external PowerShell editor). No Python, no PowerShell, no side dependencies.
- Edits 7 game setting files that were previously untouched publicly:
  - `Spawn Budget Pools` - world population tuning (civilians, vehicles, combatants, animals, encounters)
  - `Player Stats` - health, armor, movement, abilities, combat tuning
  - `Difficulty` - enemy scaling, cooldowns, damage per difficulty tier
  - `Damage Types` - bitmask definitions of every damage type
  - `Vehicle Types` - physics and handling per vehicle class
  - `Weather Settings` - presets, transitions, conditions
  - `Sun / Lighting` - global sun, sky, and lighting
- Settings files bundled into `data/settings/` - no manual extraction required on first run.
- SAVE produces a `.zip` mod ready for the Mod Manager (auto-name dialog, auto-stage, auto-package).
- Backup Manager dialog: list snapshots by timestamp, restore, delete, open folder.
- Bitmask detection: `Damage Types` node auto-detects as bitmask, only powers of 2 accepted, invalid values block SAVE.
- Breadcrumb in the properties view: `Difficulty > not_activew_damage > Weapons > T3 Cannon Cooldown (min)`.
- Hierarchical grouping of sibling entries by semantic token (`Defence`, `Stage1`, `Stage2`, ...).
- Source file descriptions shown under the file dropdown.
- `Settlements` file removed from the editable list (405 entries, zero editable properties).

### Added - Wizard redesign

- **Wizard Modding**: `MOD MANAGER` is now the primary action (620x88 magenta). `WORLD SETTINGS EDITOR` and `REPACKER` are secondary (300x70, dimmed magenta).
- **Wizard Extract**: `BROWSE & PICK MANUALLY` is now the primary entry (640x92 cyan). `EXTRACT EVERYTHING` is secondary (340x70, dimmed cyan).
- Hover tooltips with fade in/out on every action button.

### Added - Hash database

- 445 new hashes resolved for the settings editor (from RED_EYE kv table + targeted cracking).
- Classification by structural pattern (Hungarian prefix + camelCase/SCREAMING + engine suffix).
- 4 entries promoted to "probable" after cross-referencing with the exe string table and RED_EYE.
- Editor coverage: 445 / 989 = 45%. Remaining 544 are engine-internal hashes with no string in any accessible source.
- `settings_editor_cracked.json` renamed to `settings_editor_names.json` (neutral name; some AV engines flag "cracked" as a keyword).

### Added - Harness V4 (extended)

- 6 new phases (20-25): `settings-bundled`, `editor-features-strings`, `modding-wizard-strings`, `extract-wizard-strings`, `files-array-no-settlements`, `cleanup`.
- Harness paths fixed: previously pointed to `v2.0.5-build`, now `v2.1.0-build`.

### Fixed

- **CRLF vs LF handling in `Wizards.cs`.** Anchors written as LF failed against the CRLF file. Normalization now happens on read and write.
- **`lblDesc` field scoping.** The file description label was a local in `BuildUI`, unreachable from the update method. Now a class field.
- **Bitmask visual noise.** The `BITMASK - powers of 2` prefix appeared on every row. Now only in the column header, tooltip, and footer.
- **Orphan brace fallback in `Wizards.cs`.** A replacement left a `}` glued to a class declaration. Fixed with a precise block replacement.
- **`Copy-Item` relative path in build scripts.** `..\publish` resolved against the wrong CWD. Now absolute.

### Changed

- `Settings Editor` renamed to `World Settings Editor`.
- `FILES` array reordered by usefulness (Spawn Budget Pools first, Sun/Lighting last).
- 40 stale `.bak` files archived out of `release\`.

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
# Changelog

All notable changes to RAGE 2 Modding Toolkit.

---

## [2.1.5] - 2026-10-03

Extractor, converter and cancel fixes on top of v2.1.0. World Settings Editor, Mod Manager and Repacker are unchanged.

### Fixed

- **Filelist parser (ExtractorOpt + Repack).** Both were splitting the filelist by TAB; the shipped filelist uses spaces. Descriptions, extensions and RepackForm labels now populate correctly.
- **Phantom hashes in the browser tree.** `type_map_v3.json` contains 103,428 entries; only 36,695 actually live in the 50 `.tab` files of the game. The tree is now filtered at load against a live hash set built from the `.tab` files. Extraction counters now match (`Wanted == Found`, `Missing == 0`).
- **sRGB in `AssetConverters`.** Was using `-srgbi -srgbo -f R8G8B8A8_UNORM_SRGB` (double gamma). Now uses `-srgbi -f R8G8B8A8_UNORM_SRGB` (case D of the V2.0.1 addendum).
- **SingleExtractForm layout.** The tree was computed with `FOOTER_H = 76` while the footer was 110 px tall (`76 + 34`). The last 28 px of the tree were hidden behind the footer at any window size. Constant and footer height made consistent.
- **Cancel did not reset between runs.** After cancelling once, subsequent extractions aborted instantly. `_cancelRequested` is now reset and the previous `CancellationTokenSource` disposed at the start of each run.
- **Cancel during conversion.** Cancel used to be a no-op inside the avtx/atx1 loops. Now throws `OperationCanceledException`, propagated through `Extractor.ExtractAll` and the wizard catch block.

### Added

- **ATX1 mip chain support.** The old algorithm only handled square mip-0 BC1/BC3 blobs. Real `.atx1` in RAGE 2 are BC1/BC3 mip chains (mip 0+1, or 0+1+2), non-square, or cube maps. New `TryGuessAtx1Format` searches (W, H, mips, blockSize) matching the byte size, and writes a correct DDS header with `MIPMAPCOUNT` + `MIPMAP` caps. 4,682 previously failed conversions now succeed.
- **ATX1 in Wizard Extract.** `.atx1` used to be left untouched by `EXTRACT EVERYTHING`. Now converts in parallel (`MaxDegreeOfParallelism = 8`) alongside `.avtx`.
- **Per-entity output folders in Wizard Extract.** Same layout as `SingleExtractForm`: `output/MODELS/weapons/ark_assault/textures/...` instead of a flat `_EDITABLE/TEXTURES/`.
- **Conversion preferences gear in Wizard Extract.** Same gear as `SingleExtractForm`, only in the extract wizard. Modding and Repacker headers unaffected.
- **Dynamic format in the extract prompt.** Shows the target format chosen in the gear: `N texture(s) -> DDS editable` / `PNG editable`, `N audio(s) -> WAV editable / OGG editable / kept as-is`.

### Not changed

- World Settings Editor
- Mod Manager / ModInstaller / ModPackager / UninstallEngine
- RepackerCore
- Filelist (92.85% corpus coverage)

---

## [2.1.1] - 2026-09-30

### Fixed
- **Extract-all crash**: `ShowStats()` accessed `stepPanels[3]` before it existed. Extraction and classify already worked; the "Failed" dialog was a false positive on the summary. The wizard now advances to Step 4 with the full summary.
- **Wizard Extract freeze**: `BuildStep4()` was not called and `GotoStep(3)` was missing at the end.
- **Home layout**: bottom buttons rearranged in a symmetric 2x2 grid.
- **World Settings Editor**: `WORK\` cleared on open (one-shot per session).

### Added
- **World Settings Editor multi-file session**: in-memory cache of changes per `(fileHash, ValueOffset)`. Edit N nodes and M files, single SAVE produces one `.zip` with all affected `.rtpc`.
- **Wizard Extract**: removed the redundant "Extract all" button and the "Pick what interests you" label.

### Internal
- `Process.Start` in `using` blocks (6 sites).
- `TrimLogBox` with a 5000-line cap.
- Diagnostic dumps in `StartExtract` (removable in a future release).

---

## [2.1.0] - 2026-09-29

**World Settings Editor release.** The toolkit now includes a native C# settings editor that produces installable mod zips on SAVE. Wizard redesign for both Modding and Extract flows. 445/989 settings-editor hashes resolved.

### Added - World Settings Editor

- New native C# editor (replaces the external PowerShell editor). No Python, no PowerShell, no side dependencies.
- Edits 7 game setting files that were previously untouched publicly: Spawn Budget Pools, Player Stats, Difficulty, Damage Types, Vehicle Types, Weather Settings, Sun/Lighting.
- Settings files bundled into `data/settings/` - no manual extraction required on first run.
- SAVE produces a `.zip` mod ready for the Mod Manager.
- Backup Manager dialog: list snapshots by timestamp, restore, delete, open folder.
- Bitmask detection: `Damage Types` node auto-detects as bitmask, only powers of 2 accepted.
- Breadcrumb in the properties view.
- Hierarchical grouping of sibling entries by semantic token.

### Added - Wizard redesign

- **Wizard Modding**: `MOD MANAGER` is now the primary action. `WORLD SETTINGS EDITOR` and `REPACKER` are secondary.
- **Wizard Extract**: `BROWSE & PICK MANUALLY` is now the primary entry. `EXTRACT EVERYTHING` is secondary.
- Hover tooltips with fade in/out on every action button.

### Added - Hash database

- 445 new hashes resolved for the settings editor.
- Editor coverage: 445 / 989 = 45%.

### Added - Harness V4 (extended)

- 6 new phases (20-25): `settings-bundled`, `editor-features-strings`, `modding-wizard-strings`, `extract-wizard-strings`, `files-array-no-settlements`, `cleanup`.

---

## [2.0.5.1] - 2026-09-28

Hotfix on top of v2.0.5. Same archive format, same mod format. No compatibility changes.

### Fixed
- **Extraction was silently skipping assets depending on the Resolution preference.** Conversion is now unconditional.
- **The UI still reported v2.0.4 in the window title and header.** Fixed.

---

## [2.0.5] - 2026-09-28

Full modding pipeline: Mod System backend + Mod Manager GUI + redesigned Repack wizard + critical DDSC fix.

### Added - Mod System

- `ModMetadata.cs`: `mod.json` schema 1.
- `ModsStore.cs`: portable registry at `mods/mods.json` + `mods/library/` + `mods/backups/`. No `%APPDATA%`, no registry keys.
- `ModValidator.cs`: per-asset validation, hash grouping.
- `ModPackager.cs`: folder -> validated `.zip` with `mod.json` injected.
- `ModInstaller.cs`: 6-phase install. Rollback on any failure.
- `ConflictChecker.cs`: hash-level conflict detection.
- `UninstallEngine.cs`: restores backups when no other mod touches the `.arc`.
- `ModManagerForm.cs`: drag & drop `.zip`, list of installed mods, priority adjust.

### Added - Repack wizard redesign

- 3-step wizard (was 5): Drop -> Scan -> Result.
- Produces distributable `.zip` mods (1-5 MB) instead of full `.arc` replacements.

### Fixed

- **DDSC texture crash in-game**: `ddscConvert` spreads mips across three files. Now builds monolithic AVTX by hand.
- **`ObjectDisposedException` in `TryConvertAtx1ToEditable`**: `BinaryWriter` closed the `FileStream` before `fs.Write`.
- **`atx1 -> DDS` returned PNG**: hardcoded `-ft png`.
- **`_nrm` and `_mpm` textures were granulated**: sRGB applied to linear data.

# CHANGELOG - RAGE2Toolkit

## [2.0.3] - 2026-09-26

### Changed
- Filelist updated: 36,192 -> 36,695 hashes (91.58% -> 92.85% coverage).
- Added 503 new hashes via StringPathHunter + TargetedSweep + MegaHunter.
- methods.txt v2.0 with full dead-end documentation.
- README updated to reflect new coverage numbers.

### Notes
- No toolkit code changes. Only data/filelist.txt + docs.
- The toolkit reads the filelist from data/ at startup; drop-in replacement.

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

### Added - Performance

- **Extractor 10x faster.** Parallel jobs across all archives (LPT scheduling, 32 threads), memory-mapped reads, 8 MB per-thread buffers. 46 archives / 39,519 files / 61.66 GB extracted in ~108 s (was ~20 min). Peak RAM 3.3 GB.
- **Classify 20x faster.** `ddscConvert` runs in batches of 100 with `Parallel.ForEach` MaxDOP=4; file moves parallelized at MaxDOP=8. ~1 min total (was 15-19 min).
- **Parallel writer (RepackerCore v5.2).** 4-phase pipeline: PLAN (single-thread) -> COMPRESS (Parallel Oodle Kraken level 1) -> REPLAN (recompute offsets after compression) -> ALLOC/WRITE (parallel `RandomAccess.Write` with per-thread buffers). Deterministic across runs (4 consecutive SHA256-identical outputs). Multi-thread write speedup 2.78x on game8.

### Added - Validators

- **`AssetValidators` (14 types).** Structural + semantic validation for AVTX, DDSC, DDS, OGG, RIFF, BIK, CFX, RTPC, StringLookup, ADF, Havok, FMOD, Mesh, and a fallback Unknown. Returns `{Status: Valid | Warn | Invalid | Unknown}` with Errors/Warnings/Suggestions.
- **`r2probe validate`.** CLI command: `validate --path <file>` or `validate --dir <dir> --json`. Used by the master test runner.

### Added - Converters

- **`AssetConverters` (Classify + Convert).** Detect native / convert / manual / unknown per extension.
- **PNG / JPG / BMP / TGA -> DDSC** via bundled `texconv` + `ddscConvert`.
- **DDS -> DDSC** via bundled `ddscConvert`.
- **MP3 / WAV / FLAC -> OGG Vorbis** via `ffmpeg` (on-demand download; prompt if missing).
- **`r2probe convert`** and **`r2probe tools-status`** CLI commands.

### Added - GUI

- **Format & Files Guidelines window.** Replaces guesswork: what extensions exist, what's native, what needs conversion, what requires external tools, and the URLs to get them.
- **ConvertChoice A/B dialog.** When you drop a folder, the wizard asks: "My files are game-ready" (validate only) or "Convert with toolkit" (auto-convert PNG->DDSC, etc). ESC cancels.
- **Hash-name awareness.** If none of the dropped files have a 16-hex-char name, the wizard blocks with a full workflow tutorial. The filename IS the hash; the toolkit can't guess.
- **Drag & drop fixed.** Drop folder directly on the wizard's drop zone (or the button, if you prefer). Files highlight on drag-over. Works under `asInvoker` (see Fixes).
- **Crash handler.** Any unhandled exception opens a dialog with the full stack trace, a "Copy report" button, and "Open issue on GitHub" (URL pre-filled).
- **Path reset on start.** `config/paths.txt` no longer persists between sessions; the user picks game + output folder every time.

### Added - Filelist

- **Unified filelist: 36,192 hashes / 39,519 = 91.58%** of gameplay asset hashes mapped to their original engine path. Localization (~45,739 `text/*.stringlookup` hashes) excluded on purpose.
- **Bit-a-bit verified.** Every entry re-hashed from its path and confirmed present in a live `.tab`. Zero fabricated entries, zero overlap.

### Changed

- **`F14`/`F18`/`F1C` renamed** to `Padding` / `MaxCompressedBlockSize` / `UncompressedBlockSize` in the TAB 3.1 parser. Cross-verified against an independent C++ parser by [REDxEYE (ApexPredator)](https://github.com/REDxEYE).
- **`.atx1..atx9` recognized** in `Classify()`. `.atxN` are mipmaps, not independent textures: `.avtx` / `.ddsc` are the descriptors, `.atx1` is the base mip. `.atx2+` are lower LODs.
- **`r2probe tools-status`** reports bundled, downloadable and manual tools with URLs.

### Fixed

- **UIPI block on drag & drop.** The manifest used `highestAvailable`, forcing UAC elevation. Windows blocks drag & drop from Explorer (medium IL) into elevated processes (high IL). Reverted to `asInvoker`. Trade-off: if you drop into a Windows-protected folder (Pictures / Documents / Desktop), `texconv` may hit Controlled Folder Access - the wizard warns you before proceeding.
- **AVTX dimension offsets.** `width` and `height` are u16 at 0x0C/0x0E, not 0x08/0x0A. Verified against 284 real AVTX files.
- **DDSC payload_size=0 warning.** `.ddsc` variants store `payload_size=0`; the payload is `SizeBytes - header_size`. Validator no longer emits a false warning.
- **Wizard `fileCount`.** The "N files, M convertible" dialog was passing a hardcoded 0 for N. Now passes the real file count.
- **`ConvertChoiceForm` layout.** Cancel button was rendered outside the visible area; removed. ESC closes.
- **Advanced Tools button.** Renamed to "Advanced Tools (Legacy)" and no longer uses underscores (was `MOD_REPACK`, `Format_Files`).

### Removed

- Persistent config (`config/paths.txt` deleted at every startup).
- Auto-loaded Oodle DLL between sessions (deleted on close and on start).
- Legacy "MOD_REPACK" / "Format_Files" underscore labels.

### Known limitations

- **8.42% of gameplay hashes have no path** (3,327 entries). They extract and repack fine but show up as `<HASH>.<ext>`. Practically unusable for purposeful modding without opening them first.
- **MP4 / AVI / MOV -> BIK** requires RAD Video Tools installed manually. Not redistributable.
- **UI CFX / GFX -> SWF** requires JPEXS Decompiler.
- **FMOD banks** require FMOD Studio.
- **ffmpeg is not bundled.** On first audio conversion the wizard prompts; the user downloads it from https://www.gyan.dev/ffmpeg/builds/.
- **Mesh / animation editing** is not supported. Extract and repack only.
- **First-session test.** This release has been verified via the MASTER-TEST-RUNNER (13 phases, PASS) but a manual in-game load test is pending. Report any issue on GitHub.

### Breaking changes from 1.3.x

- `config/paths.txt` is no longer read or written. If you had a saved config, you'll re-select game + output folder once.
- The Oodle DLL is deleted on exit and on next start. It is re-copied automatically when you select RAGE2.exe.
- `F14`/`F18`/`F1C` field names in the source code changed (only affects anyone building from source).

## [1.3.2] - 2026-09-24

Public release currently on GitHub.

### Changed
- Filelist merged into a single `data/filelist.txt` (initial + extra + supplemental, deduplicated at hash level).
- Removed 106 phantom entries found by overlap analysis (base/extra).

### Coverage
- Union: 91.58% (36,192 / 39,519) gameplay hashes.
## [1.3.1] - 2026-09-24

### Changed
- Toolkit loads a single unified `data/filelist.txt` (36,192 hashes)
  instead of three separate files.
- Original sources preserved under `data/sources/` for documentation.


## [1.3.0] - 2026-09-24

### Added
- Supplemental universe support: toolkit enumerates both
  `archives_win64/initial` and `archives_win64/supplemental`.
- Three-file filelist loading: `filelist.txt`, `filelist_extra.txt`,
  `filelist_supplemental.txt`.
- `filelist/` folder:
  - `filelist.txt` — 36,192 unique hashes (91.58% gameplay coverage)
  - `verification.txt` — bit-a-bit re-hash report (0 mismatch, 0 ghost)
  - `methods.txt` — documentation of every method that produced hashes
- Release artifact `RAGE2TOOLKIT-v1.3.0.7z` + `.sha256`.

### Changed
- `LoadFilelist` now reads `filelist_supplemental.txt`.
- Extraction wizard walks both archive universes.
- Repack wizard can target any `.arc` in either universe.

### Coverage
- Initial:      88.25% (14,461 / 16,385)
- Supplemental: 93.11% (21,540 / 23,134)
- Union:        91.58% (36,192 / 39,519)

### Hash algorithm
- MurmurHash3 x64 128-bit, seed=0, low 64 bits of h1.
- Test vector: hash("text/master_eng.stringlookup") = 8453EE3581F31F39




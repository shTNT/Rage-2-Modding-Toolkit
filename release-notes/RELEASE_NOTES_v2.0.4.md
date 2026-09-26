# RAGE 2 Modding Toolkit — v2.0.4

**Release date**: 2026-09-26

---

## Highlights

This release focuses on **stability** and **batch extraction speed**. Two critical race conditions that caused random failures are now fixed. The semantic asset tree is completely redesigned.

---

## What's new

### Semantic asset tree v3

The single-asset browser has a fully redesigned tree organized by meaning, not filesystem:

- 12 top-level categories: WEAPONS, CHARACTERS, VEHICLES, ENVIRONMENT (with 6 subcategories), AUDIO, UI, VIDEOS, ANIMATIONS, EFFECTS, DEFINITIONS, GAME LOGIC, UNCATEGORIZED
- Deep subfolders where it matters: `WEAPONS > ark_assault > textures > skins > unicorn`
- 39,519 hashes organized

### Search by extension

Type `.ddsc`, `.atx1`, `*.ogg` in the search box to filter by extension exactly.

### Conversion preferences (gear icon)

Configure how the toolkit converts before extracting:

- **Texture resolution**: 1024 (from DDSC/AVTX descriptors) or 2048 (from ATX1 raw mip 0)
- **Texture output format**: PNG or DDS
- **Audio output format**: Keep original / Force WAV / Force OGG

Preferences persist across sessions.

### Batch extraction speed

Extracting 236 assets from a single entity now takes ~5 seconds instead of minutes. The new `ExtractMany` batches requests per archive and parallelizes decompression.

### Per-entity output folders

Extracted assets are now organized into folders by entity name (`ark_assault\`, `goon_squad\`, etc.) instead of a flat directory.

### Dark dialogs

All confirmation prompts and warnings use dark styling consistent with the rest of the toolkit.

---

## Critical fixes

### Race condition #1 — MemoryMappedViewAccessor

**Symptom**: 145 out of 236 extractions would randomly fail. Different assets failed each run.

**Cause**: The `MemoryMappedViewAccessor` used to read from `.arc` files was shared between parallel worker threads. `ReadArray` is not thread-safe.

**Fix**: Each worker thread now creates its own accessor. The underlying `MemoryMappedFile` remains shared.

**Result**: 236/236 extractions succeeded, verified across 6 different game archives.

### Race condition #2 — temporary directory

**Symptom**: 61 out of 128 texture conversions would randomly fail. Silent data loss.

**Cause**: All parallel conversions shared the same temporary directory (`__tmp_rev`). When one thread finished, it deleted the directory another thread was still using.

**Fix**: Each conversion uses a unique temporary directory with a short GUID suffix.

**Result**: All conversions succeed reliably.

### Silent data loss on conversion failure

**Symptom**: When a `.ddsc` or `.avtx` failed to convert, the source file was still deleted.

**Fix**: The cleanup now only deletes the source if the conversion produced a valid output. Failures are logged and the source is preserved.

### Wrong extension for mesh files

**Symptom**: `.hrmeshc` and `.meshc` files were exported as `.adf`.

**Cause**: The extractor prioritized internal magic bytes over the filelist path.

**Fix**: Filelist takes precedence; magic detection is only a fallback.

---

## UX improvements

- Extract wizard: `Browse & pick manually` is now the primary action (larger, top position)
- Extract wizard: auto-advance (no manual Next button)
- Single extract browser: unified selection counter (checkbox mark != add to cart)
- Single extract browser: removed redundant "- Remove" button
- Status bar shows real conversion counts and skip notices

---

## Verification

Tested on 6 different game archives (`game0`, `game3`, `game8`, `game10`, `game11`, `game12`) with a total of **10,488 files**:

- **0 extraction failures**
- **Round-trip byte-identical** verified (SHA256 matches baseline)
- **Replace asset** verified byte-perfect
- **Multi-pass x5** deterministic (5/5 identical SHAs, stable block count)
- **Baseline 46/46 intact** after all tests

---

## Requirements

- Windows 10 or 11 (64-bit)
- .NET 8 runtime — bundled self-contained, no separate install required
- RAGE 2 game installation with `archives_win64` folder

---

## Known limitations

- 8.42% of assets have no documented path (engine placeholders) and appear as `.unknown`
- MP4/AVI to BIK requires external RAD Video Tools
- FFmpeg not bundled — on-demand download for audio format conversion
- Mesh editing not supported (extraction only)

---

## Distribution notes

The `.7z` release package **does not contain** `oo2core_7_win64.dll` (RAD proprietary, non-redistributable). The toolkit copies it automatically from the game folder on first launch and deletes it on close.

**SHA256** of `RAGE2TOOLKIT-v2.0.4.7z`: `7242375871D4C6D383C08E2995454E144733D4B390228899107EDE71309C4907`

---

## Thanks

- **REDxEYE** (ApexPredator author) for cross-checking TAB 3.1 struct and clarifying `.atxN` = mipmaps
- The RAGE 2 modding community (DECA Discord)


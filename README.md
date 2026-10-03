<div align="center">

# RAGE 2 Modding Toolkit

**Extract, convert, organize and deploy modded assets for RAGE 2.**

*No Python. No setup. Single-file Windows executable.*

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform: Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6.svg)]()
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4.svg)]()
[![Release](https://img.shields.io/github/v/release/shTNT/Rage-2-Modding-Toolkit?color=brightgreen&label=release)](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases/latest)
[![Filelist](https://img.shields.io/badge/filelist-92.85%25-success.svg)](data/filelist.txt)
[![Downloads](https://img.shields.io/github/downloads/shTNT/Rage-2-Modding-Toolkit/total?color=orange&label=downloads)](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases)

[Download latest](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases/latest) &nbsp;·&nbsp; [📖 Guide](https://shtnt.github.io/Rage-2-Modding-Toolkit/guide.html) &nbsp;·&nbsp; [Filelist](data/filelist.txt) &nbsp;·&nbsp; [Methods](filelist/methods.txt) &nbsp;·&nbsp; [Issues](https://github.com/shTNT/Rage-2-Modding-Toolkit/issues)

</div>

---

## What is this?

Portable Windows GUI that **extracts**, **edits**, **repacks** and **deploys** RAGE 2 assets.

> Drop a `.arc` in, get assets out. Edit. Repack. Mod.

No Python, no virtual environments, no IDE. Just one `.exe` plus a couple of bundled converter tools.

---

## Quick Start

1. **Download** [the latest release](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases/latest)
2. **Extract** the `.7z` anywhere you want (Desktop, `D:\`, wherever)
3. **Launch** `RAGE2Toolkit.exe`
4. **Browse** your RAGE 2 install folder when prompted
5. **Extract** -> edit -> **Mod & Repack** -> play

That is it.

---

## Features at a glance

| Feature | What it does |
|---------|--------------|
| **EXTRACT** | Pulls all 39,519 assets out of the 46 `.arc` archives (61.66 GB in ~108 s) |
| **BROWSE & PICK** | Lazy-loaded tree, 4-level semantic grouping, search by hash or keyword |
| **CONVERT** | PNG/JPG/TGA -> DDSC · DDS -> DDSC · MP3/WAV/FLAC -> OGG (sRGB-aware) |
| **MOD & REPACK** | Wizard validates hashes, atomic rebuild, `.original` backup |
| **INSTALL** | Writes to `archives_win64/`, auto-backup of the original archive |
| **MOD MANAGER** | Drag & drop `.zip` mods. Manage priority, conflicts, backups, uninstall. No registry writes |
| **PACKAGE** | Repack wizard produces distributable 1-5 MB `.zip` mods instead of full `.arc` replacements |
| **DRIFT GUARD** | Detects if an `.arc` was modified outside the ModManager before install/uninstall. Refuses by default |

---

## What is new in v2.1.5

Extractor, converter and cancel fixes on top of v2.1.0. World Settings Editor, Mod Manager and Repacker are unchanged.

### Extraction

- **Phantom hash filter.** The browser tree used to list 103,428 entries; only 36,695 actually live in the 50 `.tab` files the game loads. The other 66,733 came from the REDxEYE merge and never resolved to a real archive slot, so `EXTRACT EVERYTHING` reported `2698 / 6121` while silently skipping the missing ones. The tree is now filtered at load against a live hash set built from the `.tab` files: only real assets are shown, and extraction counters match (`Wanted == Found`, `Missing == 0`).
- **Filelist parser fix (ExtractorOpt + Repack).** Both were splitting the filelist by TAB; the shipped filelist uses spaces. Descriptions, extensions and RepackForm labels now populate correctly.
- **Per-entity output folders** in both `SingleExtractForm` and `Wizard Extract`. Assets land at `output/MODELS/weapons/ark_assault/textures/...` instead of a flat dump or `_EDITABLE/TEXTURES/`.
- **Cancel is real.** `CancellationToken` threaded through `Extractor.ExtractAll` and the conversion loops (avtx + atx1). Cancel now stops within one asset, and re-running the wizard works (the flag resets at start).

### Texture conversion

- **ATX1 mip chain support.** The old algorithm only accepted square mip-0 BC1/BC3 blobs. Real RAGE 2 `.atx1` are BC1/BC3 mip chains (mip 0+1, or 0+1+2), non-square, or cube maps. New `TryGuessAtx1Format` searches (W, H, mips, blockSize) matching the byte size, and writes a correct DDS header with `MIPMAPCOUNT` + `MIPMAP` caps. **4,682 previously failed conversions now succeed.**
- **ATX1 in Wizard Extract.** `EXTRACT EVERYTHING` used to leave `.atx1` untouched (they surfaced as `.unknown` before the filelist fix). They now convert in parallel (`MaxDegreeOfParallelism = 8`) alongside `.avtx`.
- **sRGB fix in `AssetConverters`.** Was using `-srgbi -srgbo -f R8G8B8A8_UNORM_SRGB`, which double-applies the gamma. Now uses `-srgbi -f R8G8B8A8_UNORM_SRGB` (case D of the V2.0.1 addendum). Diffuse / albedo PNG output is no longer washed out.
- **Conversion preferences gear in Wizard Extract** - same gear as `SingleExtractForm`, only in the extract wizard. Modding and Repacker headers are unaffected.

### Prompt + GUI

- **Dynamic format in the extract prompt.** The `Extract selection` dialog now reflects the gear: `N texture(s) -> DDS editable` / `PNG editable`, `N audio(s) -> WAV editable / OGG editable / kept as-is`.
- **SingleExtractForm layout fix.** The tree was computed with `FOOTER_H = 76` while the footer was 110 px tall (`76 + 34`). The last 28 px of the tree were hidden behind the footer at any window size, cutting off the last alphabetical nodes (`UI`, `VIDEOS`). Constant and footer height are now consistent.

### Verified

- Single extract, 10,058 files: **10,013 `.dds`, 0 `.atx1` residual, 0 conversion failures**.
- Wizard extract: **39,673 files / 0 missing / 2 m 41 s**, avtx `5,153 / 5,153`.
- Tree: **36,695 hashes shown**, 66,733 phantom entries filtered out.
- Baseline 46/46 intact after every test.

---
## Release history

<details>
<summary><b>v2.1.1</b> (2026-09-30) - Extract-all crash fix + Editor multi-file session</summary>

Hotfix on top of v2.1.0.

### Fixed

- **Extract-all crash.** `ShowStats()` accessed `stepPanels[3]` before it existed. Extraction and classify already worked; the "Failed" dialog was a false positive on the summary. The wizard now advances to Step 4 with the full summary.
- **Wizard Extract freeze.** `BuildStep4()` was not called and `GotoStep(3)` was missing at the end.
- **Home layout.** Bottom buttons rearranged in a symmetric 2x2 grid.
- **World Settings Editor.** `WORK\` folder cleared on open (one-shot per session).

### Added

- **World Settings Editor multi-file session.** In-memory cache of changes per `(fileHash, ValueOffset)`. Edit N nodes and M files, single SAVE produces one `.zip` with all affected `.rtpc`.
- **Wizard Extract.** Removed the redundant "Extract all" button and the "Pick what interests you" label.

### Internal

- `Process.Start` in `using` blocks (6 sites).
- `TrimLogBox` with a 5000-line cap.
- Diagnostic dumps in `StartExtract` (removable in a future release).

</details>


<details>
<summary><b>v2.1.0</b> (2026-09-29) - World Settings Editor + wizard redesign</summary>

The toolkit now includes a native **World Settings Editor** that edits game settings previously untouched publicly. Save produces an installable `.zip` in one click.

### World Settings Editor

A C# editor that opens from `Wizard Modding` -> `WORLD SETTINGS EDITOR`. Edits 7 game setting files that were never publicly moddable:

| File | Controls |
|------|----------|
| Spawn Budget Pools | How many civilians, vehicles, combatants, animals and encounters live in the open world |
| Player Stats | Health, armor, movement, abilities, combat tuning |
| Difficulty | Enemy scaling, cooldowns, damage per difficulty tier |
| Damage Types | Bitmask definitions of every damage type (Fire, Bullet, EMP, Corruption, ...) |
| Vehicle Types | Physics and handling per vehicle class |
| Weather Settings | Presets, transitions, conditions |
| Sun / Lighting | Global sun, sky, lighting |

- **No extraction needed.** The 7 settings files ship inside `data/settings/` and are ready to edit.
- **SAVE produces a mod.** Hit SAVE, pick a name + author, and the toolkit builds an installable `.zip` in `RAGE2Toolkit_Output/world_editor/`.
- **Backup Manager.** Every change is snapshotted. Restore, delete or browse backups from inside the editor.
- **Bitmask detection.** `Damage Types` is a bitmask node - only powers of 2 accepted, invalid values blocked before SAVE.
- **Breadcrumb + semantic grouping.** Navigate `Difficulty > not_activew_damage > Weapons > T3 Cannon Cooldown (min)` with siblings grouped by meaning.

### Wizard redesign

- `Wizard Modding`: **MOD MANAGER** is the primary action, front and center. `WORLD SETTINGS EDITOR` and `REPACKER` are secondary.
- `Wizard Extract`: **BROWSE & PICK MANUALLY** is the primary action. `EXTRACT EVERYTHING` is secondary.
- Hover tooltips with fade in/out on every action button.

### Hash database

- 445 new hashes resolved for the settings editor (RED_EYE kv table + targeted cracking).
- Editor coverage: 445 / 989 = 45%. Remaining 544 are engine-internal constants with no string in any accessible source.
- Full structural classification (Hungarian prefix + camelCase + engine suffix).

### Harness V4 (extended)

- 6 new phases (20-25): `settings-bundled`, `editor-features-strings`, `modding-wizard-strings`, `extract-wizard-strings`, `files-array-no-settlements`, `cleanup`.
- Harness paths fixed: was pointing to `v2.0.5-build`, now `v2.1.0-build`.

</details>

<details>
<summary><b>v2.0.5</b> (2026-09-28) - Full Mod System + Mod Manager + DDSC fix</summary>


This release turns the toolkit from "extractor + repacker" into a **full modding pipeline**.

### The Mod System

Before: any mod was a 700 MB `.arc` full replacement. Not shareable, not scalable, not manageable.

Now: a mod is a 1-5 MB `.zip`. Pack it, share it, install it, uninstall it. The toolkit handles backups, priority, conflict detection, and rollback automatically.

### Mod Manager GUI

- Drag & drop `.zip` to install.
- List of installed mods with ID, name, version, priority.
- Adjust priority (higher wins on hash-level overlap).
- Conflict panel showing shared hashes between mods.
- Uninstall individual mods.
- `Restore All` returns every `.arc` to its original state.
- Backups stored per-universe (`initial_game8.arc.original` vs `supplemental_game8.arc.original`) to avoid collisions.

### Backend (7 components)

| Component | Responsibility |
|---|---|
| `ModMetadata` | `mod.json` schema 1 (name, version, author, description, license, dependencies, conflicts) |
| `ModsStore` | Portable registry at `mods/mods.json` + `mods/library/` + `mods/backups/`. No `%APPDATA%`, no registry keys |
| `ModValidator` | Validates each asset, groups by hash, prioritizes native over convertible |
| `ModPackager` | Folder -> validated `.zip` with `mod.json` injected |
| `ModInstaller` | 6-phase install: READ, DETECT, CONFLICT, PRE_INSTALL, BUILD_TMP, COMMIT, REGISTER. Rollback on failure |
| `ConflictChecker` | Hash-level conflict detection against currently installed mods |
| `UninstallEngine` | Restores backups when no other mod touches the `.arc`, rebuilds with remaining mods otherwise. Cleans up orphan backups |

### Repack wizard redesign

3 steps (was 5):

1. **Drop** - folder with edited assets. Choose raw or convert.
2. **Scan** - validates hashes, classifies each file, collapses duplicate hashes by priority.
3. **Result** - asks for name + author, produces `.zip`, shows the path.

No more direct-to-game install. The wizard now produces distributable `.zip` files by design.

### Drift detection

If an `.arc` is modified outside the ModManager (external tool, old wizard, manual copy), the next install/uninstall refuses to proceed by default. This prevents silent overwrites of external changes. `--force` overrides. State is tracked per `.arc` via SHA256 in `mods.json` (`ArcLastWritten`).

### Critical fix: DDSC textures crashed the game

The bundled `ddscConvert.exe` (from Generation Zero upstream) spreads texture mipmaps across **three separate files**: `.atx1` + `.atx2` + `.ddsc`. RAGE 2 expects a **monolithic `.ddsc`** with the full mip chain inside. Installing any mod built from a `.ddsc`/`.avtx` texture caused the game to **hang on save load**.

The toolkit now builds the AVTX file manually: **original header (128 bytes) + full mip payload from texconv**. Verified byte-identical size to the original.

| Original asset | How v2.0.5 writes it |
|---|---|
| `.atx1..9` | Raw BC1 mip 0, no header (matches original) |
| `.ddsc` / `.avtx` | Monolithic AVTX with original DXGI format preserved |

### Other fixes (12)

- **`ObjectDisposedException` in `.atx1` conversion.** 236/236 conversions were failing silently. `BinaryWriter.Dispose()` was closing the underlying `FileStream` before the payload write.
- **`.atx1` -> DDS returned PNG.** Hardcoded `-ft png`. Now respects the target format.
- **`_nrm` and `_mpm` textures came out granulated.** sRGB was being applied to linear data. Now uses `IsColorSuffix()` heuristic.
- **Cleanup deleted source files when conversion failed.** Silent data loss. Now conditional.
- **`mod.json` was case-sensitive.** Lowercase keys were ignored. Now case-insensitive.
- **CLI parser off-by-one.** `--force` at the end of args was never read.
- **Collapse by priority ignored original format.** Now uses `origExtByHash` to pick the winning file.
- **`UninstallEngine` did `RemoveById` before checking drift.** Left the registry inconsistent on abort.
- **`ddscConvert` side files polluted the converted dir.** Cleaned up.
- **`type_map.json` and `type_map_v2.json` shipped stale fallbacks.** Removed; only `type_map_v3.json` is authoritative now.
- **`Diag.cs` temporary debug logger was writing to a hardcoded dev path.** Removed.
- **CLI `--uninstall` did not accept `--force`.** Fixed.

### Changed

- `TabFormat.cs` extracted from `Repack.cs` (was 835 lines mixed with legacy writer + old GUI).
- `__converted__` staging moved from source folder to `%TEMP%\RAGE2Toolkit_conv_<guid8>`.
- `PendingStatus` enum replaces `bool Ok` + `string Status` mix.
- Legacy `Repack.cs` (`Repacker` + `RepackForm`) deprecated. The old direct-install path is no longer the recommended workflow.

### New docs

- `docs/ROLLBACK.md` - manual restore instructions if `Restore All` fails.
- `docs/FALSE_POSITIVES.md` - how to verify SHA256 and report false positives to antivirus vendors.
- `docs/HOW_TO_USE.md` - rewritten around the Repack + ModManager workflow.
- `docs/TROUBLESHOOTING.md` - drift, monolithic DDSC, false positives, rollback.

### Breaking changes

- **New monolithic `.ddsc` layout.** Mods built with v2.0.4 or older that touch DDSC textures are incompatible and must be rebuilt with v2.0.5.
- `mods.json` now has an `ArcLastWritten` field (backward compatible; missing field is treated as no baseline).
- Legacy `Repack.cs` is deprecated.

### Verified in-game

- `.atx1` reskin (ark_assault dif) - visible in-game, no crash.
- `.ddsc` reskin (ark_pistol dif) - visible in-game, no crash.
- Baseline 46/46 intact after every test.
- Harness V4: 19/19 PASS.

</details>


<details>
<summary><b>v2.0.4</b> (2026-09-26) - Semantic tree v3, race condition fixes, batch extraction</summary>


### Semantic asset tree v3 - 12 categories, recursive schema

The browser tree is completely redesigned. It groups by meaning, not filesystem:

```
WEAPONS
└── ark_assault
    ├── mesh
    ├── textures
    │   ├── base
    │   └── skins
    │       └── unicorn
    └── scripts
```

**12 top-level categories:** ANIMATIONS, AUDIO, CHARACTERS, DEFINITIONS, EFFECTS, ENVIRONMENT, GAME LOGIC, UI, UNCATEGORIZED, VEHICLES, VIDEOS, WEAPONS. All **39,519** hashes organized.

### Two critical race conditions fixed

**#1 - Shared memory-mapped accessor.** The `MemoryMappedViewAccessor` used to read `.arc` files was shared between parallel worker threads. `ReadArray` is not thread-safe. Result: 145 of 236 extractions randomly failed per run, different assets each time. Fix: one accessor per thread.

**#2 - Shared temp directory.** All parallel conversions shared the same `__tmp_rev` folder. When one thread finished, it deleted the folder another thread was using. Result: 61 of 128 texture conversions randomly failed with silent data loss. Fix: unique GUID suffix per conversion.

### Batch extraction - ~5 seconds per entity

Extracting 236 assets from a single entity now takes about 5 seconds (was minutes). New `ExtractMany` batches all requests per archive and parallelizes decompression.

### Smart extract prompt

`Extract Selected` now shows a breakdown before running:

```
236 asset(s)
  224 texture(s) -> PNG editable
   12 native asset(s) -> copied as-is
[ Convert & Extract ]  [ Extract raw only ]  [ Cancel ]
```

### Conversion preferences (gear icon)

Configure how the toolkit converts before extracting. Settings persist across sessions.

| Setting | Options |
|---------|---------|
| Texture resolution | 1024 (DDSC/AVTX) · 2048 (ATX1) |
| Texture output | PNG · DDS |
| Audio output | Keep · WAV · OGG |

### Search by extension

Type `.ddsc`, `.atx1`, `*.ogg` in the search box to filter by exact extension. Keyword and hash search still work as before.

### Per-entity output folders

Extracted assets are organized into folders by entity name (`ark_assault/`, `goon_squad/`) instead of a flat directory. Raw mode keeps everything.

### Other fixes

- **Silent data loss on conversion failure:** `.ddsc` / `.avtx` were deleted even when the conversion failed. Cleanup now preserves the original when no output is produced.
- **Wrong extension for mesh files:** `.hrmeshc` / `.meshc` were exported as `.adf`. Filelist now takes precedence over magic detection.
- **Residual temp folders:** `__atxtmp` was left behind after conversion. Now recursively deleted.
- **Dark dialogs:** warnings and confirmations use the toolkit dark theme consistently.

### Verification

Tested on 6 different archives (`game0`, `game3`, `game8`, `game10`, `game11`, `game12`) — **10,488 files, 0 failures.** Round-trip byte-identical, replace byte-perfect, multi-pass x5 deterministic, baseline 46/46 intact.

</details>


<details>
<summary><b>v2.0.3</b> (2026-09-26) - Asset browser tree v2, descriptive filenames, sRGB</summary>

### Asset browser tree v2 - 4 levels

The tree now groups by **Category -> Entity -> Resource type -> Asset**:

```
WEAPONS
└── ark_assault
    ├── mesh
    ├── scripts
    └── textures
        ├── ark_assault_dif.atx1
        ├── ark_assault_nrm.atx1
        └── ark_assault_mpm.atx1
```

12 semantic categories (WEAPONS, CHARACTERS, VEHICLES, ENVIRONMENT, AUDIO, UI, SCRIPTS, ANIMATIONS, EFFECTS, VIDEOS, DLC, UNCATEGORIZED).
No more textures buried under `MODELS`.

### Descriptive filenames on extract

| Before | After |
|--------|-------|
| `4E37BD8EAD14BEA2.atx1` | `ark_assault_dif_4E37BD8EAD14BEA2.atx1` |

The repack wizard parses both formats. Backward compatible with files extracted by older versions.

### sRGB-aware texture conversion

Correct color profile per PBR suffix:

| Suffix | Profile |
|--------|---------|
| `_dif` `_emc` `_albedo` `_color` | **sRGB** (color-accurate) |
| `_nrm` `_mpm` `_dtm` `_msk` | **linear** |

No more washed-out albedo maps.

### Convert dropdown in the browser

`[X] Convert to editable  [PNG v]` - PNG / DDS / OGG / WAV. Auto-selects based on the selected asset.

### Extract cleanup

Intermediate files (`.ddsc`, `.avtx`, `.dds`, `__tmp_rev`) are deleted after a successful conversion.

### Unknown extension fallback

When magic detection fails (BC1 raw blobs like `.atx1`), the extractor falls back to the filelist path extension instead of writing `.unknown`.

### Classify coverage expanded

- `.modelc`, `.epe`, `.epeb`, `.epeo` recognized as native model/entity containers
- `.atx1..9` flagged as raw BC1 mip blob without descriptor

### Alphabetical sort after extract

Extracted files are re-sorted alphabetically at the end of the extraction pass.

### Fixed

- Crash on tree selection (`tree.AfterSelect` was attached before the TreeView existed)

### Filelist: 92.85% coverage (was 91.58%)

| Metric | Value |
|--------|-------|
| Hashes verified | **36,695 / 39,519** |
| Coverage | **92.85%** |
| New hashes this release | **+503** |
| Remaining orphans | 2,824 (static-analysis ceiling) |
| Methods documented | 26 working · 19 dead ends |

</details>

<details>
<summary><b>v2.0.2</b> (2026-09-26) - Repack writer hotfix</summary>

**Repack writer hotfix.** Two format-level bugs were silently hanging the game on load after installing a modified `.arc`. Both were invisible from SHA comparison and only surfaced during an in-game test.

- **Block table sentinel.** The writer was emitting a block table without the terminating `FFFFFFFF FFFFFFFF` entry. The engine computes the file table offset as `0x20 + block_count * 8`; without the sentinel, `block_count` was one short and the engine read the first file entry as if it were part of the block table.
- **File entries sorted by hash.** The engine performs a binary search on the file table by hash. The writer was emitting entries in physical-offset order, so binary search returned wrong results and the game hung looking for the archive index.

**Validation:** round-trip with zero changes now produces a `.tab` that is byte-identical to the original (SHA256 match). Verified with a single `.atx1` texture replacement on `game8.arc`: game boots, menu loads, save loads, replaced texture visible.

</details>

<details>
<summary><b>v2.0.1</b> (2026-09-25) - Search fix + full type_map</summary>

Small hotfix on top of v2.0.0.

- **SingleExtractForm search by hash.** Typing a 16-hex hash (e.g. `4E37BD8EAD14BEA2`) now returns the matching asset. Before this fix, the searcher only looked at the path, so hash lookups returned 0 results even when the file was in the archive.
- **`type_map.json` regenerated** with the full **39,519 hashes**. Previous builds shipped a truncated type_map (arrays capped at 500 entries per game while `total` still claimed 39,519). The asset browser now shows all 39,519 hashes instead of 20,353.

</details>

<details>
<summary><b>v2.0.0</b> (2026-09-25) - Major release</summary>

- **Extractor 10x faster.** 46 archives / 39,519 files / 61.66 GB in ~108 s (was ~20 min). Peak RAM 3.3 GB.
- **Parallel writer (RepackerCore v5.2).** Oodle Kraken re-compression in parallel, deterministic output, atomic commit.
- **Asset validators** (14 types) and **converters** (PNG/JPG/BMP/TGA -> DDSC, DDS -> DDSC, MP3/WAV/FLAC -> OGG).
- **Redesigned drag & drop wizard** with Format & Files Guidelines.
- **Full filelist in a single** `data/filelist.txt`.

</details>

<details>
<summary><b>v1.3.1</b> (2026-09-24) - Unified filelist, phantom cleanup</summary>

- **Unified filelist.** Toolkit loads a single `data/filelist.txt` with all hashes, replacing the previous three-file split (base + extra + supplemental).
- **106 phantom entries removed** from the published filelist (hash/path pairs that did not re-hash correctly). Discovered during a full re-verification pass. Commit `5595818`.
- Coverage: 35,999 / 39,519 = **91.09%**.
- Original three sources preserved under `data/sources/` for provenance.

</details>

<details>
<summary><b>v1.3.0</b> (2026-09-24) - Supplemental universe discovery</summary>

- **Discovered `archives_win64/supplemental/`** - the second universe of 33 archives the community had missed. Zero overlap with `initial/`.
- Cross-referenced 358,057 paths from the DECA raw filelist against the supplemental universe. Recovered **20,789 new hashes** in one pass.
- Coverage: 86.74% -> **91.09%** (union of both universes, gameplay hashes only).
- Toolkit patched to scan both `initial/` and `supplemental/` folders.

</details>

<details>
<summary><b>v1.2.x</b> (2026-09-24) - Filelist methodology and brute-force</summary>

- **MasterBrute + MasterBruteV3** brute-force tools. Two formats: neighborhood-derived prefixes and filelist direct hashing. **+2,120 hashes**.
- Discovered a critical **`|` prefix bug** in the DECA filelist parser. Every previous brute-force attempt had been hashing contaminated strings. Fixing it changed everything.
- **CFX/GFX Scaleform forensic extraction.** Decompiled 114 `.cfx` modules, mined Perforce source paths from the strings. **+224 hashes**.
- Coverage reached 86.74% at end of this line.

</details>

<details>
<summary><b>v1.0.0 - v1.1.0</b> (2026-09-23) - First release and first in-game mod</summary>

- **v1.0.0:** Initial public release. Extractor + repacker for `initial/` archives only.
- **v1.1.0:** First successful in-game mod. A cyan rifle texture (`.atx1` replacement) loaded and rendered correctly. Historical first public `.arc` repack of RAGE 2.

</details>

---

## Filelist

The hash-to-path mapping is published at [`data/filelist.txt`](data/filelist.txt).

| Metric | Value |
|--------|-------|
| Entries shipped | **103,428** |
| Sourced from | REDxEYE hash extractor (ApexPredator) |
| Our own resolved hashes | 36,695 (pre-REDxEYE, kept under `data/sources/filelist_we_built.txt`) |
| Gameplay corpus | 39,519 hashes (46 `.arc` files, initial + supplemental) |
| Our corpus coverage | **92.85%** (36,695 / 39,519) |
| Remaining corpus orphans | 2,824 (engine-internal constants, no string in any accessible source) |
| Localization excluded | 45,739 hashes (separate tree) |

The **103,428 entries** shipped in `filelist.txt` cover the REDxEYE universe (full asset catalog including DLCs, regions, localization paths). The **92.85%** figure refers specifically to our gameplay corpus - the 39,519 hashes actually present in the 46 `.arc` archives the toolkit extracts. Both figures are correct and describe different things.

**Format:** `<HEX16>` + TAB + `<path>` · UTF-8 no BOM · LF line endings.

Methods documented at [`filelist/methods.txt`](filelist/methods.txt).

---

## Hash algorithm

```
Algorithm:  MurmurHash3 x64 128-bit
Input:      path UTF-8, lowercase, forward slashes, no prefix
Seed:       0
Output:     h1 (low 64 bits of the 128-bit digest)
```

**Test vector:**

```
hash("text/master_eng.stringlookup") = 8453EE3581F31F39
```

> Implementations using `h2` or XOR of halves produce **0 matches**. Verify with the test vector before assuming paths are wrong.

---

## Requirements

| Item | Details |
|------|---------|
| OS | **Windows 10 / 11** (x64) |
| Game | **RAGE 2 installed** (any distribution) |
| Disk | ~70 GB free for full extract |
| RAM | 16 GB recommended (extract peaks at ~3.3 GB) |

**No .NET install needed** - the toolkit is self-contained.

---

## Documentation

| File | Contents |
|------|----------|
| [`docs/HOW_TO_USE.md`](docs/HOW_TO_USE.md) | First-time workflow |
| [`docs/FORMAT_REFERENCE.md`](docs/FORMAT_REFERENCE.md) | TAB / AVTX / ATX internals |
| [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md) | Common issues |
| [`filelist/methods.txt`](filelist/methods.txt) | Filelist build methodology |

---

## Credits

Built on top of other open-source projects:

| Project | Use |
|---------|-----|
| [DECA](https://github.com/kk49/deca) | Initial 358k path database |
| [ApexToolset](https://github.com/PredatorCZ/ApexToolset) | `ddscConvert` + `R2SmallArchive` |
| [DirectXTex](https://github.com/microsoft/DirectXTex) | `texconv` |

Special thanks to the RAGE 2 modding community. And to **REDxEYE**, author of [ApexPredator](https://github.com/REDxEYE/APEX-PREDATOR), for his hash extractor - which produced the 103,428-path database that ships with this toolkit and made the settings editor possible - for his file format research on the Avalanche Apex engine, and for his patience on the modding community Discord.

---

## License

MIT. Do whatever you want, just do not blame me if the wasteland breaks.

Oodle (`oo2core_7_win64.dll`) is proprietary (RAD Game Tools) and **is not redistributed**. It is auto-copied from your own game install on first run.


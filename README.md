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

[Download latest](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases/latest) &nbsp;·&nbsp; [Filelist](data/filelist.txt) &nbsp;·&nbsp; [Methods](filelist/methods.txt) &nbsp;·&nbsp; [Issues](https://github.com/shTNT/Rage-2-Modding-Toolkit/issues)

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

---

## What is new in v2.0.4

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

---

## Release history

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
| Entries | **36,695** |
| Universe | 39,519 gameplay hashes |
| Coverage | **92.85%** |
| Localization excluded | 45,739 hashes (separate tree) |

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

Special thanks to the RAGE 2 modding community, and to **REDxEYE** (ApexPredator) for the `.atxN` mipmap family confirmation.

---

## License

MIT. Do whatever you want, just do not blame me if the wasteland breaks.

Oodle (`oo2core_7_win64.dll`) is proprietary (RAD Game Tools) and **is not redistributed**. It is auto-copied from your own game install on first run.


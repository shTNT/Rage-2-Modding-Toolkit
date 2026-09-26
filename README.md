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
5. **Extract** → edit → **Mod & Repack** → play

That is it.

---

## Features at a glance

| Feature | What it does |
|---------|--------------|
| **EXTRACT** | Pulls all 39,519 assets out of the 46 `.arc` archives (61.66 GB in ~108 s) |
| **BROWSE & PICK** | Lazy-loaded tree, 4-level semantic grouping, search by hash or keyword |
| **CONVERT** | PNG/JPG/TGA → DDSC · DDS → DDSC · MP3/WAV/FLAC → OGG (sRGB-aware) |
| **MOD & REPACK** | Wizard validates hashes, atomic rebuild, `.original` backup |
| **INSTALL** | Writes to `archives_win64/`, auto-backup of the original archive |

---

## What is new in v2.0.3

### Asset browser tree v2 — 4 levels

The tree now groups by **Category → Entity → Resource type → Asset**:

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

`[✓] Convert to editable  [PNG ▾]` — PNG / DDS / OGG / WAV. Auto-selects based on the selected asset.

### Extract cleanup

Intermediate files (`.ddsc`, `.avtx`, `.dds`, `__tmp_rev`) are deleted after a successful conversion.

### Unknown extension fallback

When magic detection fails (BC1 raw blobs like `.atx1`), the extractor falls back to the filelist path extension instead of writing `.unknown`.

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

---

## Previous releases

<details>
<summary><b>v2.0.2 (2026-09-26)</b> — Repack writer hotfix</summary>

Two format-level bugs silently hanging the game on load:

- **Block table sentinel.** Missing `FFFFFFFF FFFFFFFF` terminator. Engine computed the file table offset one entry short → hang.
- **File entries sorted by hash.** Engine performs a binary search by hash. The writer emitted physical-offset order → wrong results, hang.

Round-trip now produces a `.tab` byte-identical to the original (SHA256 match). Verified in-game with an `.atx1` replacement on `game8.arc`.

</details>

<details>
<summary><b>v2.0.1 (2026-09-25)</b> — Search fix + full type_map</summary>

- `SingleExtractForm` search now matches by hash, not only by path
- `type_map.json` regenerated with all 39,519 hashes (was truncated to 500 per game)

</details>

<details>
<summary><b>v2.0.0 (2026-09-25)</b> — Major release</summary>

- Extractor **10x faster**: 46 archives / 39,519 files / 61.66 GB in ~108 s
- Parallel writer (RepackerCore v5.2) with Oodle Kraken re-compression, deterministic output, atomic commit
- **14 asset validators** + converters (PNG/JPG/BMP/TGA → DDSC, DDS → DDSC, MP3/WAV/FLAC → OGG)
- Redesigned drag & drop wizard with Format & Files Guidelines

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

**No .NET install needed** — the toolkit is self-contained.

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


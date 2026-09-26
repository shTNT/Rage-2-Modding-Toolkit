# RAGE 2 Modding Toolkit

## What's new in v2.0.2 (2026-09-26)

Repack writer hotfix. Two format-level bugs were silently hanging the game
on load after installing a modified `.arc`. Both were invisible from SHA
comparison and only surfaced during an in-game test.

- **Block table sentinel.** The writer was emitting a block table without
  the terminating `FFFFFFFF FFFFFFFF` entry. The engine computes the file
  table offset as `0x20 + block_count * 8`; without the sentinel,
  `block_count` was one short and the engine read the first file entry as
  if it were part of the block table.
- **File entries sorted by hash.** The engine performs a binary search on
  the file table by hash. The writer was emitting entries in physical-offset
  order, so binary search returned wrong results and the game hung looking
  for the archive index.

Validation: round-trip with zero changes now produces a `.tab` that is
byte-identical to the original (SHA256 match). Verified with a single
`.atx1` texture replacement on `game8.arc`: game boots, menu loads, save
loads, replaced texture visible.
## What's new in v2.0.1 (2026-09-25)

Small hotfix on top of v2.0.0.

- **SingleExtractForm search by hash.** Typing a 16-hex hash (e.g.
  `4E37BD8EAD14BEA2`) now returns the matching asset. Before this fix
  the searcher only looked at the path, so hash lookups returned 0
  results even when the file was in the archive.
- **type_map.json regenerated** with the full **39,519 hashes**.
  Previous builds shipped a truncated type_map (arrays capped at 500
  entries per game while `total` still claimed 39,519). The asset
  browser now shows all 39,519 hashes instead of 20,353.

## What's new in v2.0.0 (2026-09-25)

- **Extractor 10x faster.** 46 archives / 39,519 files / 61.66 GB in
  ~108 s (was ~20 min). Peak RAM 3.3 GB.
- **Parallel writer (RepackerCore v5.2).** Oodle Kraken re-compression
  in parallel, deterministic output, atomic commit.
- **Asset validators** (14 types) and **converters**
  (PNG/JPG/BMP/TGA -> DDSC, DDS -> DDSC, MP3/WAV/FLAC -> OGG).
- **Redesigned drag & drop wizard** with Format & Files Guidelines.
- **Full filelist in a single** `data/filelist.txt` (36,695 hashes).

### Coverage

**36,695 / 39,519 = 92.85%** of gameplay asset hashes verified.

Localization strings (45,739 unique hashes) are excluded on purpose.

### Hash algorithm

MurmurHash3 x64 128-bit, seed=0, low 64 bits of h1.
Test vector: `hash("text/master_eng.stringlookup") = 8453EE3581F31F39`

> Extract, convert, organize and deploy modded assets for RAGE 2.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue)](https://www.microsoft.com/windows)
[![Version](https://img.shields.io/badge/Version-2.0.2-green)](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases)
[![VirusTotal](https://img.shields.io/badge/VirusTotal-0%2F71%20clean-brightgreen)](#antivirus--smartscreen)

## What it does

- **Extracts** all 46 `.arc` archives (initial + supplemental) from RAGE 2 (~39,519 files in ~108 s).
- **Identifies** files by hash using a community filelist with **92.85% coverage**.
- **Converts** `.avtx` textures to editable `.dds`.
- **Organizes** content by type (textures, audio, video, UI, data).
- **Repacks** modified files back into `.arc` archives that the game loads on launch.
- **Auto-detects** and copies the required Oodle runtime from your own RAGE 2 install.

## Download

- **Latest Release**: [RAGE2TOOLKIT-v2.0.2.7z](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases)
- **SHA-256**: listed on the release page.

## Documentation

Full documentation: https://shtnt.github.io/Rage-2-Modding-Toolkit/

- **[How to Use](docs/HOW_TO_USE.md)** - Step-by-step guide.
- **[Format Reference](docs/FORMAT_REFERENCE.md)** - .arc, .avtx, .ddsc, hashes.
- **[Troubleshooting](docs/TROUBLESHOOTING.md)** - Common issues.

## Filelist

The full hash-to-path mapping used by the toolkit is published at [data/filelist.txt](data/filelist.txt) - **36,695 entries, 92.85% gameplay coverage**.

The remaining 3,520 entries are:

- ~1,449 engine placeholders (byte-identical payloads, no textual path by design)
- ~116 CFX (proprietary Avalanche UI format)
- ~63 OggS audio (Vorbis, no metadata)
- ~7 RTPC manifest containers
- ~199 Oodle variant failures
- ~340 AVTX textures without public path names

Anyone can contribute new mappings by appending to `data/filelist.txt` (one per line, tab-separated).

## Installation

1. Extract RAGE2TOOLKIT-v2.0.2.7z anywhere.
2. Run RAGE2Toolkit.exe.
3. Configure game path and output path.
4. Extract, convert, modify, deploy.

## Modifying and repacking

After extracting and editing assets:

1. Open the MOD & REPACK wizard in the toolkit.
2. Select the `.arc` that contains your modified files.
3. Point the wizard at your edited folder (drag & drop supported).
4. The toolkit auto-backs up the original `.arc` as `.original` on first install.
5. The modified archive is written and the game reads it directly on next launch.

Note: modified files are re-compressed with Oodle Kraken in parallel since v2.0.0. Archive size grows only by the size of the modified assets.

## Important Notes

- This toolkit does NOT include game assets. You need your own copy of RAGE 2.
- The Oodle DLL is NOT redistributed. It is auto-copied from your game folder.
- ~9% of entries remain hash-named (mostly engine placeholders and debug assets without public path names).

## Antivirus / SmartScreen

VirusTotal: **0/71 clean**.
Report: https://www.virustotal.com/gui/home/upload

If Windows SmartScreen shows a warning, click More info then Run anyway.

## Author

**Kry0genik**
- Nexus Mods: https://www.nexusmods.com/profile/Kry0genik
- GitHub: https://github.com/shTNT

## Credits

This toolkit would not exist without the work of others. Where the credit is due:

### DECA project - file list and format documentation

The DECA project is the reason extracted files have names at all. Two of their
contributions are load-bearing for this toolkit:

- **`resources/deca/rg2/filelist.txt`** (358,057 entries) - the raw path database
  used to reverse MurmurHash3 hashes back to real file names. Without this,
  100% of extracted files would be named `4A3F8C12D5E9B7A1.avtx` style.
  It is what makes the 92.85% coverage possible.
- **`python/deca/deca/ff_arc_tab.py`** - a reference parser for the TAB v3.1
  format. This is the file that documented the multi-block compression layout
  used by RAGE 2 archives. Without studying this parser, we would not have known
  how files spanning multiple compression blocks are reassembled. The multi-block
  handling in our extractor is a direct port of the logic found here, and it
  accounts for roughly 2,500 additional files recovered beyond the single-block
  approach.

A significant portion of this toolkit's functionality is derived from DECA's
reverse engineering work. Any questions about the archive format itself should
go their way first.

### PredatorCZ (Lukas Cone) - ApexToolset

- **`ddscConvert.exe`** - converts `.avtx` textures to/from `.dds`. GPL v3.
- **`R2SmallArchive.exe`** - extracts mini-archives (.bl, .ee, .nl, .fl). GPL v3.
- Source: https://github.com/PredatorCZ/ApexToolset

### Microsoft - DirectXTex

- **`texconv.exe`** - DDS format conversion and mipmap regeneration. MIT.
- Source: https://github.com/microsoft/DirectXTex

### RAGE 2 modding community

Testing, feedback, format hunting, and years of reverse engineering that no
single contributor could have done alone.

## License

MIT - see [LICENSE](LICENSE). Third-party components: see [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt).

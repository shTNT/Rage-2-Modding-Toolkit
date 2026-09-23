# RAGE 2 Modding Toolkit

> Extract, convert, organize and deploy modded assets for RAGE 2.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue)](https://www.microsoft.com/windows)
[![Version](https://img.shields.io/badge/Version-1.0.0-green)](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases)
[![VirusTotal](https://img.shields.io/badge/VirusTotal-0%2F71%20clean-brightgreen)](https://www.virustotal.com/gui/file/2639ff38a5422b17535fd3fce6ed15a01cd5ff400440168e8d118f7ae3355d12)

## What it does

- **Extracts** all 13 `.arc` archives from RAGE 2 (~14,400 files in ~30 s).
- **Identifies** files by hash using a 358,057-line community filelist (~81% coverage).
- **Converts** `.avtx` textures to editable `.dds`.
- **Organizes** content by type (textures, audio, video, UI, data).
- **Deploys** modded files to `dropzone\` for loose-file loading.
- **Auto-detects** and copies the required Oodle runtime from your own RAGE 2 install.

## Download

- **Latest Release**: [RAGE2TOOLKIT-v1.0.7z](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases/download/v1.0.0/RAGE2TOOLKIT-v1.0.7z)
- **SHA-256**: `2639FF38A5422B17535FD3FCE6ED15A01CD5FF400440168E8D118F7AE3355D12`

## Documentation

Full documentation: https://shtnt.github.io/Rage-2-Modding-Toolkit/

- **[How to Use](docs/HOW_TO_USE.md)** - Step-by-step guide.
- **[Format Reference](docs/FORMAT_REFERENCE.md)** - .arc, .avtx, .ddsc, hashes.
- **[Troubleshooting](docs/TROUBLESHOOTING.md)** - Common issues.

## Installation

1. Extract RAGE2TOOLKIT-v1.0.7z anywhere.
2. Run RAGE2Toolkit.exe.
3. Configure game path and output path.
4. Extract, convert, modify, deploy.

## Deploying to dropzone

Add this to Steam/Epic launch options (single line):

~~~text
--vfs-fs dropzone --vfs-archive archives_win64 --vfs-archive patch_win64 --vfs-archive dlc_win64 --vfs-fs
~~~

## Important Notes

- This toolkit does NOT include game assets. You need your own copy of RAGE 2.
- The Oodle DLL is NOT redistributed. It is auto-copied from your game folder.
- Not all files can be modded (proprietary compression variants).

## Antivirus / SmartScreen

VirusTotal: **0/71 clean**.
Report: https://www.virustotal.com/gui/file/2639ff38a5422b17535fd3fce6ed15a01cd5ff400440168e8d118f7ae3355d12

If Windows SmartScreen shows a warning, click More info then Run anyway.

## Author

**Kry0genik**
- Nexus Mods: https://www.nexusmods.com/profile/Kry0genik
- GitHub: https://github.com/shTNT

## Credits

This toolkit would not exist without the work of others. Where the credit is due:

### DECA project — file list and format documentation

The DECA project is the reason extracted files have names at all. Two of their
contributions are load-bearing for this toolkit:

- **`resources/deca/rg2/filelist.txt`** (358,057 entries) — the raw path database
  used to reverse MurmurHash3 hashes back to real file names. Without this,
  100% of extracted files would be named `4A3F8C12D5E9B7A1.avtx` style.
  It is what makes the 81% coverage possible.
- **`python/deca/deca/ff_arc_tab.py`** — a reference parser for the TAB v3.1
  format. This is the file that documented the multi-block compression layout
  used by RAGE 2 archives. Without studying this parser, we would not have known
  how files spanning multiple compression blocks are reassembled. The multi-block
  handling in our extractor is a direct port of the logic found here, and it
  accounts for roughly 2,500 additional files recovered beyond the single-block
  approach.

A significant portion of this toolkit's functionality is derived from DECA's
reverse engineering work. Any questions about the archive format itself should
go their way first.

### PredatorCZ (Lukas Cone) — ApexToolset

- **`ddscConvert.exe`** — converts `.avtx` textures to/from `.dds`. GPL v3.
- **`R2SmallArchive.exe`** — extracts mini-archives (.bl, .ee, .nl, .fl). GPL v3.
- Source: https://github.com/PredatorCZ/ApexToolset

### Microsoft — DirectXTex

- **`texconv.exe`** — DDS format conversion and mipmap regeneration. MIT.
- Source: https://github.com/microsoft/DirectXTex

### RAGE 2 modding community

Testing, feedback, format hunting, and years of reverse engineering that no
single contributor could have done alone.

## License

MIT - see [LICENSE](LICENSE). Third-party components: see [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt).

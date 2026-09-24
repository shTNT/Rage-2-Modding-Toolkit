---
layout: default
title: RAGE 2 Modding Toolkit
description: Portable GUI toolkit for extracting, editing and repacking RAGE 2 .arc archives. 91.41% filelist coverage.
---

# RAGE 2 Modding Toolkit

**A portable GUI toolkit for extracting, editing and repacking RAGE 2 assets.**

Current release: [v1.3.0](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases/tag/v1.3.0) — filelist coverage **91.41%** (36,126 / 39,519 hashes).

[Download latest release](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases/latest){: .btn }

## What it does

- **Extracts** all 13 `.arc` archives (~14,400 files in ~30 seconds).
- **Identifies** files by hash using a 358,057-line community filelist (91.41% coverage).
- **Converts** `.avtx` textures to editable `.dds`.
- **Repacks** modified files back into `.arc` archives that the game loads on launch.
- **Auto-copies** the Oodle runtime from your own RAGE 2 install.

## Requirements

- Windows 10 or 11 (x64)
- Your own copy of RAGE 2
- ~25 GB free disk space during extraction

## Documentation

- [How to Use](HOW_TO_USE.html) — step-by-step guide
- [Format Reference](FORMAT_REFERENCE.html) — `.arc`, `.avtx`, `.ddsc`, TAB 3.1 format, MurmurHash3
- [Troubleshooting](TROUBLESHOOTING.html) — common issues

## Source

All source code is public on GitHub:

- [Repository](https://github.com/shTNT/Rage-2-Modding-Toolkit)
- [Releases](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases)
- [Third-party notices](https://github.com/shTNT/Rage-2-Modding-Toolkit/blob/main/THIRD_PARTY_NOTICES.txt)

## Credits

- **DECA project** (kk49) — community filelist and reference TAB parser
- **PredatorCZ** — ApexToolset (ddscConvert, R2SmallArchive), GPL v3
- **Microsoft** — DirectXTex (texconv), MIT

## Legal

Unofficial tool. Not affiliated with Avalanche Studios, Bethesda Softworks, RAD Game Tools, Epic Games or their subsidiaries. Users must own a legitimate copy of RAGE 2.
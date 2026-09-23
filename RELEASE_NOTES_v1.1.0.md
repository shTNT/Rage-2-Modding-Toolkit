# RAGE 2 Modding Toolkit v1.1.0

**Release date:** 2026-09-23
**Author:** Kry0genik (https://github.com/shTNT)

## What's new in v1.1.0

### Step-by-step wizards
The toolkit now opens with a Home screen that offers two big actions:

- **EXTRACT** — pull all assets out of the game into editable formats (DDS textures, OGG audio, BK2 video, scripts, UI). Organized automatically.
- **MOD & REPACK** — drag & drop your edited files, the wizard validates, converts and rebuilds the `.arc` files, then installs them into the game (with automatic backup) or packages them for Nexus / ModDB sharing.

### Modding .arc archives directly
v1.1 introduces full `.arc` **repacking**. You can now modify any texture inside the game (weapons, characters, terrain, UI — not just climate/dialogue/dummies) and rebuild the archive without breaking the rest of its contents.

### Validation that stops you from breaking your game
Before anything is applied, the wizard scans every file and rejects:
- PNG / JPG / TGA / BMP — must be saved as DDS first
- WAV — the game uses OGG
- Filenames that are not 16-character hashes
- Hashes that do not exist in any `.arc`

Every rejection comes with a clear reason so you know exactly what to fix.

### TOOL CHECK panel
Live status of the 5 things that matter: game folder, Oodle runtime, output folder, game archives, bundled tools. Refreshes automatically every 2 seconds.

## Installation

1. Download `RAGE2TOOLKIT-v1.1.7z`
2. Extract anywhere (no installer, no Python, no setup)
3. Run `RAGE2Toolkit.exe`
4. On first launch, click **Browse RAGE2.exe location** and select your RAGE 2 folder
5. The toolkit auto-copies the required Oodle runtime from your own game install

## Requirements

- Windows 10 or 11 (x64)
- .NET 8 runtime — **self-contained, no install needed**
- ~25 GB free disk space for extracting all assets

## Credits

- **DECA project** — for the filelist and the reference TAB parser that made multi-block extraction possible
- **PredatorCZ / Lukáš Cone** — ApexToolset (`ddscConvert`, `R2SmallArchive`)
- **Microsoft** — DirectXTex (`texconv`)
- **RAGE 2 modding community** — testing, feedback and format documentation

## Legal

- MIT license for the toolkit itself
- Oodle runtime (`oo2core_7_win64.dll`) is **not** redistributed — it is auto-copied from your own game installation
- See `THIRD_PARTY_NOTICES.txt` for full license details

## SHA256

`AFFD446AD4B3B03DAD3A0B4D3C7447D224534C378AED4C541627860D1459409F`

---

**Full changelog:** see `CHANGELOG.md`


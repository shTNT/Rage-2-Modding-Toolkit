## [1.1.0] - 2026-09-23

### Added
- **Home screen** with two big actions: **EXTRACT** and **MOD & REPACK**
- **Extract wizard** (4 steps): pick asset categories, output folder, live log, final summary
- **Mod & Repack wizard** (5 steps): drag & drop folder, scan, review, rebuild, install/package
- **Repack .arc** support: mod textures inside game archives and push them back
- **Format validation**: PNG / JPG / WAV / TGA / BMP rejected with clear reason before repack
- **Auto-backup** of `.original` .arc/.tab on first install
- **Package for sharing**: generates mod folder with README + install.bat
- **TOOL CHECK** panel with 5 live status checks (game, oodle, output, archives, tools)
- Auto-refresh TOOL CHECK every 2 seconds
- Rounded button UI in Home + wizards

### Changed
- New default entry point: **HomeForm** with wizards. Old sidebar toolkit remains available via **Advanced tools**
- Extract wizard organizes output into `_EDITABLE\TEXTURES`, `AUDIO`, `VIDEO`, `SCRIPTS`, `UI`, `OTHER`

### Fixed
- Wizard handles blank folders gracefully
- Rounded buttons no longer show black corners

### Internal
- TAB 3.1 parser + writer (byte-perfect roundtrip verified on game8 and game12)
- Oodle LZ decompress + compress via `oo2core_7_win64.dll`
- Test harness with 21 checks, all green
# Changelog

## [1.0.0] - 2026-09-23

### Added
- Initial release of the RAGE 2 Modding Toolkit.
- Full extraction of all 13 .arc archives (~14,400 files in ~30 s).
- Hash matching against a 358,057-line community filelist (~81% coverage).
- .avtx to .dds texture conversion using ddscConvert.
- Auto-copy of oo2core_7_win64.dll from the user's own RAGE 2 install.
- Modern dark-themed GUI with sidebar navigation, log panel, progress bar.
- Deploy to dropzone feature with hash renaming for loose-file loading.
- TAG0 detection at offset 4 to correctly identify model files.
- Built with .NET 8 SDK to minimize antivirus false positives.

### Known Limitations
- ~1,900 files still fail extraction due to proprietary compression variants.
- ~19% of extracted files remain hash-named.
- ddscConvert requires a full mipmap chain for DDS -> AVTX conversion.

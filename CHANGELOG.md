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
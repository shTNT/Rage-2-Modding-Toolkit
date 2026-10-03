# Release v2.1.5

**Date:** 2026-10-03  
**Type:** Extractor / converter / cancel fixes  
**Focus:** Extractor correctness, ATX1 mip chains, cancel, layout

## Summary

Fixes on top of v2.1.0. World Settings Editor, Mod Manager and Repacker are unchanged. Everything you could already do keeps working.

## What changed

### Extraction

- **Phantom hash filter.** The browser tree used to list 103,428 entries; only 36,695 actually live in the 50 `.tab` files the game loads. The tree is now filtered at load against a live hash set built from the `.tab` files: only real assets are shown, and extraction counters match (`Wanted == Found`, `Missing == 0`).
- **Filelist parser fix.** Both `ExtractorOpt` and `Repack` were splitting the filelist by TAB; the shipped filelist uses spaces. Descriptions, extensions and RepackForm labels now populate correctly.
- **Per-entity output folders** in both `SingleExtractForm` and `Wizard Extract`. Assets land at `output/MODELS/weapons/ark_assault/textures/...` instead of a flat dump or `_EDITABLE/TEXTURES/`.
- **Cancel is real.** `CancellationToken` threaded through `Extractor.ExtractAll` and the conversion loops (avtx + atx1). Cancel now stops within one asset, and re-running the wizard works (the flag resets at start).

### Texture conversion

- **ATX1 mip chain support.** The old algorithm only accepted square mip-0 BC1/BC3 blobs. Real RAGE 2 `.atx1` are BC1/BC3 mip chains (mip 0+1, or 0+1+2), non-square, or cube maps. New `TryGuessAtx1Format` searches (W, H, mips, blockSize) matching the byte size, and writes a correct DDS header with `MIPMAPCOUNT` + `MIPMAP` caps. **4,682 previously failed conversions now succeed.**
- **ATX1 in Wizard Extract.** `EXTRACT EVERYTHING` used to leave `.atx1` untouched. They now convert in parallel (`MaxDegreeOfParallelism = 8`) alongside `.avtx`.
- **sRGB fix in `AssetConverters`.** Was using `-srgbi -srgbo -f R8G8B8A8_UNORM_SRGB`, which double-applies the gamma. Now uses `-srgbi -f R8G8B8A8_UNORM_SRGB` (case D of the V2.0.1 addendum). Diffuse / albedo PNG output is no longer washed out.
- **Conversion preferences gear in Wizard Extract** - same gear as `SingleExtractForm`, only in the extract wizard.

### Prompt + GUI

- **Dynamic format in the extract prompt.** The `Extract selection` dialog now reflects the gear: `N texture(s) -> DDS editable` / `PNG editable`, `N audio(s) -> WAV editable / OGG editable / kept as-is`.
- **SingleExtractForm layout fix.** The tree was computed with `FOOTER_H = 76` while the footer was 110 px tall (`76 + 34`). The last 28 px of the tree were hidden behind the footer at any window size, cutting off the last alphabetical nodes (`UI`, `VIDEOS`). Constant and footer height are now consistent.

## Verified

- Single extract, 10,058 files: **10,013 `.dds`, 0 `.atx1` residual, 0 conversion failures**.
- Wizard extract: **39,673 files / 0 missing / 2 m 41 s**, avtx `5,153 / 5,153`.
- Tree: **36,695 hashes shown**, 66,733 phantom entries filtered out.
- Baseline 46/46 intact after every test.

## Installation

1. Download `RAGE2TOOLKIT-v2.1.5.7z`.
2. Verify SHA256 (published with the asset).
3. Extract anywhere.
4. Run `RAGE2Toolkit.exe`.
5. On the home screen, select `RAGE2.exe` and the output folder.

## Notes

- `oo2core_7_win64.dll` is not bundled (auto-copied from the game on first run).
- Requires RAGE 2 installed locally.
- Compatible with existing v2.1.0 / v2.1.1 mods.

## Credits

- REDxEYE for the hash extractor that produced the filelist shipped with this toolkit.
- DECA (kk49) for the raw string catalog reference.
- Kry0genik for testing and direction.

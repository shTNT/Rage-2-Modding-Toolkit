# v2.0.5 - Critical DDSC fix + pipeline hardening

**Release date:** 2026-09-28
**7z:** `RAGE2TOOLKIT-v2.0.5.7z` (45.14 MB)
**SHA256:** `60F382FC8B917B1AE99BDFACD9795AEE06D9A11803E79199A10F0E8DB06B4E22`

---

## TL;DR

If you install mods that touch `.ddsc` or `.avtx` textures, **v2.0.4 crashed the game on save load**. v2.0.5 fixes it. Nothing else about the toolkit was broken, but this fix touches the whole texture pipeline and adds safety nets around it.

---

## The critical bug

The bundled `ddscConvert.exe` (from Generation Zero upstream) spreads texture mipmaps across **three separate files**: `.atx1` + `.atx2` + `.ddsc`. RAGE 2 expects a **monolithic `.ddsc`** with the full mip chain inside.

Result: any mod built from a `.ddsc`/`.avtx` texture was written into the `.arc` as a 22 KB fragment instead of a 1.4 MB monolithic file. The engine read the header (`mip_count=11`), tried to read 11 mip levels from a 1-mip payload, and hung on save load.

**Fix:** the toolkit now builds the AVTX file by hand:

1. `texconv -f <original DXGI> -m <original mip count>` produces a DDS with the full mip chain.
2. The 128-byte header from the **original asset in the `.arc`** is preserved.
3. Payload = `DDS[128:]` appended to the original header.

Verified: 1,398,240 bytes - byte-identical size to the original pistol texture.

---

## What else changed

### Fixes (all silent before)

- **`ObjectDisposedException` in `.atx1` -> PNG.** 236/236 conversions were failing. Root cause: `BinaryWriter.Dispose()` was closing the underlying `FileStream` before the payload write.
- **`.atx1` -> DDS returned PNG.** Hardcoded `-ft png`. Now respects the target format.
- **`_nrm` and `_mpm` textures came out granulated.** sRGB was being applied to linear data. Now uses `IsColorSuffix()` to pick profile per PBR suffix.
- **Cleanup deleted source files when conversion failed.** Silent data loss. Now conditional.
- **`mod.json` was case-sensitive.** Lowercase keys ignored. Now case-insensitive.
- **CLI parser off-by-one.** `--force` at the end of args was never read.
- **Collapse by priority ignored original format.** Now uses `origExtByHash` to pick the winning file.
- **`UninstallEngine` did `RemoveById` before checking drift.** Left the registry inconsistent on abort.

### New

- **Drift detection.** SHA256 per `.arc` stored in `mods.json` (`ArcLastWritten`). Install/uninstall refuses if the `.arc` was modified outside the ModManager. `--force` overrides.
- **Helpers**: `IsColorSuffix()`, `MapDxgiToTexconv()`, `TryReadAvtxHeader()`.
- **Docs**: `docs/ROLLBACK.md`, `docs/FALSE_POSITIVES.md`.
- **Harness V4**: 19-phase test runner (`MASTER-TEST-RUNNER-V4.ps1`). 19/19 PASS.

### Changed

- `TabFormat.cs` extracted from `Repack.cs`.
- `__converted__` staging moved to `%TEMP%\RAGE2Toolkit_conv_<guid8>`.
- `PendingStatus` enum replaces `bool Ok` + `string Status`.
- `type_map.json` and `type_map_v2.json` removed. Only `type_map_v3.json` ships.
- Temporary `Diag.cs` debug logger removed.

---

## Breaking

**New monolithic `.ddsc` layout.** Mods built with v2.0.4 or older that touch DDSC textures are incompatible and must be rebuilt with v2.0.5.

`mods.json` now has an `ArcLastWritten` field (backward compatible; missing field is treated as no baseline).

---

## Verified in-game

- `.atx1` reskin (ark_assault dif) - visible in-game, no crash.
- `.ddsc` reskin (ark_pistol dif) - visible in-game, no crash.
- Baseline 46/46 intact after every test.

---

## Installation

1. Download `RAGE2TOOLKIT-v2.0.5.7z`.
2. Verify SHA256: `60F382FC8B917B1AE99BDFACD9795AEE06D9A11803E79199A10F0E8DB06B4E22`.
3. Extract anywhere.
4. Run `RAGE2Toolkit.exe`.
5. Select `RAGE2.exe` on first run to auto-copy `oo2core_7_win64.dll`.

---

## Credits

- **Kry0genik** - design, testing, in-game validation.
- **DeepSeek** - C# and PowerShell implementation.
- **REDxEYE** - ApexPredator author, external cross-check on AVTX header layout.
- **DECA** - initial filelist and reference TAB parser.
- **PredatorCZ** - ApexToolset (`ddscConvert`, `R2SmallArchive`).
- **Microsoft** - DirectXTex (`texconv`).
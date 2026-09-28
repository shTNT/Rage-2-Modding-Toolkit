# v2.0.5 - Full Mod System + critical DDSC fix

**Release date:** 2026-09-28
**7z:** `RAGE2TOOLKIT-v2.0.5.7z` (45.14 MB)
**SHA256:** `60F382FC8B917B1AE99BDFACD9795AEE06D9A11803E79199A10F0E8DB06B4E22`

---

## TL;DR

RAGE 2 Modding Toolkit is no longer just an extractor + repacker. **v2.0.5 ships the full modding pipeline**:

- **Mod System backend** - pack, install, uninstall, priority, conflict detection, backups, rollback.
- **Mod Manager GUI** - drag & drop mods, see what is installed, manage conflicts, restore backups.
- **Redesigned Repack wizard** - produces distributable `.zip` mods (1-5 MB) instead of 700 MB `.arc` files.

Plus the critical DDSC crash fix, drift detection, and 12 other pipeline fixes.

---

## The Mod System

### Why it matters

Before: any mod was a 700 MB `.arc` full replacement. Not shareable, not scalable, not manageable.

Now: a mod is a 1-5 MB `.zip`. Pack it, share it, install it, uninstall it. The toolkit handles backups, priority, conflict detection, and rollback automatically.

### Backend components

- **ModMetadata** - `mod.json` schema 1 (name, version, author, description, license, dependencies, conflicts).
- **ModsStore** - portable registry at `mods/mods.json` + `mods/library/` (original zips) + `mods/backups/` (original `.arc` files). No `%APPDATA%`, no registry keys.
- **ModValidator** - validates each asset, groups by hash, prioritizes native over convertible.
- **ModPackager** - folder -> validated `.zip` with `mod.json` injected.
- **ModInstaller** - 6-phase install: READ, DETECT (which `.arc` has the hash), CONFLICT, PRE_INSTALL, BUILD_TMP, COMMIT, REGISTER. Rollback on failure.
- **ConflictChecker** - hash-level conflict detection against currently installed mods.
- **UninstallEngine** - restores backups when no other mod touches the `.arc`, rebuilds with remaining mods otherwise. Cleanup of orphan backups.

### Mod Manager GUI

- Drag & drop `.zip` to install.
- List of installed mods with ID, name, version, priority.
- Priority adjust (higher wins on hash-level overlap).
- Conflict panel showing shared hashes between mods.
- Uninstall individual mods.
- `Restore All` returns every `.arc` to the original state.
- Backups stored per-universe (`initial_game8.arc.original` vs `supplemental_game8.arc.original`) to avoid collisions.

### Redesigned Repack wizard

3 steps (was 5):

1. **Drop** - folder with edited assets, choose raw or convert.
2. **Scan** - validates hashes, classifies each file, collapses duplicate hashes by priority.
3. **Result** - asks for name + author, produces `.zip`, shows path.

No more direct-to-game install. The wizard now produces distributable `.zip` files by design.

### Drift detection

If an `.arc` is modified outside the ModManager (external tool, old wizard, manual copy), the next install/uninstall refuses to proceed by default. This prevents silent overwrites of external changes. `--force` overrides. State is tracked per `.arc` via SHA256 in `mods.json` (`ArcLastWritten`).

---

## The critical DDSC fix

The bundled `ddscConvert.exe` (from Generation Zero upstream) spreads texture mipmaps across **three separate files**: `.atx1` + `.atx2` + `.ddsc`. RAGE 2 expects a **monolithic `.ddsc`** with the full mip chain inside. Installing any mod built from a `.ddsc`/`.avtx` texture caused the game to **hang on save load**.

The toolkit now builds the AVTX file manually: **original header (128 bytes) + full mip payload from texconv**. Verified byte-identical size to the original.

| Original asset | How v2.0.5 writes it |
|---|---|
| `.atx1..9` | Raw BC1 mip 0, no header (matches original) |
| `.ddsc` / `.avtx` | Monolithic AVTX with original DXGI format preserved |

---

## Other fixes (12)

- **`ObjectDisposedException` in `.atx1` conversion.** 236/236 conversions were failing silently. `BinaryWriter.Dispose()` was closing the underlying `FileStream` before the payload write.
- **`.atx1` -> DDS returned PNG.** Hardcoded `-ft png`. Now respects the target format.
- **`_nrm` and `_mpm` textures came out granulated.** sRGB was being applied to linear data. Now uses `IsColorSuffix()` heuristic.
- **Cleanup deleted source files when conversion failed.** Silent data loss. Now conditional.
- **`mod.json` was case-sensitive.** Lowercase keys were ignored. Now case-insensitive.
- **CLI parser off-by-one.** `--force` at the end of args was never read.
- **Collapse by priority ignored original format.** Now uses `origExtByHash` to pick the winning file.
- **`UninstallEngine` did `RemoveById` before checking drift.** Left the registry inconsistent on abort.
- **`ddscConvert` side files polluted the converted dir.** Cleaned up.
- **`type_map.json` and `type_map_v2.json` shipped stale fallbacks.** Removed; only `type_map_v3.json` is authoritative now.
- **`Diag.cs` temporary debug logger was writing to a hardcoded dev path.** Removed.
- **CLI `--uninstall` did not accept `--force`.** Fixed.

---

## Changed

- `TabFormat.cs` extracted from `Repack.cs` (was 835 lines mixed with legacy writer + old GUI).
- `__converted__` staging moved from source folder to `%TEMP%\RAGE2Toolkit_conv_<guid8>`.
- `PendingStatus` enum replaces `bool Ok` + `string Status` mix.
- Legacy `Repack.cs` (`Repacker` + `RepackForm`) deprecated. The old direct-install path is no longer the recommended workflow.

---

## New docs

- `docs/ROLLBACK.md` - manual restore instructions if `Restore All` fails.
- `docs/FALSE_POSITIVES.md` - how to verify SHA256 and report false positives to antivirus vendors.
- `docs/HOW_TO_USE.md` - rewritten around the Repack + ModManager workflow.
- `docs/TROUBLESHOOTING.md` - drift, monolithic DDSC, false positives, rollback.

---

## Breaking changes

- **New monolithic `.ddsc` layout.** Mods built with v2.0.4 or older that touch DDSC textures are incompatible and must be rebuilt with v2.0.5.
- `mods.json` now has an `ArcLastWritten` field (backward compatible; missing field is treated as no baseline).
- Legacy `Repack.cs` is deprecated.

---

## Verified in-game

- `.atx1` reskin (ark_assault dif) - visible in-game, no crash.
- `.ddsc` reskin (ark_pistol dif) - visible in-game, no crash.
- Baseline 46/46 intact after every test.
- Harness V4: 19/19 PASS.

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
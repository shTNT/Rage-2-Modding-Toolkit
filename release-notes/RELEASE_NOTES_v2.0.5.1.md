# v2.0.5.1 - Extraction fix + version metadata fix

**Release date:** 2026-09-28
**7z:** `RAGE2TOOLKIT-v2.0.5.1.7z`

Hotfix on top of v2.0.5. Archive format, mod format and mod compatibility unchanged.

---

## What this fixes

### Extraction was skipping assets based on preferences

With `Resolution = 2048` selected in the gear dialog, `.ddsc`/`.avtx` textures were extracted as raw bytes instead of being converted to PNG/DDS. With `Resolution = 1024`, `.atx1` textures were skipped. The user had no way to know — the status bar just said "0 ddsc / 0 atx".

Now: every supported extension is converted through its natural path, regardless of preferences. The `Resolution` group has been removed from the gear dialog.

### Window title said v2.0.4

The toolkit was showing `v2.0.4` in the window title and header even after installing v2.0.5. The csproj version number had not been bumped. Fixed to `v2.0.5.1`.

---

## What this does not change

- Archive format (TAB 3.1)
- Mod format (`mod.json` schema 1)
- Mod install / uninstall / priority / conflicts / backups
- Repack pipeline (RepackerCore v5.2)
- Monolithic DDSC fix from v2.0.5
- Drift detection

---

## Upgrade from v2.0.5

Extract the `.7z` over your existing toolkit folder. Your `mods/` folder and installed mods are preserved.

**Recommended step:** if you have any mod installed that touches textures, uninstall it, reinstall it with this build. The previous build may have written `.avtx`/`.ddsc` mods incorrectly.

---

## SHA-256

(Calculated at packaging time and attached to this release.)

---

## Known issues

The automated harness does not currently exercise the extract wizard's conversion path. That gap is documented for v2.0.6, where the extract logic will be refactored into a headless `ExtractPipeline` and covered by dedicated test phases.

Two other structural items queued for v2.0.6:

- Orphan-backup detection (warn if `mods/backups/` has entries but `mods.json` is empty)
- First-class mesh handling (reclassification + validator + docs)

---

Full changelog: [`CHANGELOG.md`](https://github.com/shTNT/Rage-2-Modding-Toolkit/blob/main/CHANGELOG.md)
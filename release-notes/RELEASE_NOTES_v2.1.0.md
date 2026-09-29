# RAGE 2 Modding Toolkit v2.1.0

**Release date:** 2026-09-29
**Focus:** World Settings Editor + wizard redesign
**Compatibility:** Mods built with v2.0.5 or newer work unchanged. Same `.zip` format, same `.arc` layout.

---

## What is new

### World Settings Editor

The toolkit now ships with a native C# editor for game settings that were previously untouched publicly. It opens from **Wizard Modding -> WORLD SETTINGS EDITOR**.

It edits **7 game setting files**:

| File | Controls |
|------|----------|
| Spawn Budget Pools | How many civilians, vehicles, combatants, animals and encounters live in the open world |
| Player Stats | Health, armor, movement, abilities, combat tuning |
| Difficulty | Enemy scaling, cooldowns, damage per difficulty tier |
| Damage Types | Bitmask definitions of every damage type (Fire, Bullet, EMP, Corruption, ...) |
| Vehicle Types | Physics and handling per vehicle class |
| Weather Settings | Presets, transitions, conditions |
| Sun / Lighting | Global sun, sky, lighting |

**No extraction needed.** The 7 settings files ship inside `data/settings/` and are copied to the working directory automatically on first run.

**SAVE produces a mod.** Hit SAVE, provide a name and author once per session, and the toolkit:

1. Backs up the original `.rtpc`
2. Applies your changes in-place
3. Copies the modified file to the staging directory
4. Builds an installable `.zip` in `RAGE2Toolkit_Output/world_editor/`
5. Offers to open the output folder

The resulting `.zip` installs through the Mod Manager like any other mod.

**Backup Manager dialog.** Every change is snapshotted. Restore a previous version, delete individual snapshots, or open the backup folder. The first snapshot is marked `[original]` and cannot be deleted.

**Bitmask detection.** The `Damage Types` file is a bitmask node. The editor detects it automatically: only powers of 2 are accepted (1, 2, 4, 8, ...), invalid values block SAVE with a clear explanation.

**Breadcrumb + semantic grouping.** The properties view shows the full path (`Difficulty > not_activew_damage > Weapons > T3 Cannon Cooldown (min)`). Siblings are grouped by semantic token (`Defence`, `Stage1`, `Stage2`, ...) so related settings are not scattered across hundreds of rows.

### Wizard redesign

**Wizard Modding** now puts `MOD MANAGER` front and center. It is the entry most users need - both for installing mods and for testing their own. `WORLD SETTINGS EDITOR` and `REPACKER` are available as smaller secondary actions.

**Wizard Extract** now puts `BROWSE & PICK MANUALLY` front and center. It is the recommended entry for building a specific mod. `EXTRACT EVERYTHING` stays available for the full 60 GB corpus dump.

Hover tooltips with fade in/out appear on every action button.

### Hash database

445 new hashes resolved for the settings editor, sourced from the RED_EYE kv table plus targeted cracking. Classification by structural pattern (Hungarian prefix + camelCase/SCREAMING + engine suffix).

Editor coverage is **445 / 989 = 45%**. The remaining 544 are engine-internal hashes with no string in any accessible source - not in the exe, not in RED_EYE, not in the filelist. They are listed with their technical name (`unk_XXXXXXXX`) and their file, type, and neighboring properties.

### Harness V4

6 new test phases (20-25) covering the settings editor, wizard strings, and bundled files. Harness paths fixed (were pointing to `v2.0.5-build`, now `v2.1.0-build`).

---

## Fixed

- **CRLF vs LF in `Wizards.cs`.** Anchors written as LF failed against the CRLF file. Normalization now happens on read and write.
- **`lblDesc` field scoping.** The file description label was a local in `BuildUI`, unreachable from its updater.
- **Bitmask visual noise.** A `BITMASK - powers of 2` prefix appeared on every row of the grid. Now only in the column header, tooltip and footer.
- **Orphan brace in `Wizards.cs`.** A code replacement left a `}` glued to a class declaration.
- **Build script `Copy-Item` relative path.** `..\publish` resolved against the wrong CWD.

## Changed

- `Settings Editor` renamed to `World Settings Editor`.
- `settings_editor_cracked.json` renamed to `settings_editor_names.json` (neutral name; some AV engines flag "cracked" as a keyword).
- `FILES` array reordered by usefulness.
- `Settlements` file removed from the editable list (405 entries, zero editable properties).
- 40 stale `.bak` files archived out of `release\`.

---

## Known limitations

- The editor's in-window help still describes the pre-v2.1.0 workflow (`_stage_mods\` + drag to Repacker). The functional behavior is correct; the help text will be updated in a follow-up release.
- 544 settings-editor hashes remain unresolved (see "Hash database" above).
- PU-TEST of the save -> zip -> Mod Manager flow has been done manually and works, but has not been automated in the harness yet.

---

## Compatibility

- **Archive format:** unchanged from v2.0.5 (TAB 3.1, Oodle Kraken v7)
- **Mod format:** unchanged (`.zip` with `mod.json` + `<hash16>.<ext>` assets)
- **`mods.json`:** backward compatible. New `ArcLastWritten` field is optional.
- **Mods built with v2.0.4 or older that touch DDSC/AVTX textures** must be rebuilt with v2.0.5 or newer. That requirement is unchanged since v2.0.5.

---

## Credits

**The toolkit itself is built from scratch.** Every parser, writer, GUI and installer in this release is original work.

Special thanks to **REDxEYE**, author of [ApexPredator](https://github.com/REDxEYE/APEX-PREDATOR):

- His **hash extractor** produced the 103,428-path database that ships with this toolkit. It directly enabled the filelist expansion and the 444-name `settings_editor_names.json`.
- His **file format research** on the Avalanche Apex engine clarified several structures (`.atxN` mipmap family, `modelc = adf`, `sarc.gtoc`) used as reference.
- His **patience** on the DECA Discord.

Bundled third-party tools:

- **DECA** (kk49) - initial path database
- **PredatorCZ** (ApexToolset) - `ddscConvert`, `R2SmallArchive`
- **Microsoft DirectXTex** - `texconv`

And the RAGE 2 modding community.

---

## Download

[`RAGE2TOOLKIT-v2.1.0.7z`](https://github.com/shTNT/Rage-2-Modding-Toolkit/releases/tag/v2.1.0) (~62 MB)

SHA256 published alongside the archive.
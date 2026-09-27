# How to Use the RAGE 2 Modding Toolkit

## Quick Start

1. Extract the toolkit to any folder.
2. Run `RAGE2Toolkit.exe`.
3. Configure game path - select the folder containing `RAGE2.exe`.
4. Configure output path - choose where files will be extracted.
5. Extract assets from the browser: pick entity, extract editable PNG/DDS.
6. Edit with external tools (Paint, GIMP, Stable Diffusion, Audacity).
7. Repack: drop your edited files into the Wizard -> produces a `.zip`.
8. Install: drop the `.zip` on the ModManager -> writes to `archives_win64/`.
9. Launch the game and see your mod in action.

---

## The Modding Workflow

RAGE 2 has no runtime override system that works for texture assets. Every mod
is a full `.arc` rewrite. The toolkit handles this safely:

- The first time you install a mod, the toolkit backs up the original `.arc` to
  `mods/backups/<universe>_<gameN>.arc.original` and `.tab.original`.
- Every subsequent install/uninstall goes through the ModManager, which knows
  what is currently applied.
- Multiple mods can share the same `.arc` as long as they touch different hashes.
- Priority is MO2-style: higher number wins when there is overlap.

### Extract & Edit

1. Open `FILE BROWSER & EXTRACTOR`.
2. Search for an entity (e.g. `ark_assault`), a hash, or by extension (`.ddsc`, `.atx1`).
3. Select the assets you want. Click `Add to Selection`.
4. Click `Extract Selected`. Choose `Convert & Extract` for editable PNGs/DDS,
   or `Extract raw only` if you want the original bytes.
5. Edit the PNG/DDS with your tool of choice.

### Repack into a Mod .zip

1. Open `MODDING -> REPACKER`.
2. Drop the folder where your edited files live (they must keep the hash in
   the filename: `<descriptive>_<HASH16>.<ext>` or `<HASH16>.<ext>`).
3. Choose `A` (files are already game-ready) or `B` (let the toolkit convert
   PNG/JPG/WAV to game formats).
4. Click `Next` and let it scan. The wizard validates each file against the
   game and drops anything the engine cannot read.
5. Click `Next` again. It produces a `.zip` you can share or install.

### Install with the ModManager

1. Open `MODDING -> MOD MANAGER`.
2. Drag the `.zip` onto the window (or click `Install .zip`).
3. The toolkit patches the target `.arc`, keeps backups in `mods/backups/`,
   and registers the mod in `mods/mods.json`.
4. To remove a mod, select it in the list and click `Uninstall`.
5. To remove everything at once, click `Restore All`.

---

## Worked Example: Texture Reskin

1. Browse to `WEAPONS > ark_assault > textures > base`.
2. Mark `ark_assault_dif_<HASH>.atx1`. Add to Selection. Extract Selected ->
   Convert & Extract (Resolution 2048 for `.atx1`).
3. Open the resulting `.png` in Paint or GIMP, repaint the surface.
4. Save as PNG with the same filename.
5. `MODDING -> REPACKER`: drop the folder. Choose `B`. Next. Next.
6. `MODDING -> MOD MANAGER`: drop the produced `.zip`. Install.
7. Launch RAGE 2. Load a save. Check the rifle.

---

## Common Workflows

### Texture Upscaling

1. Extract + Convert to DDS at 2048 resolution.
2. Run your upscaler (Chainner, ESRGAN) on the DDS files.
3. Save back as DDS (BC7 for albedo, BC5 for normal maps).
4. Repack and install via ModManager.

### Audio Replacement

1. Extract the `.ogg` or `.riff` you want to replace.
2. Edit in Audacity. Keep the same format (Vorbis for OGG).
3. Repack, install via ModManager.

---

## Troubleshooting

See `TROUBLESHOOTING.md`.
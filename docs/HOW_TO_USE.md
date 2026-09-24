# How to Use the RAGE 2 Modding Toolkit

## Quick Start

1. Extract the toolkit to any folder.
2. Run RAGE2Toolkit.exe.
3. Configure game path - select the folder containing RAGE2.exe.
4. Configure output path - choose where files will be extracted (~25 GB).
5. Extract All .arc files (~30 seconds).
6. Convert .avtx to .dds (~15 minutes).
7. Modify with external tools (Stable Diffusion, GIMP, Chainner).
8. Deploy to dropzone for in-game loading.

## The Dropzone System

RAGE 2 loads files from a dropzone\ folder next to RAGE2.exe.
No need to repack the .arc archives.

### Activating Dropzone

Add to Steam/Epic launch options (single line):

~~~text
--vfs-fs dropzone --vfs-archive archives_win64 --vfs-archive patch_win64 --vfs-archive dlc_win64 --vfs-fs
~~~

### File Naming

Files in dropzone\ must use the game's internal MurmurHash3 x64 hash,
not the friendly name. The toolkit does this automatically.

## Common Workflows

### Texture Upscaling

1. Extract + Convert to DDS.
2. Classify files by type.
3. Open output\_CLASSIFIED\textures\ in your upscaler.
4. Upscale to 2x or 4x.
5. Save back as DDS (BC3 for albedo, BC5 for normal maps).
6. Deploy to dropzone.

### Audio Replacement

1. Extract + Classify.
2. Edit .ogg files in output\_CLASSIFIED\audio\.
3. Deploy to dropzone.

## Troubleshooting

See TROUBLESHOOTING.md.
# Troubleshooting

## Game not detected

Ensure you selected the folder containing RAGE2.exe.

## Oodle DLL missing

Auto-copy failed. Manually copy oo2core_7_win64.dll from your RAGE 2
install into the toolkit's bin\ folder.

## Extraction fails for some archives

Some .arc files use unsupported compression variants (~12% of files).

Check:

- Game install is complete (not corrupted).
- ~25 GB free disk space.
- Toolkit not blocked by antivirus.

## Game crashes with mods

Remove dropzone\ and retry with fewer files.

## SmartScreen / Antivirus

The toolkit scans 0/71 clean on VirusTotal.

If SmartScreen shows a warning: More info then Run anyway.

If your antivirus flags it, verify the SHA-256:

~~~text
2639FF38A5422B17535FD3FCE6ED15A01CD5FF400440168E8D118F7AE3355D12
~~~

## Textures dark/broken in editors

Some .dds files are DX10 (BC6H/BC7), not supported by Paint.NET/GIMP.
Normalize with texconv:

~~~text
texconv -f BC3_UNORM -ft dds texture.dds
~~~

## Dropzone ignored by game

Verify launch options:

~~~text
--vfs-fs dropzone --vfs-archive archives_win64 --vfs-archive patch_win64 --vfs-archive dlc_win64 --vfs-fs
~~~

And that dropzone\ is next to RAGE2.exe.
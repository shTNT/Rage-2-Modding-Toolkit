# Format Reference

## The .arc and .tab System

RAGE 2 stores assets in pairs:

- .arc files: contain the file data.
- .tab files: table of contents describing files inside the .arc.

## Compression

The game uses Oodle (Kraken). The oo2core_7_win64.dll runtime is
required and is NOT included. The toolkit auto-copies it from your
own RAGE 2 installation.

Compression types:

- ctype=0: raw
- ctype=1: zlib
- ctype=4: Oodle v7

## Texture Formats

- .avtx: Avalanche Texture (native)
- .dds: DirectDraw Surface (editable)
- .ddsc: deployment variant

ddscConvert.exe requires a full mipmap chain for DDS -> AVTX.
Run texconv -m 0 first if you upscaled.

## File Identification: Hashes

Files are identified by MurmurHash3 (x64, 128-bit) of their original path.
The toolkit uses a community filelist to map hashes to names.

- data\filelist.txt: 11,714 hash-to-name mappings
- data\filelist_raw.txt: 358,057 raw paths from DECA

## Detected File Signatures

| Extension | Signature | Notes |
|-----------|-----------|-------|
| .avtx | AVTX | Textures |
| .dds | DDS | DirectDraw Surface |
| .ogg | OggS | Audio |
| .riff | RIFF | Audio |
| .fsb | FSB5 | FMOD audio |
| .bik | BIK | Bink video |
| .bk2 | KB2 | Bink video 2 |
| .cfx | CFX | Flash UI |
| .adf | FDA | Avalanche Data File |
| .tag | TAG0 | Models (offset 4) |
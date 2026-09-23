# Format Reference

## The .arc and .tab System

RAGE 2 stores assets in pairs:

- .arc files: contain the file data.
- .tab files: a table of contents describing files inside the .arc.

The toolkit reads the .tab file to understand how to extract and decompress
the data from the corresponding .arc.

## Compression

The game uses Oodle (Kraken). The oo2core_7_win64.dll runtime is required
and is NOT included. The toolkit auto-copies it from your own RAGE 2
installation.

Compression types:

- ctype=0: raw
- ctype=1: zlib
- ctype=4: Oodle v7

### Multi-block files

Some assets are large enough to span multiple consecutive compression
blocks in the archive. The .tab file keeps a block table where each entry
stores (compressed_size, uncompressed_size) for one block. A file entry
points to its starting block via `block_idx`, and the extractor must
iterate the block table until the cumulative compressed size matches the
file's declared compressed_size.

This behavior was documented by the DECA project in their reference parser
`ff_arc_tab.py`. The multi-block handling in this toolkit is a port of that
logic. Without it, roughly 2,500 additional files would not be recoverable.

## Texture Formats

- .avtx: Avalanche Texture (native)
- .dds: DirectDraw Surface (editable)
- .ddsc: deployment variant

ddscConvert.exe requires a full mipmap chain for DDS -> AVTX.
Run texconv -m 0 first if you upscaled.

## File Identification: Hashes

Files are identified by MurmurHash3 (x64, 128-bit) of their original path.
The toolkit uses a community filelist to map hashes to names.

- data/filelist.txt: 11,714 hash-to-name mappings (from DECA)
- data/filelist_raw.txt: 358,057 raw paths (from DECA)

The filelist data comes from the DECA project. Their reverse engineering
effort is the reason extracted files can be given readable names instead
of raw hashes.

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

## Further Reading

- DECA project source: the reference parser and filelist originate here.
- ApexToolset by PredatorCZ: https://github.com/PredatorCZ/ApexToolset
- DirectXTex by Microsoft: https://github.com/microsoft/DirectXTex

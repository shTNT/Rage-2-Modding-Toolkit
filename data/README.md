# Filelist

Hash-to-path mapping used by RAGE2Toolkit to recover real file names from the .arc archives.

## File

- filelist.txt - 14,212 entries

## Coverage

14,212 / 16,385 hashes (86.74%).

The remaining 2,173 entries are:

- ~1,449 engine placeholders (byte-identical payloads, no textual path by design)
- ~116 CFX (proprietary Avalanche UI format)
- ~63 OggS audio (Vorbis, no metadata)
- ~7 RTPC manifest containers
- ~199 Oodle variant failures
- ~340 AVTX textures without public path names

## Format

    <16-hex-digits>\t<path>

## Source

The base filelist is from the DECA project (https://github.com/kk49/deca). The extra entries were recovered by this project via MurmurHash3 brute-force and cross-orphan string mining - see CHANGELOG for details.

## Contributing

If you recover additional hash to path mappings, append them to filelist.txt (one per line, tab-separated) and open a PR or share them in the DECA Discord #rage-2 channel.

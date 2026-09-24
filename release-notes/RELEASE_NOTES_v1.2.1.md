# v1.2.1 — Filelist coverage 86.74%

## Highlights
- Filelist coverage: **86.74%** (14212 / 16385 hashes resolved)
- filelist_extra.txt: **2,498** entries (was 2,492 in v1.2.0)

## What changed since v1.2.0
- +3 DLC2 FMOD bank references recovered via cross-orphan manifest mining:
  - `sound/fmod_banks/dlc2_skeletons.fmod_bankc` + `.fmod_sbankc`
  - `sound/fmod_banks/dlc2_bone_towers.fmod_bankc` + `.fmod_sbankc`
  - `sound/fmod_banks/dlc2_creepy_hut.fmod_bankc` + `.fmod_sbankc`

## Technical findings (documented for the community)
- MurmurHash3 seed is 0 (verified across 100 known paths, 14 alternative seeds tested)
- No invisible prefixes/suffixes (31 variants tested across 500 paths)
- No hash→path table in RAGE2.exe (300 known hashes probed as uint64 LE across 30 PE files)
- No public PDB available (GUID C1A302DE-F6F6-DA4D-8F99-6A5537AA2902, Age 1)
- No gt0c/GARC files exist in the installation
- 1,449 of the remaining orphans are engine placeholders (byte-identical payloads, no textual path by design)

## Remaining orphans (2,173)
- ~1,449 engine placeholders (irreducible)
- ~116 CFX (proprietary Avalanche UI format, no SWF, no readable strings)
- ~63 OggS audio (Vorbis, no metadata)
- ~199 Oodle variant failures (compression format not supported by oo2core_7)
- ~340 AVTX textures without public path names

## Theoretical ceiling
Without runtime hooks (Rage2Hook or equivalent), **86.74% is the practical static-analysis ceiling**. The mathematical maximum is ~91.2% (16,385 - 1,449 placeholders). The remaining gap requires runtime capture, which is out of scope for this toolkit.

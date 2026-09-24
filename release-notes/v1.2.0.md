# v1.2.0 — Filelist coverage 86.2%

## Highlights

- **Filelist coverage: 86.2%** (14121 / 16385 hashes resolved)
- Integrated `filelist_extra.txt` with 2,407 additional hash→path mappings
- Filelist loads base + extra with extra taking precedence
- Rebuilt with .NET 8: Roslyn C# 12, TieredPGO, Server GC, Concurrent GC

## Coverage journey

- Initial: 71.5% (11,714 / 16,385)
- After brute-force + DECA analysis: 84.7%
- After torrent cross-reference + UI mining: 86.2%

## Methods used

- DECA community filelist (base 11,714 entries)
- Brute-force: extension swap, suffix sweep, number expansion, morph transforms
- Torrent rage-2-archive cross-reference (models + textures from RAGE2 runtime hook capture)
- UI asset discovery: vehicle icons, weapon icons, skin variants
- Binary string mining: .gfx, .cfx, .bin, .bl, .ee, .nl, .fl, .tag, .ban, .blo, .adf
- FMOD bank structural analysis
- PE (RAGE2.exe) string mining and pointer table extraction
- CFX container parsing (zlib decompression)

## Limitations (honest)

- ~1,449 of the remaining 2,264 orphan hashes are engine placeholders (identical payload repeated across hashes, no textual path exists)
- ~500 are AVTX textures for which no textual path is known (likely removed content, debug assets, or engine-internal)
- The theoretical ceiling without runtime hooks is ~91%
- Practical static-analysis ceiling achieved: 86.2%

## No runtime hooks required

All analysis done statically. No Rage2Hook, no memory injection, no Frida, no MinHook.

## Credits

- DECA project (kk49) — filelist and TAB format reference
- PredatorCZ (ApexToolset) — ddscConvert, R2SmallArchive
- Microsoft DirectXTex — texconv
- RAGE 2 modding community — format documentation

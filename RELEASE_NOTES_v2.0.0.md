# RAGE 2 Modding Toolkit v2.0.0

**Extract is 10x faster. Writer is now parallel and deterministic. Validators, converters, and a redesigned drag & drop wizard.**

This is the biggest release since 1.0. If you only care about one line: **extract all 46 archives in ~108 seconds** (was ~20 minutes). Classify + convert is ~1 minute (was 15-19). Round-trips are SHA256-identical across runs.

---

## Download

`RAGE2TOOLKIT-v2.0.0.7z` (~44 MB) + `RAGE2TOOLKIT-v2.0.0.7z.sha256`

Requirements: Windows 10 or 11 (x64). Your own copy of RAGE 2. No .NET runtime needed (self-contained).

---

## What's new

### Performance

- **Extractor 10x faster.** 46 archives, 39,519 files, 61.66 GB in ~108 s. Peak RAM 3.3 GB.
- **Classify + convert 20x faster.** ~1 min for the full corpus.
- **Parallel writer.** RepackerCore v5.2 uses a 4-phase pipeline with Parallel Oodle Kraken compression and parallel writes. SHA256-identical output across runs.

### Validation

- **14 asset validators.** Structural + semantic checks for textures, audio, video, UI, scripts, meshes, and config. Tells you Valid / Warn / Invalid / Unknown with reasons.

### Conversion

- **PNG / JPG / BMP / TGA to DDSC** (bundled texconv + ddscConvert).
- **DDS to DDSC** (bundled ddscConvert).
- **MP3 / WAV / FLAC to OGG Vorbis** (via ffmpeg, downloaded on first use).
- **Format & Files Guidelines window.** Tells you what every extension is, what's native, what needs conversion, and what needs an external tool (with URL).

### GUI

- **Drag & drop works again** (see Fixes below). Drop directly on the wizard's drop zone or on the MOD & REPACK button.
- **ConvertChoice A/B.** When you drop a folder: "My files are game-ready" or "Convert with toolkit". ESC cancels.
- **Hash-name awareness.** If your files aren't named with a 16-hex-char hash, the wizard tells you exactly why it can't guess and what to do.
- **Crash handler.** Any unhandled exception shows the stack trace and offers to file a GitHub issue.

### Filelist

- **36,192 hashes / 39,519 = 91.58%** of gameplay assets mapped to their original path. Localization excluded on purpose (~45,739 extra hashes would drop coverage to ~42%).
- Every entry is bit-a-bit re-verified against a live .tab.

---

## Fixes

- **UIPI block on drag & drop.** The manifest was highestAvailable, forcing UAC elevation. Windows then blocks drag & drop from Explorer into elevated processes. Reverted to asInvoker. Trade-off: if you drop into a Windows-protected folder (Pictures / Documents / Desktop / Videos / Music / OneDrive), texconv may be blocked by Controlled Folder Access. The wizard warns you first.
- **AVTX dimension offsets fixed.** width is u16 at 0x0C, height at 0x0E (not 0x08/0x0A). Verified against 284 real AVTX files.
- **DDSC false warning** on payload_size=0 cleared.
- **ConvertChoice dialog** used to show "Detected 0 files" (hardcoded). Now shows the real count. Cancel button that rendered off-screen removed.
- **Advanced Tools renamed** to "Advanced Tools (Legacy)". No more underscores in UI labels.

---

## Known limitations

- **8.42% of gameplay hashes have no path** (3,327 entries). They extract and repack fine, but show up as HASH.ext. In practice this means: you can edit and replace them, but you won't know what they are without opening them first.
- **MP4 / AVI to BIK** requires RAD Video Tools (install manually; not redistributable).
- **UI CFX / GFX to SWF** requires JPEXS Decompiler.
- **FMOD banks** require FMOD Studio.
- **ffmpeg not bundled.** Downloaded on first audio conversion.
- **Mesh / animation editing is not supported.** Extract and repack only.

---

## Breaking changes from 1.3.x

- **config/paths.txt is no longer persisted.** You re-select game + output folder every session. This avoids stale-config bugs.
- **Oodle DLL is deleted on exit and on next start.** It is re-copied automatically when you select RAGE2.exe.
- **Source-level field renames** in the TAB 3.1 parser (F14/F18/F1C to Padding/MaxCompressedBlockSize/UncompressedBlockSize). Only affects anyone building from source.

---

## Verification

This release was validated with MASTER-TEST-RUNNER.ps1 (13 phases, PASS):

- setup: OK (0s)
- baseline: OK (8.6s) - 46/46 files match
- extract: OK (84s) - 39519 files
- hash: OK (1.5s) - 39519 files (63140 MB)
- validate: OK (134.7s) - exit=0
- adversarial: OK (1.3s) - 3/3 corruptions detected
- convert: OK (1s) - 3/3 PNG to DDSC
- rt-game8: OK (3.5s) - determinism=True
- interrupt: OK (6.4s) - intact=True recovery=True
- cli: OK (0.2s) - tools=True hash=True
- gui-smoke: OK (20s) - 3/3 alive
- reverify: OK (5.7s) - 46/46 files match
- cleanup: OK (4.7s) - 62 GB freed

Baseline 46 .arc intact before and after.

---

## How to use

1. Extract RAGE2TOOLKIT-v2.0.0.7z anywhere. No installation. No registry.
2. Run RAGE2Toolkit.exe. Windows SmartScreen may warn about unsigned binaries - this is normal for .NET self-contained tools. Verify the SHA256 against the .sha256 file if you want to be sure.
3. Click "Browse RAGE2.exe location" and select the folder containing RAGE2.exe. The Oodle runtime is auto-copied from your install.
4. Click "Select output folder" and pick a folder with ~62 GB free (only needed if you extract everything).
5. The TOOL CHECK panel turns green. You're ready.

To mod a texture: EXTRACT, find the .dds or .atx1 you want, edit it, drop the folder on MOD & REPACK, done.

Format & Files Guidelines is available at every step and tells you exactly which files can be edited, converted, or need an external tool.

---

## SHA256

D956B0C089B4A6C3C7D6AFE618249246A566B623648446A7253C4C72AF8B9FB8

Verify with:

    certutil -hashfile RAGE2TOOLKIT-v2.0.0.7z SHA256

---

## Credits

- **DECA project** (github.com/kk49/deca) - raw filelist reference and TAB parser documentation.
- **REDxEYE** (ApexPredator) - independent C++ TAB 3.1 parser used for cross-verification.
- **PredatorCZ** (ApexToolset) - ddscConvert and R2SmallArchive. GPL v3.
- **Microsoft** (DirectXTex) - texconv. MIT.
- **The RAGE 2 modding community** - testing and format documentation.

---

## Legal

Unofficial tool. Not affiliated with Avalanche Studios, Bethesda Softworks, RAD Game Tools, Epic Games, or any of their subsidiaries. All trademarks belong to their respective owners. Users must own a legitimate copy of RAGE 2.

The toolkit redistributes no game assets and no proprietary runtimes. The Oodle DLL is copied from your own installation on first run.
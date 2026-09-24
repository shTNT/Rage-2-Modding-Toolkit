## RAGE 2 Modding Toolkit v1.0.0 — Initial Release

### What it does

- Extracts all 13 `.arc` archives from RAGE 2 (~14,400 files in ~30 s)
- Identifies files by hash using the DECA community filelist (81% coverage)
- Converts `.avtx` textures to editable `.dds`
- Classifies files by type (textures, audio, video, models, UI, scripts, data)
- Deploys modified files to `dropzone\` with automatic MurmurHash3 renaming
- Auto-copies `oo2core_7_win64.dll` from your own RAGE 2 install

### Downloads

- `RAGE2TOOLKIT-v1.0.7z` — Complete toolkit
- `RAGE2TOOLKIT-v1.0.7z.sha256` — SHA-256 checksum

### Requirements

- Windows 10/11 x64
- Your own copy of RAGE 2
- ~25 GB free disk space during extraction

### Credits

Built by Kry0genik. Uses:

- DECA project — filelist data and TAB format documentation
- PredatorCZ (Lukas Cone) — ApexToolset (`ddscConvert`, `R2SmallArchive`), GPL v3
- Microsoft — DirectXTex (`texconv`), MIT

Full docs: https://shtnt.github.io/Rage-2-Modding-Toolkit/

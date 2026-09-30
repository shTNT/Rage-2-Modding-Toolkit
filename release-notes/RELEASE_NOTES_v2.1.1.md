# RAGE 2 Modding Toolkit v2.1.1

**Date:** 2026-09-30
**SHA256:** `EC4EA085EF2AF8AFBE97CB6EC060E7C822D6F7177CAD42E8FC8660A7C54525E8`
**Size:** 47.14 MB

## Highlights

This release fixes a critical bug that broke **EXTRACT EVERYTHING** in the public v2.1.0 build, and adds multi-file support to the **World Settings Editor**.

## Critical bug fixed

The **EXTRACT EVERYTHING** button used to crash with `Index was out of range` at the very end of the process, after it had already extracted and classified all 39,673 files. A false positive: extraction was working; only the summary popup failed.

The wizard now advances to Step 4 with the full summary and closes cleanly.

## World Settings Editor: edit multiple files in one session

Before: switching node or file discarded any edited value. SAVE only wrote the visible page.

Now:

- Edit N nodes across M files in the same session.
- Switch file, come back, changes are still there.
- SAVE opens a confirmation dialog listing every affected file.
- A single ZIP containing every modified `.rtpc`.

## GUI

- Home: bottom buttons rearranged in a symmetric 2x2 grid.
- Wizard Extract: removed the redundant "Extract all" button and the orphan label.
- EXTRACT EVERYTHING now matches the primary button width.
- Settings Editor help text updated.

## Internal

- 6 `Process.Start` sites wrapped in `using` (handles released immediately).
- Extended diagnostics in `StartExtract` (dumps to `_outputs` on exception).

## Hotfix (same day)

After the initial v2.1.1 build, a bug was found in the **World Settings Editor**: the working folder (`_outputs\world_editor\work\`) accumulated edits from previous sessions, so opening the editor after a prior session showed stale "Current" values instead of the game defaults. Any SAVE would bake those stale values into the mod ZIP.

Fixed: the editor now clears `WORK\` on open and reloads it from `release\data\settings\` (bundled originals). Every session starts fresh.

**This release asset was replaced.** If you downloaded the original v2.1.1 build, redownload and verify the new SHA256 below.
## Installation

1. Download `RAGE2TOOLKIT-v2.1.1.7z`.
2. Verify SHA256: `EC4EA085EF2AF8AFBE97CB6EC060E7C822D6F7177CAD42E8FC8660A7C54525E8`
3. Extract anywhere.
4. Run `RAGE2Toolkit.exe`.
5. On the home screen, select `RAGE2.exe` and the output folder.

## Notes

- `oo2core_7_win64.dll` is not bundled (auto-copied from the game when you select `RAGE2.exe`).
- Requires RAGE 2 installed locally.
- Compatible with existing v2.1.0 mods.
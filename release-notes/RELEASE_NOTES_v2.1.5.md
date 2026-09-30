# Release v2.1.5

**Date:** 2026-09-30
**Type:** Feature + bug fix
**Focus:** World Settings Editor - snapshot tracking, pending changes viewer, AMB custom tree

## Summary

Multi-file editing sessions are now usable: restore a snapshot and see the clean state, open a viewer that lists every pending change, double-click a change to jump to that setting.
Also ships 8 ready-to-install mods for RAGE 2's open world.

## What's new

### Snapshot restore tracking
Restoring a backup now clears ONLY that file's pending changes, preserves pending changes from other files, auto-selects the first editable node, refreshes the status bar.

### Pending changes viewer
Click the status bar (underlined, hand cursor) to open a full-screen viewer with columns File / Setting / Current / New value / Offset. Double-click a row to jump to that setting.

### Asset Memory Budgets custom tree
Categories (5) + Budget Slots (12). All 12 slots are structurally identical with the same 512/460 KB vanilla values. Slot roles are NOT documented in the file (engine indexes them by ordinal only).

### Resolved property names
Spawn Budget Pool (FC0BD72039E0B01D): 20 new names.
Asset Memory Budgets (33FB199032F32E37): memory_max (KB), soft_limit (KB).

## Known limitations
28 hashes remain unresolved across Difficulty (5), Player Stats (22) and Spawn Budget (4). They are runtime properties whose names are computed from ADF assets during game build and never appear as strings in the retail executable.

## Install
1. Extract the 7z into a folder
2. Run RAGE2Toolkit.exe
3. Point it at your RAGE2.exe (only the first time)

## Credits
- REDxEYE for the hash database
- DECA (kk49) for the raw string catalog reference
- Kry0genik for testing and direction

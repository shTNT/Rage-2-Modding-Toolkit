# RAGE 2 Modding Toolkit v2.0.1

Small hotfix release. Two bugs fixed in the asset browser.

---

## Fixed

### Asset browser: search by hash

Before this fix, typing a 16-hex hash (e.g. 4E37BD8EAD14BEA2) into the search bar returned 0 results, even when the file was in the archive. The searcher only looked at the path, not the hash itself. Now it matches both.

### type_map.json was truncated

The shipped type_map.json had correct totals (39,519) but the actual hashes[] and paths[] arrays were truncated to 500 per game. The file list loaded by the asset browser was missing 19,166 entries. The type_map is now regenerated from filelist.txt + the 46 .tab files, with all 39,519 hashes present in the arrays.

---

## Unchanged

Extract, repack, validate, convert, format validation, wizards, GUIDELINES window, crash handler: all identical to v2.0.0.

---

## SHA256

E07674E617ECD7A7515B402362DB9EE1113556477C7615883B93A2DAA0D05EF0

Verify with:

certutil -hashfile RAGE2TOOLKIT-v2.0.1.7z SHA256
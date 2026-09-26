# Filelist

`filelist.txt` - 36,695 verified hashes mapping 64-bit asset hashes to their original engine paths. Single unified database, deduplicated at hash level.

## Coverage

**36,695 / 39,519 = 92.85%** of gameplay asset hashes have a verified path.

The original three sources (DECA base, initial extras, supplemental) are preserved under `sources/` for documentation only. The toolkit loads only `filelist.txt`.

## Format

`<HEX16><TAB><path>` - one entry per line, UTF-8 without BOM, LF line endings.

## Contributing

If you recover additional hash-to-path mappings, append them to `filelist.txt` (one per line, tab-separated) and open a PR, or share them in the DECA Discord #rage-2 channel.

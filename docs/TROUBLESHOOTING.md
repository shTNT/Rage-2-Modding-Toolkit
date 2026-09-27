# Troubleshooting

## Game not detected

Ensure you selected the folder containing `RAGE2.exe`.

## Oodle DLL missing

Auto-copy failed. Manually copy `oo2core_7_win64.dll` from your RAGE 2
install into the toolkit's `bin\` folder.

## Extraction fails for some archives

Some `.arc` files use unsupported compression variants. Check:

- Game install is complete (not corrupted).
- Enough free disk space (full extract needs ~65 GB).
- Toolkit not blocked by antivirus.

## Game crashes after installing a mod

1. Open `MOD MANAGER`.
2. Click `Restore All` - restores every `.arc` from its `.original` backup.
3. Launch the game to confirm it works again.
4. Re-install mods one at a time to isolate the culprit.

If `Restore All` fails, see `docs/ROLLBACK.md` for manual restore.

## Mod install refuses with "drift detected"

This means the `.arc` was modified outside the ModManager (via the old wizard,
an external tool, or manual copy). The toolkit refuses to touch it to avoid
losing those changes.

To resolve:
- Restore the `.arc` to the state the ModManager left it in, or
- Use the CLI with `--force` to override (you will lose the external changes).

## SmartScreen / Antivirus

Self-contained .NET single-file executables occasionally trigger heuristic
antivirus detections (false positives). See `docs/FALSE_POSITIVES.md` for
details and how to verify the SHA256.

If SmartScreen shows a warning: More info -> Run anyway.

## Textures dark / broken in editors

Some `.dds` files are DX10 (BC6H/BC7), not supported by Paint.NET/GIMP.
Normalize with texconv:

~~~text
texconv -f BC3_UNORM -ft dds texture.dds
~~~

## Mod loads but the texture looks wrong

Check the format of the original asset. If it was `.atx1` (raw BC1 mip 0),
the replacement must also be `.atx1` (mip 0 only, no DDS header). If it was
`.ddsc`/`.avtx`, it must be a monolithic AVTX (header + all mip levels).

As of v2.0.5, the toolkit does this automatically via the Repacker wizard.
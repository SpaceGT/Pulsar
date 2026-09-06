# Linux setup

`pulsar-linux.py` is a single executable Python 3.10+ script. It uses Python's
standard library, including curses for a terminal UI with muted slate colors.
There is no pip installation, UI toolkit download, or sudo step. Linux x64 is
supported; Steam, Space Engineers, GPU drivers, and the .NET 10 runtime are
prerequisites for running the game. Setup reports a missing runtime without
installing system packages.

Download the script from this repository or use the copy included in new Linux
release packages. From a terminal:

```sh
curl -fLO https://raw.githubusercontent.com/SpaceGT/Pulsar/main/Scripts/pulsar-linux.py
python3 pulsar-linux.py
```

Use the arrow keys and Enter to select an action. Location changes the target
folder; Release selects `latest` or a tag such as `v2.4.0`. Migration also asks
for the old binary and settings folders. The UI confirms the operation before
changing files. Run without sudo, as the user who runs Steam.

## Install, update, and uninstall

The default location is `$XDG_DATA_HOME/Pulsar`, normally
`~/.local/share/Pulsar`. A script run from an installed package defaults to that
installation instead. `PULSAR_DATA_DIR` or `--target` overrides it. Updates
keep the chosen path, profiles, custom plugins, and other user files. Existing
portable unified Pulsar installations can also be updated. Older unified
packages published by linux-compat (for example 2.3.3) use Update.

The script downloads the Linux x64 archive from **SpaceGT/Pulsar**, verifies
GitHub's SHA-256 digest, rejects unsafe archive entries, and prepares a complete
staging directory before switching installations. A failure during the switch
restores the previous program, receipt, and desktop entry. Setup refuses to
replace a running installation or an unrelated non-empty directory.

The previous directory is retained beside the target as
`.Pulsar-backup-TIMESTAMP-SUFFIX` (using your chosen folder name). This includes
program files and user data. Receipts and shortcut backups are stored under
`$XDG_STATE_HOME/pulsar-installer`, normally `~/.local/state/pulsar-installer`.
Keep enough disk space for the current installation, staging copy, and backups.
Remove backups yourself after verifying the new installation to reclaim space.

Uninstall removes the package-owned launcher files and `Libraries`, plus this
installation's menu shortcut. It **keeps** profiles, local plugins, other user
files, and the backup. Space Engineers files and saves are never removed.
After uninstalling, remove the Pulsar executable and `%command%` from Steam's
launch options, keeping any game arguments, so Steam can launch the game directly.

## Steam launch options

Setup displays the exact launch option to paste into Space Engineers' Steam
properties, for example:

```text
/home/yourname/.local/share/Pulsar/Interim.bin %command%
```

Keep extra arguments, such as `-nosplash -noprompt`, after `%command%`. Paths
containing spaces are quoted. The script does not rewrite Steam's configuration
while Steam is running. The installed menu shortcut launches app 244850 through
Steam and therefore requires this launch option to be configured first.

Setup does not add a launch wrapper, set overlay variables, or force X11/Wayland.
The normal unified Pulsar and Steam launch paths remain responsible for those.

## Migrate LinuxCompat 1.0.x

For the native 1.0.16 release, the old defaults are:

- Binaries: `~/.local/share/Pulsar` (`Interim` shell wrapper and `Bin/Interim`).
- Settings: `~/.config/Pulsar`; `~/.local/config/Pulsar` is also detected.
  `XDG_CONFIG_HOME`, `PULSAR_DIR`, or `--settings` can override this.

Migrate replaces the old binary layout and copies `config.xml`, `Profiles`,
`Local`, and source configuration into the new installation's `Legacy` folder.
It does not overwrite an existing destination profile/configuration. Old
PluginHub, preloader, compiler, and native-library caches are not migrated;
Pulsar downloads current metadata and rebuilds plugins on first launch.

Migration converts legacy core-plugin IDs to the unified equivalents. It also
moves developer-folder `DataFile` declarations from profiles to the source's
`File` field and reads plugin IDs from their manifests. Incompatible old core
developer sources are disabled so current core plugins can load. Developer
sources without a recorded manifest are reported for review.

The original settings remain available for rollback. When the destination is
different, old program files remain at the source too; remove them after
checking the migrated installation. The old release's owned menu icons are
backed up and removed when its shortcut is replaced. Flatpak application and
Steam compatibility-tool migration are not supported by this native installer.

After migration, replace the old `Interim` wrapper path in Steam launch options
with the displayed `Interim.bin` path. No old environment-setting wrapper is
retained.

## Scripted and offline use

An explicit action with `--yes` uses the same implementation without curses:

```sh
python3 pulsar-linux.py install --target "$HOME/Games/Pulsar" --yes
python3 pulsar-linux.py update --target "$HOME/Games/Pulsar" --version v2.4.0 --yes
python3 pulsar-linux.py migrate --target "$HOME/.local/share/Pulsar" \
    --settings "$HOME/.config/Pulsar" --yes
python3 pulsar-linux.py uninstall --target "$HOME/Games/Pulsar" --yes
```

`--archive /path/to/pulsar-v2.4.0-linux-x64.tar.gz` uses a local release archive.
Supply `--sha256 HEX_DIGEST` to verify it; otherwise a local archive is treated
as a file you already trust. The installer never downloads or executes the old
installer during migration.

## Validation

```sh
python3 -m unittest discover -s Tests/LinuxInstaller -v
```

The optional integration test runs the **actual** native 1.0.16 install script,
migrates its output using a real unified release archive, checks the migrated
profiles/developer sources, updates, uninstalls, and runs the old cleanup script.
Every home, XDG, and legacy install location is redirected into a temporary
directory which is removed at the end, including settings, icons, and backups.
It does not launch the old game.

```sh
PULSAR_TEST_LEGACY_BUNDLE=/path/to/extracted/PulsarForLinux-Native \
PULSAR_TEST_RELEASE_ARCHIVE=/path/to/pulsar-v2.4.0-linux-x64.tar.gz \
    python3 -m unittest discover -s Tests/LinuxInstaller -v
```

The reference bundle is the `PulsarForLinux-Native.*.7z` asset of
[linux-compat release 1.0.16](https://github.com/CometWorks/linux-compat/releases/tag/1.0.16).

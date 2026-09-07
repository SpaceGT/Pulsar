<!-- Link References -->
[plugin-loader]: https://github.com/sepluginloader/PluginLoader
[plugin-hub]: https://github.com/StarCpt/PluginHub

[pulsar-latest]: https://github.com/SpaceGT/Pulsar/releases/latest
[pulsar-installer]: https://github.com/StarCpt/Pulsar-Installer
[linux-setup]: https://github.com/CometWorks/config-tools
[linux-setup-guide]: https://github.com/CometWorks/config-tools/blob/main/Docs/PulsarConfig.md
[config-releases]: https://github.com/CometWorks/config-tools/releases
[tool-updates]: https://github.com/CometWorks/config-tools#updating-the-tools
[tool-themes]: https://github.com/CometWorks/config-tools#appearance
[discord]: https://discord.gg/z8ZczP2YZY

[se1]: https://steampowered.com/app/244850
[se2]: https://steampowered.com/app/1133870

[net-framework]: https://dotnet.microsoft.com/en-us/download/dotnet-framework
[net-10]: https://dotnet.microsoft.com/en-us/download/dotnet/10.0

[dotnet-compat]: https://github.com/CometWorks/dotnet-compat
[linux-compat]: https://github.com/CometWorks/linux-compat
[linux-compat2]: https://github.com/CometWorks/linux-compat2

[steam-launch]: https://help.steampowered.com/en/faqs/view/7D01-D2DD-D75E-2955
[msbuild-issue]: https://github.com/dotnet/msbuild/issues/5976

<!-- Main Content -->
# Pulsar

Pulsar is a plugin and mod loader for **Space Engineers 1 and 2** that also lets
you **run both games natively on Linux—without Wine or Proton**. It supports
Windows too, with a choice of game runtimes and community plugins.

Native Linux support uses .NET 10 and the
[SE1][linux-compat] / [SE2][linux-compat2] compatibility plugins. Native Linux is
part of Pulsar's unified releases—not a separate legacy LinuxCompat installation.
Individual plugins may still have platform or runtime restrictions.

**Get started:** [Windows](#windows) · [Native Linux](#native-linux) · [Latest release][pulsar-latest]

Pulsar is a hard fork of the discontinued [PluginLoader][plugin-loader].

## Installation

### Windows

Use the [Windows installer][pulsar-installer] for guided setup, including Steam
configuration. For a portable installation, download the Windows package from
the [latest release][pulsar-latest], extract it into a dedicated Pulsar folder,
and choose the [executable for your game](#executables).

### Native Linux

**For Space Engineers 1 or 2, use [PulsarConfig][linux-setup].** This standalone
terminal tool configures plugins, sources, dev folders and profiles, starts the
game through Steam, and installs, updates, uninstalls or migrates native Pulsar.

1. Install Steam and your chosen Space Engineers game, the [.NET 10 runtime][net-10],
   and working graphics drivers. PulsarConfig supports **Linux x64** and bundles
   its own .NET runtime and Terminal.Gui: **no Python, curses, separate .NET SDK,
   or NuGet installation is required to run the tool**. Standard `bash`/`stty`
   utilities are used by its terminal driver. The tool's private runtime does
   not satisfy the separate runtime requirement for Pulsar and the game.
2. Download **`PulsarConfig-linux-x64.bin`** from the current
   **`pulsarconfig-v*`** release on [config-tools releases][config-releases].
   That repository also releases MagnetarConfig; select PulsarConfig explicitly
   rather than using its repository-wide `releases/latest/download` URL.
   `SHA256SUMS.txt` and license notices accompany each release. Keep the tool
   outside Pulsar's deployment directory, then run it as your normal Steam user,
   **without sudo**:

   ```sh
   chmod +x PulsarConfig-linux-x64.bin
   ./PulsarConfig-linux-x64.bin --target "$HOME/Games/Pulsar"
   ```

3. Open **Setup / update** on Home (or **File → Setup / update / migrate**).
   Set **Installation**, choose **Game: SE1** (`Interim.bin`) or **SE2** (`Modern.bin`),
   and use **Check prerequisites** to review the host. Checks also run during
   install/update/migration; they report missing runtimes and libraries but do
   not install system packages. Choose **Install** and confirm the destination
   and release. The Linux package comes from **SpaceGT/Pulsar releases** and
   contains both launchers; the game choice controls the Steam shortcut and
   displayed launch command. Updates remember it. For an old native LinuxCompat
   1.0.x installation, choose **Migrate** instead; see [migration](#migration-and-uninstall).
4. In Steam, open **your chosen game → Properties → General → Launch Options**
   and paste the exact command printed by the tool. For SE1:

   ```text
   /path/to/your/Interim.bin %command%
   ```

   For SE2:

   ```text
   /path/to/your/Modern.bin %command%
   ```

   Replace `/path/to/your/` with the Pulsar installation folder you chose.
   Keep `%command%` exactly as written. If the path contains spaces, put the
   entire executable path in double quotes.

5. Start the selected game from Steam to launch through Pulsar. The displayed
   command launches the native runtime; it does not run the game through Proton.

PulsarConfig is maintained and released in **[CometWorks/config-tools][linux-setup]**;
it is downloaded separately from Pulsar. The old `pulsar-linux.py` URL remains a
compatibility downloader; new installations should use the release executable.
See the [PulsarConfig guide][linux-setup-guide] for custom locations, backups,
offline packages and CLI options. Native setup supports Wayland and X11 and
does not alter display-backend or overlay settings.

**Manual install:** download and extract the Linux x64
package from the [latest release][pulsar-latest] into a dedicated folder. Use
`Interim.bin` for SE1 or `Modern.bin` for SE2, with the [.NET 10 runtime][net-10]
installed. Configure that game's [Steam launch options](#steam) with the chosen
executable. Both games can use the same Pulsar installation.

> Use a dedicated Pulsar folder, separate from your game files and other data.
> Pulsar's updater cleans its deployment directory; do not store unrelated
> important files there.

## PulsarConfig on Linux

Open an existing installation with:

```sh
./PulsarConfig-linux-x64.bin --target "/path/to/Pulsar"
```

The default target is `$XDG_DATA_HOME/Pulsar` (normally `~/.local/share/Pulsar`),
or the adjacent installation when the tool is placed inside one. `--target`
selects it explicitly. Use **Open installation** to change the folder, SE1/SE2
selection or configuration override; `--game se1` / `--game se2` are the CLI equivalents.
The default configuration directory is `Legacy` for SE1 or `Modern` for SE2.

- **F1 Home** shows the installation and exact Steam launch options; **F5 Start game**
  launches the selected game through Steam using its configured launch options.
- **F3 Plugins** filters and toggles plugins in the active profile.
- **F4 Profiles** saves, loads, updates, renames or deletes named plugin sets.
- **F6 Dev folders** registers plugin manifests and edits their active-profile
  and Debug/Release settings. **F7 Sources** manages hubs, plugin repositories,
  local hubs and Workshop sources.
- **F2 Theme** selects Sandstone (default), Graphite, Sage, Plum or Turbo C.
  The preference is [stored for your user on this machine][tool-themes], shared
  with MagnetarConfig and separate from game profiles.

Use Tab/Shift+Tab between controls, arrows in lists, Enter to activate, and F10
to quit. Mouse hover highlights buttons and rows without changing keyboard
focus or the selected item; keyboard input restores keyboard highlighting.
Close Pulsar and the game before editing configuration. Changes are written
atomically with `.bak` backups and take effect on the next launch. Remote
catalogs are read from Pulsar's caches; launch Pulsar to refresh them after
adding sources. Plugin trust and runtime/platform compatibility still apply.

### Updates

PulsarConfig and Pulsar have **separate releases and update actions**:

- **Update the tool:** each interactive launch checks in the background and
  offers **Later** or **Update…** when a newer PulsarConfig is available. Select
  **Update… → Update and close**, then reopen the executable. Nothing is installed
  without that choice; offline checks do not block startup. **Tools → Tool updates**
  checks on demand. `--check-update`, `--self-update` and `--tool-version` work
  without opening the TUI. Updates verify the download and retain the old
  executable as `.previous`; see [update and recovery details][tool-updates].
- **Update Pulsar:** use **Setup / update → Update**, or
  `./PulsarConfig-linux-x64.bin update --target "/path/to/Pulsar" --yes`.
  This downloads the unified Linux package from SpaceGT/Pulsar and preserves
  the selected path/game, profiles and local plugins. A backup of the previous
  installation is retained beside the target. Stop the game before updating.

### Migration and uninstall

**Migrate** converts native **SE1 LinuxCompat 1.0.x** installations, including
1.0.16. Set **Old install** to its binary folder (usually `~/.local/share/Pulsar`)
and **Old settings** to its configuration (usually `~/.config/Pulsar`;
`~/.local/config/Pulsar` is also detected). The tool copies supported settings,
profiles, sources and local plugins into the new `Legacy` configuration and
retains the originals for rollback. Caches are rebuilt on first launch; review
any reported dev-folder entries. This does not convert SE1 profiles into SE2
profiles or migrate Flatpak/Steam compatibility-tool installations.

After migration, replace the old `Interim` wrapper in Steam with the displayed
`Interim.bin %command%` command, keeping extra arguments after `%command%`.
Verify the new installation before removing old folders or backups. Existing
unified installations from linux-compat releases, such as 2.3.3, use **Update**,
not Migrate. The [guide][linux-setup-guide] covers alternate source/settings paths.

**Uninstall** removes package-owned program files and the installation's menu
shortcut while retaining profiles, local plugins, other user files and backups.
It removes **both launchers** from a shared SE1/SE2 installation. Clear the Pulsar
executable and `%command%` from each affected game's Steam launch options,
keeping any game arguments. Game files and saves are not removed. The separate
PulsarConfig executable can be deleted independently when no longer needed.

## Executables

| Executable | Game | Runtime | Platforms |
| --- | --- | --- | --- |
| `Legacy` | [Space Engineers 1][se1] | [.NET Framework][net-framework] | Windows |
| `Interim` | [Space Engineers 1][se1] | [.NET 10][net-10], via [dotnet-compat][dotnet-compat] | Windows, native Linux |
| `Modern` | [Space Engineers 2][se2] | [.NET 10][net-10] | Windows, native Linux |

Linux executables use the `.bin` extension; Windows executables use `.exe`.

## Usage

Run the appropriate executable for your game and runtime.<br>
You can pass `-h` for a list of command line arguments.<br>

## Steam

Set the game's [launch options][steam-launch] so Steam starts Pulsar automatically.
For **native Linux / Space Engineers 1**:

```text
/path/to/your/Interim.bin %command%
```

Use `Modern.bin` instead for Space Engineers 2. On **Windows**, use the chosen
`.exe`, for example:

```text
"C:\path\to\your\Interim.exe" %command%
```

Replace the example folder with your actual Pulsar installation folder, keeping
the executable filename at the end. Keep `%command%` exactly as written—it is
a Steam placeholder, not something to replace yourself. Quote the full
executable path if it contains spaces. Optional Pulsar arguments go after
`%command%`, for example:

```text
"/path/to/your/Pulsar Folder/Interim.bin" %command% -nosplash
```

<details>
<summary>Optional: running the Windows build through Proton instead</summary>

This is separate from native Linux support and is **not required** for the Linux build:

```text
bash -c 'exec "${@:0:$#}" "/path/to/your/Interim.exe" "${@:$#}"' %command%
```

</details>

## Plugins

Pulsar officially endorses the [PluginHub][plugin-hub] for high-quality vetted plugins.<br>
Further sources may be added in-game but make sure you fully understand the risks.<br>

## Development

Fill in the required paths in `Directory.Build.props`.<br>
You **must** use `dotnet build` due to a [Microsoft bug][msbuild-issue].<br>
The deployment targets copy the built files to their configured location.<br>

## Contact

We have an active [Discord][discord] for updates and developer information.<br>
We prefer Discord over GitHub for support-related queries.<br>
Pull requests are welcome but ask **before** committing to one.<br>

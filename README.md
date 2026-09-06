<!-- Link References -->
[plugin-loader]: https://github.com/sepluginloader/PluginLoader
[plugin-hub]: https://github.com/StarCpt/PluginHub

[pulsar-latest]: https://github.com/SpaceGT/Pulsar/releases/latest
[pulsar-installer]: https://github.com/StarCpt/Pulsar-Installer
[linux-setup]: https://github.com/CometWorks/config-tools#pulsar-linux-setup
[linux-setup-guide]: https://github.com/CometWorks/config-tools/blob/main/Scripts/README.md
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

**For Space Engineers 1 or 2, use the [standalone Linux setup tool][linux-setup].**
It provides a terminal UI for installing, updating, uninstalling, and migrating
an older native LinuxCompat installation.

1. Install Steam and your chosen Space Engineers game, the [.NET 10 runtime][net-10], and working
   graphics drivers. The setup tool also requires **Linux x64, Python 3.10+,
   `curses`, and `curl`** for the download command below. It does not install
   system prerequisites.
2. Download and run the tool in a terminal as your normal Steam user, **without sudo**:

   ```sh
   curl -fLO https://raw.githubusercontent.com/CometWorks/config-tools/main/Scripts/pulsar-linux.py
   python3 pulsar-linux.py
   ```

3. Use **Game** to select SE1 (`Interim.bin`) or SE2 (`Modern.bin`), then choose
   **Install**, confirm the destination and release, and complete setup.
   The shared package installs both launchers; this selection chooses the Steam
   shortcut and displayed launch command. Updates remember your choice.
   If replacing a native LinuxCompat 1.0.x installation, choose **Migrate** instead.
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

The tool is maintained in **[CometWorks/config-tools][linux-setup]**, not bundled
with Pulsar. Keep the downloaded script outside the Pulsar installation folder.
See the [Linux setup guide][linux-setup-guide] for backups, custom locations,
offline installs, updates, and migration details.

**Manual install:** download and extract the Linux x64
package from the [latest release][pulsar-latest] into a dedicated folder. Use
`Interim.bin` for SE1 or `Modern.bin` for SE2, with the [.NET 10 runtime][net-10]
installed. Configure that game's [Steam launch options](#steam) with the chosen
executable. Both games can use the same Pulsar installation.

> Use a dedicated Pulsar folder, separate from your game files and other data.
> Pulsar's updater cleans its deployment directory; do not store unrelated
> important files there.

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

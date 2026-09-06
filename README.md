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

A plugin and mod loader for **Space Engineers 1 and 2**, with **Windows and native Linux support**.

On Linux, Pulsar runs the games **without Wine or Proton**, using .NET 10 and the
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

**For Space Engineers 1, use the [standalone Linux setup tool][linux-setup].**
It provides a terminal UI for installing, updating, uninstalling, and migrating
an older native LinuxCompat installation.

1. Install Steam and Space Engineers, the [.NET 10 runtime][net-10], and working
   graphics drivers. The setup tool also requires **Linux x64, Python 3.10+,
   `curses`, and `curl`** for the download command below. It does not install
   system prerequisites.
2. Download and run the tool in a terminal as your normal Steam user, **without sudo**:

   ```sh
   curl -fLO https://raw.githubusercontent.com/CometWorks/config-tools/main/Scripts/pulsar-linux.py
   python3 pulsar-linux.py
   ```

3. Choose **Install**, confirm the destination and release, and complete setup.
   If replacing a native LinuxCompat 1.0.x installation, choose **Migrate** instead.
4. In Steam, open **Space Engineers → Properties → General → Launch Options**
   and paste the exact command printed by the tool. For example:

   ```text
   /home/yourname/.local/share/Pulsar/Interim.bin %command%
   ```

5. Start Space Engineers from Steam to launch through Pulsar. The displayed
   command launches the native runtime; it does not run the game through Proton.

The tool is maintained in **[CometWorks/config-tools][linux-setup]**, not bundled
with Pulsar. Keep the downloaded script outside the Pulsar installation folder.
See the [Linux setup guide][linux-setup-guide] for backups, custom locations,
offline installs, updates, and migration details.

**Manual install / Space Engineers 2:** download and extract the Linux x64
package from the [latest release][pulsar-latest] into a dedicated folder. Use
`Interim.bin` for SE1 or `Modern.bin` for SE2, with the [.NET 10 runtime][net-10]
installed. Configure that game's [Steam launch options](#steam) with the chosen
executable. The setup tool above currently targets SE1.

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
For **Windows or native Linux**:

```text
"[PulsarPath]" %command% [Args]
```

Replace `[PulsarPath]` with the full path to the executable, not its folder.
Replace `[Args]` with optional Pulsar arguments, or remove it. Keep `%command%`.
For example, native SE1 with a custom installation path:

```text
"/home/yourname/Games/Pulsar/Interim.bin" %command% -nosplash
```

<details>
<summary>Optional: running the Windows build through Proton instead</summary>

This is separate from native Linux support and is **not required** for the Linux build:

```text
bash -c 'exec "${@:0:$#}" [PulsarPath] "${@:$#}" [Args]' %command%
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

<!-- Link References -->
[plugin-loader]: https://github.com/sepluginloader/PluginLoader
[plugin-hub]: https://github.com/StarCpt/PluginHub

[pulsar-latest]: https://github.com/SpaceGT/Pulsar/releases/latest
[pulsar-installer]: https://github.com/StarCpt/Pulsar-Installer
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
A plugin and mod loader for Space Engineers.<br>
This is a hard fork of the discontinued [PluginLoader][plugin-loader].<br>

## Installation
Pulsar is portable: simply download the [latest release][pulsar-latest] into a folder of choice.<br>
This folder **must not** contain important data; It **will be cleaned** during a Pulsar update!<br>
If you are building from source, the deployment targets will copy all files to their required location.<br>
A windows-only [installer][pulsar-installer] exists which can do all the work (including Steam configuration) for you.<br>

## Executables
`Legacy` runs [Space Engineers 1][se1] on [.NET Framework][net-framework]<br>
`Interim` runs [Space Engineers 1][se1] on [.NET 10][net-10] (via [dotnet-compat][dotnet-compat])<br>
`Modern` runs [Space Engineers 2][se2] on [.NET 10][net-10]<br>

## Usage
Run the appropriate executable for your game and runtime.<br>
You can pass `-h` for a list of command line arguments.<br>
Linux builds run Space Engineers natively **(without Wine)** using [linux-compat][linux-compat] and [linux-compat2][linux-compat2].<br>

## Steam
The Space Engineers [launch options][steam-launch] may be modified so Steam starts Pulsar automatically.<br>
Replace `[PulsarPath]` with a path to the desired Pulsar executable.<br>
Replace `[Args]` with the desired Pulsar arguments (leave blank for default).<br>
For Windows and Linux: `[PulsarPath] %command% [Args]`<br>
For Proton: `bash -c 'exec "${@:0:$#}" [PulsarPath] "${@:$#}" [Args]' %command%`<br>
Starting Space Engineers from Steam will now open Pulsar as well!<br>

## Plugins
Pulsar officially endorses the [PluginHub][plugin-hub] for high-quality vetted plugins.<br>
Further sources may be added in-game but make sure you fully understand the risks.<br>

## Development
Fill in the required paths in `Directory.Build.props`.<br>
You **must** use `dotnet build` due to a [Microsoft bug][msbuild-issue].<br>

## Contact
We have an active [Discord][discord] for updates and developer information.<br>
We prefer Discord over GitHub for support-related queries.<br>
Pull requests are welcome but ask **before** committing to one.<br>

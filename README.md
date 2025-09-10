# Pulsar
A plugin and mod loader for Space Engineers.<br>
This is a hard fork of the discontinued [PluginLoader](https://github.com/sepluginloader/PluginLoader).<br>

## Installation
Pulsar is portable: simply download the [latest release](https://github.com/SpaceGT/Pulsar/releases/latest) into a folder of choice.<br>
This folder **must not** contain important data; It **will be cleaned** during a Pulsar update! <br>
If you are building from source, the deploy script will copy all files to their required location.<br>
An windows-only [installer](https://github.com/StarCpt/Pulsar-Installer) exists which can do all the work (including Steam configuration) for you.<br>

## Steam
The Space Engineers [launch options](https://help.steampowered.com/en/faqs/view/7D01-D2DD-D75E-2955) may be modified so Steam starts Pulsar automatically.<br>
Use a path to **Legacy.exe** for [Space Engineers 1](https://steampowered.com/app/244850) or **Modern.exe** for [Space Engineers 2](https://steampowered.com/app/1133870).<br>
For Windows: `[PulsarPath] %command% [Args]`<br>
For Linux: `bash -c 'exec "${@:0:$#}" [PulsarPath] "${@:$#}" [Args]' %command%`<br>
Starting Space Engineers from Steam will now open Pulsar as well!<br>

## Plugins
Pulsar officially endorses the [PluginHub](https://github.com/StarCpt/PluginHub) for high-quality vetted plugins.<br>
Further sources may be added in-game but make sure you fully understand the risks.<br>

## Contact
We have an active [Discord](https://discord.gg/z8ZczP2YZY) for updates and developer information.<br>
GitHub contributions and bug reports are also welcomed!<br>
We prefer Discord over GitHub for support-related queries.<br>

#!/usr/bin/env python3
"""Pulsar Linux installer. Python 3.10+; no third-party Python packages."""

import argparse
import contextlib
import datetime
import fcntl
import hashlib
import json
import os
import platform
import re
import shlex
import shutil
import subprocess
import sys
import tarfile
import tempfile
import textwrap
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path, PurePosixPath

REPO = "SpaceGT/Pulsar"
CORE_IDS = {"se-linux-compat": "linux-compat", "se-dotnet-compat": "dotnet-compat"}
PROGRAM_FILES = {"Libraries", "LICENSE", "README.md", "pulsar-linux.py"} | {
    name + suffix
    for name in ("Interim", "Modern")
    for suffix in (".bin", ".dll", ".deps.json", ".runtimeconfig.json")
}
REQUIRED = (
    "Interim.bin",
    "Interim.dll",
    "Interim.runtimeconfig.json",
    "Libraries/Interim/Pulsar.Shared.dll",
)


class SetupError(Exception):
    pass


def xdg(variable, fallback):
    value = os.environ.get(variable)
    return Path(value).expanduser() if value else Path.home() / fallback


def data_home():
    return xdg("XDG_DATA_HOME", ".local/share")


def old_config():
    if os.environ.get("PULSAR_DIR"):
        return Path(os.environ["PULSAR_DIR"]).expanduser()
    candidates = [
        xdg("XDG_CONFIG_HOME", ".config") / "Pulsar",
        Path.home() / ".local/config/Pulsar",
    ]
    return next((p for p in candidates if p.is_dir()), candidates[0])


def install_path(value):
    if not str(value).strip() or any(ord(ch) < 32 for ch in str(value)):
        raise SetupError(
            "The installation path must be non-empty and contain no control characters."
        )
    path = Path(value).expanduser().absolute()
    if path.is_symlink():
        raise SetupError("Choose the real installation directory, not a symbolic link.")
    path = path.resolve()
    protected = {
        Path("/"),
        Path.home().resolve(),
        data_home().resolve(),
        xdg("XDG_CONFIG_HOME", ".config").resolve(),
        Path("/tmp"),
    }
    if path in protected or path == old_config().resolve():
        raise SetupError(
            "Choose a dedicated Pulsar folder, not a home/configuration root."
        )
    return path


def legacy(path):
    return (path / "Interim").is_file() and (path / "Bin/Interim").is_file()


def modern(path):
    return all((path / name).is_file() for name in REQUIRED)


def remove(path):
    if path.is_symlink() or path.is_file():
        path.unlink()
    elif path.is_dir():
        shutil.rmtree(path)


def atomic_write(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, name = tempfile.mkstemp(prefix=".pulsar-", dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as stream:
            stream.write(content)
        os.replace(name, path)
    finally:
        Path(name).unlink(missing_ok=True)


def require_stopped(*roots):
    """Check paths, not process names: another Pulsar installation may be running."""
    roots = [str(p.resolve()) + "/" for p in roots]
    for proc in Path("/proc").glob("[0-9]*"):
        if proc.name == str(os.getpid()):
            continue  # The installer itself may be running from the package folder.
        try:
            args = (proc / "cmdline").read_bytes().split(b"\0")
            executable = str((proc / "exe").resolve())
            # dotnet-hosted launchers have their assembly in argv[1].
            candidates = [executable] + [os.fsdecode(a) for a in args[:2]]
            if any(c.startswith(root) for c in candidates for root in roots):
                raise SetupError(
                    f"Close Pulsar and its game before continuing (PID {proc.name})."
                )
        except (PermissionError, FileNotFoundError, ProcessLookupError):
            continue


def request(url):
    return urllib.request.urlopen(
        urllib.request.Request(url, headers={"User-Agent": "Pulsar-Linux-Setup"}),
        timeout=60,
    )


def release_archive(version, destination, report):
    endpoint = "latest" if version == "latest" else "tags/" + version
    if not re.fullmatch(r"latest|v?\d+\.\d+\.\d+(?:[-.][A-Za-z0-9]+)*", version):
        raise SetupError("Use 'latest' or a release tag such as v2.4.0.")
    report(f"Checking {REPO} releases…")
    with request(
        f"https://api.github.com/repos/{REPO}/releases/{endpoint}"
    ) as response:
        release = json.load(response)
    assets = [
        a
        for a in release.get("assets", [])
        if re.fullmatch(r"pulsar-.*-linux-x64\.tar\.gz", a["name"], re.I)
    ]
    if len(assets) != 1:
        raise SetupError(
            "This release has no unique Linux x64 package. Try another release tag."
        )
    asset = assets[0]
    digest = asset.get("digest") or ""
    if not digest.startswith("sha256:"):
        raise SetupError(
            "GitHub did not provide a SHA-256 digest for this release asset."
        )
    url = asset["browser_download_url"]
    if not url.startswith(f"https://github.com/{REPO}/releases/download/"):
        raise SetupError("Unexpected release download URL.")
    with request(url) as response, destination.open("wb") as output:
        total = 0
        while chunk := response.read(1024 * 1024):
            output.write(chunk)
            total += len(chunk)
            report(f"Downloading {release['tag_name']} · {total // 1048576} MiB")
    return release["tag_name"], digest.removeprefix("sha256:")


def unpack(archive, destination, expected_digest):
    digest = hashlib.sha256()
    with archive.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    if expected_digest and digest.hexdigest().lower() != expected_digest.lower():
        raise SetupError("Package checksum mismatch. The installation was not changed.")
    with tarfile.open(archive, "r:gz") as bundle:
        members = bundle.getmembers()
        if sum(m.size for m in members) > 2 * 1024**3:
            raise SetupError("Package exceeds the 2 GiB extraction limit.")
        names = set()
        for member in members:
            path = PurePosixPath(member.name)
            if str(path) == "." and member.isdir():
                continue
            if (
                path.is_absolute()
                or ".." in path.parts
                or not path.parts
                or path.parts[0] not in PROGRAM_FILES
                or not (member.isfile() or member.isdir())
            ):
                raise SetupError(f"Unsafe or unexpected archive entry: {member.name!r}")
            if str(path) in names:
                raise SetupError(f"Duplicate archive entry: {member.name!r}")
            names.add(str(path))
        for member in members:
            path = destination / member.name
            if member.isdir():
                path.mkdir(parents=True, exist_ok=True)
            else:
                path.parent.mkdir(parents=True, exist_ok=True)
                with bundle.extractfile(member) as source, path.open("xb") as output:
                    shutil.copyfileobj(source, output)
                path.chmod(0o755 if member.mode & 0o111 else 0o644)
    if not modern(destination):
        raise SetupError(
            "Not a unified Pulsar Linux package: required launcher files are missing."
        )
    for binary in destination.rglob("*.bin"):
        binary.chmod(0o755)
    script = destination / "pulsar-linux.py"
    if script.exists():
        script.chmod(0o755)
    return digest.hexdigest()


def copy_settings(source, destination):
    """Copy settings, never old compiler/preloader/native-library caches."""
    if not source.is_dir():
        raise SetupError(f"Legacy settings directory does not exist: {source}")
    if destination.is_symlink():
        raise SetupError(
            "The destination settings directory must not be a symbolic link."
        )
    destination.mkdir(parents=True, exist_ok=True)
    for name in ("config.xml", "Sources", "Profiles", "Local"):
        src, dst = source / name, destination / name
        if not src.exists():
            continue
        if dst.exists():
            raise SetupError(
                f"Migration would overwrite existing settings: {dst.name}. Use an empty destination."
            )
        if src.is_dir():
            ignore = (
                shutil.ignore_patterns("Hubs", "Plugins", "*.bin")
                if name == "Sources"
                else None
            )
            # Config trees must be independent: migration rewrites XML below them.
            shutil.copytree(src, dst, symlinks=(name == "Local"), ignore=ignore)
        else:
            shutil.copy2(src, dst)


def migrate_settings(root, report):
    sources_file = root / "Sources/sources.xml"
    profiles = [(p, ET.parse(p)) for p in (root / "Profiles").glob("*.xml")]
    manifests = {}
    for _, profile in profiles:
        for item in profile.findall(".//LocalFolderConfig"):
            if item.findtext("DataFile"):
                manifests[item.findtext("Id")] = item.findtext("DataFile")
    remap = dict(CORE_IDS)
    sources = ET.parse(sources_file) if sources_file.exists() else None
    if sources is not None:
        for hub in sources.findall(".//RemoteHub"):
            for name in ("LastCheck", "Hash"):
                if hub.find(name) is not None:
                    hub.remove(hub.find(name))
        for item in sources.findall(".//LocalPlugin"):
            folder = Path(item.findtext("Folder") or "").expanduser()
            old_id = folder.name
            manifest = (
                item.findtext("File")
                or manifests.get(old_id)
                or manifests.get(item.findtext("Name"))
            )
            new_id = None
            if manifest and item.find("File") is None:
                ET.SubElement(item, "File").text = manifest
            if manifest and (folder / manifest).is_file():
                new_id = ET.parse(folder / manifest).findtext("Id")
                if new_id:
                    remap[old_id] = new_id
            # Old core dev sources cannot satisfy unified Pulsar's new core IDs.
            if old_id in CORE_IDS and new_id != CORE_IDS[old_id]:
                remap[old_id] = CORE_IDS[old_id]
                enabled = item.find("Enabled")
                if enabled is None:
                    enabled = ET.SubElement(item, "Enabled")
                enabled.text = "false"
                report(f"Using the current released core plugin: {CORE_IDS[old_id]}")
            elif not new_id:
                report(
                    f"Check developer source '{old_id}': its plugin XML manifest is missing or has no ID."
                )
        atomic_write(
            sources_file,
            ET.tostring(sources.getroot(), encoding="utf-8", xml_declaration=True),
        )
    for path, profile in profiles:
        for item in profile.findall(".//LocalFolderConfig"):
            field = item.find("Id")
            old_id = field.text if field is not None else None
            if item.find("DataFile") is not None:
                item.remove(item.find("DataFile"))
            if old_id in CORE_IDS:
                profile.find("DevFolder").remove(item)
                # Unified Pulsar force-loads these core plugins independently of the profile.
            elif old_id in remap:
                field.text = remap[old_id]
        for field in profile.findall(".//GitHubPluginConfig/Id"):
            field.text = remap.get(field.text, field.text)
        atomic_write(
            path, ET.tostring(profile.getroot(), encoding="utf-8", xml_declaration=True)
        )


class Installer:
    def __init__(self, target, report=print):
        self.target = install_path(target)
        self.report = report
        key = hashlib.sha256(os.fsencode(self.target)).hexdigest()[:20]
        self.state_dir = xdg("XDG_STATE_HOME", ".local/state") / "pulsar-installer"
        self.receipt = self.state_dir / (key + ".json")
        self.desktop = data_home() / "applications/pulsar.desktop"

    @contextlib.contextmanager
    def lock(self):
        self.state_dir.mkdir(parents=True, exist_ok=True)
        with (self.state_dir / "setup.lock").open("a") as lock:
            try:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise SetupError(
                    "Another Pulsar setup operation is already running."
                ) from None
            yield

    def launch_options(self):
        return shlex.quote(str(self.target / "Interim.bin")) + " %command%"

    def desktop_contents(self):
        # Use Steam's launch path so it supplies the game's overlay/input environment.
        return (
            "[Desktop Entry]\nType=Application\nName=Pulsar\n"
            "Comment=Space Engineers with Pulsar\nExec=steam -applaunch 244850\n"
            "Icon=applications-games\nTerminal=false\nCategories=Game;\n"
            f"X-Pulsar-Install-Path={self.target}\n"
        ).encode()

    def validate_target(self, action):
        require_stopped(self.target)
        exists = self.target.exists() and any(self.target.iterdir())
        known = modern(self.target) or legacy(self.target) or self.receipt.exists()
        if exists and not known:
            raise SetupError(
                "This non-empty folder is not a recognized Pulsar installation."
            )
        if action == "install" and (modern(self.target) or legacy(self.target)):
            raise SetupError(
                "Pulsar is already installed here. Choose Update or Migrate."
            )
        if action == "update" and not modern(self.target):
            raise SetupError(
                "Choose Migrate for an old LinuxCompat installation, or Install for a new folder."
            )
        if action == "uninstall" and not known:
            raise SetupError("No recognized Pulsar installation exists at this path.")

    def commit(self, stage, state, desktop, legacy_shortcut=False):
        """Switch only after staging succeeds; retain a complete pre-operation backup."""
        target = self.target
        backup = None
        old_state = self.receipt.read_bytes() if self.receipt.exists() else None
        old_desktop = self.desktop.read_bytes() if self.desktop.exists() else None
        try:
            if target.exists():
                stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
                backup = Path(
                    tempfile.mkdtemp(
                        prefix=f".{target.name}-backup-{stamp}-", dir=target.parent
                    )
                )
                backup.rmdir()
                target.rename(backup)
            stage.rename(target)
            atomic_write(self.receipt, json.dumps(state, indent=2).encode())
            if desktop is not None:
                atomic_write(self.desktop, desktop)
            elif old_desktop and (
                legacy_shortcut
                or f"X-Pulsar-Install-Path={target}\n".encode() in old_desktop
            ):
                self.desktop.unlink()
            if backup:
                record = self.state_dir / "backups" / backup.name
                record.mkdir(parents=True, exist_ok=True)
                if old_desktop:
                    (record / "pulsar.desktop").write_bytes(old_desktop)
                if old_state:
                    (record / "receipt.json").write_bytes(old_state)
        except BaseException:
            if backup is not None and backup.exists():
                remove(target)
                backup.rename(target)
            elif not stage.exists():
                remove(target)
            for path, content in (
                (self.receipt, old_state),
                (self.desktop, old_desktop),
            ):
                if content is None:
                    path.unlink(missing_ok=True)
                else:
                    atomic_write(path, content)
            raise
        if backup:
            self.report(f"Previous installation backed up at {backup}")

    def legacy_shortcut(self, source):
        if not legacy(source) or not self.desktop.is_file():
            return False
        command = "Exec=" + str(source / "Interim")
        return any(
            line == command or line.startswith(command + " ")
            for line in self.desktop.read_text().splitlines()
        )

    def clean_legacy_icons(self):
        # Only called when the original release's shortcut identifies this installation.
        root = data_home() / "icons/hicolor"
        for size in (16, 24, 32, 48, 64, 96, 128, 256):
            icon = root / f"{size}x{size}/apps/pulsar.png"
            if icon.is_file():
                try:
                    backup = self.state_dir / "legacy-icons" / str(size) / "pulsar.png"
                    backup.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(icon, backup)
                    icon.unlink()
                except OSError as error:
                    self.report(
                        f"Installation completed; could not clean old icon {icon}: {error}"
                    )

    def run(
        self,
        action,
        version="latest",
        archive=None,
        sha256=None,
        source=None,
        settings=None,
    ):
        with self.lock():
            self.validate_target(action)
            old = (
                install_path(source) if action == "migrate" and source else self.target
            )
            legacy_shortcut = self.legacy_shortcut(old)
            settings = (
                Path(settings).expanduser().resolve()
                if settings
                else old_config().resolve()
            )
            if action == "migrate":
                if not legacy(old):
                    raise SetupError(
                        "Legacy source must contain the 1.0.x Interim wrapper and Bin/Interim."
                    )
                require_stopped(old)
                if old != self.target and (
                    old in self.target.parents or self.target in old.parents
                ):
                    raise SetupError(
                        "Source and destination must not contain one another."
                    )
            self.target.parent.mkdir(parents=True, exist_ok=True)
            with tempfile.TemporaryDirectory(
                prefix=f".{self.target.name}-setup-", dir=self.target.parent
            ) as temp:
                work = Path(temp)
                package = work / "package"
                package.mkdir()
                if action != "uninstall":
                    if archive:
                        archive = Path(archive).expanduser().resolve()
                        if not archive.is_file():
                            raise SetupError(
                                "The selected package file does not exist."
                            )
                        version = archive.name if version == "latest" else version
                    else:
                        archive = work / "release.tar.gz"
                        version, sha256 = release_archive(version, archive, self.report)
                    self.report("Verifying and unpacking the Linux release…")
                    checksum = unpack(archive, package, sha256)
                stage = work / "install"
                self.report(
                    "Preparing installation; your current files remain in place…"
                )
                if self.target.exists():
                    shutil.copytree(self.target, stage, symlinks=True)
                else:
                    stage.mkdir()
                for name in PROGRAM_FILES:
                    remove(stage / name)
                if legacy(stage):
                    if action not in ("migrate", "uninstall"):
                        raise SetupError(
                            "This is a legacy installation. Choose Migrate."
                        )
                    remove(stage / "Bin")
                    remove(stage / "Interim")
                if action == "migrate":
                    copy_settings(settings, stage / "Legacy")
                    migrate_settings(stage / "Legacy", self.report)
                if action != "uninstall":
                    for entry in package.iterdir():
                        entry.rename(stage / entry.name)
                require_stopped(self.target, old)
                state = {
                    "target": str(self.target),
                    "installed": action != "uninstall",
                    "version": version if action != "uninstall" else None,
                }
                if action != "uninstall":
                    state["sha256"] = checksum
                self.commit(
                    stage,
                    state,
                    self.desktop_contents() if action != "uninstall" else None,
                    legacy_shortcut=legacy_shortcut,
                )
                if legacy_shortcut:
                    self.clean_legacy_icons()
                if action == "uninstall":
                    self.report(
                        f"Program files removed. Settings and other files remain in {self.target}"
                    )
                    self.report(
                        "Remove this Pulsar executable and %command% from Steam launch options; keep your game arguments."
                    )
                else:
                    self.report(f"Pulsar installed in {self.target}")
                    self.report("Set Space Engineers launch options in Steam to:")
                    self.report(self.launch_options())
                    self.report(
                        "Keep any extra game/Pulsar arguments after %command%. The menu shortcut starts Steam."
                    )
                if action == "migrate":
                    self.report(
                        f"Original settings retained at {settings}; old caches were not transferred."
                    )
                    if old != self.target:
                        self.report(
                            f"Old program files retained at {old}. Remove them after checking the new installation."
                        )


class Tui:
    """Quiet, terminal-native palette; no external UI binary or package manager."""

    def __init__(self, screen, options):
        import curses

        self.c = curses
        self.screen = screen
        self.options = options
        self.messages = []
        curses.curs_set(0)
        screen.keypad(True)
        self.accent = self.selected = self.muted = 0
        if curses.has_colors():
            curses.start_color()
            curses.use_default_colors()
            colors = (110, 252, 238, 245) if curses.COLORS >= 256 else (6, 7, 4, 7)
            curses.init_pair(1, colors[0], -1)
            curses.init_pair(2, colors[1], colors[2])
            curses.init_pair(3, colors[3], -1)
            self.accent, self.selected, self.muted = [
                curses.color_pair(n) for n in (1, 2, 3)
            ]

    def text(self, row, col, value, style=0):
        height, width = self.screen.getmaxyx()
        value = "".join(ch if ch.isprintable() else " " for ch in str(value))
        if 0 <= row < height - 1 and col < width - 1:
            with contextlib.suppress(self.c.error):
                self.screen.addnstr(row, col, value, max(0, width - col - 2), style)

    def frame(self, subtitle):
        self.screen.erase()
        self.text(1, 3, "P U L S A R", self.accent | self.c.A_BOLD)
        self.text(2, 3, subtitle, self.muted)
        self.text(4, 3, "Linux setup  /  Space Engineers", self.c.A_BOLD)
        self.text(5, 3, str(self.options.target), self.muted)

    def report(self, message):
        self.messages.append(message)
        self.frame("Working")
        height, width = self.screen.getmaxyx()
        lines = [
            line
            for message in self.messages
            for line in textwrap.wrap(message, max(1, width - 6))
        ]
        for row, line in enumerate(lines[-max(1, height - 10) :], 7):
            self.text(row, 3, line)
        self.screen.refresh()

    def input(self, label, initial):
        value = str(initial)
        while True:
            self.frame(label)
            self.text(8, 3, value + "▏", self.accent)
            self.text(
                11,
                3,
                "Type to edit · Ctrl+U clear · Enter save · Esc cancel",
                self.muted,
            )
            self.screen.refresh()
            key = self.screen.get_wch()
            if key in ("\n", "\r"):
                return value
            if key == "\x1b":
                return str(initial)
            if key in (self.c.KEY_BACKSPACE, "\x7f", "\b"):
                value = value[:-1]
            elif key == "\x15":
                value = ""
            elif isinstance(key, str) and key.isprintable():
                value += key

    def confirm(self, action):
        self.frame(action.capitalize())
        lines = [
            f"{action.capitalize()} Pulsar at the path above?",
            "Existing installations are backed up before replacement.",
            "Profiles, local plugins, and game saves are preserved.",
        ]
        if action == "migrate":
            lines += [
                f"Old installation: {self.options.source or self.options.target}",
                f"Old settings: {self.options.settings or old_config()}",
            ]
        if action != "uninstall":
            lines += [
                f"Release: {self.options.version} from {REPO}",
                "Steam launch options are shown when setup finishes.",
            ]
        for row, line in enumerate(lines, 8):
            self.text(row, 3, line)
        self.text(
            8 + len(lines) + 2, 3, "Y continue · Any other key cancel", self.accent
        )
        self.screen.refresh()
        return self.screen.get_wch() in ("y", "Y")

    def run(self):
        items = [
            ("Install", "Set up the latest Linux release", "install"),
            ("Update", "Refresh program files, keep your settings", "update"),
            ("Migrate", "Transfer an old LinuxCompat native installation", "migrate"),
            ("Uninstall", "Remove program files, keep your settings", "uninstall"),
            ("Location", "Choose the installation folder", "location"),
            ("Release", "Latest stable or a specific release tag", "release"),
            ("Exit", "", "exit"),
        ]
        selected = 0
        while True:
            self.frame("Install · Update · Transfer")
            for n, (name, description, _) in enumerate(items):
                self.text(
                    8 + n * 2,
                    3,
                    f" {'›' if n == selected else ' '} {name:12} {description}",
                    self.selected if n == selected else 0,
                )
            self.text(
                self.screen.getmaxyx()[0] - 2,
                3,
                "↑ ↓ choose · Enter select · Q quit",
                self.muted,
            )
            self.screen.refresh()
            key = self.screen.get_wch()
            if key in ("q", "Q", "\x1b"):
                return
            if key in (self.c.KEY_UP, "k"):
                selected = (selected - 1) % len(items)
            elif key in (self.c.KEY_DOWN, "j"):
                selected = (selected + 1) % len(items)
            elif key in ("\n", "\r"):
                action = items[selected][2]
                if action == "exit":
                    return
                if action == "location":
                    self.options.target = self.input(
                        "Installation folder", self.options.target
                    )
                    continue
                if action == "release":
                    self.options.version = self.input(
                        "Release tag", self.options.version
                    )
                    continue
                if action == "migrate":
                    self.options.source = self.input(
                        "Old installation folder",
                        self.options.source or self.options.target,
                    )
                    self.options.settings = self.input(
                        "Old settings folder", self.options.settings or old_config()
                    )
                if not self.confirm(action):
                    continue
                self.messages = []
                try:
                    perform(self.options, action, self.report)
                    self.report("Done. Press any key to return.")
                except (
                    SetupError,
                    OSError,
                    ValueError,
                    ET.ParseError,
                    tarfile.TarError,
                ) as error:
                    self.report(f"Setup stopped: {error}")
                    self.report("Press any key to return.")
                self.screen.get_wch()


def perform(options, action, report):
    if action != "uninstall":
        try:
            result = subprocess.run(
                ["dotnet", "--list-runtimes"],
                capture_output=True,
                text=True,
                timeout=15,
            )
            has_runtime = re.search(
                r"^Microsoft\.NETCore\.App 10\.", result.stdout, re.M
            )
        except (OSError, subprocess.TimeoutExpired):
            has_runtime = False
        if not has_runtime:
            report(
                "Install the .NET 10 runtime before launching Pulsar; setup does not change system packages."
            )
    Installer(options.target, report).run(
        action,
        options.version,
        options.archive,
        options.sha256,
        options.source,
        options.settings,
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "action",
        nargs="?",
        choices=("install", "update", "migrate", "uninstall"),
        help="omit to open the TUI",
    )
    script_dir = Path(__file__).resolve().parent
    default_target = script_dir if modern(script_dir) else data_home() / "Pulsar"
    parser.add_argument(
        "--target", default=os.environ.get("PULSAR_DATA_DIR", str(default_target))
    )
    parser.add_argument(
        "--source", help="old native binary folder for migration (default: target)"
    )
    parser.add_argument(
        "--settings", help="old settings folder (default: XDG_CONFIG_HOME/Pulsar)"
    )
    parser.add_argument(
        "--version", default="latest", help="GitHub release tag, or latest"
    )
    parser.add_argument(
        "--archive", help="use a local unified Linux .tar.gz instead of downloading"
    )
    parser.add_argument("--sha256", help="expected SHA-256 for a local archive")
    parser.add_argument(
        "--yes", action="store_true", help="confirm the explicitly named CLI action"
    )
    options = parser.parse_args()
    if sys.platform != "linux" or platform.machine() not in ("x86_64", "amd64"):
        parser.error("This installer supports Linux x64.")
    if os.geteuid() == 0:
        parser.error("Run as your normal user, without sudo.")
    try:
        if options.action:
            if not options.yes:
                parser.error(
                    "Use --yes to confirm a CLI action, or omit the action to use the TUI."
                )
            perform(options, options.action, print)
        else:
            if not sys.stdin.isatty() or not sys.stdout.isatty():
                parser.error(
                    "The TUI needs a terminal. Use an explicit action with --yes for scripts."
                )
            import curses

            curses.wrapper(lambda screen: Tui(screen, options).run())
        return 0
    except (SetupError, OSError, ValueError, ET.ParseError, tarfile.TarError) as error:
        print(f"Pulsar setup: {error}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("\nSetup cancelled.", file=sys.stderr)
        return 130


if __name__ == "__main__":
    sys.exit(main())

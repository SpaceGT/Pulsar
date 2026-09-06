"""Run: python3 -m unittest discover -s Tests/LinuxInstaller -v"""

import hashlib
import importlib.util
import io
import os
import shutil
import subprocess
import tarfile
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[2] / "Scripts/pulsar-linux.py"
spec = importlib.util.spec_from_file_location("pulsar_setup", SCRIPT)
setup = importlib.util.module_from_spec(spec)
spec.loader.exec_module(setup)


def package(path, revision=b"first", extra=None):
    files = {name: revision for name in setup.REQUIRED}
    files.update(extra or {})
    with tarfile.open(path, "w:gz") as archive:
        for name, data in files.items():
            member = tarfile.TarInfo("./" + name)
            member.size = len(data)
            member.mode = 0o755 if name.endswith(".bin") else 0o644
            archive.addfile(member, io.BytesIO(data))
    return path


class InstallerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="pulsar-setup-test-")
        self.addCleanup(self.temp.cleanup)
        self.home = Path(self.temp.name)
        self.env = patch.dict(
            os.environ,
            {
                "HOME": str(self.home),
                "XDG_DATA_HOME": str(self.home / ".local/share"),
                "XDG_CONFIG_HOME": str(self.home / ".config"),
                "XDG_STATE_HOME": str(self.home / ".local/state"),
                "XDG_CACHE_HOME": str(self.home / ".cache"),
                "DOTNET_CLI_HOME": str(self.home / ".dotnet"),
            },
        )
        self.env.start()
        self.addCleanup(self.env.stop)
        self.target = self.home / "Games/Pulsar with spaces"
        self.log = []
        self.installer = setup.Installer(self.target, self.log.append)
        self.archive = package(self.home / "release.tar.gz")

    def install(self):
        self.installer.run("install", archive=self.archive)

    def test_install_update_uninstall_preserve_user_data_and_back_up(self):
        self.install()
        self.assertTrue(os.access(self.target / "Interim.bin", os.X_OK))
        self.assertEqual(
            self.installer.launch_options(), f"'{self.target}/Interim.bin' %command%"
        )
        profile = self.target / "Legacy/Profiles/Current.xml"
        profile.parent.mkdir(parents=True)
        profile.write_text("user profile")
        user_file = self.target / "notes.txt"
        user_file.write_text("keep me")
        # Files added by Pulsar's own updater must also be removed on uninstall.
        (self.target / "Libraries/Interim/auto-updated.dll").write_bytes(b"auto update")
        newer = package(self.home / "new.tar.gz", b"second")
        self.installer.run("update", archive=newer)
        self.assertEqual((self.target / "Interim.bin").read_bytes(), b"second")
        self.assertEqual(profile.read_text(), "user profile")
        self.assertEqual(user_file.read_text(), "keep me")
        backups = list(self.target.parent.glob(".Pulsar with spaces-backup-*"))
        self.assertEqual(len(backups), 1)
        self.assertEqual((backups[0] / "Interim.bin").read_bytes(), b"first")
        self.installer.run("uninstall")
        self.assertFalse((self.target / "Libraries").exists())
        self.assertFalse((self.target / "Interim.bin").exists())
        self.assertFalse(self.installer.desktop.exists())
        self.assertEqual(profile.read_text(), "user profile")
        self.assertEqual(user_file.read_text(), "keep me")
        self.installer.run("install", archive=self.archive)
        self.assertEqual(profile.read_text(), "user profile")

    def test_failed_download_keeps_existing_installation(self):
        self.install()
        with patch.object(setup, "release_archive", side_effect=OSError("offline")):
            with self.assertRaises(OSError):
                self.installer.run("update")
        self.assertEqual((self.target / "Interim.bin").read_bytes(), b"first")

    def test_checksum_and_malicious_archive_fail_before_changes(self):
        self.install()
        with self.assertRaisesRegex(setup.SetupError, "checksum"):
            self.installer.run("update", archive=self.archive, sha256="0" * 64)
        for name, link in [
            ("../escaped", None),
            ("/absolute", None),
            ("Libraries/link", "../../outside"),
            ("Legacy/Profiles/Current.xml", None),
        ]:
            bad = self.home / "bad.tar.gz"
            with tarfile.open(bad, "w:gz") as archive:
                member = tarfile.TarInfo(name)
                if link:
                    member.type = tarfile.SYMTYPE
                    member.linkname = link
                archive.addfile(member, io.BytesIO())
            with self.assertRaises(setup.SetupError):
                self.installer.run("update", archive=bad)
            self.assertEqual((self.target / "Interim.bin").read_bytes(), b"first")
        self.assertFalse((self.target.parent / "escaped").exists())

    def test_incomplete_package_and_unrelated_directory_are_rejected(self):
        bad = self.home / "empty.tar.gz"
        with tarfile.open(bad, "w:gz"):
            pass
        with self.assertRaisesRegex(setup.SetupError, "required launcher"):
            self.installer.run("install", archive=bad)
        self.assertFalse(self.target.exists())
        self.target.mkdir()
        (self.target / "important").write_text("preserve")
        for action in ("install", "update", "uninstall"):
            with self.assertRaises(setup.SetupError):
                self.installer.run(action, archive=self.archive)
        self.assertEqual((self.target / "important").read_text(), "preserve")

    def test_desktop_failure_rolls_back_installation_and_receipt(self):
        self.install()
        receipt = self.installer.receipt.read_bytes()
        desktop = self.installer.desktop.read_bytes()
        newer = package(self.home / "new.tar.gz", b"second")
        original = setup.atomic_write
        failed = False

        def write(path, content):
            nonlocal failed
            if path == self.installer.desktop and not failed:
                failed = True
                raise OSError("simulated desktop write failure")
            original(path, content)

        with patch.object(setup, "atomic_write", side_effect=write):
            with self.assertRaises(OSError):
                self.installer.run("update", archive=newer)
        self.assertEqual((self.target / "Interim.bin").read_bytes(), b"first")
        self.assertEqual(self.installer.receipt.read_bytes(), receipt)
        self.assertEqual(self.installer.desktop.read_bytes(), desktop)

    def test_running_game_is_detected_by_path(self):
        self.install()
        binary = self.target / "Interim.bin"
        shutil.copy2("/bin/sleep", binary)
        process = subprocess.Popen([str(binary), "30"])
        self.addCleanup(process.wait)
        self.addCleanup(process.terminate)
        with self.assertRaisesRegex(setup.SetupError, "Close Pulsar"):
            self.installer.run("uninstall")

    def test_another_installation_desktop_is_not_removed(self):
        self.install()
        self.installer.desktop.write_text(
            "[Desktop Entry]\nX-Pulsar-Install-Path=/another/Pulsar\n"
        )
        self.installer.run("uninstall")
        self.assertTrue(self.installer.desktop.exists())

    def test_bundled_script_defaults_to_its_install_and_can_replace_itself(self):
        self.install()
        script = self.target / "pulsar-linux.py"
        shutil.copy2(SCRIPT, script)
        newer = package(
            self.home / "new.tar.gz",
            b"second",
            {"pulsar-linux.py": SCRIPT.read_bytes()},
        )
        environment = dict(os.environ)
        environment.pop("PULSAR_DATA_DIR", None)
        for action in ("update", "uninstall"):
            result = subprocess.run(
                ["python3", str(script), action, "--archive", str(newer), "--yes"],
                env=environment,
                capture_output=True,
                text=True,
                timeout=30,
            )
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            if action == "update":
                self.assertEqual((self.target / "Interim.bin").read_bytes(), b"second")
        self.assertFalse(script.exists())
        self.assertFalse((self.target / "Interim.bin").exists())

    def legacy_settings(self):
        state = setup.old_config()
        (state / "Profiles").mkdir(parents=True)
        (state / "Sources/Hubs").mkdir(parents=True)
        (state / "Sources/Hubs/obsolete.bin").write_bytes(b"obsolete protobuf schema")
        (state / "Local").mkdir()
        (state / "Local/Custom.dll").write_bytes(b"custom plugin")
        (state / "config.xml").write_text(
            "<CoreConfig><NetworkTimeout>30000</NetworkTimeout></CoreConfig>"
        )
        dev = self.home / "dev/se-remote"
        dev.mkdir(parents=True)
        (dev / "Remote.xml").write_text("<PluginData><Id>remote</Id></PluginData>")
        (state / "Sources/sources.xml").write_text(f"""<SourcesConfig>
          <RemoteHubSources><RemoteHub><Repo>StarCpt/PluginHub</Repo><Hash>stale</Hash><LastCheck>old</LastCheck></RemoteHub></RemoteHubSources>
          <LocalPluginSources>
            <LocalPlugin><Name>se-remote</Name><Folder>{dev}</Folder><Enabled>true</Enabled></LocalPlugin>
            <LocalPlugin><Name>se-linux-compat</Name><Folder>{self.home}/missing/se-linux-compat</Folder><Enabled>true</Enabled></LocalPlugin>
          </LocalPluginSources></SourcesConfig>""")
        (state / "Profiles/Current.xml").write_text("""<Profile><Name>Current</Name>
          <GitHub><GitHubPluginConfig><Id>se-dotnet-compat</Id></GitHubPluginConfig></GitHub>
          <DevFolder>
            <LocalFolderConfig><Id>se-linux-compat</Id><DataFile>LinuxCompatClient.xml</DataFile></LocalFolderConfig>
            <LocalFolderConfig><Id>se-remote</Id><DataFile>Remote.xml</DataFile><DebugBuild>true</DebugBuild></LocalFolderConfig>
          </DevFolder><Mods><unsignedLong>123456</unsignedLong></Mods></Profile>""")
        # These are rebuildable caches and must not migrate.
        (state / "GitHub").mkdir()
        (state / "GitHub/old.dll").write_bytes(b"old")
        return state

    def assert_migrated(self, state):
        dest = self.target / "Legacy"
        self.assertEqual((dest / "Local/Custom.dll").read_bytes(), b"custom plugin")
        self.assertEqual(
            (dest / "config.xml").read_bytes(), (state / "config.xml").read_bytes()
        )
        self.assertFalse((dest / "Sources/Hubs").exists())
        self.assertFalse((dest / "GitHub").exists())
        source = ET.parse(dest / "Sources/sources.xml")
        self.assertIsNone(source.find(".//Hash"))
        self.assertEqual(source.findtext(".//LocalPlugin/File"), "Remote.xml")
        profile = ET.parse(dest / "Profiles/Current.xml")
        self.assertEqual(profile.findtext(".//LocalFolderConfig/Id"), "remote")
        self.assertEqual(profile.findtext(".//GitHubPluginConfig/Id"), "dotnet-compat")
        self.assertEqual(profile.findtext(".//unsignedLong"), "123456")
        self.assertIsNone(profile.find(".//DataFile"))
        self.assertTrue((state / "Sources/Hubs/obsolete.bin").exists())

    def test_migration_moves_profiles_and_dev_manifests_not_caches(self):
        state = self.legacy_settings()
        (self.target / "Bin").mkdir(parents=True)
        (self.target / "Bin/Interim").write_bytes(b"old binary")
        (self.target / "Interim").write_text("old wrapper")
        self.installer.run("migrate", archive=self.archive, settings=state)
        self.assert_migrated(state)
        self.assertFalse((self.target / "Bin").exists())
        self.assertFalse((self.target / "Interim").exists())

    def test_symlinked_profile_is_copied_without_modifying_original(self):
        state = self.legacy_settings()
        original = self.home / "original-profile.xml"
        profile = state / "Profiles/Current.xml"
        profile.rename(original)
        before = original.read_bytes()
        profile.symlink_to(original)
        copied = self.home / "copied"
        setup.copy_settings(state, copied)
        setup.migrate_settings(copied, self.log.append)
        self.assertEqual(original.read_bytes(), before)
        self.assertFalse((copied / "Profiles/Current.xml").is_symlink())

    def test_symlinked_config_directories_do_not_modify_originals(self):
        state = self.legacy_settings()
        before = (state / "Profiles/Current.xml").read_bytes()
        for name in ("Profiles", "Sources"):
            original = self.home / ("original-" + name)
            (state / name).rename(original)
            (state / name).symlink_to(original, target_is_directory=True)
        copied = self.home / "copied"
        setup.copy_settings(state, copied)
        setup.migrate_settings(copied, self.log.append)
        self.assertEqual((state / "Profiles/Current.xml").read_bytes(), before)
        self.assertIsNotNone(ET.parse(state / "Sources/sources.xml").find(".//Hash"))
        self.assertFalse((copied / "Profiles").is_symlink())

    def test_migration_conflict_preserves_both_installations(self):
        state = self.legacy_settings()
        old = self.home / "old"
        (old / "Bin").mkdir(parents=True)
        (old / "Bin/Interim").write_bytes(b"old binary")
        (old / "Interim").write_bytes(b"old wrapper")
        self.install()
        (self.target / "Legacy/Profiles").mkdir(parents=True)
        (self.target / "Legacy/Profiles/Current.xml").write_text("existing profile")
        with self.assertRaisesRegex(setup.SetupError, "overwrite existing settings"):
            self.installer.run(
                "migrate", archive=self.archive, source=old, settings=state
            )
        self.assertEqual(
            (self.target / "Legacy/Profiles/Current.xml").read_text(),
            "existing profile",
        )
        self.assertTrue(setup.modern(self.target))
        self.assertTrue(setup.legacy(old))

    def test_concurrent_operation_is_rejected(self):
        with self.installer.lock():
            with self.assertRaisesRegex(setup.SetupError, "already running"):
                self.installer.run("install", archive=self.archive)
        self.assertFalse(self.target.exists())

    @unittest.skipUnless(
        os.environ.get("PULSAR_TEST_LEGACY_BUNDLE")
        and os.environ.get("PULSAR_TEST_RELEASE_ARCHIVE"),
        "Set real release fixture paths to run the 1.0.16 transfer",
    )
    def test_actual_1_0_16_install_migrate_update_uninstall(self):
        bundle = Path(os.environ["PULSAR_TEST_LEGACY_BUNDLE"])
        archive = Path(os.environ["PULSAR_TEST_RELEASE_ARCHIVE"])
        # The original release scripts run with every home/XDG destination isolated.
        self.target = setup.data_home() / "Pulsar"
        self.installer = setup.Installer(self.target, self.log.append)
        environment = dict(os.environ, PULSAR_DATA_DIR=str(self.target))
        subprocess.run(
            ["bash", str(bundle / "install.sh")],
            env=environment,
            check=True,
            capture_output=True,
            text=True,
        )
        self.assertTrue(setup.legacy(self.target))
        state = self.legacy_settings()
        environment["PULSAR_DIR"] = str(state)
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        self.installer.run("migrate", archive=archive, sha256=digest, settings=state)
        self.assertTrue(setup.modern(self.target))
        self.assert_migrated(state)
        self.assertFalse(list((setup.data_home() / "icons").rglob("pulsar.png")))
        self.installer.run("update", archive=archive, sha256=digest)
        self.installer.run("uninstall")
        self.assertFalse((self.target / "Interim.bin").exists())
        self.assertTrue((self.target / "Legacy/Profiles/Current.xml").exists())
        # Exercise the release's cleanup too; it only sees this disposable home.
        subprocess.run(
            ["bash", str(bundle / "uninstall.sh")],
            env=environment,
            check=True,
            capture_output=True,
            text=True,
        )
        self.assertFalse(self.target.exists())
        self.assertFalse((setup.data_home() / "applications/pulsar.desktop").exists())
        self.assertFalse(list((setup.data_home() / "icons").rglob("pulsar.png")))
        # TemporaryDirectory removes retained settings, backups, caches, and receipts.


if __name__ == "__main__":
    unittest.main()

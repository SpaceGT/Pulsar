#!/usr/bin/env bash
# package_pulsar_for_linux_flatpak.sh
#
# Player bundle: builds a single-file .flatpak that runs Pulsar + Space
# Engineers entirely inside the org.freedesktop.Platform 25.08 sandbox.
# The .NET 10 runtime is shipped as a self-contained linux-x64 publish so
# the host does not need a system-wide dotnet install.
#
# se-dotnet-compat and se-linux-compat (LinuxCompat) are NOT vendored:
# Pulsar seeds a default sources.xml enabling StarCpt/PluginHub on first
# launch, then fetches and compiles both from source. The Flatpak's
# network and Steam permissions cover both the PluginHub fetch and the
# compile-against-SE-Bin64 step.
#
# Output: dist/io.github.SpaceGT.Pulsar.<YYYYMMDD>.<8-hex-git-hash>.flatpak
#
# Layout of the staged tree fed to flatpak-builder (under
# $BUILD_DIR/flatpak-stage/):
#
#   io.github.SpaceGT.Pulsar.yml      generated manifest
#   pulsar-bundle/                    "dir" source consumed by the manifest:
#   ├── pulsar/                       (= /app/lib/pulsar/ inside the sandbox)
#   │   └── Bin/                          self-contained .NET 10 publish
#   ├── pulsar.sh                     /app/bin/pulsar launcher script
#   ├── io.github.SpaceGT.Pulsar.desktop
#   ├── io.github.SpaceGT.Pulsar.metainfo.xml
#   └── icons/<size>/io.github.SpaceGT.Pulsar.png
#
# Pulsar (Legacy/Program.cs) hard-codes its config root to
# $HOME/.config/Pulsar/. We mount the host's real ~/.config/Pulsar into
# the sandbox (--filesystem=~/.config/Pulsar) so the user's PulsarLogs/,
# Sources/, Profiles/, and a side-loaded Local/*.dll live in the same
# directory the developer 7z bundle uses — and survive Flatpak
# uninstall/reinstall just like SE saves do.
#
# Usage:
#   ./package_pulsar_for_linux_flatpak.sh [output_dir]
#
# Env-var overrides (defaults shown):
#   PULSAR_REPO_DIR=$HOME/dev/se1/Pulsar     (auto-detected from script location)
#   BUILD_DIR=$PULSAR_REPO_DIR/build         (gitignored staging area)
#   OUTPUT_DIR=$PULSAR_REPO_DIR/dist         (first positional arg overrides)
#   APP_ID=io.github.SpaceGT.Pulsar
#   RUNTIME_VERSION=25.08
#
# Requirements: dotnet (with .NET 10 SDK), git, magick (ImageMagick 7),
# and either a host `flatpak-builder` binary OR the Flathub-installed
# `org.flatpak.Builder` runtime (the script auto-detects).

set -euo pipefail

# ---- configuration ----------------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PULSAR_REPO_DIR_DEFAULT="$(cd "$SCRIPT_DIR/.." && pwd)"

# shellcheck source=package_pulsar_common.sh
source "$SCRIPT_DIR/package_pulsar_common.sh"

pulsar_pkg_init_common "${1:-}"

APP_ID="${APP_ID:-io.github.SpaceGT.Pulsar}"
RUNTIME_VERSION="${RUNTIME_VERSION:-25.08}"

# Self-contained linux-x64 publish lives under bin/Release/net10.0/linux-x64/
PULSAR_PUBLISH_DIR="$PULSAR_REPO_DIR/Legacy/bin/Release/net10.0/linux-x64/publish"

# ---- preflight --------------------------------------------------------------

pulsar_pkg_require_tools dotnet git magick

# Detect a usable flatpak-builder. Prefer the host binary; fall back to
# the Flathub-installed `org.flatpak.Builder` Flatpak (which itself
# unconditionally has --filesystem=host so it can read $BUILD_DIR).
if command -v flatpak-builder >/dev/null 2>&1; then
    FLATPAK_BUILDER=(flatpak-builder)
elif flatpak info --user org.flatpak.Builder >/dev/null 2>&1 \
   || flatpak info        org.flatpak.Builder >/dev/null 2>&1; then
    FLATPAK_BUILDER=(flatpak run org.flatpak.Builder)
else
    echo "ERROR: no flatpak-builder found." >&2
    echo "Install with:  sudo apt install flatpak-builder" >&2
    echo "          or:  flatpak install --user flathub org.flatpak.Builder" >&2
    exit 1
fi

# Verify the runtime + SDK + dotnet10 SDK extension are installed (any scope).
need_runtime() {
    local ref="$1"
    flatpak info        "$ref" >/dev/null 2>&1 \
    || flatpak info --user "$ref" >/dev/null 2>&1 \
    || return 1
}
MISSING_RT=()
need_runtime "org.freedesktop.Platform/x86_64/$RUNTIME_VERSION" \
    || MISSING_RT+=("org.freedesktop.Platform//$RUNTIME_VERSION")
need_runtime "org.freedesktop.Sdk/x86_64/$RUNTIME_VERSION" \
    || MISSING_RT+=("org.freedesktop.Sdk//$RUNTIME_VERSION")
need_runtime "org.freedesktop.Sdk.Extension.dotnet10/x86_64/$RUNTIME_VERSION" \
    || MISSING_RT+=("org.freedesktop.Sdk.Extension.dotnet10//$RUNTIME_VERSION")
if [ "${#MISSING_RT[@]}" -gt 0 ]; then
    echo "ERROR: missing required Flatpak runtime/SDK refs:" >&2
    for r in "${MISSING_RT[@]}"; do echo "    $r" >&2; done
    echo "Install with:" >&2
    echo "    flatpak install --user flathub ${MISSING_RT[*]}" >&2
    exit 1
fi

pulsar_pkg_require_paths "$PULSAR_CSPROJ" "$PULSAR_ICON_ICO"
pulsar_pkg_canonicalize_dirs
pulsar_pkg_compute_version_info

echo "==> Variant       : Flatpak ($APP_ID)"
echo "==> Pulsar repo   : $PULSAR_REPO_DIR (hash $GIT_HASH)"
echo "==> Build dir     : $BUILD_DIR"
echo "==> Output dir    : $OUTPUT_DIR"
echo "==> flatpak-builder : ${FLATPAK_BUILDER[*]}"

# ---- build ------------------------------------------------------------------

# Self-contained so the .NET 10 runtime is bundled into the publish dir;
# the Flatpak sandbox has no system-wide dotnet to fall back on.
pulsar_pkg_build_artifacts -r linux-x64 --self-contained true \
                           -p:PublishSingleFile=false
pulsar_pkg_verify_artifacts "$PULSAR_PUBLISH_DIR"

# ---- stage flatpak-builder source tree -------------------------------------

STAGE_DIR="$BUILD_DIR/flatpak-stage"
SRC_DIR="$STAGE_DIR/pulsar-bundle"
PULSAR_ROOT="$SRC_DIR/pulsar"

rm -rf "$STAGE_DIR"
mkdir -p \
    "$PULSAR_ROOT/Bin" \
    "$SRC_DIR/icons"

echo "==> Staging Pulsar publish output -> pulsar/Bin/"
pulsar_pkg_stage_pulsar_publish "$PULSAR_PUBLISH_DIR" "$PULSAR_ROOT/Bin"

# ---- icon extraction (Flatpak app-id naming) -------------------------------

echo "==> Extracting Pulsar icon -> icons/<size>/$APP_ID.png"
pulsar_pkg_extract_icons "$SRC_DIR/icons" "$APP_ID"

# ---- generate /app/bin launcher --------------------------------------------
# Sets LD_LIBRARY_PATH to the LinuxCompat assets dir and execs the apphost.
# The self-contained .NET 10 runtime is embedded next to the apphost.
#
# Pulsar (Legacy/Program.cs) hard-codes its config root to
# $HOME/.config/Pulsar/, and that path is bind-mounted to the host's real
# ~/.config/Pulsar via --filesystem= in the manifest, so the user can
# inspect PulsarLogs/info.log, edit Sources/sources.xml, etc. on the host.
# Pulsar itself seeds Sources/sources.xml and Profiles/Current.xml on first
# launch — they no longer ship in the bundle. LinuxCompat and DotNetCompat
# are fetched and compiled from source by Pulsar on first run.
#
# SE depot location: probed from the standard Steam install paths exposed
# via Flatpak --filesystem= overrides. SPACE_ENGINEERS_ROOT can be
# overridden via `flatpak run --env=...`.

cat > "$SRC_DIR/pulsar.sh" <<'LAUNCHER_EOF'
#!/bin/bash
set -euo pipefail

PKG_DIR=/app/lib/pulsar
INTERIM="$PKG_DIR/Bin/Interim"

if [ ! -x "$INTERIM" ]; then
    echo "ERROR: Pulsar Interim binary not found at $INTERIM" >&2
    exit 1
fi

# Probe standard Steam install paths for the SE depot; first hit wins.
# (All exposed via finish-args in the manifest.)
SE_CANDIDATES=(
    "${SPACE_ENGINEERS_ROOT:-}"
    "$HOME/.steam/steam/steamapps/common/SpaceEngineers"
    "$HOME/.local/share/Steam/steamapps/common/SpaceEngineers"
    "$HOME/.var/app/com.valvesoftware.Steam/data/Steam/steamapps/common/SpaceEngineers"
)
SE_ROOT=""
for c in "${SE_CANDIDATES[@]}"; do
    [ -n "$c" ] || continue
    if [ -f "$c/Bin64/SpaceEngineers.exe" ] || [ -f "$c/Content/Data/index.dat" ]; then
        SE_ROOT="$c"
        break
    fi
done
if [ -z "$SE_ROOT" ]; then
    echo "ERROR: Space Engineers install not found." >&2
    echo "  Install SE via Steam, or set SPACE_ENGINEERS_ROOT and re-run." >&2
    exit 1
fi
export SPACE_ENGINEERS_ROOT="$SE_ROOT"

export DXVK_ADAPTER="${DXVK_ADAPTER:-1}"
export DXVK_WSI_DRIVER="${DXVK_WSI_DRIVER:-SDL3}"
export SteamAppId=244850
export ALSOFT_DRIVERS="${ALSOFT_DRIVERS:-pulse,alsa,oss,sndio,}"

# LinuxCompat's native assets (built from source by Pulsar on first run)
# provide all binary dependencies (DXVK, native-wrappers, gaming-platforms,
# FFmpeg, OpenAL).
mkdir -p "$HOME/.config/Pulsar/GitHub/viktor-ferenczi/se-linux-compat/Assets"
export LD_LIBRARY_PATH="$HOME/.config/Pulsar/GitHub/viktor-ferenczi/se-linux-compat/Assets/:${LD_LIBRARY_PATH:-}"

# SE resolves its user-data dir via .NET's
# Environment.SpecialFolder.ApplicationData, which on Linux maps to
# $XDG_CONFIG_HOME (or $HOME/.config when unset). The Flatpak runtime
# defaults XDG_CONFIG_HOME to ~/.var/app/<app-id>/config, which would
# put SE saves/configs/logs at
# ~/.var/app/io.github.SpaceGT.Pulsar/config/SpaceEngineers/ — invisible
# to the host and to a non-Flatpak Pulsar install. Re-point it at the
# host's real ~/.config (mounted into the sandbox via the
# --filesystem=~/.config/SpaceEngineers and --filesystem=~/.config/Pulsar
# entries in the manifest) so SE writes to ~/.config/SpaceEngineers/,
# matching the Native bundle and the developer 7z layout.
export XDG_CONFIG_HOME="$HOME/.config"

# Ensure SE log dir exists at the unified host location.
mkdir -p "$HOME/.config/SpaceEngineers"

cd "$PKG_DIR/Bin"

# Steam overlay (see Native launcher for rationale).
# Opt out: pass -nosteamoverlay or set PULSAR_NO_STEAM_OVERLAY=1 to skip
# the LD_PRELOAD entirely. The flag is filtered out before exec'ing the
# apphost.
PULSAR_NO_STEAM_OVERLAY="${PULSAR_NO_STEAM_OVERLAY:-}"
FILTERED_ARGS=()
for arg in "$@"; do
    if [ "$arg" = "-nosteamoverlay" ]; then
        PULSAR_NO_STEAM_OVERLAY=1
    else
        FILTERED_ARGS+=("$arg")
    fi
done
set -- ${FILTERED_ARGS[@]+"${FILTERED_ARGS[@]}"}

if [ -n "$PULSAR_NO_STEAM_OVERLAY" ]; then
    echo "Steam overlay: disabled (-nosteamoverlay / PULSAR_NO_STEAM_OVERLAY)" >&2
else
    STEAM_ROOT_CANDIDATES=(
        "$HOME/.steam/root"
        "$HOME/.steam/steam"
        "$HOME/.local/share/Steam"
    )
    for steam_root in "${STEAM_ROOT_CANDIDATES[@]}"; do
        overlay64="$steam_root/ubuntu12_64/gameoverlayrenderer.so"
        if [ -f "$overlay64" ]; then
            export LD_PRELOAD="$overlay64${LD_PRELOAD:+:$LD_PRELOAD}"
            export SteamGameId="${SteamGameId:-244850}"
            echo "Steam overlay: enabled (hooks from $steam_root)" >&2
            break
        fi
    done
fi

exec "$INTERIM" "$@"
LAUNCHER_EOF
chmod +x "$SRC_DIR/pulsar.sh"

# ---- generate desktop entry + AppStream metainfo ---------------------------

cat > "$SRC_DIR/$APP_ID.desktop" <<DESKTOP_EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=Pulsar
GenericName=Space Engineers Launcher
Comment=Space Engineers Launcher (Native Linux)
Exec=pulsar -keepintro
Icon=$APP_ID
Terminal=false
Categories=Game;
Keywords=SpaceEngineers;Pulsar;Game;
DESKTOP_EOF

# Minimal AppStream metainfo (Flatpak / Flathub validators require it).
cat > "$SRC_DIR/$APP_ID.metainfo.xml" <<METAINFO_EOF
<?xml version="1.0" encoding="UTF-8"?>
<component type="desktop-application">
  <id>$APP_ID</id>
  <name>Pulsar</name>
  <summary>Linux launcher for Space Engineers</summary>
  <description>
    <p>
      Pulsar is the launcher used to run Space Engineers natively on Linux.
      Compatibility plugins (LinuxCompat and DotNetCompat) are fetched and
      compiled from source on first launch. Space Engineers itself must be
      installed separately via Steam.
    </p>
  </description>
  <metadata_license>CC0-1.0</metadata_license>
  <project_license>MIT</project_license>
  <developer id="io.github.SpaceGT">
    <name>SpaceGT and contributors</name>
  </developer>
  <launchable type="desktop-id">$APP_ID.desktop</launchable>
  <categories>
    <category>Game</category>
  </categories>
  <content_rating type="oars-1.1" />
  <releases>
    <release version="$BUILD_DATE.$GIT_HASH" date="$(date -u +%Y-%m-%d)" />
  </releases>
</component>
METAINFO_EOF

# ---- generate flatpak-builder manifest -------------------------------------
# Single "simple" module: copy the prebuilt staged tree into /app. We do
# NOT compile inside the sandbox — all .NET artifacts were produced above
# with the host's dotnet (matches how the Native bundle works). The
# dotnet10 SDK extension is therefore not needed at install time, but we
# still declare it as build-only so future iterations can switch to an
# in-sandbox build without rewriting the manifest.

MANIFEST="$STAGE_DIR/$APP_ID.yml"
cat > "$MANIFEST" <<MANIFEST_EOF
app-id: $APP_ID
runtime: org.freedesktop.Platform
runtime-version: '$RUNTIME_VERSION'
sdk: org.freedesktop.Sdk
sdk-extensions:
  - org.freedesktop.Sdk.Extension.dotnet10
command: pulsar
finish-args:
  # Window system + GPU
  # CRITICAL: Do NOT include --socket=wayland here, because the code can only work on X11 or with XWayland
  - --share=ipc
  - --socket=x11
  - --socket=fallback-x11
  - --device=dri
  # Audio
  - --socket=pulseaudio
  # Network (Steamworks + EOS + mod IO)
  - --share=network
  # Steam depot read access (covers native, Flatpak Steam, snap-style)
  - --filesystem=~/.steam:ro
  - --filesystem=~/.local/share/Steam:ro
  - --filesystem=~/.var/app/com.valvesoftware.Steam:ro
  # SE save/config/log dir. The launcher re-points XDG_CONFIG_HOME at
  # $HOME/.config so SE's ApplicationData lookup lands here instead of
  # the Flatpak-default ~/.var/app/<app-id>/config/.
  - --filesystem=~/.config/SpaceEngineers:create
  # Pulsar config / logs: mount the host's real ~/.config/Pulsar so the
  # user can read PulsarLogs/info.log and edit Sources/, Profiles/, and
  # any side-loaded Local/*.dll just like with the developer 7z bundle.
  - --filesystem=~/.config/Pulsar:create
  # System notifications
  - --talk-name=org.freedesktop.Notifications
  # Default env
  - --env=SteamAppId=244850
  - --env=DXVK_WSI_DRIVER=SDL3
  - --env=ALSOFT_DRIVERS=pulse,alsa,oss,sndio,
  # Steam overlay: enable the Vulkan implicit layer and expose the host's
  # Vulkan layer manifest dir so the Flatpak's Vulkan loader can find
  # steamoverlay_x86_64.json. The .so itself is reached via the loader's
  # absolute library_path inside the manifest (under ~/.steam, which is
  # already mounted via --filesystem=~/.steam:ro).
  - --env=ENABLE_VK_LAYER_VALVE_steam_overlay_1=1
  - --filesystem=~/.local/share/vulkan:ro
  - --env=VK_ADD_LAYER_PATH=/run/host/home/.local/share/vulkan/implicit_layer.d
modules:
  # Silk.NET audio (used by se-linux-compat) requires libopenal at runtime.
  # The freedesktop Platform runtime does not ship it, so build it here.
  - name: openal-soft
    buildsystem: cmake-ninja
    config-opts:
      - -DALSOFT_UTILS=OFF
      - -DALSOFT_EXAMPLES=OFF
      - -DALSOFT_INSTALL_CONFIG=OFF
    sources:
      - type: archive
        url: https://openal-soft.org/openal-releases/openal-soft-1.25.2.tar.bz2
        sha256: 1dbaac44e7579d5bc8847ca8db4b2e8b9fd3961041f35ee20def4958301e1089
  - name: pulsar
    buildsystem: simple
    sources:
      - type: dir
        path: pulsar-bundle
    build-commands:
      # Bundle layout under /app
      - install -d /app/lib/pulsar /app/bin /app/share/applications /app/share/metainfo
      - cp -a pulsar/. /app/lib/pulsar/
      - chmod +x /app/lib/pulsar/Bin/Interim
      - install -m 0755 pulsar.sh /app/bin/pulsar
      - install -m 0644 $APP_ID.desktop /app/share/applications/$APP_ID.desktop
      - install -m 0644 $APP_ID.metainfo.xml /app/share/metainfo/$APP_ID.metainfo.xml
      - |
        for d in icons/*/; do
          size=\$(basename "\$d")
          install -d /app/share/icons/hicolor/\${size}x\${size}/apps
          install -m 0644 "\$d/$APP_ID.png" /app/share/icons/hicolor/\${size}x\${size}/apps/$APP_ID.png
        done
MANIFEST_EOF

# ---- run flatpak-builder ---------------------------------------------------
# Use --user --install to also install into the user's Flatpak store as a
# convenience (lets the maintainer launch the just-built bundle with
# `flatpak run $APP_ID`). Repo + state dirs live under build/ so a
# subsequent run reuses caches.

REPO_DIR="$BUILD_DIR/flatpak-repo"
BUILDER_STATE_DIR="$BUILD_DIR/flatpak-state"
APP_BUILD_DIR="$BUILD_DIR/flatpak-app"

mkdir -p "$REPO_DIR" "$BUILDER_STATE_DIR"
rm -rf "$APP_BUILD_DIR"

echo "==> Running flatpak-builder"
(
    cd "$STAGE_DIR"
    "${FLATPAK_BUILDER[@]}" \
        --force-clean \
        --state-dir="$BUILDER_STATE_DIR" \
        --repo="$REPO_DIR" \
        --user \
        --install \
        "$APP_BUILD_DIR" \
        "$APP_ID.yml"
)

# ---- export single-file .flatpak bundle ------------------------------------

ARCHIVE_NAME="$APP_ID.$BUILD_DATE.$GIT_HASH.flatpak"
ARCHIVE_PATH="$OUTPUT_DIR/$ARCHIVE_NAME"
rm -f "$ARCHIVE_PATH"

echo "==> Exporting single-file bundle -> $ARCHIVE_NAME"
flatpak build-bundle "$REPO_DIR" "$ARCHIVE_PATH" "$APP_ID" master

echo
echo "Done: $ARCHIVE_PATH"
ls -lh "$ARCHIVE_PATH"
echo
echo "Run the freshly installed bundle with:"
echo "    flatpak run $APP_ID"
echo
echo "Distribute the .flatpak file with:"
echo "    flatpak install --user $ARCHIVE_PATH"

#!/usr/bin/env bash
# package_pulsar_for_linux_native.sh
#
# Developer bundle: Pulsar + Space Engineers run on the host directly.
# The .NET 10 runtime is required to be installed system-wide on the host
# (framework-dependent publish, so the developer can attach the host's
# `lldb` / `dotnet-dump` against the same .NET install they use everywhere
# else).
#
# se-dotnet-compat and se-linux-compat (LinuxCompat) are NOT vendored:
# Pulsar seeds a default sources.xml enabling StarCpt/PluginHub on first
# run, then discovers + compiles both from source. LinuxCompat delivers
# all native binary dependencies (DXVK, native-wrappers, gaming-platforms,
# FFmpeg, OpenAL) in its Assets/ directory.
#
# Output: dist/PulsarForLinux-Native.<YYYYMMDD>.<8-hex-git-hash>.7z
#
# Bundle layout (Pulsar/ is the staging source tree; install.sh splits it
# between ~/.local/share/Pulsar/ for binary deps and ~/.config/Pulsar/ for
# user-editable state, following XDG conventions):
#
#   PulsarForLinux-Native/
#   ├── install.sh           Copies Pulsar/{Interim,Bin} into
#   │                       ~/.local/share/Pulsar/ and installs an XDG
#   │                       menu entry. Warns if .NET 10 runtime is not on
#   │                       the host.
#   ├── uninstall.sh        Removes the binary deps from ~/.local/share/Pulsar/
#   │                       entirely, and removes ~/.config/Pulsar/ contents
#   │                       EXCEPT the user-managed entries (kept in full):
#   │                       config.xml, Sources/, Local/, Profiles/.
#   │                       Also removes the XDG menu entry & pulsar icons.
#   │                       Used to swap cleanly to/from the Flatpak.
#   ├── README.txt
#   ├── icons/<size>/pulsar.png
#   └── Pulsar/                  (staging source tree, see install.sh)
#       ├── Interim                    bash launcher (sets LD_LIBRARY_PATH +
#       │                              exec's apphost). Deploys to
#       │                              ~/.local/share/Pulsar/Interim
#       └── Bin/                       Pulsar Interim framework-dep publish
#                                      Deploys to ~/.local/share/Pulsar/Bin/
#
# Usage:
#   ./package_pulsar_for_linux_native.sh [output_dir]
#
# Env-var overrides (defaults shown):
#   PULSAR_REPO_DIR=$HOME/dev/se1/Pulsar     (auto-detected from script location)
#   BUILD_DIR=$PULSAR_REPO_DIR/build         (gitignored staging area)
#   OUTPUT_DIR=$PULSAR_REPO_DIR/dist         (first positional arg overrides)
#
# Requirements: dotnet (with .NET 10 SDK), 7z, git, magick (ImageMagick 7).

set -euo pipefail

# ---- configuration ----------------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PULSAR_REPO_DIR_DEFAULT="$(cd "$SCRIPT_DIR/.." && pwd)"

# shellcheck source=package_pulsar_common.sh
source "$SCRIPT_DIR/package_pulsar_common.sh"

pulsar_pkg_init_common "${1:-}"

# Framework-dependent publish (no RID subdir). The host must have .NET 10
# installed; the bundle's apphost (Bin/Interim) discovers it via the
# standard FrameworkResolver search path.
PULSAR_PUBLISH_DIR="$PULSAR_REPO_DIR/Legacy/bin/Release/net10.0/publish"

VARIANT="Native"

# ---- preflight --------------------------------------------------------------

pulsar_pkg_require_tools dotnet 7z git magick
pulsar_pkg_require_paths "$PULSAR_CSPROJ" "$PULSAR_ICON_ICO"
pulsar_pkg_canonicalize_dirs
pulsar_pkg_compute_version_info

echo "==> Variant       : $VARIANT"
echo "==> Pulsar repo   : $PULSAR_REPO_DIR (hash $GIT_HASH)"
echo "==> Build dir     : $BUILD_DIR"
echo "==> Output dir    : $OUTPUT_DIR"

# ---- build ------------------------------------------------------------------

pulsar_pkg_build_artifacts --no-self-contained
pulsar_pkg_verify_artifacts "$PULSAR_PUBLISH_DIR"

# ---- stage ------------------------------------------------------------------
# Staging tree lives under the gitignored build/ folder so a re-run can be
# inspected without leaving artefacts in $PWD. We wipe the previous staging
# tree wholesale (no incremental) so leftover files from an earlier variant
# can never end up in the .7z.

PKG_ROOT="$BUILD_DIR/PulsarForLinux-$VARIANT"
PULSAR_ROOT="$PKG_ROOT/Pulsar"
rm -rf "$PKG_ROOT"
mkdir -p "$PULSAR_ROOT/Bin"

echo "==> Staging Pulsar publish output -> Pulsar/Bin/"
pulsar_pkg_stage_pulsar_publish "$PULSAR_PUBLISH_DIR" "$PULSAR_ROOT/Bin"

# ---- icon extraction --------------------------------------------------------

echo "==> Extracting Pulsar icon -> icons/<size>/pulsar.png"
pulsar_pkg_extract_icons "$PKG_ROOT/icons" pulsar

# ---- generate Pulsar/Interim launcher --------------------------------------
# Lives at ~/.local/share/Pulsar/Interim. Sets LD_LIBRARY_PATH to the
# LinuxCompat assets dir and exec's the apphost. Host's stock .NET 10
# runtime is discovered by the apphost's normal FrameworkResolver search path.

cat > "$PULSAR_ROOT/Interim" <<'EOF'
#!/usr/bin/env bash
# Interim - Native variant launcher. Runs Pulsar/SE on the host directly.
# Native binary deps are provided by LinuxCompat (built from source by
# Pulsar on first run); everything else (glibc, libssl, libicu, libstdc++,
# Mesa) comes from the host. Requires .NET 10 installed system-wide.
#
# Usage:   ~/.local/share/Pulsar/Interim [extra Pulsar args]

set -euo pipefail

PKG_DIR="$(cd "$(dirname "$0")" && pwd)"
INTERIM="$PKG_DIR/Bin/Interim"

if [ ! -x "$INTERIM" ]; then
    echo "ERROR: Pulsar Interim binary not found at $INTERIM" >&2
    echo "Hint: run install.sh from the extracted PulsarForLinux-Native archive first." >&2
    exit 1
fi

# Space Engineers install path -----------------------------------------------
export SPACE_ENGINEERS_ROOT="${SPACE_ENGINEERS_ROOT:-$HOME/.steam/steam/steamapps/common/SpaceEngineers}"

# Refuse to run if Interim is already up -------------------------------------
if pgrep -x Interim >/dev/null 2>&1; then
    echo "ERROR: Interim is already running. Stop it first (pkill -x Interim)." >&2
    exit 1
fi

# Environment for DXVK + native libs -----------------------------------------
export DXVK_ADAPTER="${DXVK_ADAPTER:-1}"
export DXVK_WSI_DRIVER="${DXVK_WSI_DRIVER:-SDL3}"

# LinuxCompat's native assets (built from source by Pulsar on first run)
# provide all binary dependencies (DXVK, native-wrappers, gaming-platforms,
# FFmpeg, OpenAL). The host's /usr/lib/... is searched last via the loader's
# default path, leaving glibc / libstdc++ / Mesa / libssl / libicu to the
# host as intended.
export LD_LIBRARY_PATH="$HOME/.config/Pulsar/GitHub/viktor-ferenczi/se-linux-compat/Assets/:${LD_LIBRARY_PATH:-}"

export SteamAppId=244850

# Steam overlay --------------------------------------------------------------
# Replicate the env-var setup Steam normally injects when launching a game,
# so Shift+Tab works when Pulsar is launched standalone:
#   - LD_PRELOAD gameoverlayrenderer.so for the Steam IPC + X11 input hook
#     (Steam ships both 32- and 64-bit; the loader silently ignores the
#     wrong-bitness one for this 64-bit process, and keeping both on
#     LD_PRELOAD matches Steam's own behaviour for forward-compat)
#   - ENABLE_VK_LAYER_VALVE_steam_overlay_1=1 to opt the DXVK process
#     into the Vulkan implicit overlay layer (steamoverlayvulkanlayer.so)
#   - SteamGameId=244850 so Steam's overlay associates this PID with SE
#
# Silently skipped if no Steam install is found. Requires the Steam client
# to be running and signed in for the overlay UI to actually appear.
#
# Opt out: pass -nosteamoverlay to this launcher, or set the
# PULSAR_NO_STEAM_OVERLAY env var to any non-empty value. This skips the
# LD_PRELOAD (and the Vulkan-overlay opt-in) entirely, leaving the in-process
# environment clean of Steam overlay hooks. The flag is filtered out of the
# arg list before exec'ing the apphost so it never reaches Pulsar.
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
    echo "Steam overlay:        disabled (-nosteamoverlay / PULSAR_NO_STEAM_OVERLAY)"
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
            export ENABLE_VK_LAYER_VALVE_steam_overlay_1=1
            echo "Steam overlay:        enabled (hooks from $steam_root)"
            break
        fi
    done
fi

# Logging ---------------------------------------------------------------------
LOG_DIR="$HOME/.config/SpaceEngineers"
mkdir -p "$LOG_DIR"
LOG_FILE="$LOG_DIR/Console_$(date +%Y%m%d_%H%M%S%3N).log"
exec > >(tee -a "$LOG_FILE") 2>&1

echo "Pulsar dir:           $PKG_DIR"
echo "Space Engineers root: $SPACE_ENGINEERS_ROOT"
echo "Console log:          $LOG_FILE"

chmod +x "$INTERIM" 2>/dev/null || true

cd "$PKG_DIR/Bin"
echo "Launching:            $INTERIM $*"
echo
exec "$INTERIM" "$@"
EOF
chmod +x "$PULSAR_ROOT/Interim"

# ---- generate install.sh ----------------------------------------------------

cat > "$PKG_ROOT/install.sh" <<'EOF'
#!/usr/bin/env bash
# install.sh - Native developer bundle. Copies the bundled Pulsar/ tree
# into ~/.local/share/Pulsar/ (Bin/ and Interim launcher). Installs an
# XDG menu entry. Warns (does not fail) if the host doesn't appear to
# have .NET 10 installed.
#
# Usage:   ./install.sh
# Env-var override:
#   PULSAR_DATA_DIR target dir for binary deps (default: ~/.local/share/Pulsar)
#   XDG_DATA_HOME   menu/icon root             (default: ~/.local/share)

set -euo pipefail

ARCHIVE_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC="$ARCHIVE_DIR/Pulsar"
DATA_DST="${PULSAR_DATA_DIR:-${XDG_DATA_HOME:-$HOME/.local/share}/Pulsar}"

if [ ! -d "$SRC" ]; then
    echo "ERROR: $SRC not found - run install.sh from the extracted archive." >&2
    exit 1
fi

if pgrep -x Interim >/dev/null 2>&1; then
    echo "ERROR: Interim is running. Stop it before deploying (pkill -x Interim)." >&2
    exit 1
fi

# ---- .NET 10 detection (host requirement) ---------------------------------
# Probe `dotnet --list-runtimes` and warn (do not fail) if Microsoft.NETCore.App
# 10.x isn't present. The apphost gives a clearer "framework missing" error
# than we could synthesize, so we just nudge the user here.
if ! command -v dotnet >/dev/null 2>&1; then
    echo "WARNING: 'dotnet' not in PATH. The Native bundle requires .NET 10" >&2
    echo "         installed system-wide. The Player Flatpak (when available)" >&2
    echo "         bundles its own runtime and removes this requirement." >&2
elif ! dotnet --list-runtimes 2>/dev/null | grep -q '^Microsoft.NETCore.App 10\.'; then
    echo "WARNING: .NET 10 runtime not detected in 'dotnet --list-runtimes'." >&2
    echo "         Install Microsoft.NETCore.App 10.x or use the Player Flatpak" >&2
    echo "         (when available)." >&2
fi

# ---- copy binary deps -> $DATA_DST ----------------------------------------
mkdir -p "$DATA_DST"
echo "==> Deploying binary deps to $DATA_DST"

for dir in Bin; do
    if [ -d "$SRC/$dir" ]; then
        rm -rf "$DATA_DST/$dir"
        cp -a "$SRC/$dir" "$DATA_DST/$dir"
        echo "  Replaced $DATA_DST/$dir"
    fi
done

cp -f "$SRC/Interim" "$DATA_DST/Interim"
chmod +x "$DATA_DST/Interim"
echo "  Updated  $DATA_DST/Interim"

# ---- start-menu integration ----------------------------------------------
ICON_SRC="$ARCHIVE_DIR/icons"
ICON_DST_ROOT="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor"
APPS_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
DESKTOP_FILE="$APPS_DIR/pulsar.desktop"

if [ -d "$ICON_SRC" ]; then
    mkdir -p "$APPS_DIR"
    for size_dir in "$ICON_SRC"/*/; do
        size="$(basename "$size_dir")"
        src_png="$size_dir/pulsar.png"
        [ -f "$src_png" ] || continue
        dst_dir="$ICON_DST_ROOT/${size}x${size}/apps"
        mkdir -p "$dst_dir"
        cp -f "$src_png" "$dst_dir/pulsar.png"
    done

    # The desktop entry passes -keepintro so the launcher keeps the
    # current default behaviour, but the user can edit the .desktop file
    # to remove or extend the args list.
    cat > "$DESKTOP_FILE" <<DESKTOP_EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=Pulsar
GenericName=Space Engineers Launcher (Native Linux)
Comment=Space Engineers Launcher (Native Linux)
Exec=$DATA_DST/Interim -keepintro
Icon=pulsar
Terminal=false
Categories=Game;
Keywords=SpaceEngineers;Pulsar;Game;
DESKTOP_EOF
    chmod 644 "$DESKTOP_FILE"
    echo "  Installed $DESKTOP_FILE"

    if command -v update-desktop-database >/dev/null 2>&1; then
        update-desktop-database -q "$APPS_DIR" 2>/dev/null || true
    fi
    if command -v gtk-update-icon-cache >/dev/null 2>&1; then
        gtk-update-icon-cache -q -t -f "$ICON_DST_ROOT" 2>/dev/null || true
    fi
fi

echo
echo "Done. Run the game with:"
echo "    $DATA_DST/Interim"
echo "Or look for 'Pulsar' in your start menu (Games)."
EOF
chmod +x "$PKG_ROOT/install.sh"

# ---- generate uninstall.sh -------------------------------------------------

cat > "$PKG_ROOT/uninstall.sh" <<'EOF'
#!/usr/bin/env bash
# uninstall.sh - Native developer bundle. Wipes the bundle-installed binary
# deps from ~/.local/share/Pulsar/ (Bin/, Interim) entirely, and
# scrubs the bundle-installed parts of ~/.config/Pulsar/ plus the XDG menu
# entry and pulsar icons. PRESERVES the user-managed entries under
# ~/.config/Pulsar/ entirely:
#   - config.xml
#   - Sources/      (entire subtree, including any cached PluginHub builds)
#   - Local/        (entire subtree, including user-patched plugin DLLs)
#   - Profiles/     (entire subtree)
# Intended for clean swaps to/from the Player Flatpak without losing user
# state.
#
# Usage:   ./uninstall.sh
# Env-var override:
#   PULSAR_DATA_DIR target dir for binary deps (default: ~/.local/share/Pulsar)
#   PULSAR_DIR      target dir for user state  (default: ~/.config/Pulsar)
#   XDG_DATA_HOME   menu/icon root             (default: ~/.local/share)

set -euo pipefail

DATA_DST="${PULSAR_DATA_DIR:-${XDG_DATA_HOME:-$HOME/.local/share}/Pulsar}"
DST="${PULSAR_DIR:-$HOME/.config/Pulsar}"
APPS_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
ICON_DST_ROOT="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor"

if pgrep -x Interim >/dev/null 2>&1; then
    echo "ERROR: Interim is running. Stop it before uninstalling (pkill -x Interim)." >&2
    exit 1
fi

if [ -d "$DATA_DST" ]; then
    echo "==> Removing $DATA_DST"
    rm -rf "$DATA_DST"
else
    echo "==> $DATA_DST not present - skipping"
fi

if [ -d "$DST" ]; then
    echo "==> Cleaning $DST (preserving config.xml, Sources/, Local/, Profiles/)"
    shopt -s dotglob nullglob
    for entry in "$DST"/*; do
        name="$(basename "$entry")"
        case "$name" in
            config.xml|Sources|Local|Profiles)
                echo "    keep  $name"
                ;;
            *)
                rm -rf "$entry"
                echo "    rm    $name"
                ;;
        esac
    done
    shopt -u dotglob nullglob
else
    echo "==> $DST not present - skipping"
fi

DESKTOP_FILE="$APPS_DIR/pulsar.desktop"
if [ -f "$DESKTOP_FILE" ]; then
    rm -f "$DESKTOP_FILE"
    echo "==> Removed $DESKTOP_FILE"
fi

shopt -s nullglob
for png in "$ICON_DST_ROOT"/*/apps/pulsar.png; do
    rm -f "$png"
    echo "==> Removed $png"
done
shopt -u nullglob

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q "$APPS_DIR" 2>/dev/null || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q -t -f "$ICON_DST_ROOT" 2>/dev/null || true
fi

echo
echo "Done."
EOF
chmod +x "$PKG_ROOT/uninstall.sh"

# ---- leak check ------------------------------------------------------------

echo "==> Verifying staged tree has no build-machine path references"
LEAK_PATTERNS=(
    "$PULSAR_REPO_DIR"
    "$HOME/.nuget"
    "$HOME/.dotnet"
)
LEAK_HITS=""
for pat in "${LEAK_PATTERNS[@]}"; do
    [ -z "$pat" ] && continue
    [ "$pat" = "/" ] && continue
    if hits="$(grep -rlIF -- "$pat" "$PKG_ROOT" 2>/dev/null)"; then
        if [ -n "$hits" ]; then
            LEAK_HITS+=$'\n'"  pattern: $pat"$'\n'"$(printf '    %s\n' $hits)"
        fi
    fi
done
if [ -n "$LEAK_HITS" ]; then
    echo "ERROR: build-tree paths leaked into the staged bundle (text files):" >&2
    echo "$LEAK_HITS" >&2
    exit 1
fi

# ---- README -----------------------------------------------------------------

cat > "$PKG_ROOT/README.txt" <<EOF
PulsarForLinux-Native ($BUILD_DATE.$GIT_HASH)
==============================================

Developer bundle: Pulsar + Space Engineers run on the host directly.
The .NET 10 runtime is required to be installed system-wide on the host
(framework-dependent publish, debuggable with the host's stock
dotnet/lldb/gdb).

se-dotnet-compat and se-linux-compat (LinuxCompat) are NOT vendored:
Pulsar seeds a default sources.xml enabling StarCpt/PluginHub on first
run, then discovers + compiles both from source. LinuxCompat delivers
all native binary dependencies in its Assets/ directory.

Compare to the Player Flatpak (when available): the Flatpak is fully
self-contained, ABI-stable across distros, but harder to attach a debugger
to (sandboxed .NET runtime). Use the Player Flatpak for gameplay; use the
Native developer bundle for plugin development.

Only deploy ONE of the two bundles at a time.

Prerequisites
-------------
- Steam, with Space Engineers installed.
- .NET 10 runtime installed system-wide (Microsoft.NETCore.App 10.x).
- A Vulkan-capable GPU + drivers.
- Outbound HTTPS to GitHub on first launch (Pulsar fetches
  se-dotnet-compat and se-linux-compat from PluginHub and compiles
  them locally).

Quick start
-----------
1. Extract:
       7z x PulsarForLinux-Native.$BUILD_DATE.$GIT_HASH.7z
2. Deploy:
       cd PulsarForLinux-Native
       ./install.sh
3. Run:
       ~/.local/share/Pulsar/Interim
   or click "Pulsar" in your start menu (Games).

The desktop entry passes -keepintro so the menu shortcut keeps the
launcher's previous default behaviour. Edit ~/.local/share/applications/
pulsar.desktop to drop or change those args.

To switch to the Player Flatpak (when available), run ./uninstall.sh first.
It removes ~/.local/share/Pulsar/ entirely and removes the bundle-installed
files from ~/.config/Pulsar/, but PRESERVES your config.xml, Sources/,
Local/, and Profiles/ entirely so profiles and side-loaded plugins
survive the swap.

Files
-----
  install.sh               Deploys binary deps to ~/.local/share/Pulsar/.
  uninstall.sh            Removes ~/.local/share/Pulsar/ entirely, removes
                          bundle files from ~/.config/Pulsar/ but preserves
                          config.xml, Sources/, Local/, Profiles/.
  README.txt              This file.
  icons/<size>/pulsar.png Hicolor icon set for the menu entry.
  Pulsar/                 Staging source tree:
    Interim                  Launcher (sets LD_LIBRARY_PATH + execs apphost).
                             Deploys to ~/.local/share/Pulsar/Interim.
    Bin/                     Pulsar Interim framework-dependent publish.
                             Deploys to ~/.local/share/Pulsar/Bin/.
                             Pulsar itself seeds Sources/sources.xml and
                             Profiles/Current.xml on first launch.
EOF

# ---- pack -------------------------------------------------------------------

ARCHIVE_NAME="PulsarForLinux-$VARIANT.$BUILD_DATE.$GIT_HASH.7z"
ARCHIVE_PATH="$OUTPUT_DIR/$ARCHIVE_NAME"

rm -f "$ARCHIVE_PATH"

echo "==> Packing $ARCHIVE_NAME"
( cd "$BUILD_DIR" && 7z a -t7z -mx=9 -bso0 -bsp1 "$ARCHIVE_PATH" "PulsarForLinux-$VARIANT" >/dev/null )

echo
echo "Done: $ARCHIVE_PATH"
ls -lh "$ARCHIVE_PATH"

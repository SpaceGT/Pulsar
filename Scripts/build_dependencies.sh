#!/usr/bin/env bash
# build_dependencies.sh
#
# Top-level orchestrator that populates build/Libraries/ with every native
# / managed dependency Pulsar ships. The staged tree is what
# Legacy/Legacy.csproj's AfterBuild/AfterPublish targets copy next to the
# Interim apphost, and what Shared/Shared.csproj references for the
# managed Steamworks.NET assembly.
#
# Pipeline (in order):
#
#   1. build_ffmpeg.sh           FFmpeg 8.1 (libav*.so* / libsw*.so*)
#   2. build_dxvk.sh             DXVK Native v2.7.1
#                                (libdxvk_d3d11.so + libdxvk_dxgi.so + .0 links)
#   3. fetch_native_wrappers.sh  linux-native-wrappers release download
#                                (libD3DCompiler.so, libHavok.so,
#                                 libRecastDetour.so, libVRageNative.so)
#   4. build_steamworks_net.sh   Steamworks.NET.dll
#   5. Vendor copy:              libEOSSDK-Linux-Shipping.so + libsteam_api.so
#                                (proprietary, committed under Vendor/)
#   6. License copy:             Scripts/Licenses/*.txt -> LICENSES/ subdir
#
# After every step succeeds, a final assertion verifies that every expected
# artefact landed in build/Libraries/ and aborts otherwise so the failure
# surfaces here, not deep inside `dotnet publish`.
#
# Usage:
#   ./build_dependencies.sh                 Build the full set.
#   ./build_dependencies.sh --clean         Pass --clean to every sub-script.
#   ./build_dependencies.sh --only=ffmpeg,dxvk
#                                           Only run the listed sub-builds.
#                                           Vendor + license copies always run.
#   ./build_dependencies.sh --skip=dxvk     Run every sub-build except the listed ones.
#
# Env-var overrides (defaults shown):
#   PULSAR_REPO_DIR = <dir of this script>/..
#   BUILD_DIR       = $PULSAR_REPO_DIR/build
#   LIBRARIES_DIR   = $BUILD_DIR/Libraries
#   VENDOR_DIR      = $PULSAR_REPO_DIR/Vendor
#   LICENSES_SRC    = $PULSAR_REPO_DIR/Scripts/Licenses

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PULSAR_REPO_DIR="${PULSAR_REPO_DIR:-$(cd "$SCRIPT_DIR/.." && pwd)}"
BUILD_DIR="${BUILD_DIR:-$PULSAR_REPO_DIR/build}"
LIBRARIES_DIR="${LIBRARIES_DIR:-$BUILD_DIR/Libraries}"
VENDOR_DIR="${VENDOR_DIR:-$PULSAR_REPO_DIR/Vendor}"
LICENSES_SRC="${LICENSES_SRC:-$PULSAR_REPO_DIR/Scripts/Licenses}"

export PULSAR_REPO_DIR BUILD_DIR LIBRARIES_DIR

# ---- arg parsing ------------------------------------------------------------

CLEAN_ARGS=()
ONLY=""
SKIP=""

for arg in "$@"; do
    case "$arg" in
        --clean)    CLEAN_ARGS+=("--clean") ;;
        --only=*)   ONLY="${arg#--only=}" ;;
        --skip=*)   SKIP="${arg#--skip=}" ;;
        -h|--help)  sed -n '2,40p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "ERROR: unknown arg: $arg" >&2; exit 2 ;;
    esac
done

want_step() {
    # want_step <name> -> 0 if the step should run, 1 otherwise.
    local name="$1"
    if [ -n "$ONLY" ]; then
        case ",$ONLY," in
            *,"$name",*) return 0 ;;
            *) return 1 ;;
        esac
    fi
    if [ -n "$SKIP" ]; then
        case ",$SKIP," in
            *,"$name",*) return 1 ;;
        esac
    fi
    return 0
}

# ---- preflight --------------------------------------------------------------

mkdir -p "$LIBRARIES_DIR/LICENSES"

[ -d "$VENDOR_DIR" ] || {
    echo "ERROR: $VENDOR_DIR not found." >&2
    echo "       Vendor/ holds the committed proprietary SDK blobs" >&2
    echo "       (libEOSSDK-Linux-Shipping.so + libsteam_api.so)." >&2
    exit 1
}

[ -d "$LICENSES_SRC" ] || {
    echo "ERROR: $LICENSES_SRC not found." >&2
    echo "       Scripts/Licenses/ holds the committed third-party license texts." >&2
    exit 1
}

echo "==> Pulsar repo : $PULSAR_REPO_DIR"
echo "==> Build dir   : $BUILD_DIR"
echo "==> Staging dir : $LIBRARIES_DIR"

# ---- 1..4. per-dependency build scripts ------------------------------------

run_step() {
    local name="$1"; shift
    local script="$1"; shift
    if ! want_step "$name"; then
        echo
        echo "==> SKIP $name (filtered)"
        return 0
    fi
    echo
    echo "############################################################"
    echo "# build_dependencies: $name"
    echo "############################################################"
    bash "$script" "${CLEAN_ARGS[@]}" "$@"
}

run_step ffmpeg          "$SCRIPT_DIR/build_ffmpeg.sh"
run_step dxvk            "$SCRIPT_DIR/build_dxvk.sh"
run_step native-wrappers "$SCRIPT_DIR/fetch_native_wrappers.sh"
run_step steamworks-net  "$SCRIPT_DIR/build_steamworks_net.sh"

# ---- 5. Vendor blobs --------------------------------------------------------

echo
echo "############################################################"
echo "# build_dependencies: vendor blobs (Vendor/ -> Libraries/)"
echo "############################################################"
for blob in libEOSSDK-Linux-Shipping.so libsteam_api.so; do
    src="$VENDOR_DIR/$blob"
    if [ ! -f "$src" ]; then
        echo "ERROR: missing vendor blob: $src" >&2
        echo "       These SDKs are proprietary and must stay committed under Vendor/." >&2
        exit 1
    fi
    install -m 0755 "$src" "$LIBRARIES_DIR/$blob"
    echo "  copied $blob"
done

# ---- 6. Licenses ------------------------------------------------------------

echo
echo "############################################################"
echo "# build_dependencies: licenses (Scripts/Licenses/ -> Libraries/LICENSES/)"
echo "############################################################"
shopt -s nullglob
for f in "$LICENSES_SRC"/*.txt; do
    install -m 0644 "$f" "$LIBRARIES_DIR/LICENSES/$(basename "$f")"
    echo "  copied $(basename "$f")"
done
shopt -u nullglob

# ---- 7. final assertion ----------------------------------------------------
# Confirm every artefact every consumer expects is present. Missing files
# here are far easier to debug than a cryptic dotnet publish failure later.

EXPECTED_FILES=(
    # FFmpeg
    libavcodec.so libavcodec.so.62 libavcodec.so.62.28.100
    libavformat.so libavformat.so.62 libavformat.so.62.12.100
    libavutil.so libavutil.so.60 libavutil.so.60.26.100
    libswresample.so libswresample.so.6 libswresample.so.6.3.100
    libswscale.so libswscale.so.9 libswscale.so.9.5.100
    # DXVK
    libdxvk_d3d11.so libdxvk_d3d11.so.0
    libdxvk_dxgi.so  libdxvk_dxgi.so.0
    # Native wrappers
    libD3DCompiler.so libHavok.so libRecastDetour.so libVRageNative.so
    # Vendor
    libEOSSDK-Linux-Shipping.so libsteam_api.so
    # Managed
    Steamworks.NET.dll
    # Licenses
    LICENSES/DXVK-LICENSE.txt
    LICENSES/EOS-NOTICE.txt
    LICENSES/FFmpeg-LGPL-2.1.txt
    LICENSES/FFmpeg-README.txt
    LICENSES/README.txt
    LICENSES/Steam-NOTICE.txt
    LICENSES/Steamworks.NET-LICENSE.txt
)

MISSING=0
for rel in "${EXPECTED_FILES[@]}"; do
    if [ ! -e "$LIBRARIES_DIR/$rel" ]; then
        echo "MISSING: $LIBRARIES_DIR/$rel" >&2
        MISSING=1
    fi
done
if [ "$MISSING" = "1" ]; then
    if [ -n "$ONLY" ] || [ -n "$SKIP" ]; then
        echo "Note: --only/--skip filters were active; partial staging is expected." >&2
        exit 1
    fi
    echo "ERROR: dependency staging is incomplete." >&2
    exit 1
fi

echo
echo "==> All expected artefacts present in $LIBRARIES_DIR"
( cd "$LIBRARIES_DIR" && ls -lh | sed 's/^/  /' )

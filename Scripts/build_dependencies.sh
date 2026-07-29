#!/usr/bin/env bash
# build_dependencies.sh
#
# Top-level orchestrator that populates build/Libraries/ with every native
# / managed dependency Pulsar ships. The staged tree is what
# Legacy/Legacy.csproj's AfterBuild/AfterPublish targets copy next to the
# Interim apphost, and what Shared/Shared.csproj references for the
# managed Steamworks.NET assembly.
#
# Nothing is compiled here any more. Every artefact is downloaded from a
# GitHub release of the repo that builds it, so a clean Pulsar build no
# longer waits ~15 minutes on FFmpeg and DXVK, and the binaries are
# byte-for-byte the same ones Magnetar ships.
#
# Pipeline (in order):
#
#   1. fetch_linux_dependencies.sh   CometWorks/linux-dependencies release
#                                    (FFmpeg 8.1, DXVK Native 2.7.1,
#                                     Steamworks.NET.dll, libEOSSDK-Linux-
#                                     Shipping.so, libsteam_api.so, LICENSES/)
#   2. fetch_native_wrappers.sh      CometWorks/linux-native-wrappers release
#                                    (libD3DCompiler.so, libHavok.so,
#                                     libRecastDetour.so, libVRageNative.so)
#
# The two releases are kept separate on purpose: the PE-loader wrappers change
# far more often than FFmpeg or DXVK do, so bundling them together would put a
# full FFmpeg + DXVK rebuild in front of every wrapper fix.
#
# After every step succeeds, a final assertion verifies that every expected
# artefact landed in build/Libraries/ and aborts otherwise so the failure
# surfaces here, not deep inside `dotnet publish`.
#
# Usage:
#   ./build_dependencies.sh                 Fetch the full set.
#   ./build_dependencies.sh --clean         Pass --clean to every sub-script
#                                           (forces a fresh download).
#   ./build_dependencies.sh --only=linux-dependencies
#                                           Only run the listed sub-fetches.
#   ./build_dependencies.sh --skip=native-wrappers
#                                           Run every sub-fetch except the
#                                           listed ones.
#
# Env-var overrides (defaults shown):
#   PULSAR_REPO_DIR = <dir of this script>/..
#   BUILD_DIR       = $PULSAR_REPO_DIR/build
#   LIBRARIES_DIR   = $BUILD_DIR/Libraries
#
# To pin exact upstream releases (recommended for reproducible CI), set
# LINUX_DEPENDENCIES_TAG and NATIVE_WRAPPERS_TAG; see the sub-scripts.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PULSAR_REPO_DIR="${PULSAR_REPO_DIR:-$(cd "$SCRIPT_DIR/.." && pwd)}"
BUILD_DIR="${BUILD_DIR:-$PULSAR_REPO_DIR/build}"
LIBRARIES_DIR="${LIBRARIES_DIR:-$BUILD_DIR/Libraries}"

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
        -h|--help)  sed -n '2,49p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "ERROR: unknown arg: $arg" >&2; exit 2 ;;
    esac
done

# Reject unknown step names, so a typo doesn't silently skip everything and
# then report only that staging is incomplete.
STEP_NAMES="linux-dependencies native-wrappers"
for spec in "$ONLY" "$SKIP"; do
    [ -n "$spec" ] || continue
    IFS=',' read -ra names <<< "$spec"
    for name in "${names[@]}"; do
        case " $STEP_NAMES " in
            *" $name "*) ;;
            *) echo "ERROR: unknown step name: $name" >&2
               echo "       Valid names: $STEP_NAMES" >&2
               exit 2 ;;
        esac
    done
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

mkdir -p "$LIBRARIES_DIR"

echo "==> Pulsar repo : $PULSAR_REPO_DIR"
echo "==> Build dir   : $BUILD_DIR"
echo "==> Staging dir : $LIBRARIES_DIR"

# ---- 1..2. per-release fetch scripts ---------------------------------------

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
    bash "$script" "${CLEAN_ARGS[@]+"${CLEAN_ARGS[@]}"}" "$@"
}

run_step linux-dependencies "$SCRIPT_DIR/fetch_linux_dependencies.sh"
run_step native-wrappers    "$SCRIPT_DIR/fetch_native_wrappers.sh"

# ---- 3. final assertion ----------------------------------------------------
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
    # Proprietary SDK runtimes
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
        exit 0
    fi
    echo "ERROR: dependency staging is incomplete." >&2
    exit 1
fi

echo
echo "==> All expected artefacts present in $LIBRARIES_DIR"
( cd "$LIBRARIES_DIR" && ls -lh | sed 's/^/  /' )

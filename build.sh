#!/usr/bin/env bash
# build.sh
#
# Top-level orchestrator: builds & packages every distributable in this repo.
#
# Pipeline (in order):
#   0. Scripts/build_dependencies.sh
#         build/Libraries/ <- FFmpeg + DXVK + native wrappers + Steamworks.NET
#         + Vendor/ blobs (EOS SDK, Steamworks SDK) + Scripts/Licenses/.
#   1. Scripts/package_pulsar_for_linux_native.sh
#         developer 7z bundle -> dist/PulsarForLinux-Native.<date>.<sha>.7z
#   2. Scripts/package_pulsar_for_linux_flatpak.sh
#         player flatpak bundle -> dist/io.github.SpaceGT.Pulsar.<date>.<sha>.flatpak
#
# The intermediate packaging scripts share BUILD_DIR/OUTPUT_DIR via env so the
# whole pipeline lands under <repo>/build/ (gitignored cache) and <repo>/dist/
# (gitignored output).
#
# Usage:
#   ./build.sh                  Build everything.
#   ./build.sh --skip-deps      Skip phase 0 (assume build/Libraries/ is current).
#   ./build.sh --deps-only      Only run phase 0 (no packaging).
#   ./build.sh --native-only    Skip the Flatpak step (faster dev iteration).
#   ./build.sh --flatpak-only   Skip the 7z native bundle.
#
# Env-var overrides forwarded to the sub-scripts (defaults shown):
#   PULSAR_REPO_DIR=<dir of this script>
#   BUILD_DIR=$PULSAR_REPO_DIR/build
#   OUTPUT_DIR=$PULSAR_REPO_DIR/dist

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$SCRIPT_DIR/Scripts"

PULSAR_REPO_DIR="${PULSAR_REPO_DIR:-$SCRIPT_DIR}"
BUILD_DIR="${BUILD_DIR:-$PULSAR_REPO_DIR/build}"
OUTPUT_DIR="${OUTPUT_DIR:-$PULSAR_REPO_DIR/dist}"

export PULSAR_REPO_DIR BUILD_DIR OUTPUT_DIR

# ---- arg parsing ------------------------------------------------------------

DO_DEPS=1
DO_NATIVE=1
DO_FLATPAK=1

for arg in "$@"; do
    case "$arg" in
        --skip-deps)    DO_DEPS=0 ;;
        --deps-only)    DO_NATIVE=0; DO_FLATPAK=0 ;;
        --native-only)  DO_FLATPAK=0 ;;
        --flatpak-only) DO_NATIVE=0 ;;
        -h|--help)      sed -n '2,/^$/p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "ERROR: unknown arg: $arg" >&2; exit 2 ;;
    esac
done

# ---- 0. Build dependencies -> build/Libraries/ -----------------------------

if [ "$DO_DEPS" = "1" ]; then
    echo
    echo "############################################################"
    echo "# 0/2  Building dependencies -> $BUILD_DIR/Libraries/"
    echo "############################################################"
    bash "$SCRIPTS_DIR/build_dependencies.sh"
else
    echo "==> Skipping dependency build (--skip-deps)"
fi

# ---- 1. Native developer 7z -------------------------------------------------

if [ "$DO_NATIVE" = "1" ]; then
    echo
    echo "############################################################"
    echo "# 1/2  Packaging native developer bundle -> $OUTPUT_DIR/"
    echo "############################################################"
    bash "$SCRIPTS_DIR/package_pulsar_for_linux_native.sh"
else
    echo "==> Skipping native bundle (--flatpak-only)"
fi

# ---- 2. Player Flatpak ------------------------------------------------------

if [ "$DO_FLATPAK" = "1" ]; then
    echo
    echo "############################################################"
    echo "# 2/2  Packaging player Flatpak bundle -> $OUTPUT_DIR/"
    echo "############################################################"
    bash "$SCRIPTS_DIR/package_pulsar_for_linux_flatpak.sh"
else
    echo "==> Skipping Flatpak bundle (--native-only)"
fi

# ---- summary ----------------------------------------------------------------

echo
echo "############################################################"
echo "# DONE  Artefacts in $OUTPUT_DIR/"
echo "############################################################"
shopt -s nullglob
artefacts=("$OUTPUT_DIR"/*.7z "$OUTPUT_DIR"/*.flatpak)
shopt -u nullglob
if [ ${#artefacts[@]} -eq 0 ]; then
    echo "  (none)"
else
    for f in "${artefacts[@]}"; do
        sz="$(du -h "$f" | awk '{print $1}')"
        printf '  %-8s  %s\n' "$sz" "$(basename "$f")"
    done
fi

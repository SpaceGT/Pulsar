#!/usr/bin/env bash
# build_dxvk.sh
#
# Builds DXVK Native (the ELF/Linux variant of DXVK) from upstream sources
# at https://github.com/doitsujin/dxvk, then installs the two .so files
# Pulsar consumes into the build/Libraries staging folder:
#
#   libdxvk_d3d11.so   (SONAME libdxvk_d3d11.so.0; we ship both via a symlink)
#   libdxvk_dxgi.so    (SONAME libdxvk_dxgi.so.0;  same)
#
# DXVK Native is built by running upstream's package-native.sh helper at the
# pinned tag; we pass --64-only --no-package so the 32-bit build is skipped
# (Pulsar is x86_64-only) and the .tar.gz packaging step is skipped (we
# only need the installed .so files).
#
# Source layout (under the gitignored build/ folder of this repo):
#
#   build/
#   ├── Libraries/                 staging dir all dep scripts populate
#   ├── dxvk/                      shallow clone of doitsujin/dxvk at tag
#   ├── dxvk-out/                  package-native.sh destdir (recreated)
#   └── dxvk.stamp                 last-built tag (cache key)
#
# Usage:
#   ./build_dxvk.sh           Build (or no-op if cached).
#   ./build_dxvk.sh --clean   Wipe build dirs and rebuild from scratch.
#
# Env-var overrides (defaults shown):
#   DXVK_VERSION  = 2.7.1
#   DXVK_REPO     = https://github.com/doitsujin/dxvk.git
#   BUILD_DIR     = <repo>/build
#   LIBRARIES_DIR = $BUILD_DIR/Libraries
#   JOBS          = $(nproc)
#
# Requirements: git, meson (>=0.58), ninja, glslang (glslangValidator),
# pkg-config, gcc, g++, patchelf.
# Typical Debian/Ubuntu install:
#   sudo apt install git meson ninja-build glslang-tools libvulkan-dev \
#                    pkg-config build-essential patchelf

set -euo pipefail

DXVK_VERSION="${DXVK_VERSION:-2.7.1}"
DXVK_REPO="${DXVK_REPO:-https://github.com/doitsujin/dxvk.git}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR_DEFAULT="$REPO_DIR/build"

BUILD_DIR="${BUILD_DIR:-$BUILD_DIR_DEFAULT}"
LIBRARIES_DIR="${LIBRARIES_DIR:-$BUILD_DIR/Libraries}"
JOBS="${JOBS:-$(nproc)}"

DXVK_SRC_DIR="$BUILD_DIR/dxvk"
DXVK_OUT_DIR="$BUILD_DIR/dxvk-out"
STAMP_FILE="$BUILD_DIR/dxvk.stamp"

EXPECTED_LIBS=(libdxvk_d3d11.so libdxvk_dxgi.so)

CLEAN=0
for arg in "$@"; do
    case "$arg" in
        --clean)   CLEAN=1 ;;
        -h|--help) sed -n '2,42p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "ERROR: unknown arg: $arg" >&2; exit 2 ;;
    esac
done

# ---- preflight --------------------------------------------------------------

for tool in git meson ninja glslangValidator pkg-config gcc g++ patchelf; do
    command -v "$tool" >/dev/null 2>&1 || {
        echo "ERROR: required tool not found in PATH: $tool" >&2
        exit 1
    }
done

mkdir -p "$BUILD_DIR" "$LIBRARIES_DIR"

# ---- cache check ------------------------------------------------------------

ALL_LIBS_PRESENT=1
for lib in "${EXPECTED_LIBS[@]}"; do
    [ -f "$LIBRARIES_DIR/$lib" ] || ALL_LIBS_PRESENT=0
done

if [ "$CLEAN" = "1" ]; then
    rm -rf "$DXVK_SRC_DIR" "$DXVK_OUT_DIR"
elif [ "$ALL_LIBS_PRESENT" = "1" ] \
   && [ -f "$STAMP_FILE" ] \
   && [ "$(cat "$STAMP_FILE")" = "$DXVK_VERSION" ]; then
    echo "==> Cached build matches DXVK $DXVK_VERSION; skipping rebuild"
    echo "==> DXVK libs already in $LIBRARIES_DIR:"
    ( cd "$LIBRARIES_DIR" && ls -1 libdxvk_*.so* )
    exit 0
fi

# ---- clone (cached) --------------------------------------------------------

if [ ! -d "$DXVK_SRC_DIR/.git" ]; then
    echo "==> Cloning $DXVK_REPO @ v$DXVK_VERSION -> $DXVK_SRC_DIR"
    rm -rf "$DXVK_SRC_DIR"
    git clone --branch "v$DXVK_VERSION" --depth 1 --recurse-submodules \
        --shallow-submodules "$DXVK_REPO" "$DXVK_SRC_DIR"
else
    echo "==> Re-using cached clone at $DXVK_SRC_DIR"
    git -C "$DXVK_SRC_DIR" fetch --depth 1 origin "tag" "v$DXVK_VERSION" || true
    git -C "$DXVK_SRC_DIR" -c advice.detachedHead=false checkout "v$DXVK_VERSION"
    git -C "$DXVK_SRC_DIR" submodule update --init --recursive --depth 1
fi

# ---- build via upstream package-native.sh ----------------------------------
# package-native.sh refuses to run if its destdir/dxvk-native-<ver>/ already
# exists, so wipe the destdir on every run. The package-native.sh body is
# tiny (<200 lines) and stable across recent DXVK versions; we shell out to
# it verbatim rather than re-implementing the meson invocation, so any
# future upstream tweaks (extra meson flags, etc.) just work.

rm -rf "$DXVK_OUT_DIR"
mkdir -p "$DXVK_OUT_DIR"

echo "==> Running DXVK package-native.sh (64-only, no-package)"
(
    cd "$DXVK_SRC_DIR"
    NINJA_OPTS="-j$JOBS" \
        ./package-native.sh "$DXVK_VERSION" "$DXVK_OUT_DIR" --64-only --no-package
)

# ---- locate + stage the .so outputs ----------------------------------------
# package-native.sh installs into $DXVK_OUT_DIR/dxvk-native-$DXVK_VERSION/usr/lib/.
# We `find` rather than hard-code the path so a future upstream rearrangement
# (e.g. usr/lib64/, multiarch subdir) doesn't silently break the script.

echo "==> Staging DXVK libs into $LIBRARIES_DIR"
for lib in "${EXPECTED_LIBS[@]}"; do
    # `-L` so find follows the unversioned .so symlink chain to the real
    # versioned file (libdxvk_*.so -> libdxvk_*.so.0 -> libdxvk_*.so.0.<ver>).
    # `install` then copies the dereferenced file under the bare .so name,
    # matching the pre-existing Libraries/ layout.
    src="$(find -L "$DXVK_OUT_DIR" -type f -name "$lib" -print -quit)"
    if [ -z "$src" ]; then
        echo "ERROR: package-native.sh did not produce $lib under $DXVK_OUT_DIR" >&2
        exit 1
    fi
    install -m 0755 "$src" "$LIBRARIES_DIR/$lib"
    # Recreate the SONAME alias as a symlink so anything dlopen()ing the
    # SONAME directly (rare, but matches the pre-existing layout) finds it.
    ln -sfn "$lib" "$LIBRARIES_DIR/${lib}.0"
done

# ---- patch DT_RUNPATH=$ORIGIN onto each .so --------------------------------
# Parity with the FFmpeg payload: with DT_RUNPATH=$ORIGIN baked in, ld.so
# resolves any cross-DXVK NEEDED entries (e.g. libdxvk_d3d11 -> libdxvk_dxgi)
# via the loaded lib's own directory, so the Pulsar launcher doesn't need to
# prepend Bin/ to LD_LIBRARY_PATH.

echo "==> Patching DT_RUNPATH=\$ORIGIN onto DXVK libs"
for lib in "${EXPECTED_LIBS[@]}"; do
    patchelf --set-rpath '$ORIGIN' "$LIBRARIES_DIR/$lib"
done

# ---- update cache stamp ----------------------------------------------------

printf '%s\n' "$DXVK_VERSION" > "$STAMP_FILE"

echo
echo "==> Staged DXVK libs into $LIBRARIES_DIR:"
( cd "$LIBRARIES_DIR" && ls -1 libdxvk_*.so* )

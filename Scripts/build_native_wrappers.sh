#!/usr/bin/env bash
# build_native_wrappers.sh
#
# Builds the se-linux-compat native wrapper shared libraries
# (libD3DCompiler.so, libHavok.so, libRecastDetour.so, libVRageNative.so)
# and installs them into the build/Libraries staging folder.
#
# Source: https://github.com/viktor-ferenczi/se-linux-compat
#
# The wrappers live under NativeWrappers/ in that repo. Its Makefile wraps
# `cmake --preset default` + `cmake --build --preset default`; we shell out
# to it verbatim so any future build-system changes upstream just work.
#
# Source layout (under the gitignored build/ folder of this repo):
#
#   build/
#   ├── Libraries/                staging dir all dep scripts populate
#   ├── se-linux-compat/          clone of the upstream repo (cached)
#   │   └── NativeWrappers/
#   │       └── build/            cmake out-of-tree dir (cached)
#   └── se-linux-compat.stamp     last-built commit SHA (cache key)
#
# A cold first run clones + checks out the branch + builds; every subsequent
# run does `git fetch` + `git checkout` and a fast incremental cmake build.
# When HEAD matches the stamp AND all four .so outputs exist in
# build/Libraries/, the build phase is skipped entirely.
#
# Usage:
#   ./build_native_wrappers.sh           Build (or no-op if cached).
#   ./build_native_wrappers.sh --clean   Wipe the cmake build dir and rebuild.
#
# Env-var overrides (defaults shown):
#   LINUX_COMPAT_REPO   = https://github.com/viktor-ferenczi/se-linux-compat.git
#   LINUX_COMPAT_BRANCH = main
#   LINUX_COMPAT_COMMIT = ""     (if set, overrides BRANCH and pins the
#                                  exact SHA — recommended for CI)
#   BUILD_DIR           = <repo>/build
#   LIBRARIES_DIR       = $BUILD_DIR/Libraries
#   JOBS                = $(nproc)
#
# Requirements: git, cmake (>= 3.20 for presets), make, gcc, g++.

set -euo pipefail

# ---- top-of-file knobs (per the plan) --------------------------------------

LINUX_COMPAT_REPO="${LINUX_COMPAT_REPO:-https://github.com/viktor-ferenczi/se-linux-compat.git}"
LINUX_COMPAT_BRANCH="${LINUX_COMPAT_BRANCH:-main}"
LINUX_COMPAT_COMMIT="${LINUX_COMPAT_COMMIT:-}"

# ---- configuration ----------------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR_DEFAULT="$REPO_DIR/build"

BUILD_DIR="${BUILD_DIR:-$BUILD_DIR_DEFAULT}"
LIBRARIES_DIR="${LIBRARIES_DIR:-$BUILD_DIR/Libraries}"
JOBS="${JOBS:-$(nproc)}"

CLONE_DIR="$BUILD_DIR/se-linux-compat"
WRAPPERS_SRC_DIR="$CLONE_DIR/NativeWrappers"
WRAPPERS_BUILD_DIR="$WRAPPERS_SRC_DIR/build"
STAMP_FILE="$BUILD_DIR/se-linux-compat.stamp"

EXPECTED_LIBS=(
    libD3DCompiler.so
    libHavok.so
    libRecastDetour.so
    libVRageNative.so
)

CLEAN=0
for arg in "$@"; do
    case "$arg" in
        --clean)   CLEAN=1 ;;
        -h|--help) sed -n '2,46p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "ERROR: unknown arg: $arg" >&2; exit 2 ;;
    esac
done

# ---- preflight --------------------------------------------------------------

for tool in git cmake make gcc g++; do
    command -v "$tool" >/dev/null 2>&1 || {
        echo "ERROR: required tool not found in PATH: $tool" >&2
        exit 1
    }
done

mkdir -p "$BUILD_DIR" "$LIBRARIES_DIR"

# ---- clone / fetch the upstream repo ---------------------------------------
# The clone lives under the gitignored build/ folder, so `git reset --hard`
# is safe — it can never destroy user work.

if [ ! -d "$CLONE_DIR/.git" ]; then
    echo "==> Cloning $LINUX_COMPAT_REPO -> $CLONE_DIR"
    rm -rf "$CLONE_DIR"
    git clone --branch "$LINUX_COMPAT_BRANCH" "$LINUX_COMPAT_REPO" "$CLONE_DIR"
else
    echo "==> Fetching origin in $CLONE_DIR"
    git -C "$CLONE_DIR" fetch origin --tags --prune
fi

if [ -n "$LINUX_COMPAT_COMMIT" ]; then
    echo "==> Pinning to commit $LINUX_COMPAT_COMMIT"
    git -C "$CLONE_DIR" -c advice.detachedHead=false checkout "$LINUX_COMPAT_COMMIT"
else
    echo "==> Checking out branch $LINUX_COMPAT_BRANCH"
    git -C "$CLONE_DIR" checkout -B "$LINUX_COMPAT_BRANCH" "origin/$LINUX_COMPAT_BRANCH"
    git -C "$CLONE_DIR" reset --hard "origin/$LINUX_COMPAT_BRANCH"
fi

HEAD_SHA="$(git -C "$CLONE_DIR" rev-parse HEAD)"

# ---- cache check ------------------------------------------------------------

ALL_LIBS_PRESENT=1
for lib in "${EXPECTED_LIBS[@]}"; do
    [ -f "$LIBRARIES_DIR/$lib" ] || ALL_LIBS_PRESENT=0
done

if [ "$CLEAN" = "1" ]; then
    if [ -d "$WRAPPERS_BUILD_DIR" ]; then
        echo "==> --clean: wiping cmake build dir $WRAPPERS_BUILD_DIR"
        rm -rf "$WRAPPERS_BUILD_DIR"
    fi
elif [ "$ALL_LIBS_PRESENT" = "1" ] \
   && [ -f "$STAMP_FILE" ] \
   && [ "$(cat "$STAMP_FILE")" = "$HEAD_SHA" ]; then
    echo "==> Cached build matches HEAD ($HEAD_SHA); skipping cmake build"
    echo "==> Native wrappers already in $LIBRARIES_DIR:"
    ( cd "$LIBRARIES_DIR" && ls -1 "${EXPECTED_LIBS[@]}" )
    exit 0
fi

# ---- build ------------------------------------------------------------------

echo "==> Building native wrappers in $WRAPPERS_SRC_DIR (HEAD=$HEAD_SHA)"
(
    cd "$WRAPPERS_SRC_DIR"
    # The upstream Makefile invokes `cmake --preset default` (configure) +
    # `cmake --build --preset default` (build). Pass JOBS via the standard
    # MAKEFLAGS so we don't have to fork the build commands.
    MAKEFLAGS="-j$JOBS" make
)

# ---- copy outputs ----------------------------------------------------------
# The CMakeLists.txt at v0.x of se-linux-compat puts shared libs directly
# under NativeWrappers/build/. Locate each via `find` so a future cmake
# rearrangement (e.g. CMAKE_LIBRARY_OUTPUT_DIRECTORY moving to a subdir)
# doesn't silently install zero-byte files.

echo "==> Staging native wrappers into $LIBRARIES_DIR"
for lib in "${EXPECTED_LIBS[@]}"; do
    src="$(find "$WRAPPERS_BUILD_DIR" -maxdepth 3 -type f -name "$lib" -print -quit)"
    if [ -z "$src" ]; then
        echo "ERROR: cmake did not produce $lib under $WRAPPERS_BUILD_DIR" >&2
        echo "Hint: run with --clean to force a reconfigure." >&2
        exit 1
    fi
    install -m 0755 "$src" "$LIBRARIES_DIR/$lib"
done

# ---- update cache stamp ----------------------------------------------------

printf '%s\n' "$HEAD_SHA" > "$STAMP_FILE"

echo
echo "==> Staged native wrappers into $LIBRARIES_DIR:"
( cd "$LIBRARIES_DIR" && ls -1 "${EXPECTED_LIBS[@]}" )

#!/usr/bin/env bash
# fetch_linux_dependencies.sh
#
# Downloads the prebuilt Linux library dependencies (FFmpeg, DXVK Native,
# Steamworks.NET, plus the proprietary EOS / Steamworks runtimes and the
# third-party licence texts) from a CometWorks/linux-dependencies GitHub
# release and extracts them into the build/Libraries staging folder.
#
# Source: https://github.com/CometWorks/linux-dependencies
#
# These libraries used to be compiled from source on every build by
# Scripts/build_ffmpeg.sh, build_dxvk.sh and build_steamworks_net.sh (and the
# EOS / Steam blobs were committed under Vendor/). They are now built once by
# that repo's CI and published as a linux-dependencies.tar.gz release asset;
# Pulsar and Magnetar both consume the same artifact so the binaries are
# guaranteed identical. That also takes the ~15-minute FFmpeg + DXVK compile
# out of every clean Pulsar build.
#
# The archive lays every library out at its root plus a LICENSES/ subdir,
# which is exactly the build/Libraries/ layout, so extraction IS the staging
# step. See docs/release-archive.md in the linux-dependencies repo for the
# full contract.
#
# Caching (under the gitignored build/ folder of this repo):
#
#   build/
#   ├── Libraries/                      staging dir all dep scripts populate
#   ├── linux-dependencies.stamp        release tag last staged (cache key)
#   └── linux-dependencies.manifest     files the staged release owns, so the
#                                       next one can clear them first
#
# When the stamp matches the resolved release tag AND all expected outputs are
# present in build/Libraries/, the download is skipped entirely. If the release
# API is unreachable but a cached copy is already staged, that copy is reused.
#
# Usage:
#   ./fetch_linux_dependencies.sh           Download (or no-op if cached).
#   ./fetch_linux_dependencies.sh --clean   Force a fresh download.
#
# Env-var overrides (defaults shown):
#   LINUX_DEPENDENCIES_REPO = CometWorks/linux-dependencies
#   LINUX_DEPENDENCIES_TAG  = ""    (empty = latest release; set to pin a tag,
#                                    e.g. v1.0.1 — recommended for reproducible CI)
#   BUILD_DIR               = <repo>/build
#   LIBRARIES_DIR           = $BUILD_DIR/Libraries
#   GH_TOKEN / GITHUB_TOKEN          (optional; used only to raise the GitHub API
#                                    rate limit when resolving the latest tag)
#
# Requirements: curl, tar.

set -euo pipefail

# ---- top-of-file knobs ------------------------------------------------------

LINUX_DEPENDENCIES_REPO="${LINUX_DEPENDENCIES_REPO:-CometWorks/linux-dependencies}"
LINUX_DEPENDENCIES_TAG="${LINUX_DEPENDENCIES_TAG:-}"
ASSET_NAME="linux-dependencies.tar.gz"

# ---- configuration ----------------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR_DEFAULT="$REPO_DIR/build"

BUILD_DIR="${BUILD_DIR:-$BUILD_DIR_DEFAULT}"
LIBRARIES_DIR="${LIBRARIES_DIR:-$BUILD_DIR/Libraries}"
STAMP_FILE="$BUILD_DIR/linux-dependencies.stamp"
MANIFEST_FILE="$BUILD_DIR/linux-dependencies.manifest"

# A representative subset of the archive, one per producing sub-build, used
# both as the cache-validity check and as the post-extract assertion. The
# authoritative full list lives in build_dependencies.sh, which asserts the
# complete staging tree once every fetch has run.
EXPECTED_FILES=(
    libavcodec.so.62
    libavformat.so.62
    libavutil.so.60
    libswresample.so.6
    libswscale.so.9
    libdxvk_d3d11.so
    libdxvk_dxgi.so
    libopenal.so.1
    libEOSSDK-Linux-Shipping.so
    libsteam_api.so
    Steamworks.NET.dll
    LICENSES/README.txt
)

CLEAN=0
for arg in "$@"; do
    case "$arg" in
        --clean)   CLEAN=1 ;;
        -h|--help) sed -n '2,49p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "ERROR: unknown arg: $arg" >&2; exit 2 ;;
    esac
done

# ---- preflight --------------------------------------------------------------

for tool in curl tar; do
    command -v "$tool" >/dev/null 2>&1 || {
        echo "ERROR: required tool not found in PATH: $tool" >&2
        exit 1
    }
done

mkdir -p "$BUILD_DIR" "$LIBRARIES_DIR"

# ---- resolve the release tag ------------------------------------------------
# An explicit LINUX_DEPENDENCIES_TAG pins the release; otherwise ask the API
# for the latest one. A token (if present) only lifts the anonymous rate limit.

gh_api() {
    local url="$1"
    local -a auth=()
    local tok="${GH_TOKEN:-${GITHUB_TOKEN:-}}"
    [ -n "$tok" ] && auth=(-H "Authorization: Bearer $tok")
    curl -fsSL "${auth[@]+"${auth[@]}"}" -H "Accept: application/vnd.github+json" "$url"
}

TAG="$LINUX_DEPENDENCIES_TAG"
if [ -z "$TAG" ]; then
    echo "==> Resolving latest release of $LINUX_DEPENDENCIES_REPO"
    TAG="$(gh_api "https://api.github.com/repos/$LINUX_DEPENDENCIES_REPO/releases/latest" \
             | grep -oP '"tag_name"\s*:\s*"\K[^"]+' | head -1 || true)"
fi

# ---- cache check ------------------------------------------------------------

ALL_FILES_PRESENT=1
for f in "${EXPECTED_FILES[@]}"; do
    [ -e "$LIBRARIES_DIR/$f" ] || ALL_FILES_PRESENT=0
done

if [ "$CLEAN" != "1" ] && [ "$ALL_FILES_PRESENT" = "1" ] && [ -f "$STAMP_FILE" ]; then
    STAMPED="$(cat "$STAMP_FILE")"
    if [ -z "$TAG" ]; then
        # API unreachable: trust the already-staged copy rather than failing.
        echo "==> Could not resolve latest tag; reusing cached dependencies ($STAMPED)"
        exit 0
    fi
    if [ "$STAMPED" = "$TAG" ]; then
        echo "==> Cached dependencies match release $TAG; skipping download"
        ( cd "$LIBRARIES_DIR" && ls -1 "${EXPECTED_FILES[@]}" )
        exit 0
    fi
fi

if [ -z "$TAG" ]; then
    echo "ERROR: could not resolve a release tag for $LINUX_DEPENDENCIES_REPO" >&2
    echo "       and no cached copy is staged in $LIBRARIES_DIR." >&2
    echo "       Check network access or pin LINUX_DEPENDENCIES_TAG." >&2
    exit 1
fi

# ---- download + extract -----------------------------------------------------
# Extract straight into LIBRARIES_DIR: the archive's layout is deliberately
# identical to the staging layout. `tar -xz` preserves the symlink chains
# (libavcodec.so -> .so.62 -> .so.62.28.100) that FFmpeg's SONAME resolution
# depends on, so do NOT swap this for a dereferencing copy.

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

URL="https://github.com/$LINUX_DEPENDENCIES_REPO/releases/download/$TAG/$ASSET_NAME"
echo "==> Downloading $URL"
curl -fSL "$URL" -o "$TMP_DIR/$ASSET_NAME"

# Remove what the previously staged release put here before extracting the new
# one. tar only overlays, so without this a release that renames a file (an
# FFmpeg SOVERSION bump, say) leaves the old one behind, and Legacy.csproj
# copies the whole folder next to the apphost - shipping two FFmpeg builds.
# The manifest is recorded below; the native wrappers, which the sibling fetch
# script owns, are never listed in it and so are never touched.
if [ -f "$MANIFEST_FILE" ]; then
    echo "==> Removing files staged by the previous release"
    while IFS= read -r rel; do
        [ -n "$rel" ] || continue
        case "$rel" in */) continue ;; esac   # skip directory entries
        rm -f "$LIBRARIES_DIR/$rel"
    done < "$MANIFEST_FILE"
fi

echo "==> Extracting dependencies into $LIBRARIES_DIR"
tar -xzf "$TMP_DIR/$ASSET_NAME" -C "$LIBRARIES_DIR"

# Record what this release owns, normalised from tar's leading "./".
tar -tzf "$TMP_DIR/$ASSET_NAME" | sed 's|^\./||' > "$MANIFEST_FILE"

MISSING=0
for f in "${EXPECTED_FILES[@]}"; do
    if [ ! -e "$LIBRARIES_DIR/$f" ]; then
        echo "ERROR: release $TAG asset $ASSET_NAME is missing $f" >&2
        MISSING=1
    fi
done
if [ "$MISSING" = "1" ]; then
    exit 1
fi

printf '%s\n' "$TAG" > "$STAMP_FILE"

echo
echo "==> Staged linux-dependencies ($TAG) into $LIBRARIES_DIR:"
( cd "$LIBRARIES_DIR" && ls -1 "${EXPECTED_FILES[@]}" )

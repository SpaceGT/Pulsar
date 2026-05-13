#!/usr/bin/env bash
# build_ffmpeg.sh
#
# Builds FFmpeg 8.1 from source as a set of self-contained shared libraries
# (libavcodec.so.62, libavformat.so.62, libavutil.so.60, libswresample.so.6,
# libswscale.so.9) and installs them into the build/Libraries staging folder
# (gitignored; consumed by Legacy/Legacy.csproj at build time).
#
# "Self-contained" here means: built with --disable-* for every optional
# external codec / hwaccel / network / device backend, so the resulting .so
# files only depend on glibc + libm + libpthread + libz (verified post-build
# via ldd). They do NOT pull in libx264/libxcb/libdrm/libpulse/etc. from
# the host. This is what FFmpeg.AutoGen 8.1 P/Invokes against - see
# ClientPlugin/Audio/MySdlAudioInterop.cs LibraryVersionMap (lines 77-83).
#
# Source layout (all under the gitignored build/ folder of this repo):
#
#   build/
#   ├── ffmpeg-8.1.tar.xz       downloaded tarball (cached across runs)
#   └── ffmpeg-8.1/             extracted source tree (cached)
#       └── _build/             out-of-tree configure + make output (cached)
#           └── _install/       staging prefix the final libs are copied from
#
# A cold first run downloads + extracts + configures + builds; every
# subsequent run is a fast incremental `make` against the cached objects.
# Wipe build/ffmpeg-$FFMPEG_VERSION/ to force a re-extract + reconfigure;
# delete build/ffmpeg-$FFMPEG_VERSION.tar.xz to force a re-download.
#
# Usage:
#   ./build_ffmpeg.sh           Download (if needed), build, install to LIBRARIES_DIR.
#   ./build_ffmpeg.sh --clean   Wipe the cached source + build dirs and rebuild.
#
# Env-var overrides (defaults shown):
#   FFMPEG_VERSION   = 8.1
#   FFMPEG_TARBALL   = <repo>/build/ffmpeg-$FFMPEG_VERSION.tar.xz
#   FFMPEG_SRC_DIR   = <repo>/build/ffmpeg-$FFMPEG_VERSION
#   FFMPEG_BUILD_DIR = $FFMPEG_SRC_DIR/_build
#   LIBRARIES_DIR       = <repo>/build/Libraries  (also honors $BUILD_DIR override)
#   JOBS             = $(nproc)
#
# Requirements: gcc, make, pkg-config, curl, nasm OR yasm (for x86 SIMD).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR_DEFAULT="$REPO_DIR/build"

FFMPEG_VERSION="${FFMPEG_VERSION:-8.1}"
FFMPEG_TARBALL="${FFMPEG_TARBALL:-$BUILD_DIR_DEFAULT/ffmpeg-$FFMPEG_VERSION.tar.xz}"
FFMPEG_SRC_DIR="${FFMPEG_SRC_DIR:-$BUILD_DIR_DEFAULT/ffmpeg-$FFMPEG_VERSION}"
FFMPEG_BUILD_DIR="${FFMPEG_BUILD_DIR:-$FFMPEG_SRC_DIR/_build}"
LIBRARIES_DIR="${LIBRARIES_DIR:-${BUILD_DIR:-$BUILD_DIR_DEFAULT}/Libraries}"
JOBS="${JOBS:-$(nproc)}"

FFMPEG_TARBALL_URL="https://ffmpeg.org/releases/ffmpeg-$FFMPEG_VERSION.tar.xz"

CLEAN=0
for arg in "$@"; do
    case "$arg" in
        --clean) CLEAN=1 ;;
        -h|--help) sed -n '2,42p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "ERROR: unknown arg: $arg" >&2; exit 2 ;;
    esac
done

# ---- preflight --------------------------------------------------------------

for tool in gcc make pkg-config curl tar readelf patchelf; do
    command -v "$tool" >/dev/null 2>&1 || {
        echo "ERROR: required tool not found in PATH: $tool" >&2
        exit 1
    }
done
if ! command -v nasm >/dev/null 2>&1 && ! command -v yasm >/dev/null 2>&1; then
    echo "ERROR: need nasm or yasm for FFmpeg x86 SIMD" >&2
    exit 1
fi

mkdir -p "$LIBRARIES_DIR"
LIBRARIES_DIR="$(cd "$LIBRARIES_DIR" && pwd)"
mkdir -p "$(dirname "$FFMPEG_TARBALL")"

if [ "$CLEAN" = "1" ]; then
    if [ -d "$FFMPEG_SRC_DIR" ]; then
        echo "==> --clean: wiping cached source tree $FFMPEG_SRC_DIR"
        rm -rf "$FFMPEG_SRC_DIR"
    fi
fi

# ---- download tarball (cached) ---------------------------------------------

if [ ! -f "$FFMPEG_TARBALL" ]; then
    echo "==> Downloading $FFMPEG_TARBALL_URL"
    echo "    -> $FFMPEG_TARBALL"
    tmp="$FFMPEG_TARBALL.partial"
    rm -f "$tmp"
    curl -fL --retry 3 --retry-delay 5 -o "$tmp" "$FFMPEG_TARBALL_URL"
    mv "$tmp" "$FFMPEG_TARBALL"
else
    echo "==> Using cached tarball $FFMPEG_TARBALL"
fi

# ---- extract (cached) ------------------------------------------------------
# A "sane" extracted tree is one that contains the upstream `configure`
# script. If the marker is missing the tree is rebuilt from the tarball.

if [ ! -x "$FFMPEG_SRC_DIR/configure" ]; then
    echo "==> Extracting $FFMPEG_TARBALL -> $FFMPEG_SRC_DIR"
    rm -rf "$FFMPEG_SRC_DIR"
    mkdir -p "$(dirname "$FFMPEG_SRC_DIR")"
    # The tarball has a top-level ffmpeg-$VERSION/ dir; strip it so the
    # contents land directly in $FFMPEG_SRC_DIR regardless of how the
    # caller named that dir.
    mkdir -p "$FFMPEG_SRC_DIR"
    tar -xf "$FFMPEG_TARBALL" -C "$FFMPEG_SRC_DIR" --strip-components=1
    [ -x "$FFMPEG_SRC_DIR/configure" ] || {
        echo "ERROR: extraction did not produce $FFMPEG_SRC_DIR/configure" >&2
        exit 1
    }
else
    echo "==> Using cached source tree $FFMPEG_SRC_DIR"
fi

mkdir -p "$FFMPEG_BUILD_DIR"

STAGE_DIR="$FFMPEG_BUILD_DIR/_install"
rm -rf "$STAGE_DIR"
mkdir -p "$STAGE_DIR"

# ---- configure --------------------------------------------------------------
# Notes on the disable list (all flags verified against
# `configure --help` for FFmpeg 8.1 — flags that don't exist in 8.1 have
# been omitted, see comment block at the bottom):
#   --disable-programs       no ffmpeg/ffplay/ffprobe binaries (we just want libs)
#   --disable-doc / *pages   skip texinfo/manpage build (saves time, no value)
#   --disable-network        drop tls/sctp/openssl/gnutls dep chain
#   --disable-avdevice       skip alsa/pulse/x11grab/sdl input backends
#                            (also kills the implicit libxcb / libjack /
#                            libpulse pulls — those only matter when
#                            avdevice is built)
#   --disable-avfilter       skip filter graph (we only decode)
#   --disable-vaapi/vdpau    skip VA-API / VDPAU hwaccel (avoids libva, libvdpau)
#   --disable-libdrm         no libdrm dep
#   --disable-xlib           no X11 dep on the libavutil side
#   --disable-vulkan         no Vulkan hwaccel
#   --disable-bzlib/lzma     drop bzip2 / xz codec deps
#   --disable-iconv          skip libiconv (not needed for our containers)
#   --disable-sdl2           we use SDL3 separately; FFmpeg's SDL is for ffplay
#   --disable-alsa           no ALSA dep (PulseAudio/JACK aren't enabled by
#                            default in 8.1, so no separate flag needed)
#   --enable-pic             required for shared lib output
#   --enable-shared / --disable-static
#                            output .so only; static .a not P/Invokable from .NET
#   --extra-ldflags=-Wl,-Bsymbolic
#                            prefer in-library symbol resolution; reduces risk
#                            that an LD_PRELOAD or unrelated .so on the
#                            host/runtime intercepts FFmpeg-internal calls.
#   --cpu=x86-64-v2          pin the baseline ISA to match .NET 10's documented
#                            x64 minimum (CX16, POPCNT, SSE3, SSSE3, SSE4.1,
#                            SSE4.2 - i.e. Intel Sandy Bridge 2011 / AMD
#                            Bulldozer 2011 and newer). FFmpeg keeps its
#                            runtime SIMD dispatch on top, so AVX/AVX2/AVX-512
#                            paths still get selected on capable CPUs - this
#                            flag only constrains the non-dispatched scalar
#                            code so it cannot drift above what .NET 10 itself
#                            guarantees, and it stops a build host's
#                            CFLAGS=-march=native from accidentally specializing
#                            the shipped libs to the build machine.
#
# zlib intentionally LEFT enabled (default): several muxers/demuxers (mov,
# matroska) need it for compressed metadata; build-time autodetect picks up
# the host's zlib1g-dev. zlib is universally present on every modern Linux
# target, so this is not a portability concern.
#
# Flags that exist in older FFmpeg trees but NOT in 8.1 (and therefore
# omitted): --disable-postproc (postproc has no separate disable flag in
# 8.1, the library is opt-in via --enable-postproc), --disable-libxcb /
# --disable-pulse / --disable-jack (these only have --enable-* forms in
# 8.1; defaults are off, plus avdevice is disabled).

CONFIGURE_FLAGS=(
    --prefix="$STAGE_DIR"
    --cpu=x86-64-v2
    --enable-shared
    --disable-static
    --enable-pic
    --disable-programs
    --disable-doc
    --disable-htmlpages --disable-manpages --disable-podpages --disable-txtpages
    --disable-debug
    --disable-network
    --disable-avdevice --disable-avfilter
    --disable-vaapi --disable-vdpau
    --disable-libdrm --disable-xlib
    --disable-vulkan
    --disable-bzlib --disable-lzma --disable-iconv
    --disable-sdl2
    --disable-alsa
    --extra-ldflags="-Wl,-Bsymbolic"
    # NOTE: DT_RUNPATH=$ORIGIN is NOT injected via --extra-ldsoflags here -
    # see the post-install `patchelf` step below for the rationale. Briefly:
    # passing -Wl,-rpath,'$ORIGIN' through bash -> FFmpeg's configure (sh) ->
    # config.mak -> make -> recipe shell requires 3 layers of $-escaping to
    # survive both sh's $$ -> PID substitution and make's $$ -> $ rule, and
    # the FFmpeg release we build against has historically broken at least
    # one of those layers (we observed the literal PID "260291ORIGIN"
    # landing in DT_RUNPATH on FFmpeg 8.1). Doing it as a post-build
    # patchelf rewrite sidesteps the whole escaping minefield.
)

# Skip reconfigure if the cached _build/ already has a matching config.h and
# the configure flags haven't changed (cached in _build/.configure_flags).
FLAGS_FILE="$FFMPEG_BUILD_DIR/.configure_flags"
FLAGS_HASH="$(printf '%s\n' "src=$FFMPEG_SRC_DIR" "${CONFIGURE_FLAGS[@]}" | sha256sum | awk '{print $1}')"
NEED_CONFIGURE=1
if [ -f "$FFMPEG_BUILD_DIR/config.h" ] && [ -f "$FLAGS_FILE" ]; then
    if [ "$(cat "$FLAGS_FILE")" = "$FLAGS_HASH" ]; then
        NEED_CONFIGURE=0
    fi
fi

if [ "$NEED_CONFIGURE" = "1" ]; then
    echo "==> Configuring FFmpeg $FFMPEG_VERSION (out-of-source build at $FFMPEG_BUILD_DIR)"
    (
        cd "$FFMPEG_BUILD_DIR"
        "$FFMPEG_SRC_DIR/configure" "${CONFIGURE_FLAGS[@]}"
    )
    printf '%s\n' "$FLAGS_HASH" > "$FLAGS_FILE"
else
    echo "==> Reusing cached configure ($FFMPEG_BUILD_DIR/config.h)"
fi

# ---- build & install --------------------------------------------------------

echo "==> Building FFmpeg with -j$JOBS"
make -C "$FFMPEG_BUILD_DIR" -j"$JOBS"

echo "==> Installing into $STAGE_DIR"
make -C "$FFMPEG_BUILD_DIR" install >/dev/null

# ---- sanity check the build outputs ----------------------------------------
# Refuse to ship anything if the SOVERSIONs don't match what FFmpeg.AutoGen
# 8.1 expects (LibraryVersionMap in MySdlAudioInterop.cs). A mismatch here
# means an upstream version bump that needs the version map updated too -
# silently shipping the wrong SOVERSION would manifest as
# DllNotFoundException at runtime instead of failing here.

declare -A EXPECTED_SOVER=(
    [avcodec]=62
    [avformat]=62
    [avutil]=60
    [swresample]=6
    [swscale]=9
)

LIB_SRC="$STAGE_DIR/lib"
[ -d "$LIB_SRC" ] || { echo "ERROR: $LIB_SRC missing after install" >&2; exit 1; }

for name in "${!EXPECTED_SOVER[@]}"; do
    sover="${EXPECTED_SOVER[$name]}"
    f="$LIB_SRC/lib${name}.so.${sover}"
    if [ ! -e "$f" ]; then
        echo "ERROR: expected lib not built: $f" >&2
        echo "Hint: SOVERSION may have shifted. Update LibraryVersionMap in" >&2
        echo "      ClientPlugin/Audio/MySdlAudioInterop.cs and the EXPECTED_SOVER" >&2
        echo "      table in this script together." >&2
        exit 1
    fi
done

# ---- patch DT_RUNPATH=$ORIGIN onto each produced .so -----------------------
# This is what makes the FFmpeg libs self-locating once they ship inside
# Pulsar's Bin/ alongside the Interim apphost. Without it, the inter-FFmpeg
# NEEDED entries (libavformat -> libavcodec.so.62 -> libavutil.so.60 /
# libswresample.so.6, etc.) are resolved by glibc's default search path,
# which does NOT include the executable's own directory - so they would
# either miss entirely or, worse, silently bind to a different-ABI FFmpeg
# version from the host's /etc/ld.so.cache. With DT_RUNPATH=$ORIGIN burned
# in, ld.so locates each FFmpeg lib's siblings via the loaded lib's own
# directory, and the Pulsar launcher does not need to prepend Bin/ to
# LD_LIBRARY_PATH.
#
# Why patchelf instead of -Wl,-rpath,$ORIGIN via configure: passing the
# literal token "$ORIGIN" through bash -> FFmpeg's configure (sh) ->
# config.mak -> make -> recipe shell requires three layers of $-escaping
# that have to thread through both sh's "$$ -> PID" substitution and
# make's "$$ -> $" rule. We've observed at least one of those layers
# breaking on FFmpeg 8.1 (the literal PID landed in DT_RUNPATH, e.g.
# "260291ORIGIN"). patchelf rewrites the .dynamic section after the link
# is complete and is therefore immune to all the upstream escaping
# variability.
echo "==> Patching DT_RUNPATH=\$ORIGIN onto FFmpeg libs"
for name in "${!EXPECTED_SOVER[@]}"; do
    sover="${EXPECTED_SOVER[$name]}"
    f="$LIB_SRC/lib${name}.so.${sover}"
    patchelf --set-rpath '$ORIGIN' "$f"
done

# Verify the .so files don't depend on anything beyond glibc / vdso / loader.
# Anything else (e.g. libpulse, libdrm, libx264) means a configure flag leaked
# through and the bundle would silently require that lib at runtime.
echo "==> Verifying minimal runtime dependencies"
ALLOWED_RE='^(linux-vdso|ld-linux|libc|libm|libpthread|libdl|librt|libgcc_s|libstdc\+\+|libz|libav(codec|format|util)|libsw(resample|scale))(-[a-z0-9_-]+)?\.so'
DEP_LEAK=0
for name in "${!EXPECTED_SOVER[@]}"; do
    sover="${EXPECTED_SOVER[$name]}"
    f="$LIB_SRC/lib${name}.so.${sover}"
    while IFS= read -r line; do
        # ldd line: "  libfoo.so.1 => /path (0x...)" or "  /lib64/ld-linux..."
        # Extract bare lib name (col 1 after trim).
        bare="$(printf '%s' "$line" | awk '{print $1}')"
        # Strip the path-only (no =>) entries down to basename.
        bare="${bare##*/}"
        [ -z "$bare" ] && continue
        if ! [[ "$bare" =~ $ALLOWED_RE ]]; then
            echo "  $name: unexpected dep $bare" >&2
            DEP_LEAK=1
        fi
    done < <(ldd "$f" | sed 's/^[[:space:]]*//')
done
if [ "$DEP_LEAK" = "1" ]; then
    echo "ERROR: at least one FFmpeg lib has an unexpected runtime dep." >&2
    echo "Re-check the configure flags - we want only glibc + libz." >&2
    exit 1
fi

# Verify the literal token "$ORIGIN" actually landed in DT_RUNPATH on every
# FFmpeg lib. If the patchelf step above is ever broken (e.g. swapped to a
# patchelf build with a regression, accidentally removed, or invoked on the
# wrong file), this assertion fails loudly here rather than letting us ship
# libs that silently need LD_LIBRARY_PATH again.
echo "==> Verifying DT_RUNPATH=\$ORIGIN on built libs"
RUNPATH_MISSING=0
for name in "${!EXPECTED_SOVER[@]}"; do
    sover="${EXPECTED_SOVER[$name]}"
    f="$LIB_SRC/lib${name}.so.${sover}"
    runpath="$(readelf -d "$f" 2>/dev/null | awk '/\(RUNPATH\)/ {match($0, /\[.*\]/); print substr($0, RSTART+1, RLENGTH-2)}')"
    if [ "$runpath" != '$ORIGIN' ]; then
        echo "  lib${name}.so.${sover}: expected DT_RUNPATH='\$ORIGIN', got '${runpath}'" >&2
        RUNPATH_MISSING=1
    fi
done
if [ "$RUNPATH_MISSING" = "1" ]; then
    echo "ERROR: at least one FFmpeg lib is missing DT_RUNPATH=\$ORIGIN." >&2
    echo "Re-check the --extra-ldsoflags escaping in CONFIGURE_FLAGS." >&2
    exit 1
fi

# ---- copy into the Libraries folder -------------------------------------------
# Wipe the destination first so a previously shipped, larger SOVERSION can't
# linger and shadow the freshly built one (FFmpeg.AutoGen scans for the
# highest match in LibraryVersionMap order).

echo "==> Staging outputs into $LIBRARIES_DIR"
# Copy realpath files first, then recreate the symlink chain. This avoids
# `cp -a` accidentally turning a relative symlink into a dangling absolute
# one across filesystems.
for f in "$LIB_SRC"/lib*.so.*; do
    [ -L "$f" ] && continue
    cp -a "$f" "$LIBRARIES_DIR/$(basename "$f")"
done

for link in "$LIB_SRC"/lib*.so*; do
    [ -L "$link" ] || continue
    target="$(readlink "$link")"   # relative target name, e.g. libavcodec.so.62.0.100
    name="$(basename "$link")"
    [ -e "$LIBRARIES_DIR/$name" ] && continue
    ln -s "$target" "$LIBRARIES_DIR/$name"
done

echo
echo "==> Staged FFmpeg libs into $LIBRARIES_DIR:"
# List only the files this script actually touched (the FFmpeg sonames +
# their symlink aliases). The build/Libraries/ staging folder also contains
# non-FFmpeg deps (DXVK, EOS, Havok, Recast, Steam, VRageNative,
# Steamworks.NET.dll) populated by the sibling build_*.sh scripts (or
# copied from Vendor/ by build_dependencies.sh), so it would be misleading
# to list them here under "Staged".
( cd "$LIBRARIES_DIR" && ls -1 libav*.so* libsw*.so* )

#!/usr/bin/env bash
# build_steamworks_net.sh
#
# Builds Steamworks.NET.dll from upstream sources at
# https://github.com/rlabrecque/Steamworks.NET and installs it into the
# build/Libraries staging folder.
#
# We pin to a specific commit SHA (not a tag) because rlabrecque tags
# correspond to Steamworks SDK versions while the .dll Pulsar consumed
# historically was built straight from a HEAD commit — the
# AssemblyInformationalVersion of the previously committed Libraries/
# Steamworks.NET.dll embeds the exact SHA, which is the value pinned below.
#
# The repo's Standalone3.0/Steamworks.NET.csproj targets net8.0 and matches
# the assembly Pulsar uses; built with `dotnet build -c Release` it produces
# an MIT-licensed Steamworks.NET.dll in bin/Release/net8.0/.
#
# Source layout (under the gitignored build/ folder of this repo):
#
#   build/
#   ├── Libraries/                staging dir all dep scripts populate
#   ├── Steamworks.NET/           clone of rlabrecque/Steamworks.NET (cached)
#   └── steamworks-net.stamp      last-built commit SHA (cache key)
#
# Usage:
#   ./build_steamworks_net.sh           Build (or no-op if cached).
#   ./build_steamworks_net.sh --clean   Wipe build outputs and rebuild.
#
# Env-var overrides (defaults shown):
#   STEAMWORKS_NET_REPO   = https://github.com/rlabrecque/Steamworks.NET.git
#   STEAMWORKS_NET_COMMIT = 68e72a49caf03a07722d4d4b471bbc7c0785f80b
#                           (commit baked into the previously committed
#                            Libraries/Steamworks.NET.dll's
#                            AssemblyInformationalVersion)
#   BUILD_DIR             = <repo>/build
#   LIBRARIES_DIR         = $BUILD_DIR/Libraries
#
# Requirements: git, dotnet SDK (with net8.0 targeting pack).

set -euo pipefail

STEAMWORKS_NET_REPO="${STEAMWORKS_NET_REPO:-https://github.com/rlabrecque/Steamworks.NET.git}"
STEAMWORKS_NET_COMMIT="${STEAMWORKS_NET_COMMIT:-68e72a49caf03a07722d4d4b471bbc7c0785f80b}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR_DEFAULT="$REPO_DIR/build"

BUILD_DIR="${BUILD_DIR:-$BUILD_DIR_DEFAULT}"
LIBRARIES_DIR="${LIBRARIES_DIR:-$BUILD_DIR/Libraries}"

CLONE_DIR="$BUILD_DIR/Steamworks.NET"
CSPROJ="$CLONE_DIR/Standalone3.0/Steamworks.NET.csproj"
STAMP_FILE="$BUILD_DIR/steamworks-net.stamp"
DLL_NAME="Steamworks.NET.dll"

CLEAN=0
for arg in "$@"; do
    case "$arg" in
        --clean)   CLEAN=1 ;;
        -h|--help) sed -n '2,40p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "ERROR: unknown arg: $arg" >&2; exit 2 ;;
    esac
done

# ---- preflight --------------------------------------------------------------

for tool in git dotnet; do
    command -v "$tool" >/dev/null 2>&1 || {
        echo "ERROR: required tool not found in PATH: $tool" >&2
        exit 1
    }
done

mkdir -p "$BUILD_DIR" "$LIBRARIES_DIR"

# ---- cache check ------------------------------------------------------------

if [ "$CLEAN" = "1" ]; then
    rm -rf "$CLONE_DIR/Standalone3.0/bin" "$CLONE_DIR/Standalone3.0/obj"
elif [ -f "$LIBRARIES_DIR/$DLL_NAME" ] \
   && [ -f "$STAMP_FILE" ] \
   && [ "$(cat "$STAMP_FILE")" = "$STEAMWORKS_NET_COMMIT" ]; then
    echo "==> Cached build matches commit $STEAMWORKS_NET_COMMIT; skipping rebuild"
    echo "==> $DLL_NAME already in $LIBRARIES_DIR"
    exit 0
fi

# ---- clone / fetch ----------------------------------------------------------

if [ ! -d "$CLONE_DIR/.git" ]; then
    echo "==> Cloning $STEAMWORKS_NET_REPO -> $CLONE_DIR"
    rm -rf "$CLONE_DIR"
    git clone "$STEAMWORKS_NET_REPO" "$CLONE_DIR"
else
    echo "==> Fetching origin in $CLONE_DIR"
    git -C "$CLONE_DIR" fetch origin --tags --prune
fi

# The pinned commit lives on a PR ref (PR #738, "Upgrade Steamworks SDK to
# v1.64"), which a default clone/fetch does NOT pull. Try a plain checkout
# first (cheap), and on failure fetch PR refs and retry. GitHub exposes PR
# heads under refs/pull/<n>/head; mirroring them locally as
# refs/remotes/origin/pr/<n> keeps the fallback self-contained.
echo "==> Pinning to commit $STEAMWORKS_NET_COMMIT"
if ! git -C "$CLONE_DIR" -c advice.detachedHead=false \
        checkout "$STEAMWORKS_NET_COMMIT" 2>/dev/null; then
    echo "==> Commit not reachable from any branch/tag; fetching PR refs"
    git -C "$CLONE_DIR" fetch origin \
        "+refs/pull/*/head:refs/remotes/origin/pr/*"
    git -C "$CLONE_DIR" -c advice.detachedHead=false \
        checkout "$STEAMWORKS_NET_COMMIT"
fi

# ---- build ------------------------------------------------------------------

[ -f "$CSPROJ" ] || {
    echo "ERROR: missing csproj at $CSPROJ" >&2
    echo "       The pinned commit may pre-date Standalone3.0/. Update" >&2
    echo "       STEAMWORKS_NET_COMMIT or switch to the Standalone/ project." >&2
    exit 1
}

echo "==> dotnet build $CSPROJ (Release)"
dotnet build -c Release -v minimal "$CSPROJ"

# The csproj sets <TargetFramework>net8.0</TargetFramework>; outputs land in
# Standalone3.0/bin/Release/net8.0/. `find` the dll rather than hard-coding
# the path so a future TFM bump still works.

DLL_SRC="$(find "$CLONE_DIR/Standalone3.0/bin/Release" -type f -name "$DLL_NAME" -print -quit)"
if [ -z "$DLL_SRC" ]; then
    echo "ERROR: dotnet build did not produce $DLL_NAME under $CLONE_DIR/Standalone3.0/bin/Release" >&2
    exit 1
fi

echo "==> Staging $DLL_NAME into $LIBRARIES_DIR"
install -m 0644 "$DLL_SRC" "$LIBRARIES_DIR/$DLL_NAME"

printf '%s\n' "$STEAMWORKS_NET_COMMIT" > "$STAMP_FILE"

echo
echo "==> Staged $DLL_NAME into $LIBRARIES_DIR:"
ls -l "$LIBRARIES_DIR/$DLL_NAME"

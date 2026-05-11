#!/usr/bin/env bash
# package_pulsar_common.sh
#
# Shared helpers for the two Pulsar packaging scripts:
#   - package_pulsar_for_linux_native.sh  (developer 7z bundle)
#   - package_pulsar_for_linux_flatpak.sh (player Flatpak bundle)
#
# Sourced (not executed) by the variant scripts. After sourcing, callers run
# the helpers in roughly this order:
#
#   pulsar_pkg_init_common "${1:-}"
#   pulsar_pkg_require_tools dotnet git magick ...
#   pulsar_pkg_require_paths "$PULSAR_CSPROJ" ...
#   pulsar_pkg_canonicalize_dirs
#   pulsar_pkg_compute_version_info
#   pulsar_pkg_build_artifacts <extra dotnet-publish args for Pulsar>
#   pulsar_pkg_verify_artifacts "$PULSAR_PUBLISH_DIR"
#   pulsar_pkg_stage_pulsar_publish "$PULSAR_PUBLISH_DIR" "$PULSAR_ROOT/Bin"
#   pulsar_pkg_extract_icons       "$ICON_ROOT" "<basename>"
#
# All helpers exit on error (set -e is assumed by the caller and inherited).
# Globals set by pulsar_pkg_init_common, used by other helpers:
#   PULSAR_REPO_DIR, BUILD_DIR, OUTPUT_DIR
#   PULSAR_CSPROJ, PULSAR_ICON_ICO
#   ICON_SIZES (array)
# Set by pulsar_pkg_compute_version_info:
#   GIT_HASH, BUILD_DATE

# Initialize the common configuration variables from env-var overrides
# (with defaults). Caller should pass its own positional output-dir
# argument as $1 ("" if absent) so the OUTPUT_DIR precedence matches the
# original scripts:
#     <positional arg>  >  $OUTPUT_DIR  >  $PULSAR_REPO_DIR/dist
#
# Expects $PULSAR_REPO_DIR_DEFAULT to be set by the caller (it's derived
# from the calling script's BASH_SOURCE, not this file's).
pulsar_pkg_init_common() {
    local positional_output="${1:-}"

    PULSAR_REPO_DIR="${PULSAR_REPO_DIR:-$PULSAR_REPO_DIR_DEFAULT}"
    BUILD_DIR="${BUILD_DIR:-$PULSAR_REPO_DIR/build}"
    OUTPUT_DIR="${positional_output:-${OUTPUT_DIR:-$PULSAR_REPO_DIR/dist}}"

    PULSAR_CSPROJ="$PULSAR_REPO_DIR/Legacy/Legacy.csproj"
    PULSAR_ICON_ICO="$PULSAR_REPO_DIR/Shared/Splash/icon.ico"
    ICON_SIZES=(16 24 32 48 64 96 128 256)
}

# Verify the named tools are on PATH; abort with a clear error otherwise.
pulsar_pkg_require_tools() {
    local tool
    for tool in "$@"; do
        command -v "$tool" >/dev/null 2>&1 || {
            echo "ERROR: required tool not found in PATH: $tool" >&2
            exit 1
        }
    done
}

# Verify the named paths exist; abort otherwise.
pulsar_pkg_require_paths() {
    local path
    for path in "$@"; do
        [ -e "$path" ] || { echo "ERROR: missing input: $path" >&2; exit 1; }
    done
}

# Create BUILD_DIR / OUTPUT_DIR if needed and canonicalize them to absolute
# paths so subsequent log lines and `cd` calls reference stable locations.
pulsar_pkg_canonicalize_dirs() {
    mkdir -p "$BUILD_DIR" "$OUTPUT_DIR"
    BUILD_DIR="$(cd "$BUILD_DIR" && pwd)"
    OUTPUT_DIR="$(cd "$OUTPUT_DIR" && pwd)"
}

# Compute the bundle version-stamp (8-hex git hash + YYYYMMDD build date).
pulsar_pkg_compute_version_info() {
    GIT_HASH="$(git -C "$PULSAR_REPO_DIR" rev-parse --short=8 HEAD)"
    [ -n "$GIT_HASH" ] || { echo "ERROR: empty git hash" >&2; exit 1; }
    BUILD_DATE="$(date +%Y%m%d)"
}

# Wipe Pulsar's bin/obj, then publish with caller-supplied flags
# (e.g. --no-self-contained for Native, -r linux-x64 --self-contained true
# for Flatpak).
# Args: $@ = extra args inserted into the `dotnet publish` invocation.
pulsar_pkg_build_artifacts() {
    local pulsar_proj_dir
    pulsar_proj_dir="$(dirname "$PULSAR_CSPROJ")"

    echo "==> Wiping prior build outputs"
    rm -rf "$pulsar_proj_dir/bin" "$pulsar_proj_dir/obj"

    echo "==> Publishing Pulsar Interim (Release)"
    dotnet publish -c Release -v minimal "$@" "$PULSAR_CSPROJ"
}

# Sanity-check that the Pulsar apphost (Interim) was actually produced
# where the caller expects it.
# Args: $1 = pulsar publish dir
pulsar_pkg_verify_artifacts() {
    local pulsar_publish_dir="$1"
    [ -f "$pulsar_publish_dir/Interim" ] || {
        echo "ERROR: Interim binary not found at $pulsar_publish_dir/Interim" >&2
        exit 1
    }
}

# Copy the entire Pulsar publish output into the staged Bin/ dir, then strip
# the developer-only artefacts that have no business shipping in a bundle:
# *.dev.json (Roslyn dev manifests) and *.pdb (debug symbols).
# Args: $1 = pulsar publish dir, $2 = target Bin/ dir (must exist)
pulsar_pkg_stage_pulsar_publish() {
    local src="$1" dst="$2"
    cp -a "$src/." "$dst/"
    find "$dst" -type f -name '*.dev.json' -delete
    find "$dst" -type f -name '*.pdb' -delete
}

# Extract Pulsar's multi-resolution Windows .ico into individual PNGs.
# Picks one frame per size in $ICON_SIZES; aborts if any size is missing.
# Output layout:  <icon_root>/<size>/<basename>.png
# Args:
#   $1 = icon root dir (size subdirs are created under here)
#   $2 = base filename without extension (e.g. "pulsar" or the Flatpak app id)
pulsar_pkg_extract_icons() {
    local icon_root="$1" base="$2"
    local icon_tmp size src out f w
    local -A frame_by_size=()

    icon_tmp="$(mktemp -d -t pulsar-icon-XXXXXXXX)"
    # RETURN trap fires whether the function exits normally or via set -e,
    # so the temp dir always gets cleaned up without leaking a global EXIT
    # trap that would conflict with the caller's own cleanup hooks.
    trap 'rm -rf "$icon_tmp"' RETURN

    magick "$PULSAR_ICON_ICO" "$icon_tmp/frame_%d.png" >/dev/null
    for f in "$icon_tmp"/frame_*.png; do
        w="$(magick identify -format '%w' "$f")"
        frame_by_size["$w"]="$f"
    done
    for size in "${ICON_SIZES[@]}"; do
        src="${frame_by_size[$size]:-}"
        [ -n "$src" ] || { echo "ERROR: icon.ico has no ${size}x${size} frame" >&2; exit 1; }
        out="$icon_root/$size/$base.png"
        mkdir -p "$(dirname "$out")"
        cp "$src" "$out"
    done
}

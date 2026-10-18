#!/usr/bin/env bash

# Pulsar compatibility tool installer script for native steam
#
# ## Usage
# 1. Download the script
# 2. Run the script `./pulsar-installer.sh`
# 3. Restart Steam.
# 4. In Space Engineers properies, 
#    4.1. enable `force compatibility tool`
#    4.2. Set pulsar as the compatibility tool to use.
#
# The current iteration of the script does not take any arguments
version=0.0.1 # FIXME: do this in a better way 

set -euo pipefail
# set -o xtrace # enable to help with debugging
trap 'echo "Error" >&2' ERR

# Configurable variables

dotnet_url="https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.13/dotnet-runtime-10.0.13-linux-x64.tar.gz"
dotnet_sha256="958c952691d2c3f8ce21f717f4e911c02b5da0e21ead21bd576368c454b79c77"
dotnet_tarball=dotnet-runtime-10.0.13-linux-x64.tar.gz
steam_dir=~/.steam/steam

#
# ## Implementation notes
# This script is based on proton-ge installation script.
# https://github.com/GloriousEggroll/proton-ge-custom/blob/84bcba108e3206a0c8a14673bf1885f5260bbc7e/README.md?plain=1#L126
#
# If things break, you can reset with: `rm -r ~/.steam/steam/compatibilitytools.d/<release_name>`

echo "Installing Pulsar as a steam compatibility tool!"
echo "script version: $version"



# Verify Steam data directory exists
if [[ ! -d "$steam_dir" ]]; then
    echo "Error: Steam (native) data directory not found." >&2
    echo "Please launch Steam at least once to populate it." >&2
    echo "Or if you are using the Steam Flatpak, use the flatpak extension" >&2
    exit 1
fi

# Make a Steam compatibility tools folder if it does not exist
mkdir -p "$steam_dir/compatibilitytools.d"

# Make a temporary working directory
echo "Making sure temporary directory exists..."
mkdir -p /tmp/pulsar
cd /tmp/pulsar

# Fetch release info
echo "Fetching release info..."
release_json=$(curl -s --max-time 10 \
    https://api.github.com/repos/SpaceGT/Pulsar/releases/latest)

echo $release_json > release.json
if [[ -z "$release_json" || "$release_json" != *'"tag_name"'* ]]; then
    echo "Error: Failed to fetch release info from GitHub." >&2
    exit 1
fi

# Resolve release URL for current architecture
echo "Fetching release for your arch..."

case "$(uname -m)" in
    x86_64)        tarball_pattern='\-linux-x64\.tar\.gz$' ;;
    *)
        echo "Error: Unsupported architecture: $(uname -m)." >&2
        echo "Pulsar is only available for x86_64." >&2
        exit 1
        ;;
esac

tarball_url=$(echo "$release_json" |
    grep browser_download_url |
    cut -d\" -f4 |
    grep -E "$tarball_pattern" |
    head -n1 || true)

tarball_name=$(basename "$tarball_url")
release_name=${tarball_name%.tar.gz}


if [[ -z "$tarball_url" ]]; then
    echo "Error: Could not find a matching release for your arch ($(uname -m))." >&2
    exit 1
fi

# Skip if already installed
if [[ -d "$steam_dir/compatibilitytools.d/$release_name" ]]; then
    echo "Latest release $release_name is already installed."
    exit 0
fi

# Resolve checksum 
checksum=$(echo $release_json |
    grep -oP '(?s)"'"$tarball_name"'".+?"sha256:\K([0-9a-fA-F]{64})' || true)

echo $checksum

if [[ -z "$checksum" ]]; then
    echo "Error: Could not find a checksum for $tarball_name in the release." >&2
    exit 1
fi

# Use cached tarball from tmp if valid, resume if incomplete
if [[ -f "$tarball_name" ]]; then
    echo "Found cached release: $release_name"
    echo "Verifying download..."

    if echo "$checksum $tarball_name" | sha256sum -c - &>/dev/null; then
        echo "Cached release OK, skipping download."
    else
        echo "Cached release is incomplete, resuming download..."
        curl -C - -L "$tarball_url" -o "$tarball_name" --progress-bar
        echo "Verifying download..."

        if ! echo "$checksum $tarball_name" | sha256sum -c - &>/dev/null; then
            echo "Resumed download corrupt, falling back to fresh download..."
            rm -f "$tarball_name"
            curl -L "$tarball_url" -o "$tarball_name" --progress-bar
            echo "Verifying download..."

            if ! echo "$checksum $tarball_name" | sha256sum -c -; then
                echo "Error: Verification failed! The downloaded release may be corrupt." >&2
                exit 1
            fi
        fi
    fi

# Nuke the temporary working directory and download the tarball
else
    echo "Cleaning temporary directory..."
    rm -rf /tmp/pulsar
    mkdir /tmp/pulsar
    cd /tmp/pulsar
    echo "Downloading release: $release_name..."
    curl -L "$tarball_url" -o "$tarball_name" --progress-bar
    echo "Verifying download..."

    if ! echo "$checksum $tarball_name" | sha256sum -c -; then
        echo "Error: Verification failed! The downloaded release may be corrupt." >&2
        exit 1
    fi
fi

# Extract the Pulsar tarball to the Steam compatibility tools folder
echo "Extracting $tarball_name to the Steam compatibility tools folder..."
mkdir -p "$steam_dir/compatibilitytools.d/$release_name"
tar -xf "$tarball_name" -C "$steam_dir/compatibilitytools.d/$release_name" \
    || { echo "Error: Extraction failed!" >&2; exit 1; }
echo "Finished installing pulsar"


# =========== DOTNET INSTALL ==================
echo "Downloading dotnet-runtime: $dotnet_url..."
curl -L "$dotnet_url" -o "$dotnet_tarball" --progress-bar
if ! echo "$dotnet_sha256  $dotnet_tarball" | sha256sum -c -; then
    echo "Error: Verification failed! The downloaded release may be corrupt." >&2
    exit 1
fi

echo "Extracting $dotnet_tarball to the Steam compatibility tools folder..."
mkdir -p "$steam_dir/compatibilitytools.d/$release_name/dotnet-runtime"
tar -xf "$dotnet_tarball" -C "$steam_dir/compatibilitytools.d/$release_name/dotnet-runtime" \
    || { echo "Error: Extraction failed!" >&2; exit 1; }


# ============= COMPAT TOOL FILES INSTALL ====================
echo "Creating compatibility tool launch script"
launcher_file="$steam_dir/compatibilitytools.d/$release_name/pulsar.sh"
cat > $launcher_file <<- "EOF"
#!/usr/bin/env bash

set +e

if [[ $@ == *"d3ddriverquery64.exe"* ]]; then
   echo "Steam is trying to query Direct3D graphics driver capabilities with d3ddriverquery64.exe through pulsar compatibility tool. We'll just ignore this"
   exit 0;
fi

echo "Launching Pulsar"
echo "args" $@
if [[ $@ == *"244850"* ]]; then
    DOTNET_ROOT="{steam_dir}/compatibilitytools.d/{release_name}/dotnet-runtime" {steam_dir}/compatibilitytools.d/{release_name}/Interim.bin -useHome -noUpdate
    RETURN_CODE=$?;
elif [[ $@ == *"1133870"* ]]; then
    DOTNET_ROOT="{steam_dir}/compatibilitytools.d/{release_name}/dotnet-runtime" {steam_dir}/compatibilitytools.d/{release_name}/Modern.bin -useHome -noUpdate
    RETURN_CODE=$?;
else
    notify-send "Pulsar only supports Space Engineers and Space Engineers 2, I have no idea how to run this game."
    RETURN_CODE=-1
fi
echo "Space Engineers Exited:" $RETURN_CODE

exit $RETURN_CODE
EOF

sed -i -e 's|{steam_dir}|'"$steam_dir"'|g' $launcher_file
sed -i -e 's|{release_name}|'"$release_name"'|g' $launcher_file

chmod +x $launcher_file


echo Creating toolmanifest.vdf
cat > $steam_dir/compatibilitytools.d/$release_name/toolmanifest.vdf <<- "EOF"
"manifest"
{
  "version" "2"
  "commandline" "/pulsar.sh"
}
EOF

echo Creating compatibilitytool.vdf 
cat > $steam_dir/compatibilitytools.d/$release_name/compatibilitytool.vdf <<- "EOF"
"compatibilitytools"
{
  "compat_tools"
  {
    "{release_name}"
    {
      "install_path" "."
      "display_name" "{release_name}"
      "from_oslist"  "windows"
      "to_oslist"    "linux"
    }
  }
}
EOF

sed -i -e 's|{release_name}|'"$release_name"'|g' $steam_dir/compatibilitytools.d/$release_name/compatibilitytool.vdf

echo "Successfully installed Pulsar as a compatibility tool"
echo "Restart Steam"
echo "In Space Engineers properies, select \`force compatibility tool\`"
echo "Set $release_name as the compatibility tool to use."
echo "Done :)"

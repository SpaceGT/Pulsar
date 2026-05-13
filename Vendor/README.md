# Vendor blobs

This directory contains the **proprietary** native libraries that Pulsar
ships under `build/Libraries/` at packaging time. They are committed here
instead of being downloaded by the dependency build pipeline because their
distribution requires accepting per-vendor agreements that the Pulsar
maintainer has signed once, and the upstream download endpoints are gated
behind logged-in partner portals (no public artifact URL exists).

Updating these blobs is a manual maintainer task: download the latest SDK
from the vendor portal, replace the file here, commit, push.

## Contents

### `libEOSSDK-Linux-Shipping.so`

The Linux x86_64 runtime of the **Epic Online Services SDK**.

* Source: <https://dev.epicgames.com/portal/> -> your product ->
  SDK Downloads -> "EOS SDK for C" -> Linux build.
* License: proprietary (Epic Online Services SDK License Agreement).
  Attribution: see [Scripts/Licenses/EOS-NOTICE.txt](../Scripts/Licenses/EOS-NOTICE.txt).
* Used by Pulsar to interoperate with Space Engineers' existing EOS
  integration. No EOS SDK source code is included; only the unmodified
  shipping `.so` is redistributed.

### `libsteam_api.so`

The Linux x86_64 runtime of the **Steamworks SDK**.

* Source: <https://partner.steamgames.com/downloads/list> -> Steamworks SDK.
* License: proprietary (Steamworks SDK Access Agreement).
  Attribution: see [Scripts/Licenses/Steam-NOTICE.txt](../Scripts/Licenses/Steam-NOTICE.txt).
* Used by Pulsar to interoperate with Space Engineers' existing Steam
  integration. No Steamworks SDK source code is included; only the
  unmodified shipping `.so` is redistributed.

## Why these are committed (and the others are not)

The remaining native libraries shipped under `build/Libraries/` (FFmpeg,
DXVK, the se-linux-compat native wrappers, and Steamworks.NET) are built
from source by scripts under [Scripts/](../Scripts/) and therefore do not
need to be committed. EOS and Steamworks have no public source or a
publicly-fetchable binary, so this directory is the only practical place
for them.

Third-party licences for the binaries shipped in ../
====================================================

Pulsar itself is MIT-licensed. The native libraries and managed
assembly components, each governed by its own licence. This 
directory collects the licence text and attribution required
when redistributing Pulsar bundles.

Files in this directory:

    FFmpeg-LGPL-2.1.txt        LGPL-2.1 text covering libav*.so* / libsw*.so*
    FFmpeg-README.txt          Build provenance + LGPL relinking notes
    DXVK-LICENSE.txt           zlib licence covering libdxvk_*.so*
    EOS-NOTICE.txt             Attribution for libEOSSDK-Linux-Shipping.so
                               (proprietary, Epic Games)
    Steam-NOTICE.txt           Attribution for libsteam_api.so
                               (proprietary, Valve Corporation)
    Steamworks.NET-LICENSE.txt MIT licence covering Steamworks.NET.dll

The following native wrapper libraries are built from sources in
this project (see the se-linux-compat plugin) with the MIT licence:

    libD3DCompiler.so
    libHavok.so
    libRecastDetour.so
    libVRageNative.so

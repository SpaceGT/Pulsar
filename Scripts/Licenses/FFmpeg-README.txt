FFmpeg
======

The following shared libraries shipped under Pulsar/Libraries/ are FFmpeg 8.1:

    libavcodec.so      (-> libavcodec.so.62      -> libavcodec.so.62.28.100)
    libavformat.so     (-> libavformat.so.62     -> libavformat.so.62.12.100)
    libavutil.so       (-> libavutil.so.60       -> libavutil.so.60.26.100)
    libswresample.so   (-> libswresample.so.6    -> libswresample.so.6.3.100)
    libswscale.so      (-> libswscale.so.9       -> libswscale.so.9.5.100)

License
-------
GNU Lesser General Public License version 2.1 or later (LGPL-2.1-or-later).
See FFmpeg-LGPL-2.1.txt in this directory for the full license text.

These libraries are built WITHOUT --enable-gpl and WITHOUT --enable-nonfree,
so no GPL- or nonfree-licensed FFmpeg components are linked in. The
resulting binaries are therefore distributable under the LGPL only.

Source code
-----------
The libraries are built from the upstream FFmpeg 8.1 release tarball:

    https://ffmpeg.org/releases/ffmpeg-8.1.tar.xz

The exact build configuration used to produce the shipped binaries is in
Pulsar's repository at Scripts/build_ffmpeg.sh. The configure flags and the
post-build patchelf step are reproduced verbatim there.

Per the LGPL, users have the right to relink Pulsar against a modified
FFmpeg of their choosing. Because the libraries are loaded via dlopen at
runtime, the user can simply replace the shipped .so files with their own
build (matching the same SOVERSIONs: avcodec 62, avformat 62, avutil 60,
swresample 6, swscale 9) and Pulsar will pick them up automatically.

Copyright
---------
FFmpeg is copyright (c) 2000-present the FFmpeg developers.
See https://ffmpeg.org/ for the full list of contributors.

YARA
====================

yara is an audio visualizer — built for Windows.

Windows binary releases are on the [releases page](../../releases): download `yara-installer-win-x64.exe`, install, and run `yara`. The config lives at `%APPDATA%\yara\config`.

---
(https://raw.githubusercontent.com/karlstav/cava/refs/heads/master/example_files/cava.gif)
====================

**C**ross-platform **A**udio **V**isu**a**lizer


[Demo video](https://youtu.be/9PSp8VA6yjU)

- [What it is](#what-it-is)
- [Installing](#installing)
  - [From Source](#from-source)
  - [Package managers](#package-managers)
- [Capturing audio](#capturing-audio)
  - [Pulseaudio](#pulseaudio)
  - [Pipewire](#pipewire)
  - [ALSA](#alsa)
  - [MPD](#mpd)
  - [Sndio](#sndio)
  - [OSS](#oss)
  - [JACK](#jack)
  - [squeezelite](#squeezelite)
  - [macOS](#macos-1)
  - [Windows](#windows)
- [Running via ssh](#running-via-ssh)
- [Troubleshooting](#troubleshooting)
- [Usage](#usage)
  - [Controls](#controls)
- [Configuration](#configuration)
- [Using cava in other applications](#using-cava-in-other-applications)
  - [cavacore](#cavacore-library)
  - [Raw Output](#raw-output)
- [Contribution](#contribution)


Installing
------------------

### From Source

#### Installing Build Requirements

Required components:
* [FFTW](http://www.fftw.org/)
* libtool
* automake
* pkgconf
* build-essentials
* [iniparser](https://github.com/ndevilla/iniparser)


Recommended components:

The development lib of one of these audio frameworks, depending on your distro:
* ALSA
* Pulseaudio
* Pipewire
* Portaudio
* Sndio
* JACK


Optional components:
* SDL2 dev files
* autoconf-archive (needed for setting up OpenGL)
* [ncursesw dev files](http://www.gnu.org/software/ncurses/) (bundled in ncurses in arch)

Only FFTW, iniparser and the build tools are actually required for CAVA to compile, but this will only give you the ability to read from fifo files. To capture audio directly from your system, additional audio development files may be needed depending on your input backend (pipewire, pulseaudio, alsa, sndio, jack or portaudio). On macOS, the built-in Core Audio framework is supported without extra audio capture libraries.

Ncurses can be used as an alternative output method if you have issues with the default one. But it is not required.


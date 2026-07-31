# yara

A console audio visualizer for Windows — a port of
[cava](https://github.com/karlstav/cava) that captures your system audio with
WASAPI loopback and draws real-time FFT bars in the terminal.

This project builds the upstream cava C source with only minimal changes for a
standalone Windows build (no SDL, no ncurses, no Linux audio backends). The
rendering, FFT, and configuration are all the real cava.

## Features

- Captures the audio actually playing on your PC (Spotify, YouTube, games, anything)
- Native WASAPI loopback capture, event-driven and low-latency
- 8 sub-levels per character row, auto sensitivity, log-scaled bars
- Rainbow gradients, mirror/split orientations, raw output, and more
- Configured with the standard cava `config` file
- No runtime dependencies — a single exe plus one FFT library DLL

## Install

Download the latest `yara-installer.exe` from the
[Releases page](https://github.com/yeyeyebo7-maker/yara/releases) and run it.

The installer:

- installs `yara` to `%LOCALAPPDATA%\Programs\yara`
- adds it to your user PATH
- creates a Start Menu shortcut
- registers an uninstaller in Apps & Features

Then open a new terminal and run:

```
yara
```

> For the best look, use **Windows Terminal** — the font should support block
> characters (Cascadia Mono, Consolas, etc.).

### Uninstall

- Open **Settings > Apps > Installed apps**, find *yara*, and choose Uninstall, or
- run `yara-uninstaller.exe` from the install folder.

## Configuration

On first run yara creates its config file at:

```
%USERPROFILE%\.config\yara\config
```

Everything is configured there: number of bars, frame rate, sensitivity,
colors, gradient, orientation, FFT cutoffs, and more. See
[example_files/config](example_files/config) for the full option list with
comments, and [TERMINAL.md](TERMINAL.md) for the terminal output modes.

### Command-line options

```
Usage: yara [options]
  -p, --config <path>   Path to config file
  -v, --version         Print version and exit
  -h, --help            Show this help and exit
```

### Keys

| Key | Action |
| --- | ------ |
| `Up` / `Down` | Increase / decrease sensitivity |
| `Left` / `Right` | Decrease / increase number of bars |
| `r` | Reload config |
| `c` | Reload colors only |
| `f` / `b` | Cycle foreground / background color |
| `o` | Change orientation |
| `q` | Quit |

## Build from source

On Windows, the build needs:

- CMake (>= 3.13)
- MinGW-w64 (gcc, with `posix` threads)
- FFTW3 for Windows (`fftw-3.3.5-dll64.zip` from fftw.org)

```powershell
git clone https://github.com/yeyeyebo7-maker/yara.git
cd yara
.\scripts\build.ps1
```

`scripts/build.ps1` downloads FFTW if needed, runs CMake + MinGW, then builds
the installer. Artifacts are written to `publish\yara.exe` and
`yara-installer\publish\yara-installer.exe`.

## Credits

This is the source of [cava](https://github.com/karlstav/cava) by Karl
Stavestrand, adapted into a Windows-only terminal build. All upstream files
keep their original form wherever possible.

## License

[MIT](LICENSE) (cava is MIT licensed)

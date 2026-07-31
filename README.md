# yara

A cava-style console audio visualizer for Windows. It captures your system audio
(WASAPI loopback) and draws real-time FFT bars in the terminal.

![rainbow bars in a terminal]

## Features

- Captures the audio actually playing on your PC (Spotify, YouTube, games, anything)
- Smooth, glitch-free bars with 8 sub-levels per character row
- Rainbow gradient across the frequency spectrum, or a solid color
- Log-scaled frequencies, auto gain, mirror mode
- Runs entirely in the terminal, no window decorations
- Self-contained — no .NET runtime or audio drivers needed to install

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

> For the best look, use **Windows Terminal** and enable a font that supports
> block characters (Cascadia Mono, Consolas, etc.).

### Uninstall

- Open **Settings > Apps > Installed apps**, find *yara*, and choose Uninstall, or
- run `yara-uninstaller.exe` from the install folder.

## Options

| Option          | Description                            | Default   |
| --------------- | -------------------------------------- | --------- |
| `--bars N`      | Number of bars (auto-fits width)       | auto      |
| `--fps N`       | Frame rate                             | 60        |
| `--fft N`       | FFT size (power of two)                | 1024      |
| `--fmin Hz`     | Lowest frequency shown                 | 20        |
| `--fmax Hz`     | Highest frequency shown                | 16000     |
| `--gain F`      | Manual gain multiplier                 | 1.0       |
| `--barwidth N`  | Bar width in cells                     | 1         |
| `--gap N`       | Cells between bars                     | 1         |
| `--color RRGGBB`| Solid color instead of rainbow         | —         |
| `--mirror`      | Symmetric vertical mirror              | off       |
| `--no-autosens` | Disable auto gain                      | —         |
| `--no-help`     | Hide the status bar                    | —         |

### Keys

| Key   | Action          |
| ----- | --------------- |
| `q`   | Quit            |
| `+`/`-` | Gain up/down  |
| `m`   | Toggle mirror   |
| `a`   | Toggle auto gain|
| `h`   | Toggle status bar |

## Build from source

Requires the .NET 9 SDK.

```powershell
git clone https://github.com/yeyeyebo7-maker/yara.git
cd yara
.\scripts\build.ps1
```

Artifacts are written to `publish\yara.exe` and `yara-installer\publish\yara-installer.exe`.

To just run it from source:

```powershell
dotnet run --project .\yara.csproj -c Release
```

## License

[MIT](LICENSE)

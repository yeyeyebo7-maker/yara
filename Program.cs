using System.Runtime.InteropServices;
using System.Text;
using Yara;

Console.OutputEncoding = new UTF8Encoding(false);
EnableVirtualTerminal();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.Write("\x1b[?25h\x1b[2J\x1b[H");
    Console.Out.Flush();
    Environment.Exit(0);
};

var cfg = ParseArgs(args);

try
{
    using var viz = new Visualizer(cfg);
    viz.Run();
}
catch (Exception ex)
{
    Console.Write("\x1b[?25h\x1b[2J\x1b[H");
    Console.Out.Flush();
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("yara: " + ex.Message);
    Console.ResetColor();
    Environment.Exit(1);
}

static Config ParseArgs(string[] args)
{
    var cfg = new Config();
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--bars" when i + 1 < args.Length:
                cfg.Bars = int.Parse(args[++i]);
                break;
            case "--fps" when i + 1 < args.Length:
                cfg.Fps = int.Parse(args[++i]);
                break;
            case "--fft" when i + 1 < args.Length:
                cfg.FftSize = int.Parse(args[++i]);
                break;
            case "--fmin" when i + 1 < args.Length:
                cfg.FMin = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "--fmax" when i + 1 < args.Length:
                cfg.FMax = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "--gain" when i + 1 < args.Length:
                cfg.Gain = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "--barwidth" when i + 1 < args.Length:
                cfg.BarWidth = int.Parse(args[++i]);
                break;
            case "--gap" when i + 1 < args.Length:
                cfg.Gap = int.Parse(args[++i]);
                break;
            case "--color" when i + 1 < args.Length:
                cfg.Solid = true;
                cfg.SolidColor = ParseHex(args[++i]);
                break;
            case "--mirror":
                cfg.Mirror = true;
                break;
            case "--no-autosens":
                cfg.AutoSens = false;
                break;
            case "--no-help":
                cfg.ShowHelp = false;
                break;
            case "--help":
            case "-h":
                PrintUsage();
                Environment.Exit(0);
                break;
            default:
                Console.Error.WriteLine($"yara: unknown option '{args[i]}'");
                PrintUsage();
                Environment.Exit(1);
                break;
        }
    }
    return cfg;
}

static (byte, byte, byte) ParseHex(string hex)
{
    if (hex.Length == 6 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int v))
        return ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    Console.Error.WriteLine("yara: color must be RRGGBB hex, e.g. --color 00ff88");
    Environment.Exit(1);
    return default;
}

static void PrintUsage()
{
    Console.WriteLine(
        "yara - console audio visualizer (Windows)\n" +
        "Captures system audio (WASAPI loopback) and draws FFT bars.\n\n" +
        "Options:\n" +
        "  --bars N        number of bars (default: auto by terminal width)\n" +
        "  --fps N         frame rate (default 60)\n" +
        "  --fft N         FFT size, power of two (default 1024)\n" +
        "  --fmin Hz       lowest frequency shown (default 20)\n" +
        "  --fmax Hz       highest frequency shown (default 16000)\n" +
        "  --gain F        manual gain multiplier (default 1.0)\n" +
        "  --barwidth N    bar width in cells (default 1)\n" +
        "  --gap N         cells between bars (default 1)\n" +
        "  --color RRGGBB  solid color instead of rainbow\n" +
        "  --mirror        symmetric vertical mirror\n" +
        "  --no-autosens   disable auto gain\n" +
        "  --no-help       hide the status bar\n\n" +
        "Keys: q quit, +/- gain, m mirror, a auto-sens, h status bar");
}

static void EnableVirtualTerminal()
{
    if (!OperatingSystem.IsWindows()) return;
    nint handle = GetStdHandle(-11);
    if (GetConsoleMode(handle, out uint mode))
    {
        SetConsoleMode(handle, mode | 0x0004 | 0x0008 | 0x0002 | 0x0001);
    }
}

[DllImport("kernel32.dll")]
static extern nint GetStdHandle(int nStdHandle);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);

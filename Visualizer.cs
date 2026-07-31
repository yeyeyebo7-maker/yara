using System;
using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace Yara;

internal sealed class Config
{
    public int Bars { get; set; }
    public int Fps { get; set; } = 60;
    public bool Mirror { get; set; }
    public bool AutoSens { get; set; } = true;
    public float Gain { get; set; } = 1.0f;
    public float FMin { get; set; } = 20f;
    public float FMax { get; set; } = 16000f;
    public int FftSize { get; set; } = 1024;
    public bool Solid { get; set; }
    public (byte R, byte G, byte B) SolidColor { get; set; } = (0, 255, 136);
    public int BarWidth { get; set; } = 1;
    public int Gap { get; set; } = 1;
    public bool ShowHelp { get; set; } = true;
}

internal sealed class Visualizer : IDisposable
{
    private const int Cube = 6;

    private readonly Config _cfg;
    private readonly AudioCapture _capture;
    private readonly float[] _samples;
    private readonly Complex[] _fft;
    private readonly double[] _window;
    private readonly float[] _levels;
    private readonly int[] _palette;
    private readonly string[] _colorEsc;
    private readonly (int Lo, int Hi)[] _bands;
    private readonly StringBuilder _sb = new();
    private readonly Stopwatch _timer = new();
    private float _globalPeak;
    private float _fps;
    private int _fpsCount;
    private long _fpsStart;

    public Visualizer(Config cfg)
    {
        _cfg = cfg;
        _capture = new AudioCapture();

        int fftSize = cfg.FftSize;
        while ((fftSize & (fftSize - 1)) != 0) fftSize++;
        cfg.FftSize = fftSize;

        _samples = new float[fftSize];
        _fft = new Complex[fftSize];
        _window = Fft.HannWindow(fftSize);

        int bars = cfg.Bars > 0 ? cfg.Bars : 32;
        _levels = new float[bars];
        _bands = BuildBands(bars, fftSize);
        _palette = new int[bars];
        _colorEsc = new string[256];
        for (int i = 0; i < 256; i++) _colorEsc[i] = $"\x1b[38;5;{i}m";

        for (int i = 0; i < bars; i++)
        {
            _palette[i] = cfg.Solid
                ? ToCubeIndex(cfg.SolidColor)
                : ToCubeIndex(HslToRgb(300.0 * i / bars, 1.0, 0.5));
        }
        cfg.Bars = bars;
    }

    private static (byte R, byte G, byte B) HslToRgb(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double hp = h / 60.0;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        double r = 0, g = 0, b = 0;
        if (hp < 1) (r, g) = (c, x);
        else if (hp < 2) (r, g) = (x, c);
        else if (hp < 3) (g, b) = (c, x);
        else if (hp < 4) (g, b) = (x, c);
        else if (hp < 5) (r, b) = (x, c);
        else (r, b) = (c, x);
        double m = l - c / 2;
        return ((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    private static int ToCubeIndex((byte R, byte G, byte B) c)
    {
        int R = (int)Math.Round(c.R / 255f * (Cube - 1));
        int G = (int)Math.Round(c.G / 255f * (Cube - 1));
        int B = (int)Math.Round(c.B / 255f * (Cube - 1));
        return 16 + 36 * R + 6 * G + B;
    }

    private (int Lo, int Hi)[] BuildBands(int bars, int fftSize)
    {
        double sr = _capture.SampleRate;
        double fMin = Math.Max(10.0, _cfg.FMin);
        double fMax = Math.Min(sr / 2.0, _cfg.FMax);
        double logMin = Math.Log(fMin);
        double span = Math.Log(fMax) - logMin;
        var bands = new (int, int)[bars];
        for (int i = 0; i < bars; i++)
        {
            int lo = (int)Math.Floor(Math.Exp(logMin + span * i / bars) / sr * fftSize);
            int hi = (int)Math.Ceiling(Math.Exp(logMin + span * (i + 1) / bars) / sr * fftSize) - 1;
            bands[i] = (Math.Max(1, lo), Math.Max(lo, hi));
        }
        return bands;
    }

    public void Run()
    {
        try { Console.Title = "yara"; } catch { }
        _capture.Start();
        _timer.Start();
        _fpsStart = _timer.ElapsedMilliseconds;

        double frameTimeMs = 1000.0 / _cfg.Fps;
        Console.Write("\x1b[2J\x1b[H\x1b[?25l");
        Console.Out.Flush();

        try
        {
            while (true)
            {
                long t0 = _timer.ElapsedMilliseconds;
                HandleKeys();
                ProcessAudio();
                Render();
                if (_cfg.ShowHelp) RenderHud();
                Console.Write(_sb);
                Console.Out.Flush();

                long elapsed = _timer.ElapsedMilliseconds - t0;
                long wait = (long)frameTimeMs - elapsed;
                if (wait > 0)
                {
                    Thread.Sleep((int)wait);
                }
                else if (wait < -frameTimeMs * 4)
                {
                    frameTimeMs = Math.Max(frameTimeMs * 1.1, (double)elapsed / 4);
                }
            }
        }
        finally
        {
            Console.Write("\x1b[?25h\x1b[2J\x1b[H");
            Console.Out.Flush();
        }
    }

    private void ProcessAudio()
    {
        int got = _capture.Read(_samples, _samples.Length);
        for (int i = got; i < _samples.Length; i++) _samples[i] = 0f;

        for (int i = 0; i < _samples.Length; i++)
            _fft[i] = new Complex(_samples[i] * _window[i], 0.0);

        Fft.Transform(_fft);

        float peak = 0f;
        for (int b = 0; b < _bands.Length; b++)
        {
            var (lo, hi) = _bands[b];
            float sum = 0f;
            for (int bin = lo; bin <= hi; bin++)
            {
                var c = _fft[bin];
                sum += (float)Math.Sqrt(c.Real * c.Real + c.Imaginary * c.Imaginary);
            }
            float mag = sum / (hi - lo + 1) / (_samples.Length * 0.25f);
            if (mag > peak) peak = mag;
            _levels[b] = mag;
        }

        _globalPeak = Math.Max(_globalPeak * 0.992f, peak);

        float gain = _cfg.Gain;
        if (_cfg.AutoSens && _globalPeak > 0.001f) gain *= 0.95f / _globalPeak;

        const float attack = 0.8f, release = 0.14f;
        for (int b = 0; b < _bands.Length; b++)
        {
            float target = Math.Min(_levels[b] * gain, 1.6f);
            float cur = _levels[b];
            _levels[b] = cur + (target - cur) * (target > cur ? attack : release);
        }

        _fpsCount++;
        long now = _timer.ElapsedMilliseconds;
        if (now - _fpsStart >= 1000)
        {
            _fps = _fpsCount * 1000f / (now - _fpsStart);
            _fpsCount = 0;
            _fpsStart = now;
        }
    }

    private void Render()
    {
        int width, height;
        try
        {
            width = Console.WindowWidth;
            height = Console.WindowHeight - 1;
        }
        catch
        {
            width = 80;
            height = 24;
        }
        if (width < 4 || height < 2) return;

        int rows = _cfg.Mirror ? height / 2 : height;
        if (rows < 1) rows = 1;
        int maxUnits = rows * 8 - 1;

        int margin = 1;
        int usable = Math.Max(1, width - margin * 2);
        int cols = _cfg.BarWidth + _cfg.Gap;
        int bars = Math.Min(_bands.Length, Math.Max(1, usable / cols));

        var lines = new Line[rows];
        for (int r = 0; r < rows; r++)
        {
            lines[r] = new Line(usable * 8);
            lines[r].Sb.Append(' ', margin);
        }

        for (int i = 0; i < bars; i++)
        {
            int units = (int)(_levels[i] * maxUnits);
            if (units < 0) units = 0;
            if (units > maxUnits) units = maxUnits;
            int color = _palette[i];

            for (int r = 0; r < rows; r++)
            {
                int bottomIndex = rows - 1 - r;
                int level = units - bottomIndex * 8;
                char ch = level >= 8 ? '█'
                    : level <= 0 ? ' '
                    : "\u2581\u2582\u2583\u2584\u2585\u2586\u2587"[level - 1];
                var line = lines[r];
                if (line.LastColor != color)
                {
                    line.Sb.Append(_colorEsc[color]);
                    line.LastColor = color;
                }
                line.Sb.Append(ch);
            }
        }

        for (int i = bars; i < _bands.Length; i++)
        {
            _levels[i] *= 0.98f;
        }

        _sb.Clear();
        _sb.Append("\x1b[H");
        for (int r = 0; r < rows; r++)
        {
            if (r > 0) _sb.Append('\n');
            _sb.Append(lines[r].Sb);
            if (_cfg.Mirror)
            {
                _sb.Append('\n');
                _sb.Append(lines[r].Sb);
            }
        }
    }

    private void RenderHud()
    {
        string msg = $" yara {_fps:0} fps  gain {_cfg.Gain:0.00}  [{(_cfg.AutoSens ? "auto" : "man")}]  [{(_cfg.Mirror ? "mirror" : "bottom")}]   +/-:gain  m:mirror  a:auto  h:help  q:quit";
        int hudRow;
        try { hudRow = Console.WindowHeight; } catch { hudRow = 25; }
        _sb.Append('\x1b');
        _sb.Append('[');
        _sb.Append(hudRow);
        _sb.Append(";1H\x1b[38;5;240m");
        _sb.Append(msg);
    }

    private void HandleKeys()
    {
        try
        {
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                switch (key.KeyChar)
                {
                case 'q':
                case 'Q':
                    Dispose();
                    Environment.Exit(0);
                    break;
                case '+':
                case '=':
                    _cfg.Gain = Math.Min(_cfg.Gain + 0.15f, 10f);
                    break;
                case '-':
                case '_':
                    _cfg.Gain = Math.Max(_cfg.Gain - 0.15f, 0.05f);
                    break;
                case 'm':
                case 'M':
                    _cfg.Mirror = !_cfg.Mirror;
                    break;
                case 'a':
                case 'A':
                    _cfg.AutoSens = !_cfg.AutoSens;
                    break;
                case 'h':
                case 'H':
                    _cfg.ShowHelp = !_cfg.ShowHelp;
                    break;
            }
        }
        }
        catch
        {
        }
    }

    public void Dispose() => _capture.Dispose();

    private sealed class Line
    {
        public readonly StringBuilder Sb;
        public int LastColor = -1;

        public Line(int capacity) => Sb = new StringBuilder(capacity);
    }
}

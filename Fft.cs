using System;
using System.Numerics;

namespace Yara;

internal static class Fft
{
    public static void Transform(Span<Complex> buffer)
    {
        int n = buffer.Length;
        if (n <= 1) return;

        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2.0 * Math.PI / len;
            var wlen = new Complex(Math.Cos(angle), Math.Sin(angle));
            for (int i = 0; i < n; i += len)
            {
                var w = Complex.One;
                int half = len >> 1;
                for (int k = 0; k < half; k++)
                {
                    var u = buffer[i + k];
                    var v = buffer[i + k + half] * w;
                    buffer[i + k] = u + v;
                    buffer[i + k + half] = u - v;
                    w *= wlen;
                }
            }
        }
    }

    public static double[] HannWindow(int n)
    {
        var window = new double[n];
        for (int i = 0; i < n; i++)
            window[i] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (n - 1)));
        return window;
    }
}

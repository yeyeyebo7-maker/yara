using System;
using System.Collections.Concurrent;
using NAudio.Wave;

namespace Yara;

internal sealed class AudioCapture : IDisposable
{
    private readonly WasapiLoopbackCapture _capture;
    private readonly ConcurrentQueue<float> _samples = new();
    private bool _disposed;

    public int SampleRate { get; }
    public bool HasData => !_samples.IsEmpty;

    public AudioCapture()
    {
        _capture = new WasapiLoopbackCapture();
        SampleRate = _capture.WaveFormat.SampleRate;
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception != null) throw e.Exception;
        };
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var format = _capture.WaveFormat;
        int channels = format.Channels;
        int bytesPerSample = format.BitsPerSample / 8;
        if (bytesPerSample < 1) return;
        int frameBytes = bytesPerSample * channels;
        int frames = e.BytesRecorded / frameBytes;
        if (frames == 0) return;

        byte[] buffer = e.Buffer;
        for (int i = 0; i < frames; i++)
        {
            float sum = 0f;
            int frameStart = i * frameBytes;
            for (int c = 0; c < channels; c++)
            {
                int offset = frameStart + c * bytesPerSample;
                float sample;
                if (format.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    sample = BitConverter.ToSingle(buffer, offset);
                }
                else if (format.Encoding == WaveFormatEncoding.Pcm && bytesPerSample == 2)
                {
                    sample = BitConverter.ToInt16(buffer, offset) / 32768f;
                }
                else if (format.Encoding == WaveFormatEncoding.Pcm && bytesPerSample == 3)
                {
                    int v = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
                    if ((v & 0x800000) != 0) v |= ~0xFFFFFF;
                    sample = v / 8388608f;
                }
                else
                {
                    continue;
                }
                sum += sample;
            }
            _samples.Enqueue(sum / channels);
        }
    }

    public int Read(float[] target, int count)
    {
        int n = 0;
        while (n < count && _samples.TryDequeue(out float sample))
            target[n++] = sample;
        return n;
    }

    public void Start() => _capture.StartRecording();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _capture.StopRecording(); } catch { }
        _capture.Dispose();
    }
}

using NAudio.Wave;

namespace OpenAI.Examples.Realtime;

/// <summary>
/// Uses NAudio (https://github.com/naudio/NAudio) to provide a simple speaker output pipeline
/// for 24 kHz, 16-bit, mono PCM audio buffers played through the system default output device.
/// </summary>
public class SpeakerOutput : IDisposable
{
    BufferedWaveProvider _waveProvider;
    WaveOutEvent _waveOutEvent;

    public SpeakerOutput()
    {
        WaveFormat outputAudioFormat = new(rate: 24000, bits: 16, channels: 1);

        _waveProvider = new(outputAudioFormat)
        {
            BufferDuration = TimeSpan.FromMinutes(2),
        };

        _waveOutEvent = new();
        _waveOutEvent.Init(_waveProvider);
        _waveOutEvent.Play();
    }

    public void EnqueueForPlayback(byte[] buffer)
    {
        _waveProvider.AddSamples(buffer, 0, buffer.Length);
    }

    public void ClearPlayback()
    {
        _waveProvider.ClearBuffer();
    }

    public void Dispose()
    {
        _waveOutEvent?.Dispose();
    }
}

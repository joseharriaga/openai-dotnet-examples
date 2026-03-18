using NAudio.Wave;

namespace OpenAI.Examples.Realtime;

#nullable disable

/// <summary>
/// Uses NAudio (https://github.com/naudio/NAudio) to expose microphone capture as a readable
/// stream of 24 kHz, 16-bit, mono PCM audio buffered in memory.
/// </summary>
public class MicrophoneAudioStream : Stream, IDisposable
{
    private const int SAMPLES_PER_SECOND = 24000;
    private const int BYTES_PER_SAMPLE = 2;
    private const int CHANNELS = 1;

    // For simplicity, this is configured to use a static 10-second ring buffer.
    private readonly byte[] _buffer = new byte[BYTES_PER_SAMPLE * SAMPLES_PER_SECOND * CHANNELS * 10];
    private readonly object _bufferLock = new();
    private int _bufferReadPos = 0;
    private int _bufferWritePos = 0;

    // Signal that is set whenever new audio data is available in the buffer.
    // This allows Read() to block efficiently instead of spin-waiting, and
    // critically avoids ever returning 0 (which the SDK interprets as end-of-stream).
    private readonly ManualResetEventSlim _dataAvailable = new(false);
    private volatile bool _disposed = false;

    private readonly WaveInEvent _waveInEvent;

    private MicrophoneAudioStream()
    {
        _waveInEvent = new()
        {
            WaveFormat = new WaveFormat(SAMPLES_PER_SECOND, BYTES_PER_SAMPLE * 8, CHANNELS),
        };
        _waveInEvent.DataAvailable += (_, e) =>
        {
            lock (_bufferLock)
            {
                int bytesToCopy = e.BytesRecorded;
                if (_bufferWritePos + bytesToCopy >= _buffer.Length)
                {
                    int bytesToCopyBeforeWrap = _buffer.Length - _bufferWritePos;
                    Array.Copy(e.Buffer, 0, _buffer, _bufferWritePos, bytesToCopyBeforeWrap);
                    bytesToCopy -= bytesToCopyBeforeWrap;
                    _bufferWritePos = 0;
                }
                Array.Copy(e.Buffer, e.BytesRecorded - bytesToCopy, _buffer, _bufferWritePos, bytesToCopy);
                _bufferWritePos += bytesToCopy;

                // Signal that new data is available for readers.
                _dataAvailable.Set();
            }
        };
        _waveInEvent.StartRecording();
    }

    public static MicrophoneAudioStream Start() => new();

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotImplementedException();

    public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int GetBytesAvailable() => _bufferWritePos < _bufferReadPos
            ? _bufferWritePos + (_buffer.Length - _bufferReadPos)
            : _bufferWritePos - _bufferReadPos;

        // Block until at least some data is available. We perform partial reads
        // (returning however many bytes are available up to count) so the SDK
        // receives audio promptly and never sees a 0-byte return while the
        // stream is still alive.
        while (true)
        {
            if (_disposed)
                return 0; // True end-of-stream: only on disposal.

            lock (_bufferLock)
            {
                int available = GetBytesAvailable();
                if (available > 0)
                {
                    int toRead = Math.Min(count, available);
                    int totalRead = toRead;

                    if (_bufferReadPos + toRead >= _buffer.Length)
                    {
                        int bytesBeforeWrap = _buffer.Length - _bufferReadPos;
                        Array.Copy(
                            sourceArray: _buffer,
                            sourceIndex: _bufferReadPos,
                            destinationArray: buffer,
                            destinationIndex: offset,
                            length: bytesBeforeWrap);
                        _bufferReadPos = 0;
                        toRead -= bytesBeforeWrap;
                        offset += bytesBeforeWrap;
                    }

                    Array.Copy(_buffer, _bufferReadPos, buffer, offset, toRead);
                    _bufferReadPos += toRead;

                    // If we consumed all available data, reset the signal
                    // so the next Read blocks until more data arrives.
                    if (GetBytesAvailable() == 0)
                    {
                        _dataAvailable.Reset();
                    }

                    return totalRead;
                }

                // No data available — reset the signal before waiting.
                _dataAvailable.Reset();
            }

            // Wait for the DataAvailable callback to signal new data,
            // or for disposal to unblock us.
            _dataAvailable.Wait();
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        _dataAvailable.Set(); // Unblock any pending Read() so it returns 0.
        _waveInEvent?.Dispose();
        if (disposing)
        {
            _dataAvailable.Dispose();
        }
        base.Dispose(disposing);
    }
}

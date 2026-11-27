using NAudio.Wave;

namespace AtomicArt.Desktop.Services.Notifications;

internal static class NotificationSoundDecoder
{
    private const int WaveHeaderLength = 12;
    private const int WaveFormatOffset = 8;
    private const int SignatureLength = 4;
    private const int MaximumChannels = 2;
    private const int MaximumSampleRate = 192000;
    private const int ValidationBufferSize = 4096;

    private static readonly TimeSpan MaximumDuration = TimeSpan.FromSeconds(30);

    internal static WaveStream Open(byte[] content)
    {
        MemoryStream stream = new(content, writable: false);
        WaveStream? reader = null;

        try
        {
            bool isWave = content.Length >= WaveHeaderLength
                && content.AsSpan(0, SignatureLength).SequenceEqual("RIFF"u8)
                && content.AsSpan(WaveFormatOffset, SignatureLength).SequenceEqual("WAVE"u8);

            reader = isWave ? new WaveFileReader(stream) : new Mp3FileReader(stream);
            return reader;
        }
        finally
        {
            if (reader is null)
            {
                stream.Dispose();
            }
        }
    }

    internal static void Validate(byte[] content)
    {
        try
        {
            using WaveStream reader = Open(content);

            if ((reader.TotalTime <= TimeSpan.Zero) || (reader.TotalTime > MaximumDuration)
                || (reader.WaveFormat.Channels <= 0) || (reader.WaveFormat.Channels > MaximumChannels)
                || (reader.WaveFormat.SampleRate <= 0) || (reader.WaveFormat.SampleRate > MaximumSampleRate)
                || (reader.WaveFormat.BlockAlign <= 0)
                || reader.WaveFormat.Encoding is not (WaveFormatEncoding.Pcm or WaveFormatEncoding.IeeeFloat))
            {
                throw new NotificationSoundException();
            }

            int blockSize = reader.WaveFormat.BlockAlign;
            int sampleBytes = Math.Max(blockSize, ValidationBufferSize - ValidationBufferSize % blockSize);
            byte[] samples = new byte[sampleBytes];

            if (reader.Read(samples, 0, samples.Length) == 0)
            {
                throw new NotificationSoundException();
            }
        }
        catch (Exception exception) when (exception is not NotificationSoundException)
        {
            throw new NotificationSoundException(exception);
        }
    }
}

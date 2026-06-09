using NAudio.Wave;

namespace NAudio.MediaFoundation
{
    internal class MediaFoundationResampler : IWaveProvider
    {
        private AudioFileReader reader;
        private WaveFormat outFmt;

        public MediaFoundationResampler(AudioFileReader reader, WaveFormat outFmt)
        {
            this.reader = reader;
            this.outFmt = outFmt;
        }

        public int ResamplerQuality { get; set; }

        // ── Métodos obligatorios de IWaveProvider ─────────────────
        public WaveFormat WaveFormat => outFmt;

        public int Read(byte[] buffer, int offset, int count)
        {
            return reader.Read(buffer, offset, count);
        }
    }
}
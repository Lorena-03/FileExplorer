using NAudio.Wave;
using NAudio.MediaFoundation;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Implementación de escritor MP3 usando Windows Media Foundation.
    /// Acumula el audio PCM en memoria y al hacer Dispose lo codifica a MP3.
    /// No requiere NAudio.Lame ni DLLs externas — usa el encoder de Windows.
    /// </summary>
    internal class LameMP3FileWriter : Stream
    {
        private readonly MemoryStream _buffer = new();
        private readonly string _mp3Path;
        private readonly WaveFormat _waveFormat;
        private readonly int _bitrate;   // kbps (ej: 128)
        private bool _disposed = false;

        public LameMP3FileWriter(string mp3Path, WaveFormat waveFormat, int bitrate)
        {
            _mp3Path = mp3Path;
            _waveFormat = waveFormat;
            _bitrate = bitrate;
        }

        // ── Stream obligatorios ───────────────────────────────────────
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _buffer.Length;
        public override long Position
        {
            get => _buffer.Position;
            set => _buffer.Position = value;
        }

        /// <summary>Recibe los bytes PCM del WaveFileReader.</summary>
        public override void Write(byte[] buffer, int offset, int count)
            => _buffer.Write(buffer, offset, count);

        public override void Flush() { /* nada que hacer en el buffer */ }

        // ── Al cerrar/disponer: codificar a MP3 ───────────────────────
        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing && _buffer.Length > 0)
            {
                try
                {
                    _buffer.Position = 0;
                    MediaFoundationApi.Startup();

                    using var reader = new RawSourceWaveStream(_buffer, _waveFormat);
                    MediaFoundationEncoder.EncodeToMp3(
                        reader,
                        _mp3Path,
                        _bitrate * 1000);   // Media Foundation espera bps, no kbps
                }
                catch (Exception ex)
                {
                    // Re-lanzar para que AudioRecorderForm lo muestre al usuario
                    throw new InvalidOperationException(
                        "Error al codificar MP3 con Windows Media Foundation:\n" + ex.Message, ex);
                }
                finally
                {
                    _buffer.Dispose();
                    _disposed = true;
                }
            }
            base.Dispose(disposing);
        }

        // ── Operaciones no soportadas ─────────────────────────────────
        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();
        public override void SetLength(long value)
            => throw new NotSupportedException();
    }
}
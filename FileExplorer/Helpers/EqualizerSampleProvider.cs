using NAudio.Wave;

namespace FileExplorer.Forms
{
    // ══════════════════════════════════════════════════════════════════════
    //  Filtro BiQuad — Peaking EQ profesional
    // ══════════════════════════════════════════════════════════════════════
    public class BiQuadFilter
    {
        double _a0, _a1, _a2, _b1, _b2;
        double _z1, _z2;

        private BiQuadFilter() { }

        public static BiQuadFilter PeakingEQ(float sampleRate,
            float frequency, float q, float dbGain)
        {
            var f = new BiQuadFilter();
            f.Set(sampleRate, frequency, q, dbGain);
            return f;
        }

        public void Set(float sampleRate, float frequency, float q, float dbGain)
        {
            double a = Math.Pow(10.0, dbGain / 40.0);
            double w0 = 2.0 * Math.PI * frequency / sampleRate;
            double cosW0 = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2.0 * q);

            double b0 = 1.0 + alpha * a;
            double b1 = -2.0 * cosW0;
            double b2 = 1.0 - alpha * a;
            double a0 = 1.0 + alpha / a;
            double a1 = -2.0 * cosW0;
            double a2 = 1.0 - alpha / a;

            _a0 = b0 / a0;
            _a1 = b1 / a0;
            _a2 = b2 / a0;
            _b1 = a1 / a0;
            _b2 = a2 / a0;
            _z1 = _z2 = 0;
        }

        public float Process(float s)
        {
            double o = _a0 * s + _z1;
            _z1 = _a1 * s - _b1 * o + _z2;
            _z2 = _a2 * s - _b2 * o;
            return (float)o;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Banda del ecualizador
    // ══════════════════════════════════════════════════════════════════════
    public class EqualizerBand
    {
        public float Frequency { get; set; }
        public float Gain { get; set; }      // dB
        public float Bandwidth { get; set; } = 0.8f;  // Q
    }

    // ══════════════════════════════════════════════════════════════════════
    //  EqualizerSampleProvider — EQ de 10 bandas en tiempo real
    // ══════════════════════════════════════════════════════════════════════
    public class EqualizerSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly EqualizerBand[] _bands;
        private BiQuadFilter[,] _filters;   // [canal, banda]

        public static readonly float[] Frequencies =
        {
            32f, 64f, 125f, 250f, 500f,
            1000f, 2000f, 4000f, 8000f, 16000f
        };

        public EqualizerBand[] Bands => _bands;
        public WaveFormat WaveFormat => _source.WaveFormat;

        public EqualizerSampleProvider(ISampleProvider source, EqualizerBand[] bands)
        {
            _source = source;
            _bands = bands;
            BuildFilters();
        }

        public static EqualizerBand[] DefaultBands() =>
            Frequencies.Select(f => new EqualizerBand
            {
                Frequency = f,
                Gain = 0f,
                Bandwidth = 0.8f
            }).ToArray();

        private void BuildFilters()
        {
            int ch = _source.WaveFormat.Channels;
            float sr = _source.WaveFormat.SampleRate;
            _filters = new BiQuadFilter[ch, _bands.Length];
            for (int c = 0; c < ch; c++)
                for (int b = 0; b < _bands.Length; b++)
                    _filters[c, b] = BiQuadFilter.PeakingEQ(
                        sr, _bands[b].Frequency, _bands[b].Bandwidth, _bands[b].Gain);
        }

        // Llamar después de cambiar ganancias para actualizar en tiempo real
        public void Update()
        {
            int ch = _source.WaveFormat.Channels;
            float sr = _source.WaveFormat.SampleRate;
            for (int c = 0; c < ch; c++)
                for (int b = 0; b < _bands.Length; b++)
                    _filters[c, b].Set(sr, _bands[b].Frequency,
                        _bands[b].Bandwidth, _bands[b].Gain);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            if (read == 0) return 0;

            int channels = _source.WaveFormat.Channels;

            for (int n = 0; n < read; n++)
            {
                int ch = n % channels;
                float s = buffer[offset + n];
                for (int b = 0; b < _bands.Length; b++)
                    s = _filters[ch, b].Process(s);
                buffer[offset + n] = s;
            }
            return read;
        }
    }
}
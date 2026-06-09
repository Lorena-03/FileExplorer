namespace FileExplorer.Controls
{
    public class EqualizerControl : UserControl
    {
        static readonly Color BgColor = Color.FromArgb(28, 28, 30);
        static readonly Color AccentBlue = Color.FromArgb(0, 122, 255);
        static readonly Color TextGray = Color.FromArgb(174, 174, 178);

        static readonly string[] Labels =
        {
            "32","64","125","250","500","1k","2k","4k","8k","16k"
        };

        static readonly float[][] Presets =
        {
            new float[10],
            new float[]{ 4, 3, 0,-1,-2, 1, 2, 4, 4, 4 },
            new float[]{-1, 2, 3, 3, 1, 0,-1,-1, 1, 2 },
            new float[]{ 3, 2, 1, 2,-1,-1, 0, 1, 2, 3 },
            new float[]{ 4, 3, 2, 1, 0,-1,-1, 0, 2, 4 },
            new float[]{ 4, 3, 1, 0,-1,-1, 2, 4, 4, 4 },
            new float[]{ 6, 5, 4, 2, 1, 0, 0, 0, 0, 0 },
            new float[]{ 0, 0, 0, 0, 0, 0, 2, 3, 4, 4 },
        };

        static readonly string[] PresetNames =
        {
            "Plano","Rock","Pop","Jazz",
            "Clásica","Electrónica","Bass Boost","Treble Boost"
        };

        public event Action<float[]> GainChanged;

        TrackBar[] _sliders = new TrackBar[10];
        Label[] _valLabels = new Label[10];
        ComboBox _presets;

        public EqualizerControl()
        {
            BackColor = BgColor;
            Font = new Font("Segoe UI", 8.5f);
            MinimumSize = new Size(460, 300);
            BuildUI();
        }

        void BuildUI()
        {
            // Etiqueta "Preset:"
            var lp = new Label
            {
                Text = "Preset:",
                Left = 12,
                Top = 14,
                Width = 50,
                Height = 22,
                ForeColor = TextGray,
                BackColor = Color.Transparent,
            };

            // Selector de presets
            _presets = new ComboBox
            {
                Left = 65,
                Top = 11,
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(44, 44, 46),
                ForeColor = Color.White,
            };
            _presets.Items.AddRange(PresetNames);
            _presets.SelectedIndex = 0;
            _presets.SelectedIndexChanged += (s, e) =>
                Apply(_presets.SelectedIndex);

            // Botón reiniciar
            var br = new Button
            {
                Text = "Reiniciar",
                Left = 240,
                Top = 10,
                Width = 75,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(58, 58, 60),
                ForeColor = Color.White,
            };
            br.FlatAppearance.BorderSize = 0;
            br.Click += (s, e) => { _presets.SelectedIndex = 0; Apply(0); };

            Controls.AddRange(new Control[] { lp, _presets, br });

            // 10 sliders verticales
            for (int i = 0; i < 10; i++)
            {
                int idx = i;
                int left = 16 + i * 44;

                var freqLbl = new Label
                {
                    Text = Labels[i],
                    Left = left,
                    Top = 278,
                    Width = 40,
                    Height = 16,
                    ForeColor = TextGray,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 7.5f),
                };

                _valLabels[i] = new Label
                {
                    Text = "0 dB",
                    Left = left,
                    Top = 258,
                    Width = 40,
                    Height = 16,
                    ForeColor = AccentBlue,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                };

                _sliders[i] = new TrackBar
                {
                    Orientation = Orientation.Vertical,
                    Left = left,
                    Top = 48,
                    Width = 40,
                    Height = 208,
                    Minimum = -12,
                    Maximum = 12,
                    Value = 0,
                    TickFrequency = 3,
                    BackColor = BgColor,
                };

                _sliders[i].ValueChanged += (s, e) =>
                {
                    _valLabels[idx].Text =
                        $"{_sliders[idx].Value:+0;-0;0} dB";
                    GainChanged?.Invoke(GetGains());
                };

                Controls.AddRange(new Control[]
                {
                    _sliders[i], freqLbl, _valLabels[i]
                });
            }
        }

        public float[] GetGains() =>
            _sliders.Select(s => (float)s.Value).ToArray();

        public void SetGains(float[] gains)
        {
            for (int i = 0; i < Math.Min(10, gains.Length); i++)
            {
                _sliders[i].Value = (int)Math.Clamp(gains[i], -12, 12);
                _valLabels[i].Text = $"{_sliders[i].Value:+0;-0;0} dB";
            }
        }

        void Apply(int idx)
        {
            if (idx >= 0 && idx < Presets.Length)
                SetGains(Presets[idx]);
        }
    }
}
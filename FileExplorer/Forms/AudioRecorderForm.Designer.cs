namespace FileExplorer.Forms
{
    public partial class AudioRecorderForm
    {
        /// <summary>
        /// Construye toda la interfaz: titulo, cronometro, visualizador de forma de onda,
        /// medidor de nivel, selector de microfono y formato, nombre de archivo y botones.
        /// </summary>
        void InitializeComponent()
        {
            const int W  = 580;
            const int MX = 20;
            const int IW = W - MX * 2;

            Text            = "Grabador de audio";
            ClientSize      = new Size(W, 520);
            MinimumSize     = MaximumSize = new Size(W + 16, 559);
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = C_BG;
            Font            = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            DoubleBuffered  = true;
            FormClosed     += (s, e) => StopAndCleanup();

            // ── Título ────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text = "Grabador de audio", Left = 0, Top = 14, Width = W, Height = 28,
                ForeColor = C_TEXT, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
            });

            // ── Cronómetro ────────────────────────────────────────────
            lblTime = new Label
            {
                Text = "00:00:00", Left = 0, Top = 46, Width = W, Height = 72,
                ForeColor = C_TEXT, BackColor = Color.Transparent,
                Font = new Font("Consolas", 34f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            Controls.Add(lblTime);

            // ── Estado ────────────────────────────────────────────────
            lblStatus = new Label
            {
                Text = "Listo para grabar", Left = 0, Top = 122, Width = W, Height = 22,
                ForeColor = C_SUB, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            Controls.Add(lblStatus);

            // ── Visualizador de forma de onda ─────────────────────────
            pnlWave = new Panel { Left = MX, Top = 150, Width = IW, Height = 74, BackColor = C_SURFACE };
            pnlWave.Paint += PnlWave_Paint;
            pnlWave.Paint += (s, e) =>
            {
                using var pen = new Pen(C_BORDER, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, pnlWave.Width - 1, pnlWave.Height - 1);
            };
            Controls.Add(pnlWave);

            // ── Medidor de nivel ──────────────────────────────────────
            Controls.Add(new Label { Text = "Nivel:", Left = MX, Top = 236, Width = 50, Height = 20, ForeColor = C_SUB, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) });
            pnlLevel = new Panel { Left = MX + 56, Top = 234, Width = IW - 56, Height = 20, BackColor = C_CARD };
            pnlLevelFill = new Panel { Left = 0, Top = 0, Width = 0, Height = 20, BackColor = C_GREEN };
            pnlLevel.Controls.Add(pnlLevelFill);
            Controls.Add(pnlLevel);

            Controls.Add(Sep(264, W));

            // ── Micrófono ─────────────────────────────────────────────
            const int LW = 120, CX = MX + 120 + 8, CW = IW - 120 - 8;
            Controls.Add(RowLabel("Microfono:", MX, 278, LW));
            cboDevice = new ComboBox { Left = CX, Top = 274, Width = CW, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f), BackColor = C_CARD, ForeColor = C_TEXT };
            Controls.Add(cboDevice);

            // ── Formato ───────────────────────────────────────────────
            Controls.Add(RowLabel("Formato:", MX, 316, LW));
            cboFormat = new ComboBox { Left = CX, Top = 312, Width = 230, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f), BackColor = C_CARD, ForeColor = C_TEXT };
            cboFormat.Items.AddRange(new object[]
            {
                "WAV — 44.1 kHz 16bit Stereo",
                "WAV — 44.1 kHz 16bit Mono",
                "WAV — 22 kHz 16bit Mono",
                "MP3 — 128 kbps",
            });
            cboFormat.SelectedIndex = 0;
            Controls.Add(cboFormat);

            Controls.Add(Sep(350, W));

            // ── Nombre del archivo ────────────────────────────────────
            Controls.Add(RowLabel("Nombre:", MX, 364, LW));
            txtFileName = new TextBox
            {
                Left = CX, Top = 360, Width = CW, Height = 28,
                Font = new Font("Segoe UI", 9.5f), BackColor = C_CARD, ForeColor = C_TEXT,
                BorderStyle = BorderStyle.FixedSingle,
                Text = $"grabacion_{DateTime.Now:yyyyMMdd_HHmmss}",
            };
            Controls.Add(txtFileName);

            lblSavedPath = new Label { Left = MX, Top = 394, Width = IW, Height = 18, ForeColor = C_GREEN, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f), AutoEllipsis = true };
            Controls.Add(lblSavedPath);

            Controls.Add(Sep(420, W));

            // ── Botones ───────────────────────────────────────────────
            const int BTN_Y = 432, BTN_H = 36, GAP = 8;
            int btnW = (IW - GAP * 3) / 4;

            btnRecord = MkBtn("Grabar",   MX + 0 * (btnW + GAP), BTN_Y, btnW, BTN_H, C_RED,   C_RED2);
            btnPause  = MkBtn("Pausar",   MX + 1 * (btnW + GAP), BTN_Y, btnW, BTN_H, C_CARD,  C_BORDER);
            btnStop   = MkBtn("Detener",  MX + 2 * (btnW + GAP), BTN_Y, btnW, BTN_H, C_CARD,  C_BORDER);
            btnSave   = MkBtn("Guardar",  MX + 3 * (btnW + GAP), BTN_Y, btnW, BTN_H, C_ACCENT, Color.FromArgb(0, 99, 220));

            btnPause.Enabled = false; btnStop.Enabled = false; btnSave.Enabled = false;

            btnRecord.Click += BtnRecord_Click;
            btnPause.Click  += BtnPause_Click;
            btnStop.Click   += BtnStop_Click;
            btnSave.Click   += BtnSave_Click;

            Controls.AddRange(new Control[] { btnRecord, btnPause, btnStop, btnSave });

            // ── Tip ───────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text = "Puedes grabar, pausar, reanudar y guardar en WAV o MP3",
                Left = 0, Top = 490, Width = W, Height = 22,
                ForeColor = C_SUB, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8f), TextAlign = ContentAlignment.MiddleCenter,
            });
        }

        // ── Helpers de construcción ───────────────────────────────────

        /// <summary>Crea una linea separadora horizontal.</summary>
        Label Sep(int y, int w) => new Label { Left = 20, Top = y, Width = w - 40, Height = 1, BackColor = C_BORDER };

        /// <summary>Crea un label de campo con estilo del tema.</summary>
        Label RowLabel(string text, int x, int y, int w) => new Label
        {
            Text = text, Left = x, Top = y + 5, Width = w, Height = 20,
            ForeColor = C_SUB, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9f), TextAlign = ContentAlignment.MiddleLeft,
        };

        /// <summary>Crea un boton con esquinas redondeadas y color hover.</summary>
        Button MkBtn(string text, int x, int y, int w, int h, Color bg, Color hover)
        {
            var b = new Button
            {
                Text = text, Left = x, Top = y, Width = w, Height = h,
                FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 9f), Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = hover;
            b.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, w, h, 8, 8));
            return b;
        }

        /// <summary>
        /// Dibuja la forma de onda acumulada en el panel, usando rojo si esta grabando
        /// o gris si esta pausado/detenido.
        /// </summary>
        void PnlWave_Paint(object s, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(C_SURFACE);
            int w = pnlWave.Width, h = pnlWave.Height; float mid = h / 2f;
            using (var pen = new Pen(Color.FromArgb(60, 60, 70), 1)) g.DrawLine(pen, 0, (int)mid, w, (int)mid);
            List<float> snap;
            lock (_lock) snap = new List<float>(_samples);
            if (snap.Count < 2) return;
            using var wavePen = new Pen(_recording && !_paused ? C_RED : Color.FromArgb(80, 80, 100), 1.5f);
            int step = Math.Max(1, snap.Count / w);
            var pts = new List<PointF>();
            for (int i = 0; i < w && i * step < snap.Count; i++)
                pts.Add(new PointF(i, mid - snap[Math.Min(i * step, snap.Count - 1)] * (mid - 4)));
            if (pts.Count >= 2) g.DrawLines(wavePen, pts.ToArray());
        }
    }
}

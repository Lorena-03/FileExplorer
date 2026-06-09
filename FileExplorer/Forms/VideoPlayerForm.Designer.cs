using LibVLCSharp.WinForms;

namespace FileExplorer.Forms
{
    public partial class VideoPlayerForm
    {
        // Cambia el tipo de videoView al correcto para WinForms
        LibVLCSharp.WinForms.VideoView videoView;
        Panel pnlBottom;
        Label lblTitle;
        Panel pnlProgBg, pnlProgFg, pnlProgThumb;
        Label lblElapsed, lblTotal;
        Button btnPrev, btnPlay, btnStop, btnNext, btnFull;
        ComboBox cboSpeed;
        Panel pnlVolBg, pnlVolFg;
        Label lblVol, lblSpeedLbl;

        /// <summary>
        /// Construye la UI: título, área de video y barra inferior
        /// con progreso, botones, velocidad y volumen.
        /// </summary>
        void InitializeComponent()
        {
            Text = "Video — " + Path.GetFileName(_path);
            Size = new Size(1120, 700);
            MinimumSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.Black;
            Font = new Font("Segoe UI", 9.5f);
            KeyPreview = true;
            KeyDown += OnKey;

            // ── Título ────────────────────────────────────────────────
            lblTitle = new Label
            {
                Text = Path.GetFileName(_path),
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = SpPanel,
                ForeColor = SpWhite,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            // ── Área de video ─────────────────────────────────────────
            videoView = new LibVLCSharp.WinForms.VideoView { Dock = DockStyle.Fill, BackColor = Color.Black };
            videoView.DoubleClick += (s, e) => ToggleFull();
            videoView.MouseMove += (s, e) => { if (_full) ShowBarFullscreen(); };
            videoView.MouseWheel += (s, e) => { if (_full) ShowBarFullscreen(); };

            // ── Barra inferior ────────────────────────────────────────
            BuildBottomBar();

            Controls.AddRange(new Control[] { videoView, pnlBottom, lblTitle });

            _uiTimer.Tick += (s, e) => UpdateProgress();
            _hideTimer.Tick += (s, e) =>
            {
                if (_full) { pnlBottom.Visible = false; _hideTimer.Stop(); }
            };

            pnlBottom.Resize += (s, e) => DoLayout();
            Resize += (s, e) => DoLayout();
        }

        void BuildBottomBar()
        {
            pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 110,
                BackColor = SpBottom,
                Visible = true,
            };
            pnlBottom.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(55, 55, 55));
                e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
            };

            // Progreso
            pnlProgBg = new Panel { BackColor = SpBarBg, Cursor = Cursors.Hand };
            pnlProgFg = new Panel { BackColor = SpGreen, Location = Point.Empty };
            pnlProgThumb = new Panel { Size = new Size(12, 12), BackColor = SpWhite, Visible = false };
            pnlProgBg.Controls.AddRange(new Control[] { pnlProgFg, pnlProgThumb });
            pnlProgBg.MouseEnter += (s, e) => { pnlProgBg.Height = 7; pnlProgFg.Height = 7; pnlProgThumb.Visible = true; };
            pnlProgBg.MouseLeave += (s, e) =>
            {
                if (!_seekingProg) { pnlProgBg.Height = 4; pnlProgFg.Height = 4; pnlProgThumb.Visible = false; }
            };
            pnlProgBg.MouseDown += ProgDown;
            pnlProgBg.MouseMove += ProgMove;
            pnlProgBg.MouseUp += ProgUp;

            lblElapsed = MkLabel("0:00:00", 64);
            lblTotal = MkLabel("0:00:00", 64);

            // Botones
            btnPrev = MkCtrlBtn("⏮", 36, 36);
            btnPlay = MkPlayBtn();
            btnStop = MkCtrlBtn("⏹", 36, 36);
            btnNext = MkCtrlBtn("⏭", 36, 36);
            btnFull = MkCtrlBtn("⛶", 30, 30);

            new ToolTip().SetToolTip(btnPrev, "Retroceder 10 s  (←)");
            new ToolTip().SetToolTip(btnPlay, "Play / Pausa  (Espacio)");
            new ToolTip().SetToolTip(btnStop, "Detener");
            new ToolTip().SetToolTip(btnNext, "Avanzar 10 s  (→)");
            new ToolTip().SetToolTip(btnFull, "Pantalla completa  (F)");

            btnPrev.Click += (s, e) => Seek(-10);
            btnPlay.Click += (s, e) => Toggle();
            btnStop.Click += (s, e) => StopVideo();
            btnNext.Click += (s, e) => Seek(10);
            btnFull.Click += (s, e) => ToggleFull();

            // Velocidad
            lblSpeedLbl = new Label
            {
                Text = "Vel:",
                Width = 30,
                Height = 18,
                ForeColor = SpGray,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8f),
                TextAlign = ContentAlignment.MiddleRight,
            };
            cboSpeed = new ComboBox
            {
                Width = 68,
                Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = SpCard,
                ForeColor = SpWhite,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
            };
            cboSpeed.Items.AddRange(new object[] { "0.25×", "0.5×", "0.75×", "1×", "1.25×", "1.5×", "2×" });
            cboSpeed.SelectedIndex = 3;
            cboSpeed.SelectedIndexChanged += (s, e) =>
            {
                if (_mp == null) return;
                float[] r = { 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
                _mp.SetRate(r[cboSpeed.SelectedIndex]);
            };

            // Volumen
            var lblVolIco = new Label
            {
                Text = "🔊",
                Width = 22,
                Height = 22,
                ForeColor = SpGray,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10f),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            pnlVolBg = new Panel { BackColor = SpBarBg, Cursor = Cursors.Hand };
            pnlVolFg = new Panel { BackColor = SpGreen };
            pnlVolBg.Controls.Add(pnlVolFg);
            pnlVolBg.MouseEnter += (s, e) => { pnlVolBg.Height = 6; pnlVolFg.Height = 6; };
            pnlVolBg.MouseLeave += (s, e) =>
            {
                if (!_seekingVol) { pnlVolBg.Height = 4; pnlVolFg.Height = 4; }
            };
            pnlVolBg.MouseDown += VolDown;
            pnlVolBg.MouseMove += VolMove;
            pnlVolBg.MouseUp += VolUp;

            lblVol = new Label
            {
                Text = "80%",
                Width = 40,
                Height = 18,
                ForeColor = SpGray,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            pnlBottom.Controls.AddRange(new Control[]
            {
                pnlProgBg, lblElapsed, lblTotal,
                btnPrev, btnPlay, btnStop, btnNext, btnFull,
                lblSpeedLbl, cboSpeed,
                lblVolIco, pnlVolBg, lblVol,
            });
        }

        /// <summary>Posiciona todos los controles de la barra inferior.</summary>
        void DoLayout()
        {
            if (pnlBottom == null) return;
            int w = pnlBottom.Width;
            int cx = w / 2;

            const int marginX = 70, progY = 12;
            pnlProgBg.SetBounds(marginX, progY, w - marginX * 2, 4);
            pnlProgFg.Height = pnlProgBg.Height;
            lblElapsed.SetBounds(6, progY - 2, 62, 18);
            lblTotal.SetBounds(w - 68, progY - 2, 62, 18);
            lblTotal.TextAlign = ContentAlignment.MiddleRight;

            const int btnY = progY + 24;
            btnPlay.SetBounds(cx - 26, btnY, 52, 52);
            btnPrev.SetBounds(cx - 86, btnY + 8, 36, 36);
            btnNext.SetBounds(cx + 50, btnY + 8, 36, 36);
            btnStop.SetBounds(cx + 94, btnY + 8, 36, 36);
            btnFull.SetBounds(cx + 138, btnY + 12, 30, 30);

            int vy = btnY + 18;
            lblSpeedLbl.SetBounds(8, vy - 8, 30, 18);
            cboSpeed.SetBounds(40, vy - 4, 68, 24);

            var vi = pnlBottom.Controls.OfType<Label>()
                .FirstOrDefault(l => l.Text == "🔊");
            if (vi != null) vi.SetBounds(w - 178, vy - 10, 22, 22);
            pnlVolBg.SetBounds(w - 152, vy, 90, 4);
            pnlVolFg.Width = (int)(90 * _volume);
            lblVol.SetBounds(w - 56, vy - 8, 46, 18);
        }

        // ── Helpers de UI ─────────────────────────────────────────────

        Label MkLabel(string text, int w) => new()
        {
            Text = text,
            Width = w,
            Height = 18,
            ForeColor = SpGray,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8.5f),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        Button MkCtrlBtn(string text, int w, int h)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(w, h),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", w <= 30 ? 10f : 14f),
                ForeColor = SpGray,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.Transparent;
            b.MouseEnter += (s, e) => { if (b.ForeColor != SpGreen) b.ForeColor = SpWhite; };
            b.MouseLeave += (s, e) => { if (b.ForeColor != SpGreen) b.ForeColor = SpGray; };
            return b;
        }

        Button MkPlayBtn()
        {
            var b = new Button
            {
                Text = "▶",
                Size = new Size(52, 52),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 18f),
                ForeColor = Color.Black,
                BackColor = SpGreen,
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = SpGreenH;
            b.Paint += (s, e) =>
            {
                var r = b.ClientRectangle; r.Inflate(-1, -1);
                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddEllipse(r);
                b.Region = new Region(path);
                e.Graphics.SmoothingMode =
                    System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(b.BackColor), r);
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                e.Graphics.DrawString(b.Text, b.Font,
                    new SolidBrush(Color.Black),
                    new RectangleF(0, 0, b.Width, b.Height), sf);
            };
            return b;
        }
    }
}
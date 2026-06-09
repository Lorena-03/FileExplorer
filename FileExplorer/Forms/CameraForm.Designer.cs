namespace FileExplorer.Forms
{
    public partial class CameraForm
    {
        // ── Controles ─────────────────────────────────────────────────
        PictureBox _preview, _pbMiniatura;
        Panel _pnlFlash, _pnlRecDot;
        ComboBox _cboCamara;
        Label _lblEstado, _lblContador, _lblVideoTimer;
        Button _btnCapturar, _btnGuardar, _btnGrabarVideo, _btnGuardarVideo;

        [System.Runtime.InteropServices.DllImport("Gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int cw, int ch);

        /// <summary>
        /// Construye toda la interfaz: título, selector de cámara, preview,
        /// flash, punto REC, miniatura, estado y botones de acción.
        /// </summary>
        void InitializeComponent()
        {
            Text = "Cámara";
            Size = new Size(820, 660);
            MinimumSize = new Size(680, 560);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = C_BG;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            DoubleBuffered = true;

            // ── Título ────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text = "Cámara",
                Left = 0,
                Top = 12,
                Width = 820,
                Height = 28,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            });

            // ── Selector de cámara ────────────────────────────────────
            Controls.Add(new Label
            {
                Text = "Cámara:",
                Left = 20,
                Top = 50,
                Width = 70,
                Height = 22,
                ForeColor = C_SUB,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9f),
            });

            _cboCamara = new ComboBox
            {
                Left = 94,
                Top = 46,
                Width = 310,
                Height = 26,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = C_CARD,
                ForeColor = C_TEXT,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            _cboCamara.SelectedIndexChanged += (s, e) => CambiarCamara();
            Controls.Add(_cboCamara);

            // ── Contador ──────────────────────────────────────────────
            _lblContador = new Label
            {
                Text = "0 fotos",
                Left = 420,
                Top = 50,
                Width = 200,
                Height = 22,
                ForeColor = C_SUB,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            Controls.Add(_lblContador);

            // ── Timer de grabación ────────────────────────────────────
            _lblVideoTimer = new Label
            {
                Text = "",
                Left = 630,
                Top = 50,
                Width = 140,
                Height = 22,
                ForeColor = C_RED,
                BackColor = Color.Transparent,
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Visible = false,
            };
            Controls.Add(_lblVideoTimer);

            // ── Preview ───────────────────────────────────────────────
            _preview = new PictureBox
            {
                Left = 20,
                Top = 80,
                Width = 620,
                Height = 440,
                BackColor = C_SURFACE,
                SizeMode = PictureBoxSizeMode.Zoom,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
                          | AnchorStyles.Right | AnchorStyles.Bottom,
            };
            _preview.Paint += PnlPreview_Paint;
            Controls.Add(_preview);

            // ── Flash ─────────────────────────────────────────────────
            _pnlFlash = new Panel
            {
                Left = 20,
                Top = 80,
                Width = 620,
                Height = 440,
                BackColor = Color.White,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
                       | AnchorStyles.Right | AnchorStyles.Bottom,
            };
            Controls.Add(_pnlFlash);

            // ── Punto rojo REC — círculo via Paint ────────────────────
            _pnlRecDot = new Panel
            {
                Left = 28,
                Top = 88,
                Width = 14,
                Height = 14,
                BackColor = Color.Transparent,
                Visible = false,
            };
            _pnlRecDot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(C_RED), 0, 0, 13, 13);
            };
            Controls.Add(_pnlRecDot);

            // ── Miniatura ─────────────────────────────────────────────
            _pbMiniatura = new PictureBox
            {
                Left = 650,
                Top = 80,
                Width = 140,
                Height = 106,
                BackColor = C_CARD,
                SizeMode = PictureBoxSizeMode.Zoom,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Visible = false,
            };
            Controls.Add(_pbMiniatura);
            Controls.Add(new Label
            {
                Text = "Última foto",
                Left = 650,
                Top = 190,
                Width = 140,
                Height = 18,
                ForeColor = C_SUB,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8f),
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            });

            // ── Estado ────────────────────────────────────────────────
            _lblEstado = new Label
            {
                Text = "Iniciando cámara...",
                Left = 20,
                Top = 530,
                Width = 560,
                Height = 22,
                ForeColor = C_SUB,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            Controls.Add(_lblEstado);

            // ── Botones ───────────────────────────────────────────────
            const int BY = 560, BH = 34;

            _btnCapturar = MkBtn("📷  Capturar", 20, BY, 140, BH, C_RED, Color.FromArgb(185, 20, 20));
            _btnCapturar.Enabled = false;
            _btnCapturar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnCapturar.Click += BtnCapturar_Click;

            _btnGuardar = MkBtn("💾  Guardar foto", 168, BY, 155, BH, C_ACCENT, Color.FromArgb(0, 99, 220));
            _btnGuardar.Enabled = false;
            _btnGuardar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnGuardar.Click += BtnGuardar_Click;

            _btnGrabarVideo = MkBtn("⏺  Grabar video", 331, BY, 155, BH, Color.FromArgb(180, 40, 40), Color.FromArgb(140, 20, 20));
            _btnGrabarVideo.Enabled = false;
            _btnGrabarVideo.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnGrabarVideo.Click += BtnGrabarVideo_Click;

            _btnGuardarVideo = MkBtn("💾  Guardar video", 494, BY, 160, BH, Color.FromArgb(80, 160, 60), Color.FromArgb(55, 120, 40));
            _btnGuardarVideo.Enabled = false;
            _btnGuardarVideo.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnGuardarVideo.Click += BtnGuardarVideo_Click;

            var btnCerrar = MkBtn("✕  Cerrar", 662, BY, 110, BH, C_CARD, C_BORDER);
            btnCerrar.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCerrar.Click += (_, __) => Close();

            Controls.AddRange(new Control[]
            {
                _btnCapturar, _btnGuardar, _btnGrabarVideo, _btnGuardarVideo, btnCerrar,
            });
        }

        /// <summary>
        /// Dibuja el mensaje de estado en el preview cuando no hay imagen.
        /// </summary>
        void PnlPreview_Paint(object s, PaintEventArgs e)
        {
            if (_preview.Image != null) return;
            string msg = _devices?.Count == 0 ? "No se detectó ninguna cámara" : "Conectando...";
            using var font = new Font("Segoe UI", 14f);
            using var brush = new SolidBrush(C_SUB);
            var sz = e.Graphics.MeasureString(msg, font);
            e.Graphics.DrawString(msg, font, brush,
                (_preview.Width - sz.Width) / 2f,
                (_preview.Height - sz.Height) / 2f);
        }

        /// <summary>Crea un botón plano con color hover.</summary>
        Button MkBtn(string text, int x, int y, int w, int h, Color bg, Color hover)
        {
            var b = new Button
            {
                Text = text,
                Left = x,
                Top = y,
                Width = w,
                Height = h,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = hover;
            return b;
        }
    }
}
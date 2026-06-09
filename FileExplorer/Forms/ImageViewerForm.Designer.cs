using FileExplorer.Helpers;
using FileExplorer.Models;

namespace FileExplorer.Forms
{
    public partial class ImageViewerForm : Form
    {
        // ── Colores del tema ─────────────────────────────────────────
        static readonly Color C_BG = Color.FromArgb(24, 24, 26);
        static readonly Color C_SURFACE = Color.FromArgb(36, 36, 40);
        static readonly Color C_CARD = Color.FromArgb(52, 52, 58);
        static readonly Color C_BORDER = Color.FromArgb(68, 68, 76);
        static readonly Color C_ACCENT = Color.FromArgb(10, 132, 255);
        static readonly Color C_TEXT = Color.FromArgb(242, 242, 247);
        static readonly Color C_SUB = Color.FromArgb(142, 142, 148);
        static readonly Color C_HOVER = Color.FromArgb(74, 74, 82);

        // ── Controles principales ────────────────────────────────────
        Panel topBar, editBar, strip, infoPanel, centerPanel;
        PictureBox pic;
        Label lblTopName, lblInfoName, lblSize, lblDim, lblMapMsg;
        TextBox txtLat, txtLon;
        Button btnAddGps, btnCrop, btnDraw, btnDrawColor, btnDrawSize, btnText;
        TrackBar trkBright, trkContr;
        OsmMapPanel mapPanel;

        /// <summary>
        /// Inicializa y compone todos los paneles de la ventana.
        /// </summary>
        void InitializeComponent()
        {
            Text = "Visor de imágenes";
            Size = new Size(1340, 840);
            MinimumSize = new Size(1000, 660);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = C_BG;
            Font = new Font("Segoe UI", 9.5f);
            KeyPreview = true;
            DoubleBuffered = true;

            KeyDown += Form_KeyDown;

            BuildTopBar();
            BuildEditBar();
            BuildStrip();
            BuildInfoPanel();
            BuildCenter();

            Controls.Add(centerPanel);
            Controls.Add(infoPanel);
            Controls.Add(strip);
            Controls.Add(editBar);
            Controls.Add(topBar);
        }

        // ── Top bar ──────────────────────────────────────────────────

        /// <summary>
        /// Construye la barra superior con título y botones de navegación.
        /// </summary>
        void BuildTopBar()
        {
            topBar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = C_SURFACE };
            topBar.Paint += PaintBottomBorder;

            lblTopName = new Label
            {
                Left = 16,
                Top = 14,
                Width = 440,
                Height = 22,
                ForeColor = C_TEXT,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                AutoEllipsis = true
            };
            topBar.Controls.Add(lblTopName);

            int rx = topBar.Width - 16;
            NavBtn(topBar, ref rx, "⤡  Ajustar", () => SetZoom(0f));
            NavBtn(topBar, ref rx, "Siguiente  ▶", () => Navigate(1));
            NavBtn(topBar, ref rx, "◀  Anterior", () => Navigate(-1));
            NavBtn(topBar, ref rx, "📧  Enviar", () => new EmailForm(_allImages[_currentIdx]).Show(this));
            NavBtn(topBar, ref rx, "🗑  Eliminar", () => DeleteCurrentImage(), Color.FromArgb(160, 30, 30));

            topBar.Resize += (_, __) =>
            {
                int r = topBar.Width - 16;
                foreach (Control c in topBar.Controls)
                    if (c is Button b)
                    { r -= b.Width + 6; b.Left = r; }
            };
        }

        /// <summary>
        /// Construye la barra de edición con transformaciones, filtros y herramientas.
        /// </summary>
        void BuildEditBar()
        {
            editBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(28, 28, 32) };
            editBar.Paint += PaintBottomBorder;
            int x = 12;

            GroupLbl(editBar, "TRANSFORMAR", x);
            ToolBtn(editBar, ref x, "↻", "Rotar →", RotateCW);
            ToolBtn(editBar, ref x, "↺", "Rotar ←", RotateCCW);
            ToolBtn(editBar, ref x, "↔", "Voltear H", FlipH);
            ToolBtn(editBar, ref x, "↕", "Voltear V", FlipV);
            VSep(editBar, ref x);

            GroupLbl(editBar, "FILTROS", x);
            ToolBtn(editBar, ref x, "◼", "Escala gris", ApplyGrayscale);
            ToolBtn(editBar, ref x, "🟫", "Sepia", ApplySepia);
            VSep(editBar, ref x);

            GroupLbl(editBar, "BRILLO", x);
            SliderGrp(editBar, ref x, ref trkBright, -10, 10, 0,
                v => { _brightness = v / 10f; ApplyFilters(); });

            GroupLbl(editBar, "CONTRASTE", x);
            SliderGrp(editBar, ref x, ref trkContr, 1, 30, 10,
                v => { _contrast = v / 10f; ApplyFilters(); });
            VSep(editBar, ref x);

            GroupLbl(editBar, "HERRAMIENTAS", x);
            btnCrop = ToolBtn(editBar, ref x, "✂", "Recortar", ToggleCrop);
            btnDraw = ToolBtn(editBar, ref x, "✏", "Dibujar a mano", ToggleDraw);

            btnDrawColor = new Button
            {
                Left = x,
                Top = 12,
                Width = 30,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = _drawColor,
                Cursor = Cursors.Hand
            };
            btnDrawColor.FlatAppearance.BorderSize = 1;
            btnDrawColor.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 130);
            btnDrawColor.Click += (_, __) => PickDrawColor();
            new ToolTip().SetToolTip(btnDrawColor, "Color del pincel");
            editBar.Controls.Add(btnDrawColor);
            x += 34;

            btnDrawSize = new Button
            {
                Left = x,
                Top = 12,
                Width = 36,
                Height = 30,
                Text = "S",
                FlatStyle = FlatStyle.Flat,
                BackColor = C_CARD,
                ForeColor = C_TEXT,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnDrawSize.FlatAppearance.BorderSize = 0;
            btnDrawSize.Click += (_, __) => CycleBrushSize();
            new ToolTip().SetToolTip(btnDrawSize, "Tamaño del pincel");
            editBar.Controls.Add(btnDrawSize);
            x += 40;
            VSep(editBar, ref x);

            GroupLbl(editBar, "TEXTO", x);
            btnText = ToolBtn(editBar, ref x, "T", "Agregar texto a la imagen", ToggleTextMode);
            btnText.Font = new Font("Segoe UI", 13f, FontStyle.Bold);

            int rx = editBar.Width - 12;
            ActionBtn(editBar, ref rx, "💾  Guardar como", SaveImageAs, C_ACCENT);
            ActionBtn(editBar, ref rx, "🔄  Convertir", ShowImageConvertDialog, Color.FromArgb(60, 60, 70));
            ActionBtn(editBar, ref rx, "↩  Restablecer", ResetEdits, C_CARD);
        }

        /// <summary>
        /// Construye la tira de miniaturas en la parte inferior.
        /// </summary>
        void BuildStrip()
        {
            strip = new Panel { Dock = DockStyle.Bottom, Height = 90, BackColor = Color.FromArgb(18, 18, 20) };
            strip.Paint += (s, e) =>
            {
                using var p = new Pen(C_BORDER);
                e.Graphics.DrawLine(p, 0, 0, strip.Width, 0);
            };
        }

        /// <summary>
        /// Construye el panel lateral con info de archivo y coordenadas GPS.
        /// </summary>
        void BuildInfoPanel()
        {
            infoPanel = new Panel { Dock = DockStyle.Right, Width = 290, BackColor = C_SURFACE, AutoScroll = true };
            infoPanel.Paint += (s, e) =>
            {
                using var p = new Pen(C_BORDER);
                e.Graphics.DrawLine(p, 0, 0, 0, infoPanel.Height);
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(14),
                BackColor = Color.Transparent
            };

            lblInfoName = new Label
            {
                Width = 262,
                Height = 22,
                ForeColor = C_TEXT,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                AutoEllipsis = true,
                Margin = new Padding(0, 0, 0, 2)
            };
            lblDim = InfoLbl(262, C_SUB, 8.5f);
            lblSize = InfoLbl(262, C_SUB, 8.5f);

            var sepGps = SecHdr("📍  GPS / Ubicación");
            var rowLat = CoordRow("Latitud", out txtLat);
            var rowLon = CoordRow("Longitud", out txtLon);
            var btnGmaps = MapBtn("🗺  Abrir en Google Maps", C_ACCENT);
            var btnOsm = MapBtn("🗺  Abrir en OpenStreetMap", C_CARD);
            btnGmaps.Click += (_, __) => OpenInMaps(false);
            btnOsm.Click += (_, __) => OpenInMaps(true);

            btnAddGps = MapBtn("📍  Agregar coordenadas GPS", Color.FromArgb(52, 58, 52));
            btnAddGps.ForeColor = Color.FromArgb(48, 209, 88);
            btnAddGps.Click += (_, __) => ShowAddGpsDialog();

            var sepMap = SecHdr("🗺  Vista de mapa");
            mapPanel = new OsmMapPanel { Width = 262, Height = 220, Margin = new Padding(0, 0, 0, 8) };
            mapPanel.Click += (_, __) => OpenInMaps(true);

            lblMapMsg = new Label
            {
                Width = 262,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = C_SUB,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f),
                Text = "Sin coordenadas GPS"
            };

            flow.Controls.AddRange(new Control[]
            {
                lblInfoName, lblDim, lblSize,
                sepGps, rowLat, rowLon, btnGmaps, btnOsm, btnAddGps,
                sepMap, mapPanel, lblMapMsg
            });
            infoPanel.Controls.Add(flow);
        }

        /// <summary>
        /// Construye el área central con el PictureBox y botones de navegación superpuestos.
        /// </summary>
        void BuildCenter()
        {
            centerPanel = new Panel { Dock = DockStyle.Fill, BackColor = C_BG };

            pic = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = C_BG,
                Cursor = Cursors.Default
            };
            pic.MouseDown += Pic_MouseDown;
            pic.MouseMove += Pic_MouseMove;
            pic.MouseUp += Pic_MouseUp;
            pic.Paint += Pic_Paint;
            pic.MouseWheel += (s, e) => ChangeZoom(e.Delta > 0 ? +0.25f : -0.25f);
            pic.MouseEnter += (_, __) => pic.Focus();

            var btnPrev = ArrowBtn("❮");
            var btnNext = ArrowBtn("❯");
            btnPrev.Click += (_, __) => Navigate(-1);
            btnNext.Click += (_, __) => Navigate(1);

            centerPanel.Resize += (_, __) =>
            {
                int cy = centerPanel.Height / 2 - 40;
                btnPrev.Left = 12; btnPrev.Top = cy;
                btnNext.Left = centerPanel.Width - 60; btnNext.Top = cy;
            };

            centerPanel.Controls.Add(btnNext);
            centerPanel.Controls.Add(btnPrev);
            centerPanel.Controls.Add(pic);
        }

        // ── Helpers de construcción de controles ─────────────────────

        /// <summary>
        /// Crea un botón de flecha semitransparente para navegación sobre la imagen.
        /// </summary>
        Button ArrowBtn(string t)
        {
            var b = new Button
            {
                Width = 48,
                Height = 80,
                Text = t,
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(140, 0, 0, 0),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 0, 0, 0);
            return b;
        }

        /// <summary>
        /// Agrega un label de grupo (etiqueta de sección) a la barra de edición.
        /// </summary>
        void GroupLbl(Panel p, string text, int x) =>
            p.Controls.Add(new Label
            {
                Text = text,
                Left = x,
                Top = 3,
                AutoSize = true,
                ForeColor = Color.FromArgb(110, 110, 120),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 7f, FontStyle.Bold)
            });

        /// <summary>
        /// Agrega un botón de navegación anclado a la derecha del panel.
        /// </summary>
        void NavBtn(Panel p, ref int rx, string text, Action act, Color? bg = null)
        {
            var b = new Button
            {
                Text = text,
                Width = TextRenderer.MeasureText(text, new Font("Segoe UI", 9f)).Width + 28,
                Height = 32,
                Top = 9,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg ?? C_CARD,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = bg.HasValue ? bg.Value : C_HOVER;
            rx -= b.Width + 6;
            b.Left = rx;
            b.Click += (_, __) => act();
            p.Controls.Add(b);
        }

        /// <summary>
        /// Agrega un botón de herramienta con icono y tooltip a la barra de edición.
        /// </summary>
        Button ToolBtn(Panel p, ref int x, string icon, string tip, Action act)
        {
            var b = new Button
            {
                Text = icon,
                Left = x,
                Top = 10,
                Width = 44,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_CARD,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 10f),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = C_HOVER;
            new ToolTip().SetToolTip(b, tip);
            b.Click += (_, __) => act();
            p.Controls.Add(b);
            x += 48;
            return b;
        }

        /// <summary>
        /// Agrega un botón de acción anclado a la derecha de la barra de edición.
        /// </summary>
        void ActionBtn(Panel p, ref int rx, string text, Action act, Color bg)
        {
            var b = new Button
            {
                Text = text,
                Width = TextRenderer.MeasureText(text, new Font("Segoe UI", 9f)).Width + 28,
                Height = 32,
                Top = 10,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = C_HOVER;
            rx -= b.Width + 6;
            b.Left = rx;
            b.Click += (_, __) => act();
            p.Controls.Add(b);
        }

        /// <summary>
        /// Agrega un separador vertical a la barra de edición.
        /// </summary>
        void VSep(Panel p, ref int x)
        {
            p.Controls.Add(new Label { Left = x + 4, Top = 8, Width = 1, Height = 34, BackColor = C_BORDER });
            x += 14;
        }

        /// <summary>
        /// Agrega un TrackBar con sus eventos de cambio de valor.
        /// </summary>
        void SliderGrp(Panel p, ref int x, ref TrackBar trk, int min, int max, int val, Action<int> onChange)
        {
            var tb = new TrackBar
            {
                Left = x,
                Top = 18,
                Width = 90,
                Height = 28,
                Minimum = min,
                Maximum = max,
                Value = val,
                TickStyle = TickStyle.None,
                BackColor = C_BG
            };
            tb.ValueChanged += (_, __) => onChange(tb.Value);
            trk = tb;
            p.Controls.Add(tb);
            x += 96;
        }

        /// <summary>
        /// Crea un encabezado de sección con línea separadora para el panel de info.
        /// </summary>
        Panel SecHdr(string text)
        {
            var pnl = new Panel { Width = 262, Height = 32, Margin = new Padding(0, 10, 0, 6) };
            pnl.Controls.Add(new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                ForeColor = C_TEXT,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft
            });
            pnl.Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 1, BackColor = C_BORDER });
            return pnl;
        }

        /// <summary>
        /// Crea una fila de coordenada GPS con label y TextBox de solo lectura.
        /// </summary>
        Panel CoordRow(string label, out TextBox box)
        {
            var pnl = new Panel { Width = 262, Height = 30, Margin = new Padding(0, 0, 0, 4) };
            pnl.Controls.Add(new Label
            {
                Text = label + ":",
                Left = 0,
                Top = 7,
                Width = 68,
                Height = 16,
                ForeColor = C_SUB,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f)
            });
            box = new TextBox
            {
                Left = 72,
                Top = 4,
                Width = 190,
                Height = 22,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = C_CARD,
                ForeColor = C_TEXT,
                Font = new Font("Consolas", 9.5f),
                Text = "—"
            };
            pnl.Controls.Add(box);
            return pnl;
        }

        /// <summary>
        /// Crea un botón de mapa con ancho fijo y estilo flat.
        /// </summary>
        Button MapBtn(string text, Color bg)
        {
            var b = new Button
            {
                Text = text,
                Width = 262,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 6),
                TextAlign = ContentAlignment.MiddleCenter
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = C_HOVER;
            return b;
        }

        /// <summary>
        /// Crea un Label estándar para el panel de información.
        /// </summary>
        Label InfoLbl(int w, Color fg, float size) => new Label
        {
            Width = w,
            Height = 18,
            ForeColor = fg,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", size),
            Margin = new Padding(0, 0, 0, 2)
        };

        /// <summary>
        /// Dibuja el borde inferior de un panel (usado en Paint events).
        /// </summary>
        void PaintBottomBorder(object s, PaintEventArgs e)
        {
            var p2 = (Panel)s;
            using var pen = new Pen(C_BORDER, 1);
            e.Graphics.DrawLine(pen, 0, p2.Height - 1, p2.Width, p2.Height - 1);
        }
    }
}
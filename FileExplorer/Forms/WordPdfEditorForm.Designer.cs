using System.Text;
using WDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument;
using WBody = DocumentFormat.OpenXml.Wordprocessing.Body;
using WPara = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace FileExplorer.Forms
{
    public partial class WordPdfEditorForm
    {
        RichTextBox _rtb;
        ComboBox _cboFont, _cboSize;
        Button _btnBold, _btnItalic, _btnUnder, _btnStrike;
        Button _btnAlignL, _btnAlignC, _btnAlignR, _btnAlignJ;
        Button _btnColor, _btnBgColor;
        Button _btnBullet, _btnNumber;
        Button _btnTable, _btnImage;
        Button _btnSave;
        Label _lblInfo;
        Panel _pnlColor;

        // ToolStrip nativo — maneja DPI automáticamente
        ToolStrip _ts;
        private ToolStripButton _tsBtnTextColor;
        private ToolStripButton _tsBtnBgColor;

        void InitUI()
        {
            Text = Path.GetFileName(_filePath) + " — Editor";
            Size = new Size(1100, 760);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = C_BG;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;
            KeyPreview = true;
            KeyDown += OnKeyDown;

            BuildToolbar();
            BuildEditor();
            BuildStatusBar();
        }

        /// <summary>
        /// Toolbar basada en ToolStrip nativo — DPI-aware por defecto.
        /// Agrupa: Fuente | Tamaño | Estilos | Color | Alineación | Listas | Insertar | Guardar
        /// </summary>
        void BuildToolbar()
        {
            _ts = new ToolStrip
            {
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 252),
                GripStyle = ToolStripGripStyle.Hidden,
                RenderMode = ToolStripRenderMode.System,
                Padding = new Padding(4, 2, 4, 2),
                Font = new Font("Segoe UI", 9.5f),
            };

            // ── Fuente ────────────────────────────────────────────────
            var tsCboFont = new ToolStripControlHost(new ComboBox
            {
                Width = 148,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.White,
            })
            { Margin = new Padding(2, 1, 2, 1) };
            _cboFont = (ComboBox)tsCboFont.Control;
            foreach (var f in new[] { "Segoe UI","Arial","Times New Roman","Courier New",
                                       "Calibri","Verdana","Georgia","Tahoma","Impact" })
                _cboFont.Items.Add(f);
            _cboFont.SelectedIndex = 0;
            _cboFont.SelectedIndexChanged += (_, __) => ApplyFont();
            _ts.Items.Add(tsCboFont);

            // ── Tamaño ────────────────────────────────────────────────
            var tsCboSize = new ToolStripControlHost(new ComboBox
            {
                Width = 52,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.White,
            })
            { Margin = new Padding(2, 1, 4, 1) };
            _cboSize = (ComboBox)tsCboSize.Control;
            foreach (var s in new[] { "8", "9", "10", "11", "12", "14", "16", "18", "20", "24", "28", "32", "36", "48", "72" })
                _cboSize.Items.Add(s);
            _cboSize.SelectedItem = "11";
            _cboSize.SelectedIndexChanged += (_, __) => ApplySize();
            _ts.Items.Add(tsCboSize);

            _ts.Items.Add(new ToolStripSeparator());

            // ── Estilos ───────────────────────────────────────────────
            _btnBold = AddTsBtn("B", "Negrita (Ctrl+B)", () => ToggleStyle(FontStyle.Bold), new Font("Segoe UI", 10f, FontStyle.Bold));
            _btnItalic = AddTsBtn("I", "Cursiva (Ctrl+I)", () => ToggleStyle(FontStyle.Italic), new Font("Segoe UI", 10f, FontStyle.Italic));
            _btnUnder = AddTsBtn("U", "Subrayado (Ctrl+U)", () => ToggleStyle(FontStyle.Underline), new Font("Segoe UI", 10f, FontStyle.Underline));
            // Tachado eliminado de la toolbar (sigue disponible via código)
            _btnStrike = new Button { Visible = false }; Controls.Add(_btnStrike);

            _ts.Items.Add(new ToolStripSeparator());

            // ── Color texto y fondo — ToolStripButton con owner-draw ──
            // Panel dummy para mantener referencia _pnlColor (usado en Invalidate)
            _pnlColor = new Panel { Visible = false };
            Controls.Add(_pnlColor);
            _btnColor = new Button { Visible = false }; Controls.Add(_btnColor);
            _btnBgColor = new Button { Visible = false }; Controls.Add(_btnBgColor);

            // Botón "A" con franja de color de texto
            var tsBtnTextColor = new ToolStripButton
            {
                ToolTipText = "Color de texto (clic para cambiar)",
                AutoSize = false,
                Width = 34,
                Height = 24,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Margin = new Padding(2, 1, 0, 1),
            };
            tsBtnTextColor.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // Letra A
                using var fnt = new Font("Segoe UI", 11f, FontStyle.Bold);
                using var br0 = new SolidBrush(Color.FromArgb(28, 28, 30));
                g.DrawString("A", fnt, br0, 6, -1);
                // Franja color texto
                using var br = new SolidBrush(_currentColor == Color.Empty ? Color.Black : _currentColor);
                g.FillRectangle(br, 4, 19, 26, 4);
            };
            tsBtnTextColor.Click += (_, __) => { PickColor(false); tsBtnTextColor.Invalidate(); };
            _ts.Items.Add(tsBtnTextColor);

            // Botón "ab" con franja de color de fondo
            var tsBtnBgColor = new ToolStripButton
            {
                ToolTipText = "Color de fondo / resaltado",
                AutoSize = false,
                Width = 36,
                Height = 24,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Margin = new Padding(0, 1, 4, 1),
            };
            tsBtnBgColor.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var fnt = new Font("Segoe UI", 8.5f);
                using var br0 = new SolidBrush(Color.FromArgb(28, 28, 30));
                g.DrawString("ab", fnt, br0, 5, 2);
                Color stripe = (_currentBgColor == Color.Transparent || _currentBgColor == Color.Empty)
                    ? Color.FromArgb(255, 220, 0) : _currentBgColor;
                using var br = new SolidBrush(stripe);
                g.FillRectangle(br, 4, 19, 28, 4);
            };
            tsBtnBgColor.Click += (_, __) => { PickColor(true); tsBtnBgColor.Invalidate(); };
            _ts.Items.Add(tsBtnBgColor);

            // Guardar referencia a los botones owner-draw para invalidar desde PickColor
            _tsBtnTextColor = tsBtnTextColor;
            _tsBtnBgColor = tsBtnBgColor;

            _ts.Items.Add(new ToolStripSeparator());

            // ── Alineación ────────────────────────────────────────────
            _btnAlignL = AddTsBtn("≡L", "Alinear izquierda", () => SetAlign(HorizontalAlignment.Left), new Font("Segoe UI", 9f));
            _btnAlignC = AddTsBtn("≡C", "Centrar", () => SetAlign(HorizontalAlignment.Center), new Font("Segoe UI", 9f));
            _btnAlignR = AddTsBtn("≡R", "Alinear derecha", () => SetAlign(HorizontalAlignment.Right), new Font("Segoe UI", 9f));
            _btnAlignJ = AddTsBtn("≡J", "Justificar", () => SetAlignJustify(), new Font("Segoe UI", 9f));

            _ts.Items.Add(new ToolStripSeparator());

            // ── Listas ────────────────────────────────────────────────
            _btnBullet = AddTsBtn("• Lista", "Lista con viñetas", InsertBullet, new Font("Segoe UI", 9f), 70);
            _btnNumber = AddTsBtn("1. Lista", "Lista numerada", InsertNumbered, new Font("Segoe UI", 9f), 70);

            _ts.Items.Add(new ToolStripSeparator());

            // ── Insertar ──────────────────────────────────────────────
            _btnImage = AddTsBtn("Imagen", "Insertar imagen", InsertImage, new Font("Segoe UI", 9f), 64);
            _btnTable = AddTsBtn("Tabla", "Insertar tabla", InsertTable, new Font("Segoe UI", 9f), 58);

            // ── Guardar (alineado a la derecha) ──────────────────────
            _ts.Items.Add(new ToolStripSeparator());

            var tsBtnSave = new ToolStripButton("Guardar")
            {
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = C_ACCENT,
                ForeColor = Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Margin = new Padding(2, 1, 4, 1),
                Padding = new Padding(12, 3, 12, 3),
                AutoToolTip = false,
                Alignment = ToolStripItemAlignment.Right,
            };
            tsBtnSave.Click += (_, __) => SaveFile();
            _ts.Items.Add(tsBtnSave);

            // Mantener referencia en _btnSave (panel dummy para compatibilidad)
            _btnSave = new Button { Visible = false };
            Controls.Add(_btnSave);

            Controls.Add(_ts);
        }

        /// <summary>Agrega un ToolStripButton estilizado al ToolStrip.</summary>
        Button AddTsBtn(string text, string tip, Action act, Font font, int w = 36)
        {
            // Wrapper Button invisible para mantener referencias (_btnBold etc.)
            var b = new Button
            {
                Text = text,
                Visible = false,
                Tag = text,
            };

            var tsb = new ToolStripButton(text)
            {
                ToolTipText = tip,
                Font = font,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                AutoToolTip = true,
                Padding = new Padding(3, 1, 3, 1),
                Margin = new Padding(1, 1, 1, 1),
                Tag = b,   // referencia al Button wrapper
            };
            tsb.Click += (_, __) =>
            {
                act();
                // Actualizar BackColor del wrapper
                b.PerformClick();
            };
            tsb.Click += (_, __) => act();
            _ts.Items.Add(tsb);

            // Conectar el estado activo al ToolStripButton
            b.Tag = tsb;
            return b;
        }

        // ── Override de UpdateToolbarState para ToolStrip ─────────────

        /// <summary>
        /// Actualiza el estado visual de los botones del ToolStrip según
        /// el formato de la selección actual en el RichTextBox.
        /// </summary>
        internal void UpdateToolbarStateTs()
        {
            if (_ts == null) return;
            var f = _rtb?.SelectionFont ?? _rtb?.Font;
            if (f == null) return;

            SetTsActive("B", f.Bold);
            SetTsActive("I", f.Italic);
            SetTsActive("U", f.Underline);
            SetTsActive("S", f.Strikeout);

            var align = _rtb?.SelectionAlignment ?? HorizontalAlignment.Left;
            SetTsActive("≡L", align == HorizontalAlignment.Left);
            SetTsActive("≡C", align == HorizontalAlignment.Center);
            SetTsActive("≡R", align == HorizontalAlignment.Right);

            // Fuente y tamaño
            if (_cboFont.Items.Contains(f.Name)) _cboFont.SelectedItem = f.Name;
            string sz = ((int)f.Size).ToString();
            if (_cboSize.Items.Contains(sz)) _cboSize.SelectedItem = sz;

            // Color
            if (_pnlColor != null) { _pnlColor.BackColor = _currentColor; _pnlColor.Invalidate(); }
        }

        void SetTsActive(string text, bool active)
        {
            foreach (ToolStripItem item in _ts.Items)
                if (item is ToolStripButton tsb && tsb.Text == text)
                {
                    tsb.BackColor = active
                        ? Color.FromArgb(210, 224, 255)
                        : Color.FromArgb(250, 250, 252);
                    break;
                }
        }

        void BuildEditor()
        {
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(200, 200, 206),
                AutoScroll = true,
            };

            var page = new Panel
            {
                Width = 794,
                Height = 1123,
                BackColor = Color.White,
                Left = 0,
                Top = 20,
            };
            page.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(185, 185, 192), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, page.Width - 1, page.Height - 1);
            };

            _rtb = new RichTextBox
            {
                Left = 72,
                Top = 72,
                Width = page.Width - 144,
                Height = page.Height - 144,
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                ForeColor = C_TEXT,
                ScrollBars = RichTextBoxScrollBars.None,
                WordWrap = true,
                AcceptsTab = true,
                DetectUrls = false,
            };
            _rtb.TextChanged += (_, __) => { _isDirty = true; UpdateInfo(); };
            _rtb.SelectionChanged += (_, __) => { UpdateToolbarState(); UpdateToolbarStateTs(); };

            page.Controls.Add(_rtb);
            scroll.Controls.Add(page);
            scroll.Resize += (_, __) =>
                page.Left = Math.Max(20, (scroll.Width - page.Width) / 2);

            Controls.Add(scroll);
        }

        void BuildStatusBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = Color.FromArgb(250, 250, 252),
            };
            bar.Paint += PaintBorderTop;
            _lblInfo = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = C_SUB,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f),
                Padding = new Padding(12, 0, 0, 0),
                Text = "Listo",
            };
            bar.Controls.Add(_lblInfo);
            Controls.Add(bar);
        }

        Button TBtn(Panel parent, ref int x, string text, string tip, Action act)
        {
            var b = new Button { Text = text, Visible = false };
            Controls.Add(b); return b;
        }

        void PaintBorder(object s, PaintEventArgs e)
        {
            var p = (Panel)s;
            using var pen = new Pen(C_BORDER);
            e.Graphics.DrawLine(pen, 0, p.Height - 1, p.Width, p.Height - 1);
        }

        void PaintBorderTop(object s, PaintEventArgs e)
        {
            using var pen = new Pen(C_BORDER);
            e.Graphics.DrawLine(pen, 0, 0, ((Panel)s).Width, 0);
        }
    }
}
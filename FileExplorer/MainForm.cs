using FileExplorer.Controls;
using FileExplorer.Forms;
using FileExplorer.Helpers;
using System.Drawing.Drawing2D;

namespace FileExplorer
{
    public class MainForm : Form
    {
        static readonly Color C_BG = Color.FromArgb(246, 246, 248);
        static readonly Color C_SIDEBAR = Color.FromArgb(240, 240, 243);
        static readonly Color C_TOOLBAR = Color.FromArgb(250, 250, 252);
        static readonly Color C_BORDER = Color.FromArgb(218, 218, 223);
        static readonly Color C_ACCENT = Color.FromArgb(10, 132, 255);
        static readonly Color C_ACCENT2 = Color.FromArgb(0, 99, 220);
        static readonly Color C_TEXT = Color.FromArgb(28, 28, 30);
        static readonly Color C_SUBTEXT = Color.FromArgb(142, 142, 147);
        static readonly Color C_HOVER = Color.FromArgb(228, 228, 234);
        static readonly Color C_SELECTED = Color.FromArgb(213, 228, 255);

        readonly Stack<string> _back = new();
        readonly Stack<string> _fwd = new();
        string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        SidebarControl _sidebar;
        FileListControl _fileList;
        Panel _homePanel;
        SplitContainer _split;
        StatusStrip _status;
        ToolStripStatusLabel _lbl;
        Button _btnBack, _btnFwd, _btnUp;
        TextBox _txtAddr, _txtSearch;

        readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 30_000 };
        Label _lblSaludoHome;
        Label _lblFechaHome;

        public MainForm()
        {
            try
            {
                BuildUI();
                _clockTimer.Tick += (_, __) => UpdateHomeClock();
                Shown += (s, e) =>
                {
                    try { if (_split.Width > 420) _split.SplitterDistance = 200; }
                    catch { }
                    ShowHome();
                };
                NavigateTo(_currentPath, false);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error en MainForm:\n\n" + ex.Message + "\n\n" + ex.StackTrace, "Error critico");
                throw;
            }
        }

        void UpdateHomeClock()
        {
            if (_lblSaludoHome == null || _lblFechaHome == null) return;
            string saludo = DateTime.Now.Hour < 12 ? "Buenos días" :
                            DateTime.Now.Hour < 19 ? "Buenas tardes" : "Buenas noches";
            string culturaES = DateTime.Now.ToString("dddd, d 'de' MMMM",
                new System.Globalization.CultureInfo("es-MX"));
            string horaStr = DateTime.Now.ToString("HH:mm");
            _lblSaludoHome.Text = saludo;
            _lblFechaHome.Text = $"{culturaES}   •   {horaStr}";
        }

        void BuildUI()
        {
            Text = "Explorador de Checo y Lore";
            Size = new Size(1280, 800);
            MinimumSize = new Size(960, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = C_BG;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            var toolbar = BuildToolbar();
            BuildStatus();
            BuildSplit();

            Controls.Add(_split);
            Controls.Add(toolbar);
            Controls.Add(_status);
        }

        Panel BuildToolbar()
        {
            var outer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = C_TOOLBAR,
                Padding = new Padding(0),
            };
            outer.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(C_BORDER), 0, outer.Height - 1, outer.Width, outer.Height - 1);

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 6,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 10, 8, 8),
                Margin = new Padding(0),
            };
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 196));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));

            var navPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _btnBack = NavBtn("‹", 0, "Atras  (Alt+←)");
            _btnFwd = NavBtn("›", 32, "Adelante  (Alt+→)");
            _btnUp = NavBtn("↑", 64, "Subir  (Alt+↑)");
            _btnBack.Enabled = false; _btnFwd.Enabled = false;
            _btnBack.Click += (_, __) => GoBack();
            _btnFwd.Click += (_, __) => GoFwd();
            _btnUp.Click += (_, __) => GoUp();
            navPanel.Controls.AddRange(new Control[] { _btnBack, _btnFwd, _btnUp });
            tlp.Controls.Add(navPanel, 0, 0);

            var addrPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Cursor = Cursors.IBeam,
                Margin = new Padding(0, 0, 6, 0),
            };
            addrPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(C_BORDER, 1f);
                using var path = RoundRect(new Rectangle(0, 0, addrPanel.Width - 1, addrPanel.Height - 1), 6);
                e.Graphics.DrawPath(pen, path);
            };
            _txtAddr = new TextBox
            {
                Left = 6,
                Top = 4,
                Height = 22,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                ForeColor = C_TEXT,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            };
            _txtAddr.KeyDown += (_, e) =>
            { if (e.KeyCode == Keys.Enter) NavigateTo(_txtAddr.Text.Trim(), true); };
            addrPanel.Controls.Add(_txtAddr);
            addrPanel.Resize += (_, __) => _txtAddr.Width = addrPanel.Width - 12;
            tlp.Controls.Add(addrPanel, 1, 0);

            var searchPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 6, 0),
            };
            searchPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(C_BORDER, 1f);
                using var path = RoundRect(new Rectangle(0, 0, searchPanel.Width - 1, searchPanel.Height - 1), 6);
                e.Graphics.DrawPath(pen, path);
            };
            _txtSearch = new TextBox
            {
                Left = 24,
                Top = 4,
                Width = 150,
                Height = 22,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                ForeColor = C_SUBTEXT,
                Text = "Buscar...",
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            };
            var lblSrch = new Label
            {
                Text = "⌕",
                Left = 4,
                Top = 3,
                Width = 20,
                Height = 22,
                Font = new Font("Segoe UI", 11f),
                ForeColor = C_SUBTEXT,
                BackColor = Color.Transparent,
            };
            _txtSearch.GotFocus += (_, __) =>
            {
                if (_txtSearch.ForeColor == C_SUBTEXT)
                { _txtSearch.Text = ""; _txtSearch.ForeColor = C_TEXT; }
            };
            _txtSearch.LostFocus += (_, __) =>
            {
                if (_txtSearch.Text == "")
                {
                    _txtSearch.Text = "Buscar..."; _txtSearch.ForeColor = C_SUBTEXT;
                    if (_fileList.Visible) _fileList.ClearFilter();
                }
            };
            var _searchTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _searchTimer.Tick += (_, __) =>
            {
                _searchTimer.Stop();
                string q = _txtSearch.Text.Trim();
                if (_txtSearch.ForeColor != C_TEXT || q.Length < 2) return;
                if (_homePanel.Visible)
                    BuscarEnSubcarpetas(q, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                else
                    _fileList.FilterItems(q);
            };
            _txtSearch.TextChanged += (_, __) =>
            {
                _searchTimer.Stop();
                if (_txtSearch.ForeColor != C_TEXT || _txtSearch.Text == "")
                { if (_fileList.Visible) _fileList.ClearFilter(); return; }
                _searchTimer.Start();
            };
            _txtSearch.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter && _txtSearch.ForeColor == C_TEXT && _txtSearch.Text.Trim() != "")
                {
                    e.SuppressKeyPress = true; _searchTimer.Stop();
                    string root = _homePanel.Visible
                        ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                        : _currentPath;
                    BuscarEnSubcarpetas(_txtSearch.Text.Trim(), root);
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    _searchTimer.Stop();
                    _txtSearch.Text = "Buscar..."; _txtSearch.ForeColor = C_SUBTEXT;
                    if (_fileList.Visible) _fileList.ClearFilter();
                    _txtSearch.Parent?.Focus();
                }
            };
            searchPanel.Controls.AddRange(new Control[] { lblSrch, _txtSearch });
            searchPanel.Resize += (_, __) => _txtSearch.Width = searchPanel.Width - 30;
            tlp.Controls.Add(searchPanel, 2, 0);

            var btnNew = new Button
            {
                Text = "+ Nueva",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 6, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = C_ACCENT,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand,
            };
            btnNew.FlatAppearance.BorderSize = 0;
            btnNew.FlatAppearance.MouseOverBackColor = C_ACCENT2;
            btnNew.Click += (_, __) => _fileList.NewFolder();
            tlp.Controls.Add(btnNew, 3, 0);

            var btnCam = new Button
            {
                Text = "📷  Camara",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 6, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 120, 246),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            btnCam.FlatAppearance.BorderSize = 0;
            btnCam.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 90, 210);
            btnCam.Click += BtnCam_Click;
            new ToolTip().SetToolTip(btnCam, "Abrir camara y tomar foto");
            tlp.Controls.Add(btnCam, 4, 0);

            var btnRec = new Button
            {
                Text = "🎙  Grabar",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 38, 38),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            btnRec.FlatAppearance.BorderSize = 0;
            btnRec.FlatAppearance.MouseOverBackColor = Color.FromArgb(185, 20, 20);
            btnRec.Click += BtnRec_Click;
            new ToolTip().SetToolTip(btnRec, "Abrir grabador de audio");
            tlp.Controls.Add(btnRec, 5, 0);

            outer.Controls.Add(tlp);
            return outer;
        }

        void BtnCam_Click(object sender, EventArgs e)
        {
            try
            {
                var frm = new CameraForm();
                frm.PhotoSaved += path =>
                {
                    string dir = System.IO.Path.GetDirectoryName((string?)path);
                    if (dir != null) NavigateTo(dir, true);
                };
                frm.Show(this); frm.BringToFront(); frm.Activate();
            }
            catch (NotImplementedException ex)
            {
                MessageBox.Show(
                    "No se pudo abrir la cámara.\n\n" +
                    "Posibles causas:\n" +
                    "• La cámara está en uso por otra aplicación\n" +
                    "• Falta permiso en Configuración → Privacidad → Cámara\n" +
                    "• Driver no instalado\n\n" +
                    "Detalle: " + ex.Message,
                    "Cámara no disponible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir la cámara:\n\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void BtnRec_Click(object sender, EventArgs e)
        {
            try
            {
                var frm = new AudioRecorderForm();
                frm.FileSaved += path =>
                {
                    string dir = System.IO.Path.GetDirectoryName(path);
                    if (dir != null) NavigateTo(dir, true);
                };
                frm.Show(this); frm.BringToFront(); frm.Activate();
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo abrir el grabador:\n\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        Button NavBtn(string text, int left, string tip)
        {
            var b = new Button
            {
                Text = text,
                Left = left,
                Top = 0,
                Width = 30,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 15f),
                ForeColor = C_TEXT,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = C_HOVER;
            b.FlatAppearance.CheckedBackColor = C_SELECTED;
            new ToolTip().SetToolTip(b, tip);
            return b;
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int cw, int ch);

        void BuildStatus()
        {
            _status = new StatusStrip
            {
                BackColor = C_TOOLBAR,
                SizingGrip = false,
                Padding = new Padding(8, 0, 0, 0),
            };
            _status.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(C_BORDER), 0, 0, _status.Width, 0);
            _lbl = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = C_SUBTEXT,
                Font = new Font("Segoe UI", 8.5f),
            };
            _status.Items.Add(_lbl);
        }

        void BuildSplit()
        {
            _sidebar = new SidebarControl { Dock = DockStyle.Fill };
            _fileList = new FileListControl { Dock = DockStyle.Fill };
            _homePanel = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = Color.FromArgb(246, 246, 248),
                AutoScroll = true,
            };

            _sidebar.NavigateRequested += p => NavigateTo(p, true);
            _sidebar.ShowHomeRequested += ShowHome;
            _sidebar.NewFileRequested += ext =>
            {
                string folder = _currentPath;
                if (!Directory.Exists(folder))
                    folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                string nombre = AskNewFileName(ext);
                if (nombre == null) return;

                string fullPath = Path.Combine(folder, nombre);
                int n = 2;
                while (File.Exists(fullPath))
                {
                    string noExt = Path.GetFileNameWithoutExtension(nombre);
                    fullPath = Path.Combine(folder, $"{noExt} ({n++}){ext}");
                }

                try
                {
                    switch (ext)
                    {
                        case ".pdf":
                        case ".docx":
                            new WordPdfEditorForm(fullPath).Show(this);
                            break;
                        case ".xlsx":
                            new ExcelEditorForm(fullPath).Show(this);
                            break;
                    }
                    NavigateTo(folder, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo crear el archivo:\n" + ex.Message,
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            _fileList.FolderNavigated += p => NavigateTo(p, true);
            _fileList.FileOpened += OpenFile;
            _fileList.SelectionChanged += _ => UpdateStatus();

            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterWidth = 1,
                BackColor = C_BORDER,
            };
            _split.Panel1.BackColor = C_SIDEBAR;
            _split.Panel1.Controls.Add(_sidebar);
            _split.Panel2.Controls.Add(_homePanel);
            _split.Panel2.Controls.Add(_fileList);
        }

        void ShowHome()
        {
            _txtAddr.Text = "Inicio";
            _btnUp.Enabled = false;
            _lbl.Text = "  Inicio";
            _fileList.Visible = false;
            _homePanel.Visible = true;
            _homePanel.BringToFront();
            BuildHomeContent();
            _sidebar.SetSelected(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            _clockTimer.Start();
        }

        void BuildHomeContent()
        {
            _lblSaludoHome = null; _lblFechaHome = null;
            _homePanel.Controls.Clear();
            _homePanel.AutoScrollPosition = new Point(0, 0);
            const int PX = 36;
            int y = 20;

            void Add(Control c) => _homePanel.Controls.Add(c);

            Label MkLbl(string text, int left, int top, float size,
                FontStyle fs = FontStyle.Regular, Color? col = null, int h = 0)
            {
                var l = new Label
                {
                    Text = text,
                    Left = left,
                    Top = top,
                    Font = new Font("Segoe UI", size, fs),
                    ForeColor = col ?? C_TEXT,
                    BackColor = Color.Transparent,
                    AutoSize = true,
                };
                Add(l); return l;
            }
            void Sep(int top)
            {
                var s = new Label
                {
                    Left = PX,
                    Top = top,
                    Height = 1,
                    BackColor = C_BORDER,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                };
                s.Width = _homePanel.Width - PX * 2;
                _homePanel.Resize += (_, __) => s.Width = _homePanel.Width - PX * 2;
                Add(s);
            }

            string saludo = DateTime.Now.Hour < 12 ? "Buenos días" :
                            DateTime.Now.Hour < 19 ? "Buenas tardes" : "Buenas noches";
            string culturaES = DateTime.Now.ToString("dddd, d 'de' MMMM",
                new System.Globalization.CultureInfo("es-MX"));
            string horaStr = DateTime.Now.ToString("HH:mm");

            _lblSaludoHome = new Label
            {
                Text = saludo,
                Left = PX,
                Top = y,
                Font = new Font("Segoe UI", 26f, FontStyle.Bold),
                ForeColor = C_TEXT,
                BackColor = Color.Transparent,
                AutoSize = true,
            };
            Add(_lblSaludoHome);
            y += _lblSaludoHome.PreferredHeight + 4;

            _lblFechaHome = new Label
            {
                Text = $"{culturaES}   •   {horaStr}",
                Left = PX,
                Top = y,
                Font = new Font("Segoe UI", 10f),
                ForeColor = C_SUBTEXT,
                BackColor = Color.Transparent,
                AutoSize = true,
            };
            Add(_lblFechaHome);
            y += _lblFechaHome.PreferredHeight + 8; Sep(y); y += 10;

            MkLbl("Acceso rapido", PX, y, 11f, FontStyle.Bold, h: 24); y += 28;

            var qf = new (string emoji, string name, string path, Color bg, Color fg)[]
            {
                ("🏠","Inicio",     Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Color.FromArgb(232,240,255), Color.FromArgb(30,80,200)),
                ("🖥️","Escritorio", Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Color.FromArgb(235,250,240), Color.FromArgb(20,140,60)),
                ("📄","Documentos", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Color.FromArgb(255,244,230), Color.FromArgb(200,100,0)),
                ("⬇️","Descargas",  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"),
                    Color.FromArgb(245,235,255), Color.FromArgb(120,40,200)),
                ("🎵","Musica",     Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    Color.FromArgb(255,235,240), Color.FromArgb(200,30,60)),
                ("🖼️","Imagenes",   Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    Color.FromArgb(230,250,255), Color.FromArgb(0,140,180)),
                ("🎬","Videos",     Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    Color.FromArgb(255,240,230), Color.FromArgb(200,80,20)),
            };

            var flow = new FlowLayoutPanel
            {
                Left = PX,
                Top = y,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            flow.Width = Math.Max(300, _homePanel.Width - PX * 2);
            _homePanel.Resize += (_, __) => flow.Width = Math.Max(300, _homePanel.Width - PX * 2);

            foreach (var (ico, name, path, bg, fg) in qf)
            {
                if (!Directory.Exists(path)) continue;
                var card = new Panel { Width = 108, Height = 88, BackColor = bg, Margin = new Padding(0, 0, 8, 8), Cursor = Cursors.Hand };
                var cardBg = bg;
                var cardHov = Color.FromArgb(Math.Max(0, bg.R - 15), Math.Max(0, bg.G - 15), Math.Max(0, bg.B - 15));
                card.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var pen = new Pen(Color.FromArgb(80, fg.R, fg.G, fg.B), 1.2f);
                    using var gp = HomeRoundRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 12);
                    e.Graphics.DrawPath(pen, gp);
                };
                card.MouseEnter += (_, __) => card.BackColor = cardHov;
                card.MouseLeave += (_, __) => card.BackColor = cardBg;

                int csz = 40;
                var circle = new Panel { Left = (card.Width - csz) / 2, Top = 10, Width = csz, Height = csz, BackColor = fg };
                circle.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, csz, csz, csz, csz));
                var lblIco = new Label
                {
                    Text = ico,
                    Left = 0,
                    Top = 0,
                    Width = csz,
                    Height = csz,
                    Font = new Font("Segoe UI", 15f),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    ForeColor = Color.White,
                };
                circle.Controls.Add(lblIco);

                var lblName = new Label
                {
                    Text = name,
                    Left = 2,
                    Top = 56,
                    Width = card.Width - 4,
                    Height = 24,
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = fg,
                    BackColor = Color.Transparent,
                    AutoEllipsis = false,
                };
                card.Controls.AddRange(new Control[] { circle, lblName });
                var cp = path;
                EventHandler nav = (_, __) => NavigateTo(cp, true);
                card.Click += nav; circle.Click += nav; lblIco.Click += nav; lblName.Click += nav;
                flow.Controls.Add(card);
            }
            Add(flow); flow.PerformLayout();
            y = flow.Top + (flow.Height > 0 ? flow.Height : 100) + 6;
            Sep(y); y += 10;

            MkLbl("Archivos recientes", PX, y, 11f, FontStyle.Bold, h: 24); y += 28;
            var recientes = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            }
            .Where(Directory.Exists)
            .SelectMany(d => { try { return Directory.GetFiles(d); } catch { return Array.Empty<string>(); } })
            .Select(f => new FileInfo(f)).Where(fi => fi.Exists)
            .OrderByDescending(fi => fi.LastWriteTime).Take(8).ToList();

            if (recientes.Count == 0)
            { MkLbl("No hay archivos recientes", PX, y, 9.5f, FontStyle.Italic, C_SUBTEXT, 28); y += 36; }
            else foreach (var fi in recientes)
                {
                    string icon = FileHelper.GetEmoji(fi.FullName);
                    string sz = FileHelper.FormatSize(fi.Length);
                    var row = new Panel
                    {
                        Left = PX,
                        Top = y,
                        Height = 52,
                        BackColor = Color.White,
                        Cursor = Cursors.Hand,
                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    };
                    row.Width = Math.Max(300, _homePanel.Width - PX * 2);
                    _homePanel.Resize += (_, __) => row.Width = Math.Max(300, _homePanel.Width - PX * 2);
                    row.Paint += (s, e) =>
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        using var pen = new Pen(C_BORDER);
                        using var gp = HomeRoundRect(new Rectangle(0, 0, row.Width - 1, row.Height - 1), 6);
                        e.Graphics.DrawPath(pen, gp);
                    };
                    row.MouseEnter += (_, __) => row.BackColor = Color.FromArgb(240, 245, 255);
                    row.MouseLeave += (_, __) => row.BackColor = Color.White;
                    row.Controls.Add(new Label { Text = icon, Left = 8, Top = 10, Width = 36, Height = 38, Font = new Font("Segoe UI", 16f), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent });
                    row.Controls.Add(new Label { Text = fi.Name, Left = 50, Top = 9, Width = row.Width - 60, Height = 22, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = C_TEXT, BackColor = Color.Transparent, AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
                    row.Controls.Add(new Label { Text = $"{fi.LastWriteTime:dd/MM/yyyy HH:mm}  ·  {sz}", Left = 50, Top = 33, Width = 400, Height = 18, Font = new Font("Segoe UI", 7.5f), ForeColor = C_SUBTEXT, BackColor = Color.Transparent });
                    var fic = fi;
                    row.Click += (_, __) => OpenFile(fic.FullName);
                    Add(row); y += 56;
                }
            Sep(y); y += 10;

            MkLbl("Almacenamiento", PX, y, 11f, FontStyle.Bold, h: 24); y += 28;
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                long total = drive.TotalSize, free = drive.AvailableFreeSpace, used = total - free;
                float pct = total > 0 ? (float)used / total : 0f;
                string usedS = HomeFormatBytes(used), totS = HomeFormatBytes(total);
                var sc = new Panel
                {
                    Left = PX,
                    Top = y,
                    Height = 70,
                    BackColor = Color.White,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                };
                sc.Width = Math.Max(300, _homePanel.Width - PX * 2);
                _homePanel.Resize += (_, __) => sc.Width = Math.Max(300, _homePanel.Width - PX * 2);
                sc.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var pen = new Pen(C_BORDER);
                    using var gp = HomeRoundRect(new Rectangle(0, 0, sc.Width - 1, sc.Height - 1), 8);
                    e.Graphics.DrawPath(pen, gp);
                };
                sc.Controls.Add(new Label { Text = "💾", Left = 12, Top = 8, Width = 28, Height = 28, Font = new Font("Segoe UI", 14f), BackColor = Color.Transparent });
                sc.Controls.Add(new Label { Text = $"{drive.Name}  {drive.VolumeLabel}", Left = 46, Top = 8, Width = 300, Height = 18, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = C_TEXT, BackColor = Color.Transparent });
                sc.Controls.Add(new Label { Text = $"{usedS} de {totS}  ({pct * 100:F0}%)", Left = 46, Top = 26, Width = 300, Height = 16, Font = new Font("Segoe UI", 8f), ForeColor = C_SUBTEXT, BackColor = Color.Transparent });
                var barBg = new Panel { Left = 12, Top = 50, Height = 8, BackColor = Color.FromArgb(230, 230, 235), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                barBg.Width = sc.Width - 24;
                var barFill = new Panel { Left = 0, Top = 0, Height = 8, BackColor = pct > 0.85f ? Color.FromArgb(255, 59, 48) : pct > 0.65f ? Color.FromArgb(255, 149, 0) : C_ACCENT };
                barFill.Width = (int)(barBg.Width * Math.Min(pct, 1f));
                sc.Resize += (_, __) => { barBg.Width = sc.Width - 24; barFill.Width = (int)(barBg.Width * Math.Min(pct, 1f)); };
                barBg.Controls.Add(barFill);
                sc.Controls.Add(barBg);
                Add(sc); y += 74;
            }
        }

        static GraphicsPath HomeRoundRect(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            p.CloseFigure(); return p;
        }

        static string HomeFormatBytes(long b) =>
            b >= 1_073_741_824 ? $"{b / 1_073_741_824.0:F1} GB" :
            b >= 1_048_576 ? $"{b / 1_048_576.0:F0} MB" : $"{b / 1024.0:F0} KB";

        void NavigateTo(string path, bool hist)
        {
            if (!Directory.Exists(path))
            {
                MessageBox.Show($"Ruta no existe:\n{path}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (hist && _currentPath != path) { _back.Push(_currentPath); _fwd.Clear(); }
            _currentPath = path;
            _txtAddr.Text = path;
            _fileList.Tag = path;
            _btnBack.Enabled = _back.Count > 0;
            _btnFwd.Enabled = _fwd.Count > 0;
            _btnUp.Enabled = Directory.GetParent(path) != null;

            _homePanel.Visible = false;
            _fileList.Visible = true;
            _fileList.BringToFront();
            _fileList.LoadDirectory(path);
            _sidebar.SetSelected(path);
            UpdateStatus();
            _clockTimer.Stop();
        }

        void GoBack() { if (_back.Count == 0) return; _fwd.Push(_currentPath); NavigateTo(_back.Pop(), false); }
        void GoFwd() { if (_fwd.Count == 0) return; _back.Push(_currentPath); NavigateTo(_fwd.Pop(), false); }
        void GoUp() { var p = Directory.GetParent(_currentPath); if (p != null) NavigateTo(p.FullName, true); }
        void UpdateStatus() => _lbl.Text = $"  {_fileList.TotalItems} elemento(s)  —  {_currentPath}";

        void OpenFile(string path)
        {
            if (!File.Exists(path)) return;
            var cat = FileHelper.Categorize(path);

            if (cat == FileHelper.FileCategory.Audio)
            {
                // Usar el directorio del archivo, no _currentPath (que puede ser Home)
                string audioDir = Path.GetDirectoryName(path) ?? _currentPath;
                var af = Directory.Exists(audioDir)
                    ? Directory.GetFiles(audioDir)
                        .Where(f => FileHelper.Categorize(f) == FileHelper.FileCategory.Audio)
                        .OrderBy(f => f).ToList()
                    : new List<string> { path };
                new MusicPlayerForm(path, af).Show(this);
            }
            else if (cat == FileHelper.FileCategory.Video)
                new VideoPlayerForm(path).Show(this);
            else if (cat == FileHelper.FileCategory.Image)
            {
                var imgs = Directory.GetFiles(_currentPath)
                    .Where(f => FileHelper.Categorize(f) == FileHelper.FileCategory.Image)
                    .OrderBy(f => f).ToList();
                new ImageViewerForm(path, imgs).Show(this);
            }
            else if (cat == FileHelper.FileCategory.Office || cat == FileHelper.FileCategory.Data)
            {
                // Si el archivo está bloqueado (OneDrive, Word abierto), copiar a temp
                string openPath = path;
                try
                {
                    using var test = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                catch
                {
                    try
                    {
                        string tmp = Path.Combine(Path.GetTempPath(),
                            Path.GetFileNameWithoutExtension(path) + "_readonly" + Path.GetExtension(path));
                        File.Copy(path, tmp, true);
                        openPath = tmp;
                    }
                    catch { /* usar el original de todas formas */ }
                }
                new FileEditorForm(openPath).Show(this);
            }
            else
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
                catch { new FileEditorForm(path).Show(this); }
            }
        }

        async void BuscarEnSubcarpetas(string query, string rootOverride = null)
        {
            string root = rootOverride ?? _currentPath;
            var resultWin = new SearchResultsForm(query, root);
            resultWin.NavigateRequested += p =>
            {
                if (Directory.Exists(p)) NavigateTo(p, true);
                else if (File.Exists(p)) { NavigateTo(Path.GetDirectoryName(p)!, true); OpenFile(p); }
            };
            resultWin.Show(this);

            var results = new System.Collections.Concurrent.ConcurrentBag<string>();
            await Task.Run(() =>
            {
                try
                {
                    var opts = new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        MatchCasing = MatchCasing.CaseInsensitive,
                    };
                    foreach (var f in Directory.EnumerateFileSystemEntries(root, $"*{query}*", opts))
                    {
                        results.Add(f);
                        if (results.Count >= 500) break;
                    }
                }
                catch { }
            });

            if (!resultWin.IsDisposed)
                resultWin.ShowResults(results.OrderBy(x => x).ToList());
        }

        private sealed class SearchResultsForm : Form
        {
            static readonly Color C_BG = Color.FromArgb(246, 246, 248);
            static readonly Color C_CARD = Color.White;
            static readonly Color C_BORDER = Color.FromArgb(218, 218, 223);
            static readonly Color C_ACCENT = Color.FromArgb(10, 132, 255);
            static readonly Color C_TXT = Color.FromArgb(28, 28, 30);
            static readonly Color C_SUB = Color.FromArgb(142, 142, 147);

            public event Action<string> NavigateRequested;

            readonly string _query, _rootPath;
            ListView _lv;
            Label _lblStatus;

            public SearchResultsForm(string query, string rootPath)
            {
                _query = query; _rootPath = rootPath;
                Text = $"Buscando \"{query}\"...";
                Size = new Size(820, 560); MinimumSize = new Size(600, 400);
                StartPosition = FormStartPosition.CenterParent;
                BackColor = C_BG; Font = new Font("Segoe UI", 9.5f);

                var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = C_CARD };
                header.Paint += (s, e) => { using var pen = new Pen(C_BORDER); e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1); };
                var lblTitle = new Label { Left = 16, Top = 8, Height = 20, AutoSize = true, Text = $"Resultados para  \"{query}\"", Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = C_TXT, BackColor = Color.Transparent };
                _lblStatus = new Label { Left = 16, Top = 30, Height = 16, AutoSize = true, Text = "⏳  Buscando en subcarpetas...", Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = C_SUB, BackColor = Color.Transparent };
                header.Controls.AddRange(new Control[] { lblTitle, _lblStatus });

                _lv = new ListView
                {
                    Dock = DockStyle.Fill,
                    View = View.Details,
                    FullRowSelect = true,
                    GridLines = false,
                    MultiSelect = false,
                    HideSelection = false,
                    BorderStyle = BorderStyle.None,
                    BackColor = C_CARD,
                    ForeColor = C_TXT,
                    Font = new Font("Segoe UI", 9.5f),
                };
                _lv.Columns.Add("Nombre", 280);
                _lv.Columns.Add("Ubicacion", 420);
                _lv.Columns.Add("Tipo", 90);
                _lv.DoubleClick += (s, e) => { if (_lv.SelectedItems.Count == 0) return; NavigateRequested?.Invoke(_lv.SelectedItems[0].Tag as string ?? ""); };
                _lv.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && _lv.SelectedItems.Count > 0) NavigateRequested?.Invoke(_lv.SelectedItems[0].Tag as string ?? ""); };

                var hint = new Label
                {
                    Dock = DockStyle.Bottom,
                    Height = 26,
                    Text = "  Doble clic o Enter para abrir  ·  Esc para cerrar",
                    ForeColor = C_SUB,
                    BackColor = C_CARD,
                    Font = new Font("Segoe UI", 8f),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(8, 0, 0, 0),
                };
                hint.Paint += (s, e) => { using var pen = new Pen(C_BORDER); e.Graphics.DrawLine(pen, 0, 0, hint.Width, 0); };

                Controls.Add(_lv); Controls.Add(hint); Controls.Add(header);
                KeyPreview = true;
                KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            }

            public void ShowResults(List<string> paths)
            {
                if (IsDisposed) return;
                Text = $"Resultados para \"{_query}\"  ({paths.Count} encontrado{(paths.Count != 1 ? "s" : "")})";
                _lblStatus.Text = paths.Count == 0
                    ? "Sin resultados en esta carpeta."
                    : $"✓  {paths.Count} resultado{(paths.Count != 1 ? "s" : "")} en  {_rootPath}";
                _lblStatus.ForeColor = paths.Count == 0 ? Color.FromArgb(255, 59, 48) : Color.FromArgb(52, 199, 89);

                _lv.BeginUpdate(); _lv.Items.Clear();
                foreach (var p in paths)
                {
                    bool isDir = Directory.Exists(p);
                    string name = Path.GetFileName(p);
                    string rel = Path.GetRelativePath(_rootPath, isDir ? p : Path.GetDirectoryName(p)!);
                    string tipo = isDir ? "Carpeta" : Path.GetExtension(p).TrimStart('.').ToUpper();
                    var item = new ListViewItem(name) { Tag = p };
                    item.SubItems.Add(rel == "." ? "(carpeta raiz)" : rel);
                    item.SubItems.Add(tipo);
                    if (isDir) item.ForeColor = C_ACCENT;
                    _lv.Items.Add(item);
                }
                _lv.EndUpdate();
            }
        }

        /// <summary>Muestra un diálogo compacto para pedir el nombre del nuevo archivo.</summary>
        string AskNewFileName(string ext)
        {
            string tipo = ext.ToUpper().TrimStart('.');

            using var dlg = new Form
            {
                Text = $"Nuevo {tipo}",
                ClientSize = new Size(360, 180),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(245, 245, 248),
                AutoScaleMode = AutoScaleMode.None,
            };

            dlg.Controls.Add(new Label
            {
                Text = "Nombre del archivo:",
                Left = 16,
                Top = 20,
                Width = 328,
                Height = 26,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(28, 28, 30),
                BackColor = Color.Transparent,
            });

            var txt = new TextBox
            {
                Left = 16,
                Top = 50,
                Width = 328,
                Height = 24,
                Font = new Font("Segoe UI", 9.5f),
                Text = "nuevo_archivo",
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
            };
            dlg.Controls.Add(txt);

            dlg.Controls.Add(new Label
            {
                Text = $"{tipo}  ·  carpeta actual",
                Left = 16,
                Top = 80,
                Width = 328,
                Height = 16,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.FromArgb(150, 150, 155),
                BackColor = Color.Transparent,
            });

            dlg.Controls.Add(new Label
            {
                Left = 0,
                Top = 110,
                Width = 360,
                Height = 1,
                BackColor = Color.FromArgb(218, 218, 223),
            });

            var btnCan = new Button
            {
                Text = "Cancelar",
                Left = 156,
                Top = 124,
                Width = 90,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(225, 225, 230),
                ForeColor = Color.FromArgb(28, 28, 30),
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel,
            };
            btnCan.FlatAppearance.BorderSize = 0;
            btnCan.FlatAppearance.MouseOverBackColor = Color.FromArgb(205, 205, 215);

            var btnOk = new Button
            {
                Text = "Crear",
                Left = 254,
                Top = 124,
                Width = 90,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 255),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK,
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 99, 220);

            dlg.Controls.AddRange(new Control[] { btnCan, btnOk });
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCan;
            dlg.Shown += (_, __) => { txt.SelectAll(); txt.Focus(); };

            if (dlg.ShowDialog(this) != DialogResult.OK) return null;
            string name = txt.Text.Trim();
            if (string.IsNullOrEmpty(name)) return null;
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "");
            return string.IsNullOrEmpty(name) ? null : name;
        }

        static GraphicsPath RoundRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
using FileExplorer.Helpers;

namespace FileExplorer.Controls
{
    public class SidebarControl : UserControl
    {
        static readonly Color C_BG = Color.FromArgb(240, 240, 244);
        static readonly Color C_HEADER = Color.FromArgb(140, 140, 148);
        static readonly Color C_TEXT = Color.FromArgb(28, 28, 30);
        static readonly Color C_HOVER = Color.FromArgb(218, 218, 226);
        static readonly Color C_SELECTED = Color.FromArgb(200, 218, 255);
        static readonly Color C_SEL_TEXT = Color.FromArgb(0, 80, 200);

        public event Action<string> NavigateRequested;
        public event Action ShowHomeRequested;

        /// <summary>
        /// Se dispara cuando el usuario hace clic en uno de los botones de nuevo archivo.
        /// El parámetro es la extensión: ".pdf", ".docx" o ".xlsx".
        /// </summary>
        public event Action<string> NewFileRequested;

        static readonly Dictionary<Environment.SpecialFolder, (string Emoji, string Label)> _specialFolders
            = new()
            {
                [Environment.SpecialFolder.UserProfile] = ("🏠", "Inicio"),
                [Environment.SpecialFolder.Desktop] = ("🖥️", "Escritorio"),
                [Environment.SpecialFolder.MyDocuments] = ("📄", "Documentos"),
                [Environment.SpecialFolder.MyMusic] = ("🎵", "Música"),
                [Environment.SpecialFolder.MyPictures] = ("🖼️", "Imágenes"),
                [Environment.SpecialFolder.MyVideos] = ("🎬", "Videos"),
            };

        static readonly Dictionary<DriveType, string> _driveEmoji
            = new()
            {
                [DriveType.CDRom] = "💿",
                [DriveType.Network] = "🌐",
                [DriveType.Removable] = "💾",
                [DriveType.Fixed] = "🖴",
            };

        struct SideItem
        {
            public string Label, Path, Emoji;
            public bool IsHeader, IsHome;
        }

        readonly List<SideItem> _items = new();
        string _selectedPath = "";

        public SidebarControl()
        {
            BackColor = C_BG;
            BorderStyle = BorderStyle.None;
            Font = new Font("Segoe UI", 9.5f);
            AutoScroll = true;
            DoubleBuffered = true;
            BuildItems();
            BuildUI();
        }

        void BuildItems()
        {
            _items.Add(new SideItem { Label = "FAVORITOS", IsHeader = true });

            foreach (var (folder, (emoji, label)) in _specialFolders)
                _items.Add(new SideItem
                {
                    Label = label,
                    Emoji = emoji,
                    Path = Environment.GetFolderPath(folder),
                    IsHome = folder == Environment.SpecialFolder.UserProfile,
                });

            _items.Insert(_items.FindIndex(i => i.Label == "Música"), new SideItem
            {
                Label = "Descargas",
                Emoji = "⬇️",
                Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            });

            _items.Add(new SideItem { Label = "DISPOSITIVOS", IsHeader = true });

            foreach (var d in DriveInfo.GetDrives().Where(x => x.IsReady))
            {
                string lbl = string.IsNullOrEmpty(d.VolumeLabel)
                    ? d.Name.TrimEnd('\\')
                    : $"{d.VolumeLabel} ({d.Name.TrimEnd('\\')})";
                string emoji = _driveEmoji.TryGetValue(d.DriveType, out var e) ? e : "🖴";
                _items.Add(new SideItem { Label = lbl, Path = d.RootDirectory.FullName, Emoji = emoji });
            }
        }

        public void SetSelected(string path)
        {
            _selectedPath = path ?? "";
            BuildUI();
        }

        void BuildUI()
        {
            Controls.Clear();
            int y = 12;

            // ── Favoritos y Dispositivos ──────────────────────────────
            foreach (var item in _items)
            {
                if (item.IsHeader)
                {
                    Controls.Add(new Label
                    {
                        Text = item.Label,
                        Left = 16,
                        Top = y,
                        Width = Width - 20,
                        Height = 20,
                        Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                        ForeColor = C_HEADER,
                        BackColor = Color.Transparent,
                        AutoSize = false,
                    });
                    y += 24;
                }
                else
                {
                    bool isSel = !string.IsNullOrEmpty(_selectedPath) && _selectedPath == item.Path;
                    var row = new Panel
                    {
                        Left = 6,
                        Top = y,
                        Width = Width - 12,
                        Height = 34,
                        BackColor = isSel ? C_SELECTED : Color.Transparent,
                        Cursor = Cursors.Hand,
                        Tag = item.Path,
                    };
                    if (isSel)
                        row.Region = System.Drawing.Region.FromHrgn(
                            CreateRoundRectRgn(0, 0, row.Width, row.Height, 8, 8));

                    row.MouseEnter += (s, e2) =>
                    {
                        var r = (Panel)s;
                        if ((string)r.Tag != _selectedPath)
                        { r.BackColor = C_HOVER; r.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, r.Width, r.Height, 8, 8)); }
                    };
                    row.MouseLeave += (s, e2) =>
                    {
                        var r = (Panel)s;
                        if ((string)r.Tag != _selectedPath)
                        { r.BackColor = Color.Transparent; r.Region = null; }
                    };

                    var lblEmoji = new Label
                    {
                        Text = item.Emoji,
                        Left = 6,
                        Top = 0,
                        Width = 28,
                        Height = 34,
                        Font = new Font("Segoe UI", 12f),
                        TextAlign = ContentAlignment.MiddleCenter,
                        BackColor = Color.Transparent,
                        ForeColor = isSel ? C_SEL_TEXT : C_TEXT,
                    };
                    var lblText = new Label
                    {
                        Text = item.Label,
                        Left = 36,
                        Top = 0,
                        Width = row.Width - 40,
                        Height = 34,
                        Font = new Font("Segoe UI", 9.5f, isSel ? FontStyle.Bold : FontStyle.Regular),
                        TextAlign = ContentAlignment.MiddleLeft,
                        ForeColor = isSel ? C_SEL_TEXT : C_TEXT,
                        BackColor = Color.Transparent,
                        AutoEllipsis = true,
                    };
                    row.Controls.AddRange(new Control[] { lblEmoji, lblText });

                    var captured = item;
                    EventHandler click = (s, e2) =>
                    {
                        _selectedPath = captured.Path;
                        BuildUI();
                        if (captured.IsHome) ShowHomeRequested?.Invoke();
                        else NavigateRequested?.Invoke(captured.Path);
                    };
                    row.Click += click; lblEmoji.Click += click; lblText.Click += click;
                    Controls.Add(row);
                    y += 38;
                }
            }

            // ── Sección NUEVO ARCHIVO ─────────────────────────────────
            y += 8;
            Controls.Add(new Label
            {
                Text = "NUEVO ARCHIVO",
                Left = 16,
                Top = y,
                Width = Width - 20,
                Height = 20,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = C_HEADER,
                BackColor = Color.Transparent,
                AutoSize = false,
            });
            y += 26;

            var opciones = new (string Emoji, string Label, string Ext, Color Accent)[]
            {
                ("📄", "Nuevo PDF",   ".pdf",  Color.FromArgb(220, 50,  50)),
                ("📝", "Nuevo Word",  ".docx", Color.FromArgb(0,   100, 200)),
                ("📊", "Nuevo Excel", ".xlsx", Color.FromArgb(30,  140, 60)),
            };

            foreach (var (emoji, label, ext, accent) in opciones)
            {
                var btn = new Panel
                {
                    Left = 6,
                    Top = y,
                    Width = Width - 12,
                    Height = 34,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                };
                btn.MouseEnter += (s, _) =>
                {
                    var p = (Panel)s;
                    p.BackColor = C_HOVER;
                    p.Region = System.Drawing.Region.FromHrgn(
                        CreateRoundRectRgn(0, 0, p.Width, p.Height, 8, 8));
                };
                btn.MouseLeave += (s, _) =>
                {
                    var p = (Panel)s;
                    p.BackColor = Color.Transparent;
                    p.Region = null;
                };

                var ic = new Label
                {
                    Text = emoji,
                    Left = 6,
                    Top = 0,
                    Width = 28,
                    Height = 34,
                    Font = new Font("Segoe UI", 12f),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    ForeColor = accent,
                };
                var lbl = new Label
                {
                    Text = label,
                    Left = 36,
                    Top = 0,
                    Width = btn.Width - 40,
                    Height = 34,
                    Font = new Font("Segoe UI", 9.5f),
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = C_TEXT,
                    BackColor = Color.Transparent,
                    AutoEllipsis = true,
                };
                btn.Controls.AddRange(new Control[] { ic, lbl });

                var capturedExt = ext;
                EventHandler click = (_, __) => NewFileRequested?.Invoke(capturedExt);
                btn.Click += click; ic.Click += click; lbl.Click += click;

                Controls.Add(btn);
                y += 38;
            }
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int cw, int ch);

        protected override void OnResize(EventArgs e) { base.OnResize(e); BuildUI(); }
    }
}
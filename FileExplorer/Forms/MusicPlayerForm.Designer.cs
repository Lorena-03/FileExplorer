using FileExplorer.Controls;
using FileExplorer.Models;

namespace FileExplorer.Forms
{
    public partial class MusicPlayerForm
    {
        void InitializeComponent()
        {
            Text = "Musica";
            Size = new Size(1240, 820);
            MinimumSize = new Size(1020, 700);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = SpBg;
            Font = new Font("Segoe UI", 9.5f);

            BuildSidebar();
            BuildCenter();
            BuildRight();
            BuildBottom();

            Controls.AddRange(new Control[] { pnlSidebar, pnlCenter, pnlRight, pnlBottom });
            Resize += (s, e) => Relayout();
            Relayout();
        }

        void Relayout()
        {
            const int SW = 300, RW = 340, BH = 110;
            int CW = ClientSize.Width - SW - RW, MH = ClientSize.Height - BH;
            pnlSidebar.SetBounds(0, 0, SW, MH);
            pnlCenter.SetBounds(SW, 0, CW, MH);
            pnlRight.SetBounds(SW + CW, 0, RW, MH);
            pnlBottom.SetBounds(0, MH, ClientSize.Width, BH);
            LayoutCenter();
        }

        void BuildSidebar()
        {
            pnlSidebar = new Panel { BackColor = SpSidebar };
            tabSidebar = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), BackColor = SpSidebar };

            var pgQ = new TabPage("  Cola  ") { BackColor = SpSidebar };
            var barQ = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = SpSidebar };

            var bAdd = SBtn("+ Agregar", 8, 100); bAdd.Click += (_, __) => AddFiles();
            var bA2L = SBtn("+ A lista", 116, 80);
            bA2L.BackColor = Color.FromArgb(30, 60, 30); bA2L.ForeColor = SpGreen;
            bA2L.Click += (_, __) => AddSelectedToList();
            barQ.Controls.AddRange(new Control[] { bAdd, bA2L });

            lstQueue = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = SpSidebar,
                ForeColor = SpGray,
                Font = new Font("Segoe UI", 9f),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 58,
                BorderStyle = BorderStyle.None,
                SelectionMode = SelectionMode.MultiExtended,
            };
            lstQueue.DrawItem += DrawQueueItem;
            lstQueue.DoubleClick += (_, __) => { if (lstQueue.SelectedIndex >= 0) PlayIdx(lstQueue.SelectedIndex); };
            lstQueue.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) RemoveSelectedFromQueue(); };

            pgQ.Controls.Add(lstQueue); pgQ.Controls.Add(barQ);

            var pgL = new TabPage("  Listas  ") { BackColor = SpSidebar };
            BuildListsTab(pgL);

            tabSidebar.TabPages.AddRange(new TabPage[] { pgQ, pgL });
            pnlSidebar.Controls.Add(tabSidebar);
        }

        void BuildListsTab(TabPage pg)
        {
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = SpSidebar };
            btnNewList = SBtn("+ Nueva", 8, 80); btnNewList.Click += (_, __) => CreateNewList();
            btnDelList = SBtn("X Eliminar", 96, 80); btnDelList.Click += (_, __) => DeleteList();
            btnPlayList = SBtn("Play", 184, 80);
            btnPlayList.BackColor = Color.FromArgb(20, 50, 20); btnPlayList.ForeColor = SpGreen;
            btnPlayList.Click += (_, __) => PlayList();
            toolbar.Controls.AddRange(new Control[] { btnNewList, btnDelList, btnPlayList });

            lblListName = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "Mis listas",
                ForeColor = SpGray,
                BackColor = Color.FromArgb(22, 22, 22),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
            };

            lstLists = new ListBox
            {
                Height = 140,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(22, 22, 22),
                ForeColor = SpWhite,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 36,
            };
            lstLists.DrawItem += DrawListItem;
            lstLists.SelectedIndexChanged += (_, __) => RefreshListSongs();

            var sep = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "Canciones en la lista",
                ForeColor = SpGray,
                BackColor = Color.FromArgb(22, 22, 22),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
            };

            var barSongs = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = SpSidebar };
            btnAddToList = SBtn("+ Desde cola", 6, 100); btnAddToList.Click += (_, __) => AddSelectedToList();
            btnRemFromList = SBtn("X Quitar", 114, 80); btnRemFromList.Click += (_, __) => RemoveFromList();
            barSongs.Controls.AddRange(new Control[] { btnAddToList, btnRemFromList });

            lstListSongs = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = SpSidebar,
                ForeColor = SpGray,
                Font = new Font("Segoe UI", 9f),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 56,
                BorderStyle = BorderStyle.None,
            };
            lstListSongs.DrawItem += DrawListSongItem;
            lstListSongs.DoubleClick += (_, __) =>
            {
                if (lstLists.SelectedIndex < 0 || lstListSongs.SelectedIndex < 0) return;
                _queue.Clear(); _queue.AddRange(_lists[lstLists.SelectedIndex].Songs);
                RefreshQueueList(); tabSidebar.SelectedIndex = 0;
                PlayIdx(lstListSongs.SelectedIndex);
            };

            pg.Controls.Add(lstListSongs); pg.Controls.Add(barSongs);
            pg.Controls.Add(sep); pg.Controls.Add(lstLists);
            pg.Controls.Add(lblListName); pg.Controls.Add(toolbar);
        }

        void BuildCenter()
        {
            pnlCenter = new Panel { BackColor = SpPanel };

            // Imagen con paint manual — escalado proporcional sin distorsión
            picArt = new PictureBox
            {
                Width = 220,
                Height = 220,
                SizeMode = PictureBoxSizeMode.Normal,
                BackColor = SpCard,
            };
            picArt.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(SpCard);

                if (picArt.Image != null)
                {
                    var img = picArt.Image;
                    int pw = picArt.Width, ph2 = picArt.Height;
                    double scl = Math.Min((double)pw / img.Width, (double)ph2 / img.Height);
                    int dw = (int)(img.Width * scl), dh = (int)(img.Height * scl);
                    g.DrawImage(img, (pw - dw) / 2, (ph2 - dh) / 2, dw, dh);
                }
                else
                {
                    var sf0 = new StringFormat
                    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("♪", new Font("Segoe UI", 60f),
                        new SolidBrush(Color.FromArgb(60, 60, 65)), picArt.ClientRectangle, sf0);
                }
            };


            // Panel que auto-ajusta la fuente del texto al ancho disponible
            var pnlText = new Panel { BackColor = Color.Transparent, Height = 90 };
            pnlText.Paint += (s, e) =>
            {
                var g2 = e.Graphics;
                g2.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int pw = pnlText.Width;
                var sfC = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                };

                // Título: reduce fuente hasta que quepa
                string ttl = lblTitle?.Text ?? "";
                float tsz = 15f;
                for (; tsz > 9f; tsz -= 0.5f)
                {
                    using var tf = new Font("Segoe UI", tsz, FontStyle.Bold);
                    if (g2.MeasureString(ttl, tf).Width <= pw - 16) break;
                }
                using var tfinal = new Font("Segoe UI", tsz, FontStyle.Bold);
                g2.DrawString(ttl, tfinal, BrWhite, new RectangleF(0, 0, pw, 34), sfC);

                // Artista
                string art = lblArtist?.Text ?? "";
                float asz = 11f;
                for (; asz > 7.5f; asz -= 0.5f)
                {
                    using var af = new Font("Segoe UI", asz);
                    if (g2.MeasureString(art, af).Width <= pw - 16) break;
                }
                using var afinal = new Font("Segoe UI", asz);
                g2.DrawString(art, afinal, BrGray, new RectangleF(0, 36, pw, 26), sfC);

                // Album
                using var albF = new Font("Segoe UI", 8.5f);
                g2.DrawString(lblAlbum?.Text ?? "", albF,
                    new SolidBrush(SpDark), new RectangleF(0, 64, pw, 22), sfC);
            };

            // Labels invisibles — solo guardan el texto
            lblTitle = new Label { Visible = false, UseMnemonic = false, Text = "—" };
            lblArtist = new Label { Visible = false, UseMnemonic = false, Text = "—" };
            lblAlbum = new Label { Visible = false, UseMnemonic = false, Text = "" };
            lblTitle.TextChanged += (_, __) => pnlText.Invalidate();
            lblArtist.TextChanged += (_, __) => pnlText.Invalidate();
            lblAlbum.TextChanged += (_, __) => pnlText.Invalidate();

            btnEditMeta = new Button
            {
                Text = "Editar metadatos",
                Width = 150,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = SpGray,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand,
            };
            btnEditMeta.FlatAppearance.BorderSize = 0;
            btnEditMeta.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
            btnEditMeta.Click += (_, __) => EditCurrentMetadata();

            pnlCenter.Controls.AddRange(new Control[] { picArt, pnlText, lblTitle, lblArtist, lblAlbum, btnEditMeta });
        }

        void LayoutCenter()
        {
            if (pnlCenter == null || picArt == null) return;
            int w = pnlCenter.Width;
            int ph = pnlCenter.Height;

            // Imagen: 55% del ancho, mín 160, máx 280
            int imgSz = Math.Min(280, Math.Max(160, (int)(w * 0.55)));
            int imgX = (w - imgSz) / 2;

            // Bloque total = imgSz + 8 + 90 (pnlText) + 10 + 26 (btn)
            int blockH = imgSz + 8 + 90 + 10 + 26;
            int startY = Math.Max(12, (ph - blockH) / 2);

            picArt.SetBounds(imgX, startY, imgSz, imgSz);
            picArt.Invalidate();

            int ty = startY + imgSz + 8;

            // Posicionar pnlText y btnEditMeta
            foreach (Control c in pnlCenter.Controls)
            {
                if (c is Panel p && p != picArt as Control)
                {
                    p.SetBounds(0, ty, w, 90);
                    p.Invalidate();
                }
            }
            btnEditMeta?.SetBounds((w - 150) / 2, ty + 96, 150, 26);
        }

        void BuildRight()
        {
            pnlRight = new Panel { BackColor = Color.FromArgb(10, 10, 10) };
            var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f) };

            var pgL = new TabPage("  Letra  ") { BackColor = Color.FromArgb(10, 10, 10) };
            lblLyricsStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = SpDark,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            };
            rtbLyrics = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(10, 10, 10),
                ForeColor = SpDark,
                Font = new Font("Segoe UI", 11.5f),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Padding = new Padding(16, 12, 16, 12),
                DetectUrls = false,
            };
            pgL.Controls.Add(rtbLyrics); pgL.Controls.Add(lblLyricsStatus);

            var pgEq = new TabPage("  Ecualizador  ") { BackColor = SpBg };
            eqCtrl = new EqualizerControl { Dock = DockStyle.Fill };
            eqCtrl.GainChanged += gains =>
            {
                if (_eqProvider == null) return;
                for (int i = 0; i < gains.Length && i < _eqProvider.Bands.Length; i++)
                    _eqProvider.Bands[i].Gain = gains[i];
                _eqProvider.Update();
            };
            pgEq.Controls.Add(eqCtrl);

            tabs.TabPages.AddRange(new TabPage[] { pgL, pgEq });
            pnlRight.Controls.Add(tabs);
        }

        void BuildBottom()
        {
            pnlBottom = new Panel { BackColor = SpBottom };
            pnlBottom.Paint += (s, e) =>
            { using var p = new Pen(Color.FromArgb(50, 50, 50)); e.Graphics.DrawLine(p, 0, 0, pnlBottom.Width, 0); };

            pnlProgBg = new Panel { BackColor = SpBarBg, Height = 4, Cursor = Cursors.Hand };
            pnlProgFg = new Panel { BackColor = SpGreen, Height = 4, Width = 0 };
            pnlProgThumb = new Panel { Size = new Size(12, 12), BackColor = SpWhite, Visible = false };
            pnlProgBg.Controls.AddRange(new Control[] { pnlProgFg, pnlProgThumb });
            pnlProgBg.MouseEnter += (_, __) => { pnlProgBg.Height = 6; pnlProgFg.Height = 6; pnlProgThumb.Visible = true; };
            pnlProgBg.MouseLeave += (_, __) => { if (!_seekingProg) { pnlProgBg.Height = 4; pnlProgFg.Height = 4; pnlProgThumb.Visible = false; } };
            pnlProgBg.MouseDown += ProgDown;
            pnlProgBg.MouseMove += ProgMove;
            pnlProgBg.MouseUp += ProgUp;

            lblElapsed = TLabel("0:00"); lblTotal = TLabel("0:00");

            btnShuffle = new SpIconButton { IconType = SpIconType.Shuffle, Size = new Size(32, 32) };
            btnPrev = new SpIconButton { IconType = SpIconType.Previous, Size = new Size(36, 36) };
            btnPlay = new SpIconButton { IconType = SpIconType.Play, Size = new Size(54, 54), IsCircle = true };
            btnStop = new SpIconButton { IconType = SpIconType.Stop, Size = new Size(36, 36), BackColor = SpBottom };
            btnNext = new SpIconButton { IconType = SpIconType.Next, Size = new Size(36, 36) };
            btnRepeat = new SpIconButton { IconType = SpIconType.Repeat, Size = new Size(32, 32) };

            new ToolTip().SetToolTip(btnShuffle, "Aleatorio");
            new ToolTip().SetToolTip(btnPrev, "Anterior");
            new ToolTip().SetToolTip(btnPlay, "Reproducir/Pausar");
            new ToolTip().SetToolTip(btnStop, "Detener");
            new ToolTip().SetToolTip(btnNext, "Siguiente");
            new ToolTip().SetToolTip(btnRepeat, "Repetir");

            btnShuffle.Click += (_, __) => { _shuffle = !_shuffle; btnShuffle.Active = _shuffle; btnShuffle.Invalidate(); };
            btnPrev.Click += (_, __) => PlayPrev();
            btnPlay.Click += (_, __) => Toggle();
            btnStop.Click += (_, __) => StopPlay();
            btnNext.Click += (_, __) => PlayNext();
            btnRepeat.Click += (_, __) => { _repeat = !_repeat; btnRepeat.Active = _repeat; btnRepeat.Invalidate(); };

            var lblVi = new Label
            {
                Width = 22,
                Height = 22,
                ForeColor = SpGray,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9f),
                Text = "🔊",
                TextAlign = ContentAlignment.MiddleCenter,
            };
            pnlVolBg = new Panel { BackColor = SpBarBg, Height = 4, Width = 90, Cursor = Cursors.Hand };
            pnlVolFg = new Panel { BackColor = SpGreen, Height = 4, Width = 72 };
            pnlVolBg.Controls.Add(pnlVolFg);
            pnlVolBg.MouseEnter += (_, __) => { pnlVolBg.Height = 6; pnlVolFg.Height = 6; };
            pnlVolBg.MouseLeave += (_, __) => { if (!_seekingVol) { pnlVolBg.Height = 4; pnlVolFg.Height = 4; } };
            pnlVolBg.MouseDown += VolDown;
            pnlVolBg.MouseMove += VolMove;
            pnlVolBg.MouseUp += VolUp;

            lblVol = new Label
            {
                Text = "80%",
                Width = 40,
                Height = 20,
                ForeColor = SpGray,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            pnlBottom.Controls.AddRange(new Control[]
            { pnlProgBg, lblElapsed, lblTotal, btnShuffle, btnPrev, btnPlay,
              btnStop, btnNext, btnRepeat, lblVi, pnlVolBg, lblVol });

            pnlBottom.Resize += (_, __) => LayoutBottom(lblVi);
        }

        void LayoutBottom(Label vi)
        {
            int w = pnlBottom.Width, cx = w / 2;
            const int PY = 10, BY = PY + 22;

            pnlProgBg.SetBounds(68, PY, w - 136, 4); pnlProgFg.Height = pnlProgBg.Height;
            lblElapsed.SetBounds(6, PY - 2, 60, 18);
            lblTotal.SetBounds(w - 66, PY - 2, 60, 18); lblTotal.TextAlign = ContentAlignment.MiddleRight;

            btnPlay.SetBounds(cx - 27, BY, 54, 54);
            btnPrev.SetBounds(cx - 90, BY + 9, 36, 36);
            btnNext.SetBounds(cx + 54, BY + 9, 36, 36);
            btnStop.SetBounds(cx + 98, BY + 9, 36, 36);
            btnShuffle.SetBounds(cx - 140, BY + 11, 32, 32);
            btnRepeat.SetBounds(cx + 140, BY + 11, 32, 32);

            int VY = BY + 19;
            vi.SetBounds(w - 176, VY - 10, 22, 22);
            pnlVolBg.SetBounds(w - 150, VY, 90, 4);
            pnlVolFg.Width = (int)(90 * _volume);
            lblVol.SetBounds(w - 54, VY - 8, 46, 18);
        }

        void DrawQueueItem(object s, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _queue.Count) return;
            DrawSongRow(e, _queue[e.Index], e.Index == _currentIdx);
        }

        void DrawListSongItem(object s, DrawItemEventArgs e)
        {
            if (lstLists.SelectedIndex < 0 || lstLists.SelectedIndex >= _lists.Count) return;
            var songs = _lists[lstLists.SelectedIndex].Songs;
            if (e.Index < 0 || e.Index >= songs.Count) return;
            DrawSongRow(e, songs[e.Index], false);
        }

        void DrawSongRow(DrawItemEventArgs e, PlaylistItem item, bool cur)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            bool sel = e.State.HasFlag(DrawItemState.Selected);
            g.FillRectangle(new SolidBrush(
                cur ? Color.FromArgb(48, 48, 52)
                : sel ? Color.FromArgb(38, 38, 42)
                : SpSidebar), e.Bounds);

            // Barra verde activa
            if (cur)
                g.FillRectangle(new SolidBrush(SpGreen), e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);

            g.DrawLine(new Pen(Color.FromArgb(30, 30, 34)),
                e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            // Miniatura
            int isz = 38, ix = e.Bounds.X + 10;
            int iy = e.Bounds.Y + (e.Bounds.Height - isz) / 2;
            if (item.AlbumArt != null)
                g.DrawImage(item.AlbumArt, ix, iy, isz, isz);
            else
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(50, 50, 55)), ix, iy, isz, isz);
                var sf2 = new StringFormat
                { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("♪", FntList, BrDark, new RectangleF(ix, iy, isz, isz), sf2);
            }

            // Texto
            int tx = ix + isz + 8;
            int tw = e.Bounds.Width - tx - 6;
            var sfE = new StringFormat
            { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

            // Nombre — línea 1
            g.DrawString(item.DisplayName,
                cur ? FntBold : FntNormal,
                cur ? BrWhite : BrGray,
                new RectangleF(tx, e.Bounds.Y + 9, tw, 18), sfE);

            // Artista izquierda, duración derecha — línea 2
            g.DrawString(item.DisplayArtist, FntSmall,
                cur ? new SolidBrush(Color.FromArgb(140, 140, 140)) : BrDark,
                new RectangleF(tx, e.Bounds.Y + 30, tw - 44, 16), sfE);

            if (!string.IsNullOrEmpty(item.DurationText))
                g.DrawString(item.DurationText, FntSmall,
                    cur ? new SolidBrush(Color.FromArgb(110, 110, 110)) : BrDark,
                    new RectangleF(tx, e.Bounds.Y + 30, tw, 16),
                    new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap });
        }

        void DrawListItem(object s, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _lists.Count) return;
            bool sel = e.State.HasFlag(DrawItemState.Selected);
            e.Graphics.FillRectangle(new SolidBrush(sel ? Color.FromArgb(40, 80, 40) : Color.FromArgb(22, 22, 22)), e.Bounds);
            e.Graphics.DrawLine(new Pen(Color.FromArgb(30, 30, 30)),
                e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            e.Graphics.DrawString("♫", FntList, sel ? BrGreen : BrDark,
                e.Bounds.X + 8, e.Bounds.Y + (e.Bounds.Height - 18) / 2);
            e.Graphics.DrawString(lstLists.Items[e.Index].ToString(),
                sel ? FntBold : FntNormal, sel ? BrWhite : BrGray,
                new RectangleF(e.Bounds.X + 30, e.Bounds.Y, e.Bounds.Width - 38, e.Bounds.Height),
                new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter });
        }
    }
}
using FileExplorer.Helpers;
using DrawColor = System.Drawing.Color;
using DrawFont = System.Drawing.Font;
using DrawFontStyle = System.Drawing.FontStyle;

namespace FileExplorer.Forms
{
    public partial class FileEditorForm
    {
        void InitializeComponent()
        {
            Text = Path.GetFileName(_path);
            Size = new System.Drawing.Size(1300, 880);
            MinimumSize = new System.Drawing.Size(980, 620);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = C_BG;
            Font = new DrawFont("Segoe UI", 9.5f);

            BuildToolbar();
            BuildTabs();
            ConfigureTabs();

            Controls.Add(tabs);
            Controls.Add(pnlToolbar);
        }

        void BuildToolbar()
        {
            pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = C_TOOL };
            pnlToolbar.Paint += (s, e) =>
            {
                using var p = new System.Drawing.Pen(C_BORDER);
                e.Graphics.DrawLine(p, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1);
            };

            var lblPath = new Label
            {
                Left = 10,
                Top = 15,
                Width = 260,
                Height = 18,
                Text = Path.GetFileName(_path),
                ForeColor = C_SEC,
                Font = new DrawFont("Segoe UI", 8.5f, DrawFontStyle.Bold),
                AutoEllipsis = true,
                BackColor = DrawColor.Transparent,
            };

            int x = 280;
            btnSave = TBtn("💾", ref x, C_ACCENT, DrawColor.White, Save);
            btnExport = TBtn("📤", ref x, DrawColor.FromArgb(80, 80, 90), DrawColor.White, ExportMenu);
            if (IsExcel) btnExport.Visible = false;

            // Botón Enviar: visible para documentos, datos y archivos de texto/datos planos
            Button btnEnviar = null;
            if (_ext is ".pdf" or ".docx" or ".doc"
                     or ".xlsx" or ".xls"
                     or ".csv" or ".xml" or ".json" or ".txt")
                btnEnviar = TBtn("📧 Enviar", ref x,
                    DrawColor.FromArgb(10, 132, 200), DrawColor.White,
                    () => new EmailForm(_path).Show(this));

            if (IsDataFile)
            {
                btnDetect = TBtn("Detectar errores", ref x, C_ORANGE, DrawColor.White, DetectErrors);
                btnFixAll = TBtn("Corregir todo", ref x, C_GREEN, DrawColor.White, FixAll);
                btnFixAll.Enabled = false;
            }

            if (IsDataFile)
                btnMigrate = TBtn("Migrar BD", ref x,
                    DrawColor.FromArgb(100, 60, 160), DrawColor.White, OpenMigration);

            if (!IsImage && (_ext == ".txt" || _ext == ".md" || _ext == ".log"))
                btnToggleView = TBtn("Ver tabla", ref x,
                    DrawColor.FromArgb(72, 72, 76), DrawColor.White, ToggleTxtTable);

            lblInfo = new Label
            {
                Left = pnlToolbar.Width - 220,
                Top = 15,
                Width = 208,
                Height = 18,
                ForeColor = C_SEC,
                Font = new DrawFont("Segoe UI", 8f),
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
                BackColor = DrawColor.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };

            pnlToolbar.Controls.Add(lblPath);
            pnlToolbar.Controls.Add(btnSave);
            pnlToolbar.Controls.Add(btnExport);
            if (btnEnviar != null) pnlToolbar.Controls.Add(btnEnviar);
            if (btnDetect != null) pnlToolbar.Controls.Add(btnDetect);
            if (btnFixAll != null) pnlToolbar.Controls.Add(btnFixAll);
            if (btnMigrate != null) pnlToolbar.Controls.Add(btnMigrate);
            if (btnToggleView != null) pnlToolbar.Controls.Add(btnToggleView);
            pnlToolbar.Controls.Add(lblInfo);
        }

        void BuildTabs()
        {
            tabs = new TabControl { Dock = DockStyle.Fill, Font = new DrawFont("Segoe UI", 9f) };

            pgView = new TabPage("  Vista  ") { BackColor = DrawColor.FromArgb(240, 240, 244) };
            pnlDocScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = DrawColor.FromArgb(240, 240, 244) };
            flowDoc = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = DrawColor.Transparent,
                Padding = new Padding(40, 20, 40, 40),
            };
            pnlDocScroll.Controls.Add(flowDoc);
            pgView.Controls.Add(pnlDocScroll);

            pgTable = new TabPage("  Tabla  ") { BackColor = C_BG };
            BuildTableTab();

            pgEdit = new TabPage("  Editor  ") { BackColor = DrawColor.White };
            rtbEdit = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new DrawFont("Segoe UI", 11f),
                BackColor = DrawColor.White,
                ForeColor = C_TXT,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Both,
                WordWrap = true,
                AcceptsTab = true,
                DetectUrls = false,
                Padding = new Padding(20),
            };
            rtbEdit.TextChanged += (s, e) => { _isDirty = true; UpdateInfo(); };
            pgEdit.Controls.Add(rtbEdit);

            pgChart = new TabPage("  Graficas  ") { BackColor = C_BG };
            BuildChartTab();

            tabs.TabPages.AddRange(new TabPage[] { pgView, pgTable, pgEdit, pgChart });
        }

        void BuildChartTab()
        {
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = C_TOOL };
            toolbar.Paint += (s, e) =>
            {
                using var p = new System.Drawing.Pen(C_BORDER);
                e.Graphics.DrawLine(p, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
            };

            var lblCol = new Label { Left = 10, Top = 14, AutoSize = true, Text = "Columna:", ForeColor = C_TXT, BackColor = DrawColor.Transparent, Font = new DrawFont("Segoe UI", 9.5f) };
            cboChartX = new ComboBox { Left = 115, Top = 10, Width = 180, Height = 26, DropDownStyle = ComboBoxStyle.DropDownList, Font = new DrawFont("Segoe UI", 9.5f) };
            cboChartY = new ComboBox { Visible = false, Width = 10 };

            var lblType = new Label { Left = 310, Top = 14, AutoSize = true, Text = "Tipo:", ForeColor = C_TXT, BackColor = DrawColor.Transparent, Font = new DrawFont("Segoe UI", 9.5f) };
            cboChartType = new ComboBox { Left = 370, Top = 10, Width = 130, Height = 26, DropDownStyle = ComboBoxStyle.DropDownList, Font = new DrawFont("Segoe UI", 9.5f) };
            cboChartType.Items.AddRange(new object[] { "Barras", "Pastel" });
            cboChartType.SelectedIndex = 0;

            var btnDraw = new Button
            {
                Left = 515,
                Top = 8,
                Width = 100,
                Height = 28,
                Text = "Generar",
                FlatStyle = FlatStyle.Flat,
                BackColor = C_ACCENT,
                ForeColor = DrawColor.White,
                Font = new DrawFont("Segoe UI", 9.5f),
                Cursor = Cursors.Hand,
            };
            btnDraw.FlatAppearance.BorderSize = 0;
            btnDraw.Click += (s, e) => DrawChart();

            toolbar.Controls.AddRange(new Control[] { lblCol, cboChartX, lblType, cboChartType, btnDraw });
            pnlChart = new Panel { Dock = DockStyle.Fill, BackColor = DrawColor.White };
            pnlChart.Paint += PnlChart_Paint;
            pnlChart.Resize += (s, e) => pnlChart.Invalidate();

            pgChart.Controls.Clear();
            pgChart.Controls.Add(pnlChart);
            pgChart.Controls.Add(toolbar);
        }

        void BuildTableTab()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 5,
                BackColor = C_BORDER,
            };
            var topWrap = new Panel { Dock = DockStyle.Fill };

            var searchBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = C_TOOL };
            searchBar.Paint += (s, e) =>
            {
                using var p = new System.Drawing.Pen(C_BORDER);
                e.Graphics.DrawLine(p, 0, searchBar.Height - 1, searchBar.Width, searchBar.Height - 1);
            };

            var lblSrch = new Label { Left = 8, Top = 11, Width = 60, Height = 18, Text = "Buscar:", ForeColor = C_TXT, Font = new DrawFont("Segoe UI", 9f), BackColor = DrawColor.Transparent };
            cboSearchCol = new ComboBox { Left = 72, Top = 9, Width = 150, Height = 24, DropDownStyle = ComboBoxStyle.DropDownList, Font = new DrawFont("Segoe UI", 9f), BackColor = DrawColor.White };
            cboSearchCol.Items.Add("(Todas las columnas)"); cboSearchCol.SelectedIndex = 0;
            cboSearchCol.SelectedIndexChanged += (s, e) =>
            {
                txtSearch.PlaceholderText = cboSearchCol.SelectedIndex == 0
                    ? "Buscar en todas las columnas..."
                    : "Buscar en " + (cboSearchCol.SelectedItem?.ToString() ?? "") + "...";
                if (!string.IsNullOrEmpty(txtSearch?.Text)) SearchInGrid();
            };

            txtSearch = new TextBox { Left = 228, Top = 9, Width = 220, Height = 24, Font = new DrawFont("Segoe UI", 9.5f), BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle, PlaceholderText = "Buscar en todas las columnas..." };
            txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) SearchInGrid(); };
            txtSearch.TextChanged += (s, e) => { if (string.IsNullOrEmpty(txtSearch.Text)) ClearSearch(); };

            var btnSrch = new Button { Left = 454, Top = 8, Width = 70, Height = 26, Text = "Buscar", FlatStyle = FlatStyle.Flat, BackColor = C_ACCENT, ForeColor = DrawColor.White, Font = new DrawFont("Segoe UI", 8.5f), Cursor = Cursors.Hand };
            btnSrch.FlatAppearance.BorderSize = 0; btnSrch.Click += (s, e) => SearchInGrid();

            var btnClr = new Button { Left = 530, Top = 8, Width = 46, Height = 26, Text = "X", FlatStyle = FlatStyle.Flat, BackColor = DrawColor.FromArgb(58, 58, 60), ForeColor = DrawColor.White, Font = new DrawFont("Segoe UI", 9f), Cursor = Cursors.Hand };
            btnClr.FlatAppearance.BorderSize = 0; btnClr.Click += (s, e) => ClearSearch();

            lblSearchResult = new Label { Left = 584, Top = 11, Width = 360, Height = 18, ForeColor = C_SEC, Font = new DrawFont("Segoe UI", 8.5f), BackColor = DrawColor.Transparent };
            searchBar.Controls.AddRange(new Control[] { lblSrch, cboSearchCol, txtSearch, btnSrch, btnClr, lblSearchResult });

            var gridBar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = C_HDR };
            gridBar.Paint += (s, e) =>
            {
                using var p = new System.Drawing.Pen(C_BORDER);
                e.Graphics.DrawLine(p, 0, gridBar.Height - 1, gridBar.Width, gridBar.Height - 1);
            };

            var bAdd = GBtn("+ Fila", 8, C_GREEN, DrawColor.White);
            var bDel = GBtn("X Fila", 82, C_RED, DrawColor.White);
            bAdd.Click += (s, e) => { _rows.Add(new List<string>(new string[_headers.Count])); dgv.RowCount = _rows.Count; _isDirty = true; };
            bDel.Click += (s, e) =>
            {
                var toDelete = new List<int>();
                foreach (DataGridViewCell c in dgv.SelectedCells)
                    if (!toDelete.Contains(c.RowIndex)) toDelete.Add(c.RowIndex);
                toDelete.Sort(); toDelete.Reverse();
                foreach (int ri in toDelete) if (ri >= 0 && ri < _rows.Count) _rows.RemoveAt(ri);
                dgv.RowCount = _rows.Count; _isDirty = true;
            };

            lblErrCount = new Label { Left = 160, Top = 9, Width = 520, Height = 18, ForeColor = C_SEC, Font = new DrawFont("Segoe UI", 8.5f), BackColor = DrawColor.Transparent, Text = "" };
            gridBar.Controls.AddRange(new Control[] { bAdd, bDel, lblErrCount });

            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = DrawColor.White,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
                Font = new DrawFont("Segoe UI", 9.5f),
                GridColor = DrawColor.FromArgb(228, 228, 235),
                VirtualMode = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersWidth = 48,
                RowHeadersVisible = true,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ColumnHeadersHeight = 38,
                ColumnHeadersVisible = true,
                EnableHeadersVisualStyles = false,
                DefaultCellStyle = new DataGridViewCellStyle { BackColor = DrawColor.White, ForeColor = C_TXT, Padding = new Padding(4, 2, 4, 2), SelectionBackColor = C_ACCENT, SelectionForeColor = DrawColor.White },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = C_HDR_BG, ForeColor = DrawColor.FromArgb(10, 10, 40), Font = new DrawFont("Segoe UI", 9.5f, DrawFontStyle.Bold), SelectionBackColor = C_HDR_BG, SelectionForeColor = DrawColor.FromArgb(10, 10, 40), Alignment = DataGridViewContentAlignment.MiddleCenter, Padding = new Padding(4, 4, 4, 4) },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = C_ROWALT },
            };
            dgv.RowTemplate.Height = 24;
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(dgv, true);

            dgv.CellValueNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
                if (e.ColumnIndex == 0) { e.Value = e.RowIndex; return; }
                int dc = e.ColumnIndex - 1; var row = _rows[e.RowIndex];
                e.Value = dc < row.Count ? row[dc] : "";
            };
            dgv.CellValuePushed += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _rows.Count || e.ColumnIndex == 0) return;
                int dc = e.ColumnIndex - 1; var row = _rows[e.RowIndex];
                while (row.Count <= dc) row.Add("");
                row[dc] = e.Value?.ToString() ?? ""; _isDirty = true;
            };
            dgv.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex == 0) return;
                if (_cellColors.TryGetValue((e.RowIndex, e.ColumnIndex - 1), out DrawColor color))
                { e.CellStyle.BackColor = color; e.FormattingApplied = true; }
            };
            dgv.CellPainting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (_searchHighlight.Contains((e.RowIndex, e.ColumnIndex - 1)))
                { e.Graphics.FillRectangle(new System.Drawing.SolidBrush(C_SEARCH), e.CellBounds); e.PaintContent(e.CellBounds); e.Handled = true; }
            };

            topWrap.Controls.Add(dgv); topWrap.Controls.Add(gridBar); topWrap.Controls.Add(searchBar);
            split.Panel1.Controls.Add(topWrap);
            split.Panel2.Controls.Add(BuildErrorPanel());
            pgTable.Controls.Clear(); pgTable.Controls.Add(split);

            if (IsExcel) { split.Panel2Collapsed = true; }
            else
            {
                pgTable.VisibleChanged += (s, e) =>
                {
                    if (pgTable.Visible && split.Height > 0)
                        try { split.SplitterDistance = (int)(split.Height * 0.70); } catch { }
                };
            }
        }

        Panel BuildErrorPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = DrawColor.FromArgb(248, 248, 250) };

            var lblHint = new Label { Dock = DockStyle.Top, Height = 36, Text = "  Haz clic en 'Detectar errores' para analizar el archivo", ForeColor = C_SEC, BackColor = DrawColor.FromArgb(250, 250, 252), Font = new DrawFont("Segoe UI", 8.5f, DrawFontStyle.Italic), TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0) };
            lblHint.Paint += (s, e) =>
            {
                using var p = new System.Drawing.Pen(C_BORDER);
                e.Graphics.DrawLine(p, 0, 0, lblHint.Width, 0);
                e.Graphics.DrawLine(p, 0, lblHint.Height - 1, lblHint.Width, lblHint.Height - 1);
            };

            lstErrors = new ListBox { Dock = DockStyle.Fill, Font = new DrawFont("Segoe UI", 9.5f), DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 56, BorderStyle = System.Windows.Forms.BorderStyle.None, BackColor = DrawColor.White };
            lstErrors.DrawItem += DrawErrorItem;
            lstErrors.DoubleClick += (s, e) => NavigateToError();

            pnl.Controls.Add(lstErrors); pnl.Controls.Add(lblHint);
            return pnl;
        }

        internal void DrawBarChart(System.Drawing.Graphics g, Panel panel,
            List<KeyValuePair<string, double>> data, string title)
        {
            int w = panel.Width, h = panel.Height;
            const int PAD_L = 70, PAD_R = 20, PAD_T = 50, PAD_B = 80;
            int chartW = w - PAD_L - PAD_R;
            int chartH = h - PAD_T - PAD_B;
            if (chartW < 50 || chartH < 50) return;

            double maxVal = data.Max(kv => kv.Value);
            if (maxVal == 0) maxVal = 1;

            using var fntTitle = new DrawFont("Segoe UI", 11f, DrawFontStyle.Bold);
            g.DrawString(title, fntTitle, new System.Drawing.SolidBrush(C_TXT),
                new System.Drawing.RectangleF(PAD_L, 10, chartW, 30),
                new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center });

            g.FillRectangle(new System.Drawing.SolidBrush(DrawColor.FromArgb(250, 250, 253)),
                PAD_L, PAD_T, chartW, chartH);

            using var penGrid = new System.Drawing.Pen(DrawColor.FromArgb(220, 220, 228), 1f);
            using var fntAxis = new DrawFont("Segoe UI", 7.5f);
            for (int i = 0; i <= 5; i++)
            {
                int y2 = PAD_T + chartH - (int)(chartH * i / 5.0);
                g.DrawLine(penGrid, PAD_L, y2, PAD_L + chartW, y2);
                double val = maxVal * i / 5.0;
                string lbl = val >= 1000 ? $"{val / 1000:F1}k" : $"{val:F0}";
                g.DrawString(lbl, fntAxis, new System.Drawing.SolidBrush(C_SEC),
                    new System.Drawing.RectangleF(2, y2 - 8, PAD_L - 6, 16),
                    new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Far, LineAlignment = System.Drawing.StringAlignment.Center });
            }

            int n = data.Count;
            float barW = Math.Max(4, (float)chartW / n * 0.65f);
            float gap = (float)chartW / n;
            using var fntLbl = new DrawFont("Segoe UI", 7.5f);
            var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, Trimming = System.Drawing.StringTrimming.EllipsisCharacter };

            for (int i = 0; i < n; i++)
            {
                var kv = data[i];
                float bx = PAD_L + gap * i + (gap - barW) / 2f;
                float bh2 = (float)(chartH * kv.Value / maxVal);
                float by = PAD_T + chartH - bh2;
                var color = ChartColors[i % ChartColors.Length];
                using var br = new System.Drawing.SolidBrush(color);
                using var brH = new System.Drawing.SolidBrush(DrawColor.FromArgb(
                    Math.Min(255, color.R + 30), Math.Min(255, color.G + 30), Math.Min(255, color.B + 30)));
                g.FillRectangle(br, bx, by, barW, bh2);
                g.FillRectangle(brH, bx, by, barW, Math.Min(bh2, 8));

                string valStr = kv.Value >= 1000 ? $"{kv.Value / 1000:F1}k" : $"{kv.Value:F0}";
                g.DrawString(valStr, fntLbl, new System.Drawing.SolidBrush(C_TXT),
                    new System.Drawing.RectangleF(bx, by - 16, barW, 16), sf);

                string lbl2 = kv.Key.Length > 10 ? kv.Key[..10] + "." : kv.Key;
                if (n > 10)
                {
                    g.TranslateTransform(bx + barW / 2, PAD_T + chartH + 6);
                    g.RotateTransform(-35);
                    g.DrawString(lbl2, fntLbl, new System.Drawing.SolidBrush(C_TXT),
                        new System.Drawing.RectangleF(-30, 0, 60, 20),
                        new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center });
                    g.ResetTransform();
                }
                else
                {
                    g.DrawString(lbl2, fntLbl, new System.Drawing.SolidBrush(C_TXT),
                        new System.Drawing.RectangleF(bx - gap / 4, PAD_T + chartH + 4, barW + gap / 2, PAD_B - 8), sf);
                }
            }

            using var penAxis = new System.Drawing.Pen(C_BORDER, 1.5f);
            g.DrawLine(penAxis, PAD_L, PAD_T, PAD_L, PAD_T + chartH);
            g.DrawLine(penAxis, PAD_L, PAD_T + chartH, PAD_L + chartW, PAD_T + chartH);
        }

        internal void DrawPieChart(System.Drawing.Graphics g, Panel panel,
            List<KeyValuePair<string, double>> data, string title)
        {
            int w = panel.Width, h = panel.Height;
            const int PAD = 20, LEG_W = 200;
            int chartW = w - LEG_W - PAD * 2;
            int chartH = h - PAD * 2 - 40;
            if (chartW < 80 || chartH < 80) return;

            using var fntTitle = new DrawFont("Segoe UI", 11f, DrawFontStyle.Bold);
            g.DrawString(title, fntTitle, new System.Drawing.SolidBrush(C_TXT),
                new System.Drawing.RectangleF(PAD, 10, chartW + LEG_W, 30),
                new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center });

            double total = data.Sum(kv => Math.Abs(kv.Value));
            if (total == 0) { DrawCenteredMsg(g, panel, "Sin datos numericos para pastel"); return; }

            int diameter = Math.Min(chartW, chartH) - 20;
            int cx = PAD + chartW / 2 - diameter / 2;
            int cy = 40 + chartH / 2 - diameter / 2;
            var rect = new System.Drawing.Rectangle(cx, cy, diameter, diameter);

            float startAngle = -90f;
            using var fntLbl = new DrawFont("Segoe UI", 8f);

            for (int i = 0; i < data.Count; i++)
            {
                float sweep = (float)(data[i].Value / total * 360.0);
                var color = ChartColors[i % ChartColors.Length];
                using var br = new System.Drawing.SolidBrush(color);
                g.FillPie(br, rect, startAngle, sweep);
                using var pen = new System.Drawing.Pen(DrawColor.White, 2f);
                g.DrawPie(pen, rect, startAngle, sweep);

                if (sweep > 20)
                {
                    float midAngle = (startAngle + sweep / 2f) * (float)Math.PI / 180f;
                    float r2 = diameter / 2f * 0.65f;
                    float lx = cx + diameter / 2f + r2 * (float)Math.Cos(midAngle) - 18;
                    float ly = cy + diameter / 2f + r2 * (float)Math.Sin(midAngle) - 8;
                    double pct = data[i].Value / total * 100;
                    g.DrawString($"{pct:F1}%", fntLbl,
                        new System.Drawing.SolidBrush(DrawColor.White),
                        new System.Drawing.RectangleF(lx, ly, 36, 16),
                        new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center });
                }
                startAngle += sweep;
            }

            int legX = PAD + chartW + PAD;
            int legY = 50;
            using var fntLeg = new DrawFont("Segoe UI", 8.5f);
            for (int i = 0; i < data.Count; i++)
            {
                var color = ChartColors[i % ChartColors.Length];
                using var br = new System.Drawing.SolidBrush(color);
                g.FillRectangle(br, legX, legY + i * 22, 14, 14);
                double pct2 = data[i].Value / total * 100;
                string lbl = data[i].Key.Length > 16 ? data[i].Key[..16] + "." : data[i].Key;
                g.DrawString($"{lbl}  ({pct2:F1}%)", fntLeg,
                    new System.Drawing.SolidBrush(C_TXT),
                    new System.Drawing.RectangleF(legX + 18, legY + i * 22, LEG_W - 22, 18));
                if (legY + i * 22 > h - 40) break;
            }
        }

        internal static void DrawCenteredMsg(System.Drawing.Graphics g, Panel panel, string msg)
        {
            using var f = new DrawFont("Segoe UI", 11f, DrawFontStyle.Italic);
            using var br = new System.Drawing.SolidBrush(DrawColor.FromArgb(160, 160, 170));
            var sf = new System.Drawing.StringFormat
            { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
            g.DrawString(msg, f, br, new System.Drawing.RectangleF(0, 0, panel.Width, panel.Height), sf);
        }

        Button TBtn(string t, ref int x, DrawColor bg, DrawColor fg, Action act)
        {
            var b = new Button
            {
                Text = t,
                Left = x,
                Top = 10,
                Height = 28,
                Width = t.Length * 7 + 20,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Font = new DrawFont("Segoe UI", 8.5f),
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, ev) => act();
            x += b.Width + 5;
            return b;
        }

        internal Button GBtn(string t, int l, DrawColor bg, DrawColor fg)
        {
            var b = new Button
            {
                Text = t,
                Left = l,
                Top = 6,
                Width = 68,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Font = new DrawFont("Segoe UI", 8.5f),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }
    }
}
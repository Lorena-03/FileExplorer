using DrawColor     = System.Drawing.Color;
using DrawFont      = System.Drawing.Font;
using DrawFontStyle = System.Drawing.FontStyle;

namespace FileExplorer.Forms
{
    public partial class DbDataViewerForm
    {
        /// <summary>
        /// Construye la grilla editable con banner de errores, toolbar de aplicar cambios
        /// y leyenda de colores en la parte inferior.
        /// </summary>
        void InitializeComponent()
        {
            int autoFix = _errores.Count(e => !string.IsNullOrEmpty(e.AutoFix));
            int manual  = _errores.Count - autoFix;

            Text          = $"Datos BD — tabla 'archivos'  ({_editedRows.Count} filas)";
            Size          = new System.Drawing.Size(1200, 760);
            MinimumSize   = new System.Drawing.Size(800, 560);
            StartPosition = FormStartPosition.CenterParent;
            BackColor     = C_BG;
            Font          = new DrawFont("Segoe UI", 9.5f);

            // ── Toolbar ───────────────────────────────────────────────
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = DrawColor.FromArgb(240, 248, 240) };
            toolbar.Paint += (s, e) =>
            { using var pen = new System.Drawing.Pen(DrawColor.FromArgb(180, 210, 180)); e.Graphics.DrawLine(pen, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1); };

            btnApply = new Button
            {
                Text = "Aplicar cambios a tabla original",
                Left = 14, Top = 8, Width = 260, Height = 30,
                FlatStyle = FlatStyle.Flat, BackColor = C_GREEN, ForeColor = DrawColor.White,
                Font = new DrawFont("Segoe UI", 9.5f, DrawFontStyle.Bold), Cursor = Cursors.Hand, Enabled = false,
            };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Click += BtnApply_Click;

            lblEditHint = new Label
            {
                Left = 284, Top = 13, AutoSize = true,
                Text = "Edita cualquier celda y luego haz clic en 'Aplicar cambios'",
                ForeColor = C_SEC, BackColor = DrawColor.Transparent,
                Font = new DrawFont("Segoe UI", 8.5f, DrawFontStyle.Italic),
            };
            toolbar.Controls.AddRange(new Control[] { btnApply, lblEditHint });

            // ── Banner ────────────────────────────────────────────────
            DrawColor bannerBg; string bannerText;
            if (_errores.Count == 0)
            { bannerBg = DrawColor.FromArgb(232, 255, 238); bannerText = $"Todo correcto — {_editedRows.Count} filas sin errores."; }
            else
            { bannerBg = DrawColor.FromArgb(255, 248, 225); bannerText = $"{_errores.Count} error(es): {autoFix} auto-corregibles (amarillo)  -  {manual} requieren revision (rojo)."; }

            lblBanner = new Label
            {
                Dock = DockStyle.Top, Height = 42, Text = bannerText, BackColor = bannerBg,
                ForeColor = _errores.Count == 0 ? DrawColor.FromArgb(0, 120, 50) : DrawColor.FromArgb(140, 80, 0),
                Font = new DrawFont("Segoe UI", 9.5f, DrawFontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0), AutoEllipsis = true,
            };

            foreach (var err in _errores)
                _cellColors[(err.Row, err.Col)] = string.IsNullOrEmpty(err.AutoFix) ? C_ERR : C_WARN;

            // ── DataGridView editable ─────────────────────────────────
            dgv = new DataGridView
            {
                Dock = DockStyle.Fill, BackgroundColor = DrawColor.White,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
                Font = new DrawFont("Segoe UI", 9.5f), GridColor = DrawColor.FromArgb(228, 228, 235),
                VirtualMode = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                ReadOnly = false, EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                RowHeadersWidth = 48, SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, ColumnHeadersHeight = 38,
                EnableHeadersVisualStyles = false,
                DefaultCellStyle = new DataGridViewCellStyle { BackColor = DrawColor.White, ForeColor = C_TXT, Padding = new Padding(4, 2, 4, 2), SelectionBackColor = DrawColor.FromArgb(200, 220, 255), SelectionForeColor = C_TXT },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = C_HDR_BG, ForeColor = DrawColor.FromArgb(10, 10, 40), Font = new DrawFont("Segoe UI", 9.5f, DrawFontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleCenter },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = DrawColor.FromArgb(250, 250, 253) },
            };
            dgv.RowTemplate.Height = 26;
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(dgv, true);

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "#", Width = 50, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle { BackColor = C_IDX_BG, ForeColor = C_ACCENT, Font = new DrawFont("Segoe UI", 8.5f, DrawFontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleCenter },
            });
            foreach (var h in _headers)
                dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = h, Width = Math.Max(100, Math.Min(280, h.Length * 13 + 40)), SortMode = DataGridViewColumnSortMode.NotSortable });
            dgv.RowCount = _editedRows.Count;

            dgv.CellValueNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _editedRows.Count) return;
                if (e.ColumnIndex == 0) { e.Value = e.RowIndex + 1; return; }
                int dc = e.ColumnIndex - 1; var row = _editedRows[e.RowIndex];
                e.Value = dc < row.Count ? row[dc] : "";
            };

            dgv.CellValuePushed += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _editedRows.Count || e.ColumnIndex == 0) return;
                int dc = e.ColumnIndex - 1; var row = _editedRows[e.RowIndex];
                while (row.Count <= dc) row.Add("");
                string newVal = e.Value?.ToString() ?? "";
                if (row[dc] == newVal) return;
                row[dc] = newVal; _cellColors.Remove((e.RowIndex, dc));
                if (!_isDirty)
                {
                    _isDirty = true; btnApply.Enabled = true;
                    lblEditHint.Text = "Hay cambios sin aplicar"; lblEditHint.ForeColor = DrawColor.FromArgb(180, 80, 0);
                }
                dgv.InvalidateRow(e.RowIndex);
            };

            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex == 0) return;
                if (_cellColors.TryGetValue((e.RowIndex, e.ColumnIndex - 1), out var col))
                { e.CellStyle.BackColor = col; e.FormattingApplied = true; }
            };

            dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) ShowRowErrors(e.RowIndex); };

            // ── Leyenda ───────────────────────────────────────────────
            var legend = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = C_BG };
            legend.Paint += (s, e) =>
            { using var pen = new System.Drawing.Pen(DrawColor.FromArgb(210, 210, 218)); e.Graphics.DrawLine(pen, 0, 0, legend.Width, 0); };

            void Chip(string text, DrawColor bg, int x) =>
                legend.Controls.Add(new Label { Text = text, Left = x, Top = 6, Height = 20, AutoSize = true, BackColor = bg, ForeColor = C_TXT, Font = new DrawFont("Segoe UI", 8.5f), Padding = new Padding(6, 2, 6, 2), BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle });
            Chip("Sin error",              DrawColor.White,                  14);
            Chip("Auto-corregible",        C_WARN,                          104);
            Chip("Revision manual",        C_ERR,                           250);
            Chip("F2 o escribir = editar", DrawColor.FromArgb(230, 245, 255), 380);

            Controls.Add(dgv); Controls.Add(legend); Controls.Add(lblBanner); Controls.Add(toolbar);
        }
    }
}

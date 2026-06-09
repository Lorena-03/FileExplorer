using ClosedXML.Excel;

namespace FileExplorer.Forms
{
    public partial class ExcelEditorForm
    {
        // ── Controles ────────────────────────────────────────────────
        DataGridView _dgv;
        TextBox _txtFormula;
        Label _lblCell;
        Label _lblInfo;
        Button _btnSave;

        /// <summary>
        /// Construye toda la interfaz: toolbar de operaciones, barra de fórmulas,
        /// grilla virtual con encabezados de columna (A–T) y filas numeradas,
        /// y barra de estado con conteo de celdas.
        /// </summary>
        void InitUI()
        {
            Text = Path.GetFileName(_filePath) + " — Excel";
            Size = new Size(1100, 720);
            MinimumSize = new Size(800, 500);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = C_BG;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;
            KeyPreview = true;
            KeyDown += OnKeyDown;

            BuildToolbar();
            BuildFormulaBar();
            BuildGrid();
            BuildStatusBar();
        }

        /// <summary>
        /// Construye la toolbar con botones para agregar/eliminar filas y columnas,
        /// insertar funciones (SUM, AVG, MIN, MAX, COUNT, IF) y guardar.
        /// </summary>
        void BuildToolbar()
        {
            var tb = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = C_TOOLBAR };
            tb.Paint += (s, e) =>
            {
                using var pen = new Pen(C_BORDER);
                e.Graphics.DrawLine(pen, 0, tb.Height - 1, tb.Width, tb.Height - 1);
            };

            int x = 8;
            TBtn(tb, ref x, "➕ Fila", "Agregar fila", AddRow);
            TBtn(tb, ref x, "➕ Columna", "Agregar columna", AddCol);
            TBtn(tb, ref x, "🗑 Fila", "Eliminar fila", DelRow);
            TBtn(tb, ref x, "🗑 Col", "Eliminar columna", DelCol);

            x += 8;
            TBtn(tb, ref x, "Σ SUM", "Insertar SUM", () => InsertFunc("SUM"));
            TBtn(tb, ref x, "∅ AVG", "Insertar AVG", () => InsertFunc("AVG"));
            TBtn(tb, ref x, "↓ MIN", "Insertar MIN", () => InsertFunc("MIN"));
            TBtn(tb, ref x, "↑ MAX", "Insertar MAX", () => InsertFunc("MAX"));
            TBtn(tb, ref x, "# CNT", "Insertar COUNT", () => InsertFunc("COUNT"));
            TBtn(tb, ref x, "? IF", "Insertar IF", () => InsertFunc("IF"));

            _btnSave = new Button
            {
                Text = "Guardar",
                Top = 6,
                Width = 90,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_ACCENT,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 99, 220);
            _btnSave.Click += (_, __) => SaveFile();
            _btnSave.Left = 900;
            tb.Controls.Add(_btnSave);
            tb.Resize += (_, __) => _btnSave.Left = tb.Width - _btnSave.Width - 8;

            Controls.Add(tb);
        }

        /// <summary>
        /// Construye la barra de fórmulas: label con nombre de celda (A1, B3…)
        /// y TextBox donde se edita/muestra el valor o fórmula de la celda activa.
        /// </summary>
        void BuildFormulaBar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = C_TOOLBAR };
            bar.Paint += (s, e) =>
            {
                using var pen = new Pen(C_BORDER);
                e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };

            _lblCell = new Label
            {
                Left = 8,
                Top = 6,
                Width = 50,
                Height = 22,
                Text = "A1",
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                ForeColor = C_FORMULA,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle,
            };

            var sep = new Label { Left = 62, Top = 6, Width = 1, Height = 22, BackColor = C_BORDER };

            _txtFormula = new TextBox
            {
                Left = 68,
                Top = 6,
                Height = 22,
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None,
                BackColor = C_TOOLBAR,
                ForeColor = C_FORMULA,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                PlaceholderText = "Escribe un valor o =FORMULA(rango)",
            };
            _txtFormula.KeyDown += FormulaBar_KeyDown;

            bar.Controls.AddRange(new Control[] { _lblCell, sep, _txtFormula });
            bar.Resize += (_, __) => _txtFormula.Width = bar.Width - 76;

            Controls.Add(bar);
        }

        /// <summary>
        /// Construye la grilla virtual: columnas A–T (índice 0–19), 50 filas numeradas,
        /// doble buffer activado via reflexión, y eventos de edición y selección.
        /// </summary>
        void BuildGrid()
        {
            _dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5f),
                GridColor = C_BORDER,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersWidth = 50,
                ColumnHeadersHeight = 26,
                EnableHeadersVisualStyles = false,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = C_TEXT,
                    SelectionBackColor = C_SEL,
                    SelectionForeColor = C_TEXT,
                    Padding = new Padding(2, 0, 2, 0),
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = C_HDR_BG,
                    ForeColor = Color.FromArgb(60, 60, 80),
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    SelectionBackColor = C_HDR_BG,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                },
                RowHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = C_HDR_BG,
                    ForeColor = Color.FromArgb(60, 60, 80),
                    Font = new Font("Segoe UI", 8.5f),
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                },
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
            };
            typeof(DataGridView)
                .GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_dgv, true);

            for (int c = 0; c < _cols; c++)
                _dgv.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = ColName(c),
                    Width = 90,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                });

            for (int r = 0; r < _rows; r++)
            {
                _dgv.Rows.Add();
                _dgv.Rows[r].HeaderCell.Value = (r + 1).ToString();
                _dgv.Rows[r].Height = 22;
            }

            _dgv.CellEndEdit += DgvCellEndEdit;
            _dgv.SelectionChanged += DgvSelectionChanged;
            _dgv.CellBeginEdit += DgvCellBeginEdit;

            Controls.Add(_dgv);
        }

        /// <summary>Construye la barra de estado con conteo de filas, columnas y celdas con datos.</summary>
        void BuildStatusBar()
        {
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = C_TOOLBAR };
            bar.Paint += (s, e) =>
            {
                using var pen = new Pen(C_BORDER);
                e.Graphics.DrawLine(pen, 0, 0, ((Panel)s).Width, 0);
            };
            _lblInfo = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = C_SUB,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f),
                Padding = new Padding(10, 0, 0, 0),
                Text = "Listo",
            };
            bar.Controls.Add(_lblInfo);
            Controls.Add(bar);
        }

        /// <summary>Crea un botón de toolbar con tooltip y acción.</summary>
        Button TBtn(Panel parent, ref int x, string text, string tip, Action act)
        {
            var b = new Button
            {
                Text = text,
                Left = x,
                Top = 6,
                Width = text.Length * 7 + 14,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 220, 228);
            new ToolTip().SetToolTip(b, tip);
            b.Click += (_, __) => act();
            parent.Controls.Add(b);
            x += b.Width + 4;
            return b;
        }
    }
}
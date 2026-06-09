using ClosedXML.Excel;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Editor de hojas de cálculo Excel con grilla editable, barra de fórmulas
    /// y soporte para SUM, AVG, MIN, MAX, COUNT, IF y referencias de celda.
    /// </summary>
    public partial class ExcelEditorForm : Form
    {
        // ── Colores ───────────────────────────────────────────────────
        static readonly Color C_BG = Color.FromArgb(245, 245, 248);
        static readonly Color C_TOOLBAR = Color.FromArgb(250, 250, 252);
        static readonly Color C_BORDER = Color.FromArgb(210, 210, 218);
        static readonly Color C_ACCENT = Color.FromArgb(0, 122, 255);
        static readonly Color C_TEXT = Color.FromArgb(28, 28, 30);
        static readonly Color C_SUB = Color.FromArgb(142, 142, 147);
        static readonly Color C_HDR_BG = Color.FromArgb(240, 242, 255);
        static readonly Color C_SEL = Color.FromArgb(198, 219, 255);
        static readonly Color C_FORMULA = Color.FromArgb(0, 80, 160);

        readonly string _filePath;
        bool _isDirty = false;

        // ── Estado ────────────────────────────────────────────────────
        int _rows = 50, _cols = 20;
        string[,] _data;       // datos crudos
        string[,] _formulas;   // fórmulas si la celda tiene una
        int _selRow = 0, _selCol = 0;

        // ── Controles definidos en Designer ──────────────────────────

        public ExcelEditorForm(string filePath)
        {
            _filePath = filePath;
            _data = new string[_rows, _cols];
            _formulas = new string[_rows, _cols];
            InitUI();
            LoadFile();
        }

        // ════════════════════════════════════════════════════════════
        //  UI
        // ════════════════════════════════════════════════════════════





        // ════════════════════════════════════════════════════════════
        //  CARGA
        // ════════════════════════════════════════════════════════════
        void LoadFile()
        {
            if (!File.Exists(_filePath)) return;
            try
            {
                using var wb = new XLWorkbook(_filePath);
                var ws = wb.Worksheets.First();
                int maxR = Math.Min(ws.LastRowUsed()?.RowNumber() ?? 0, _rows);
                int maxC = Math.Min(ws.LastColumnUsed()?.ColumnNumber() ?? 0, _cols);

                for (int r = 1; r <= maxR; r++)
                    for (int c = 1; c <= maxC; c++)
                    {
                        var cell = ws.Cell(r, c);
                        string val = cell.IsEmpty() ? "" : cell.GetString();
                        _data[r - 1, c - 1] = val;
                        if (cell.HasFormula)
                            _formulas[r - 1, c - 1] = "=" + cell.FormulaA1;
                        _dgv.Rows[r - 1].Cells[c - 1].Value = val;
                    }
                _isDirty = false;
                UpdateInfo();
            }
            catch { /* archivo nuevo */ }
        }

        // ════════════════════════════════════════════════════════════
        //  GUARDAR
        // ════════════════════════════════════════════════════════════
        /// <summary>
        /// Abre un SaveFileDialog para que el usuario elija dónde guardar el XLSX,
        /// luego escribe el archivo y muestra confirmación.
        /// </summary>
        void SaveFile()
        {
            // Pedir destino al usuario
            string destPath = null;
            var thread = new Thread(() =>
            {
                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar archivo Excel",
                    Filter = "Excel (*.xlsx)|*.xlsx|Todos los archivos|*.*",
                    FileName = Path.GetFileName(_filePath),
                    InitialDirectory = File.Exists(_filePath)
                        ? Path.GetDirectoryName(_filePath)
                        : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    OverwritePrompt = true,
                };
                if (dlg.ShowDialog() == DialogResult.OK)
                    destPath = dlg.FileName;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (destPath == null) return; // usuario canceló

            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Hoja1");

                for (int r = 0; r < _rows; r++)
                    for (int c = 0; c < _cols; c++)
                    {
                        string formula = _formulas[r, c];
                        string data = _data[r, c] ?? "";
                        if (!string.IsNullOrEmpty(formula))
                            ws.Cell(r + 1, c + 1).FormulaA1 = formula.TrimStart('=');
                        else if (!string.IsNullOrEmpty(data))
                        {
                            if (double.TryParse(data, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out double num))
                                ws.Cell(r + 1, c + 1).Value = num;
                            else
                                ws.Cell(r + 1, c + 1).Value = data;
                        }
                    }

                ws.Columns().AdjustToContents();

                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                File.WriteAllBytes(destPath, ms.ToArray());

                _isDirty = false;
                Flash("Guardado  ✓", Color.FromArgb(52, 199, 89));

                MessageBox.Show(
                    "Archivo guardado correctamente en:\n" + destPath,
                    "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al guardar:\n" + ex.Message,
                    "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // ════════════════════════════════════════════════════════════
        //  FÓRMULAS
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Evalúa una fórmula simple del tipo =FUNC(A1:B3) o =A1+B2.
        /// Soporta SUM, AVG, MIN, MAX, COUNT, IF y referencias directas.
        /// </summary>
        string EvalFormula(string formula, int srcRow, int srcCol)
        {
            if (string.IsNullOrEmpty(formula) || !formula.StartsWith("="))
                return formula ?? "";

            string expr = formula.Substring(1).Trim().ToUpperInvariant();

            try
            {
                // Detectar función
                var m = System.Text.RegularExpressions.Regex.Match(expr,
                    @"^(SUM|AVG|AVERAGE|MIN|MAX|COUNT|IF)\((.+)\)$");

                if (m.Success)
                {
                    string func = m.Groups[1].Value;
                    string args = m.Groups[2].Value;

                    if (func == "IF")
                        return EvalIf(args, srcRow, srcCol);

                    var nums = GetRange(args, srcRow, srcCol);
                    if (nums.Count == 0) return "0";

                    double result = func switch
                    {
                        "SUM" => nums.Sum(),
                        "AVG" or "AVERAGE" => nums.Average(),
                        "MIN" => nums.Min(),
                        "MAX" => nums.Max(),
                        "COUNT" => nums.Count,
                        _ => 0,
                    };
                    return result % 1 == 0 ? ((long)result).ToString() : result.ToString("F2");
                }

                // Aritmética simple: =A1+B2, =A1*2, etc.
                string resolved = System.Text.RegularExpressions.Regex.Replace(expr,
                    @"([A-Z]+)(\d+)", match =>
                    {
                        int c = ColIndex(match.Groups[1].Value);
                        int r = int.Parse(match.Groups[2].Value) - 1;
                        if (r >= 0 && r < _rows && c >= 0 && c < _cols)
                            return _data[r, c] ?? "0";
                        return "0";
                    });

                var dt = new System.Data.DataTable();
                return dt.Compute(resolved, "").ToString() ?? "0";
            }
            catch { return "#ERROR"; }
        }

        string EvalIf(string args, int srcRow, int srcCol)
        {
            // IF(condición, verdadero, falso)
            var parts = SplitArgs(args);
            if (parts.Length < 3) return "#ERROR";
            string cond = parts[0].Trim();
            string ifTrue = parts[1].Trim();
            string ifFalse = parts[2].Trim();

            try
            {
                // Resolver referencias en condición
                string resolvedCond = System.Text.RegularExpressions.Regex.Replace(cond.ToUpperInvariant(),
                    @"([A-Z]+)(\d+)", match =>
                    {
                        int c = ColIndex(match.Groups[1].Value);
                        int r = int.Parse(match.Groups[2].Value) - 1;
                        return (r >= 0 && r < _rows && c >= 0 && c < _cols) ? _data[r, c] ?? "0" : "0";
                    });

                var dt = new System.Data.DataTable();
                bool result = Convert.ToBoolean(dt.Compute(resolvedCond, ""));
                return result ? ifTrue : ifFalse;
            }
            catch { return "#ERROR"; }
        }

        /// <summary>Obtiene los valores numéricos de un rango como A1:C3 o lista A1,B2.</summary>
        List<double> GetRange(string rangeStr, int srcRow, int srcCol)
        {
            var nums = new List<double>();

            // Rango A1:C3
            var rangeMatch = System.Text.RegularExpressions.Regex.Match(rangeStr,
                @"^([A-Z]+)(\d+):([A-Z]+)(\d+)$");
            if (rangeMatch.Success)
            {
                int c1 = ColIndex(rangeMatch.Groups[1].Value), r1 = int.Parse(rangeMatch.Groups[2].Value) - 1;
                int c2 = ColIndex(rangeMatch.Groups[3].Value), r2 = int.Parse(rangeMatch.Groups[4].Value) - 1;
                for (int r = r1; r <= r2 && r < _rows; r++)
                    for (int c = c1; c <= c2 && c < _cols; c++)
                        if (double.TryParse(_data[r, c],
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double v))
                            nums.Add(v);
                return nums;
            }

            // Lista A1,B2,C3
            foreach (var part in SplitArgs(rangeStr))
            {
                var cellMatch = System.Text.RegularExpressions.Regex.Match(part.Trim(), @"^([A-Z]+)(\d+)$");
                if (cellMatch.Success)
                {
                    int c = ColIndex(cellMatch.Groups[1].Value);
                    int r = int.Parse(cellMatch.Groups[2].Value) - 1;
                    if (r >= 0 && r < _rows && c >= 0 && c < _cols &&
                        double.TryParse(_data[r, c],
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double v))
                        nums.Add(v);
                }
                else if (double.TryParse(part.Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double literal))
                    nums.Add(literal);
            }
            return nums;
        }

        static string[] SplitArgs(string s) => s.Split(',');

        /// <summary>Convierte nombre de columna (A, B, AA) a índice 0-based.</summary>
        static int ColIndex(string name)
        {
            int idx = 0;
            foreach (char c in name.ToUpperInvariant())
                idx = idx * 26 + (c - 'A' + 1);
            return idx - 1;
        }

        /// <summary>Convierte índice 0-based a nombre de columna (0→A, 25→Z, 26→AA).</summary>
        static string ColName(int idx)
        {
            string name = "";
            idx++;
            while (idx > 0)
            {
                int rem = (idx - 1) % 26;
                name = (char)('A' + rem) + name;
                idx = (idx - 1) / 26;
            }
            return name;
        }

        // ════════════════════════════════════════════════════════════
        //  EVENTOS GRILLA
        // ════════════════════════════════════════════════════════════
        void DgvCellBeginEdit(object s, DataGridViewCellCancelEventArgs e)
        {
            // Mostrar fórmula en la celda al editar
            string f = _formulas[e.RowIndex, e.ColumnIndex];
            if (!string.IsNullOrEmpty(f))
                _dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = f;
        }

        void DgvCellEndEdit(object s, DataGridViewCellEventArgs e)
        {
            string val = _dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";
            if (val.StartsWith("="))
            {
                _formulas[e.RowIndex, e.ColumnIndex] = val;
                string result = EvalFormula(val, e.RowIndex, e.ColumnIndex);
                _data[e.RowIndex, e.ColumnIndex] = result;
                _dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = result;
            }
            else
            {
                _formulas[e.RowIndex, e.ColumnIndex] = "";
                _data[e.RowIndex, e.ColumnIndex] = val;
            }
            _isDirty = true;
            UpdateInfo();
            UpdateFormulaBar(e.RowIndex, e.ColumnIndex);
        }

        void DgvSelectionChanged(object s, EventArgs e)
        {
            if (_dgv.CurrentCell == null) return;
            int r = _dgv.CurrentCell.RowIndex, c = _dgv.CurrentCell.ColumnIndex;
            _selRow = r; _selCol = c;
            UpdateFormulaBar(r, c);
            UpdateSelectionInfo();
        }

        void UpdateFormulaBar(int r, int c)
        {
            _lblCell.Text = ColName(c) + (r + 1).ToString();
            string f = _formulas[r, c];
            _txtFormula.Text = string.IsNullOrEmpty(f) ? (_data[r, c] ?? "") : f;
            _txtFormula.ForeColor = string.IsNullOrEmpty(f) ? C_TEXT : C_FORMULA;
        }

        void FormulaBar_KeyDown(object s, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            string val = _txtFormula.Text;
            _dgv.Rows[_selRow].Cells[_selCol].Value = val;
            DgvCellEndEdit(_dgv, new DataGridViewCellEventArgs(_selCol, _selRow));
            _dgv.Focus();
        }

        // ════════════════════════════════════════════════════════════
        //  OPERACIONES DE FILAS/COLUMNAS
        // ════════════════════════════════════════════════════════════
        void AddRow()
        {
            _rows++;
            var newData = new string[_rows, _cols];
            var newForm = new string[_rows, _cols];
            Array.Copy(_data, newData, (_rows - 1) * _cols);
            Array.Copy(_formulas, newForm, (_rows - 1) * _cols);
            _data = newData; _formulas = newForm;
            _dgv.Rows.Add();
            _dgv.Rows[_rows - 1].HeaderCell.Value = _rows.ToString();
            _dgv.Rows[_rows - 1].Height = 22;
            _isDirty = true; UpdateInfo();
        }

        void AddCol()
        {
            _cols++;
            var newData = new string[_rows, _cols];
            var newForm = new string[_rows, _cols];
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols - 1; c++)
                { newData[r, c] = _data[r, c]; newForm[r, c] = _formulas[r, c]; }
            _data = newData; _formulas = newForm;
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = ColName(_cols - 1),
                Width = 90,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });
            _isDirty = true; UpdateInfo();
        }

        void DelRow()
        {
            if (_rows <= 1 || _dgv.CurrentCell == null) return;
            int r = _dgv.CurrentCell.RowIndex;
            _dgv.Rows.RemoveAt(r);
            _rows--;
            var newData = new string[_rows, _cols];
            var newForm = new string[_rows, _cols];
            int dest = 0;
            for (int i = 0; i < _rows + 1; i++)
            {
                if (i == r) continue;
                for (int c = 0; c < _cols; c++)
                { newData[dest, c] = _data[i, c]; newForm[dest, c] = _formulas[i, c]; }
                dest++;
            }
            _data = newData; _formulas = newForm;
            for (int i = 0; i < _rows; i++)
                _dgv.Rows[i].HeaderCell.Value = (i + 1).ToString();
            _isDirty = true; UpdateInfo();
        }

        void DelCol()
        {
            if (_cols <= 1 || _dgv.CurrentCell == null) return;
            int c = _dgv.CurrentCell.ColumnIndex;
            _dgv.Columns.RemoveAt(c);
            _cols--;
            var newData = new string[_rows, _cols];
            var newForm = new string[_rows, _cols];
            for (int r = 0; r < _rows; r++)
            {
                int dest = 0;
                for (int i = 0; i < _cols + 1; i++)
                {
                    if (i == c) continue;
                    newData[r, dest] = _data[r, i]; newForm[r, dest] = _formulas[r, i];
                    dest++;
                }
            }
            _data = newData; _formulas = newForm;
            for (int i = 0; i < _cols; i++)
                _dgv.Columns[i].HeaderText = ColName(i);
            _isDirty = true; UpdateInfo();
        }

        /// <summary>Inserta una fórmula de ejemplo en la barra de fórmulas.</summary>
        void InsertFunc(string func)
        {
            string example = func switch
            {
                "SUM" => "=SUM(A1:A10)",
                "AVG" => "=AVG(A1:A10)",
                "MIN" => "=MIN(A1:A10)",
                "MAX" => "=MAX(A1:A10)",
                "COUNT" => "=COUNT(A1:A10)",
                "IF" => "=IF(A1>0,verdadero,falso)",
                _ => $"={func}()",
            };
            _txtFormula.Text = example;
            _txtFormula.Focus();
            _txtFormula.SelectAll();
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void UpdateInfo()
        {
            int used = 0;
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    if (!string.IsNullOrEmpty(_data[r, c])) used++;
            _lblInfo.Text = $"{_rows} filas  ·  {_cols} cols  ·  {used} celdas con datos" +
                                 (_isDirty ? "  *" : "");
            _lblInfo.ForeColor = C_SUB;
        }

        void UpdateSelectionInfo()
        {
            // Mostrar suma de selección si hay múltiples celdas
            if (_dgv.SelectedCells.Count <= 1) return;
            double sum = 0; int cnt = 0;
            foreach (DataGridViewCell cell in _dgv.SelectedCells)
                if (double.TryParse(cell.Value?.ToString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double v))
                { sum += v; cnt++; }
            if (cnt > 0)
                _lblInfo.Text = $"Selección: {_dgv.SelectedCells.Count} celdas  ·  Suma={sum:F2}  ·  Promedio={sum / cnt:F2}";
        }

        void Flash(string msg, Color color)
        {
            _lblInfo.Text = msg; _lblInfo.ForeColor = color;
            var t = new System.Windows.Forms.Timer { Interval = 2000 };
            t.Tick += (_, __) => { UpdateInfo(); t.Dispose(); };
            t.Start();
        }


        void OnKeyDown(object s, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S)
            { SaveFile(); e.SuppressKeyPress = true; }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isDirty)
            {
                var r = MessageBox.Show("¿Guardar cambios antes de cerrar?", "Cambios sin guardar",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Yes) SaveFile();
                else if (r == DialogResult.Cancel) { e.Cancel = true; return; }
            }
            base.OnFormClosing(e);
        }
    }
}
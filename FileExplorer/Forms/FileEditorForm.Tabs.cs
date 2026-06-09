using FileExplorer.Helpers;
using System.Collections.Concurrent;
using DrawColor = System.Drawing.Color;

namespace FileExplorer.Forms
{
    public partial class FileEditorForm
    {
        /// <summary>Paleta de colores cíclica usada para barras y sectores en las gráficas.</summary>
        static readonly DrawColor[] ChartColors = {
            DrawColor.FromArgb(0, 122, 255),  DrawColor.FromArgb(52, 199, 89),
            DrawColor.FromArgb(255, 149, 0),  DrawColor.FromArgb(255, 59, 48),
            DrawColor.FromArgb(175, 82, 222), DrawColor.FromArgb(90, 200, 250),
            DrawColor.FromArgb(255, 204, 0),  DrawColor.FromArgb(255, 45, 85),
            DrawColor.FromArgb(48, 209, 88),  DrawColor.FromArgb(100, 210, 255),
        };

        /// <summary>
        /// Reconstruye la grilla virtual desde cero: limpia columnas, agrega la columna índice "#",
        /// agrega una columna por cada header con ancho proporcional al nombre,
        /// actualiza el ComboBox de búsqueda y refresca los combos de gráficas.
        /// </summary>
        internal void BuildVirtualGrid()
        {
            dgv.Columns.Clear(); _cellColors.Clear(); _searchHighlight.Clear();
            if (lblErrCount != null) lblErrCount.Text = "";

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "#",
                Width = 50,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = C_IDX_BG,
                    ForeColor = C_ACCENT,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });
            foreach (var h in _headers)
            {
                int w = Math.Max(100, Math.Min(280, h.Length * 13 + 40));
                dgv.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = h,
                    Width = w,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    DefaultCellStyle = new DataGridViewCellStyle { Padding = new Padding(4, 2, 4, 2) }
                });
            }
            dgv.RowCount = _rows.Count; dgv.Invalidate();

            if (cboSearchCol != null)
            {
                cboSearchCol.Items.Clear();
                cboSearchCol.Items.Add("(Todas las columnas)");
                cboSearchCol.Items.Add("# Indice");
                foreach (var h in _headers) cboSearchCol.Items.Add(h);
                cboSearchCol.SelectedIndex = 0;
            }

            RefreshChartCombos();
            UpdateInfo();
        }

        /// <summary>Sincroniza los ComboBox de columna X/Y de la pestaña Gráficas con los headers actuales.</summary>
        void RefreshChartCombos()
        {
            if (cboChartX == null || cboChartY == null) return;
            cboChartX.Items.Clear();
            cboChartY.Items.Clear();
            foreach (var h in _headers)
            {
                cboChartX.Items.Add(h);
                cboChartY.Items.Add(h);
            }
            if (cboChartX.Items.Count > 0)
            {
                cboChartX.SelectedIndex = 0;
                cboChartY.SelectedIndex = 0;
            }
        }

        /// <summary>Fuerza el repintado del panel de gráfica para regenerar la vista actual.</summary>
        void DrawChart() => pnlChart?.Invalidate();

        /// <summary>
        /// Pinta la gráfica activa (barras o pastel) al repintar el panel.
        /// Agrega los valores de la columna seleccionada: si son numéricos los suma por etiqueta;
        /// si todos son cero, cuenta frecuencias. Toma los 20 valores más altos.
        /// </summary>
        void PnlChart_Paint(object s, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(DrawColor.White);

            if (_headers.Count == 0 || _rows.Count == 0)
            { DrawCenteredMsg(g, pnlChart, "Carga un archivo con datos para ver graficas"); return; }

            if (cboChartX?.SelectedIndex < 0)
            { DrawCenteredMsg(g, pnlChart, "Selecciona una columna"); return; }

            int colX = cboChartX.SelectedIndex;
            int colY = colX;
            bool isPie = cboChartType.SelectedIndex == 1;

            var data = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _rows)
            {
                string label = colX < row.Count ? row[colX] : "";
                string valStr = colY < row.Count ? row[colY] : "0";
                if (string.IsNullOrWhiteSpace(label)) continue;
                double.TryParse(valStr.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double val);
                if (data.ContainsKey(label)) data[label] += val; else data[label] = val;
            }

            // Fallback a frecuencias si no hay valores numéricos
            if (data.Values.All(v => v == 0))
            {
                data.Clear();
                foreach (var row in _rows)
                {
                    string label = colX < row.Count ? row[colX] : "";
                    if (string.IsNullOrWhiteSpace(label)) continue;
                    if (data.ContainsKey(label)) data[label]++; else data[label] = 1;
                }
            }

            if (data.Count == 0) { DrawCenteredMsg(g, pnlChart, "Sin datos para graficar"); return; }

            var sorted = data.OrderByDescending(kv => kv.Value).Take(20).ToList();
            string title = $"{(isPie ? "Pastel" : "Barras")}: {cboChartX.SelectedItem}";

            if (isPie) DrawPieChart(g, pnlChart, sorted, title);
            else DrawBarChart(g, pnlChart, sorted, title);
        }

        /// <summary>
        /// Busca el texto del campo de búsqueda en la grilla en paralelo.
        /// Resalta las celdas que coinciden y desplaza la vista a la primera fila encontrada.
        /// Puede filtrar por todas las columnas, por el índice o por una columna específica.
        /// </summary>
        void SearchInGrid()
        {
            string query = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(query)) { ClearSearch(); return; }
            _searchHighlight.Clear();
            int colFilter = cboSearchCol.SelectedIndex, found = 0, firstRow = -1;
            var bag = new ConcurrentBag<int>();
            Parallel.For(0, _rows.Count, rowIdx =>
            {
                var row = _rows[rowIdx]; bool match = false;
                Action<string, int> chk = (v, col) =>
                {
                    if (v.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    { lock (_searchHighlight) _searchHighlight.Add((rowIdx, col)); match = true; }
                };
                if (colFilter == 0) { chk(rowIdx.ToString(), -1); for (int c = 0; c < row.Count; c++) chk(row[c], c); }
                else if (colFilter == 1) chk(rowIdx.ToString(), -1);
                else { int dc = colFilter - 2; if (dc >= 0 && dc < row.Count) chk(row[dc], dc); }
                if (match) bag.Add(rowIdx);
            });
            found = bag.Count; if (found > 0) firstRow = bag.Min();
            dgv.Invalidate();
            if (firstRow >= 0) dgv.FirstDisplayedScrollingRowIndex = Math.Min(firstRow, _rows.Count - 1);
            string colName = colFilter == 0 ? "todas las columnas" : colFilter == 1 ? "indice #"
                : colFilter < cboSearchCol.Items.Count ? cboSearchCol.Items[colFilter].ToString() : "";
            lblSearchResult.Text = found > 0 ? found + " resultado(s) en " + colName : "Sin resultados";
            lblSearchResult.ForeColor = found > 0 ? C_GREEN : C_RED;
        }

        /// <summary>Limpia el resaltado de búsqueda y el campo de texto, restaurando la grilla al estado normal.</summary>
        void ClearSearch()
        {
            _searchHighlight.Clear(); txtSearch.Clear();
            lblSearchResult.Text = ""; dgv.Invalidate();
        }

        /// <summary>
        /// Alterna entre la vista de tabla y la vista de texto para archivos TXT/MD/LOG.
        /// Al activar la tabla intenta parsear el texto como datos estructurados;
        /// si no puede, lo muestra como una columna de líneas.
        /// </summary>
        void ToggleTxtTable()
        {
            _tableMode = !_tableMode;
            if (_tableMode)
            {
                string[] lines = rtbEdit.Text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd();
                if (!DataParser.TryParseTxt(lines, out var hdrs, out var rws))
                { _headers = new List<string> { "Linea" }; _rows = lines.Select(l => new List<string> { l }).ToList(); }
                else { _headers = hdrs; _rows = rws; }
                BuildVirtualGrid();
                if (tabs.TabPages.Contains(pgTable)) tabs.SelectedTab = pgTable;
                if (btnToggleView != null) { btnToggleView.Text = "Ver texto"; btnToggleView.BackColor = C_ACCENT; }
            }
            else
            {
                if (tabs.TabPages.Contains(pgEdit)) tabs.SelectedTab = pgEdit;
                if (btnToggleView != null) { btnToggleView.Text = "Ver tabla"; btnToggleView.BackColor = DrawColor.FromArgb(72, 72, 76); }
            }
        }

        /// <summary>
        /// Elimina del TabControl las pestañas que no aplican al tipo de archivo actual.
        /// PDF/Word: sin Tabla ni Gráficas. PPTX: solo Vista.
        /// Imagen: solo Vista. Excel: sin Vista ni Editor. Datos: sin Vista. Resto: solo Editor.
        /// </summary>
        void ConfigureTabs()
        {
            if (_ext == ".pdf" || _ext == ".docx" || _ext == ".doc")
            {
                // Quitar editor de texto plano — se usa el editor enriquecido (pestaña "Editor")
                if (tabs.TabPages.Contains(pgTable)) tabs.TabPages.Remove(pgTable);
                if (tabs.TabPages.Contains(pgChart)) tabs.TabPages.Remove(pgChart);
                if (tabs.TabPages.Contains(pgEdit)) tabs.TabPages.Remove(pgEdit);
            }
            else if (_ext == ".pptx" || _ext == ".ppt")
            {
                if (tabs.TabPages.Contains(pgTable)) tabs.TabPages.Remove(pgTable);
                if (tabs.TabPages.Contains(pgEdit)) tabs.TabPages.Remove(pgEdit);
                if (tabs.TabPages.Contains(pgChart)) tabs.TabPages.Remove(pgChart);
            }
            else if (IsImage)
            {
                if (tabs.TabPages.Contains(pgTable)) tabs.TabPages.Remove(pgTable);
                if (tabs.TabPages.Contains(pgEdit)) tabs.TabPages.Remove(pgEdit);
                if (tabs.TabPages.Contains(pgChart)) tabs.TabPages.Remove(pgChart);
            }
            else if (IsExcel)
            {
                if (tabs.TabPages.Contains(pgView)) tabs.TabPages.Remove(pgView);
                if (tabs.TabPages.Contains(pgEdit)) tabs.TabPages.Remove(pgEdit);
            }
            else if (IsDataFile)
            {
                if (tabs.TabPages.Contains(pgView)) tabs.TabPages.Remove(pgView);
            }
            else
            {
                if (tabs.TabPages.Contains(pgView)) tabs.TabPages.Remove(pgView);
                if (tabs.TabPages.Contains(pgTable)) tabs.TabPages.Remove(pgTable);
                if (tabs.TabPages.Contains(pgChart)) tabs.TabPages.Remove(pgChart);
            }
        }
    }
}
using FileExplorer.Helpers;
using FileExplorer.Models;
using DrawColor = System.Drawing.Color;
using DrawFont = System.Drawing.Font;
using DrawFontStyle = System.Drawing.FontStyle;

namespace FileExplorer.Forms
{
    public partial class FileEditorForm
    {
        // ════════════════════════════════════════════════════════════
        //  DETECTAR ERRORES
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Inicia la detección de errores de forma asíncrona y navega a la pestaña Tabla al terminar.
        /// Solo actúa si el archivo es de tipo datos y no está cargándose.
        /// </summary>
        async void DetectErrors()
        {
            if (!IsDataFile || _isLoading) return;
            if (btnDetect != null) btnDetect.Enabled = false;
            await DetectAndHandleErrors();
            if (btnDetect != null) btnDetect.Enabled = true;
            if (tabs.TabPages.Contains(pgTable)) tabs.SelectedTab = pgTable;
        }

        /// <summary>
        /// Ejecuta la detección de errores en background usando DataValidator,
        /// actualiza la lista de errores y colorea las celdas afectadas
        /// (rojo = manual, amarillo = auto-corregible).
        /// </summary>
        internal async Task DetectAndHandleErrors()
        {
            _errors.Clear(); lstErrors.Items.Clear();
            _cellColors.Clear(); dgv.Invalidate();
            lblErrCount.Text = "  Analizando..."; lblErrCount.ForeColor = C_ACCENT;
            if (btnFixAll != null) btnFixAll.Enabled = false;

            var hCopy = new List<string>(_headers);
            var rCopy = _rows.Select(r => new List<string>(r)).ToList();
            _errors = await Task.Run(() => DataValidator.Validate(hCopy, rCopy));

            foreach (var err in _errors)
            {
                lstErrors.Items.Add(err);
                DrawColor col = string.IsNullOrEmpty(err.AutoFix) ? C_ERR_BG : C_WARN_BG;
                _cellColors[(err.Row, err.Col)] = col;
            }

            int total = _errors.Count,
                auto = _errors.Count(e => !string.IsNullOrEmpty(e.AutoFix)),
                man = total - auto;

            if (total == 0)
            {
                lblErrCount.Text = "  Sin errores detectados";
                lblErrCount.ForeColor = C_GREEN;
                AppLogger.Info("FileEditorForm: sin errores detectados en " + Path.GetFileName(_path));
            }
            else
            {
                lblErrCount.Text = $"  {total} error(es)  ·  {auto} auto-corregibles (amarillo)  ·  {man} manuales (rojo)";
                lblErrCount.ForeColor = C_RED;
                if (btnFixAll != null) btnFixAll.Enabled = auto > 0;
                AppLogger.Info($"FileEditorForm: {total} errores detectados en {Path.GetFileName(_path)}");
            }
            dgv.Invalidate();
        }

        // ════════════════════════════════════════════════════════════
        //  CORREGIR
        // ════════════════════════════════════════════════════════════

        /// <summary>Aplica la corrección automática al error seleccionado en la lista.</summary>
        void FixSelected()
        {
            if (lstErrors.SelectedItem is ValidationError err)
            { ApplyFix(err); lstErrors.Invalidate(); }
        }

        /// <summary>Aplica todas las correcciones automáticas pendientes en la lista de errores.</summary>
        void FixAll()
        {
            foreach (var e in _errors.Where(e => !string.IsNullOrEmpty(e.AutoFix) && !e.Fixed))
                ApplyFix(e);
            lstErrors.Invalidate(); dgv.Invalidate();
            if (btnFixAll != null) btnFixAll.Enabled = false;
            AppLogger.Info("FileEditorForm: correcciones automáticas aplicadas");
        }


        /// <summary>
        /// Reemplaza el valor de la celda con la corrección sugerida y marca el error como corregido.
        /// </summary>
        void ApplyFix(ValidationError err)
        {
            if (string.IsNullOrEmpty(err.AutoFix)) return;
            try
            {
                if (err.Row < _rows.Count)
                {
                    while (_rows[err.Row].Count <= err.Col) _rows[err.Row].Add("");
                    _rows[err.Row][err.Col] = err.AutoFix;
                }
                _cellColors[(err.Row, err.Col)] = C_FIX_BG;
                err.Fixed = true;
                dgv.InvalidateRow(err.Row);
            }
            catch (Exception ex) { AppLogger.Error("FileEditorForm.ApplyFix falló", ex); }
        }

        /// <summary>
        /// Desplaza la grilla hasta la celda correspondiente al error seleccionado en la lista.
        /// </summary>
        void NavigateToError()
        {
            if (lstErrors.SelectedItem is ValidationError err)
            {
                try
                {
                    int gc = err.Col + 1;
                    if (err.Row >= 0 && err.Row < dgv.RowCount && gc >= 0 && gc < dgv.ColumnCount)
                    {
                        dgv.FirstDisplayedScrollingRowIndex = Math.Max(0, err.Row - 3);
                        dgv.CurrentCell = dgv.Rows[err.Row].Cells[gc];
                    }
                }
                catch (Exception ex) { AppLogger.Warn("FileEditorForm.NavigateToError: " + ex.Message); }
            }
        }

        // ════════════════════════════════════════════════════════════
        //  DIBUJO DE ITEMS DE ERROR
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Pinta cada elemento del ListBox de errores con color de fondo, barra lateral de color,
        /// badge de número de fila, nombre de columna, descripción y sugerencia de corrección.
        /// </summary>
        void DrawErrorItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstErrors.Items.Count) return;
            try
            {
                var err = (ValidationError)lstErrors.Items[e.Index];
                bool sel = e.State.HasFlag(DrawItemState.Selected);
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                DrawColor bg = sel ? DrawColor.FromArgb(218, 232, 252)
                             : err.Fixed ? DrawColor.FromArgb(240, 252, 244)
                             : !string.IsNullOrEmpty(err.AutoFix) ? DrawColor.FromArgb(255, 250, 235)
                             : DrawColor.FromArgb(255, 245, 245);
                g.FillRectangle(new System.Drawing.SolidBrush(bg), e.Bounds);

                DrawColor bar = err.Fixed ? C_GREEN : !string.IsNullOrEmpty(err.AutoFix) ? C_ORANGE : C_RED;
                g.FillRectangle(new System.Drawing.SolidBrush(bar), e.Bounds.X, e.Bounds.Y, 6, e.Bounds.Height);

                var br = new System.Drawing.RectangleF(e.Bounds.X + 14, e.Bounds.Y + 10, 58, 18);
                g.FillRectangle(new System.Drawing.SolidBrush(bar), br);
                g.DrawString("Fila " + err.Row,
                    new DrawFont("Segoe UI", 7.5f, DrawFontStyle.Bold), System.Drawing.Brushes.White,
                    new System.Drawing.RectangleF(br.X, br.Y + 2, br.Width, br.Height),
                    new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center });

                g.DrawString(err.ColumnName, new DrawFont("Segoe UI", 9f, DrawFontStyle.Bold),
                    new System.Drawing.SolidBrush(C_TXT), e.Bounds.X + 80, e.Bounds.Y + 8);
                g.DrawString(err.Description, new DrawFont("Segoe UI", 8.5f),
                    new System.Drawing.SolidBrush(C_SEC), e.Bounds.X + 80, e.Bounds.Y + 27);

                string rt = err.Fixed
                    ? "Corregido: " + Trunc(err.AutoFix)
                    : string.IsNullOrEmpty(err.AutoFix)
                        ? "Valor: " + Trunc(err.Value) + "  (manual)"
                        : Trunc(err.Value) + "  ->  " + Trunc(err.AutoFix);
                DrawColor rc = err.Fixed ? C_GREEN : string.IsNullOrEmpty(err.AutoFix) ? C_RED : C_ORANGE;
                g.DrawString(rt,
                    new DrawFont("Segoe UI", 8.5f, err.Fixed ? DrawFontStyle.Bold : DrawFontStyle.Regular),
                    new System.Drawing.SolidBrush(rc),
                    new System.Drawing.RectangleF(e.Bounds.Right - 440, e.Bounds.Y, 430, e.Bounds.Height),
                    new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Far, LineAlignment = System.Drawing.StringAlignment.Center, Trimming = System.Drawing.StringTrimming.EllipsisCharacter });

                using var pen = new System.Drawing.Pen(DrawColor.FromArgb(225, 225, 230));
                g.DrawLine(pen, e.Bounds.Left + 6, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
            catch (Exception ex) { AppLogger.Warn("FileEditorForm.DrawErrorItem: " + ex.Message); }
        }

        /// <summary>Trunca una cadena al máximo de caracteres indicado añadiendo "..." si es necesario.</summary>
        static string Trunc(string v, int max = 32) =>
            v != null && v.Length > max ? v.Substring(0, max) + "..." : (v ?? "");
    }
}
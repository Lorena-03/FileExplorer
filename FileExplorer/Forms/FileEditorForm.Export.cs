using FileExplorer.Helpers;
using System.Text;
using UglyToad.PdfPig;
using ClosedXML.Excel;
using DrawColor = System.Drawing.Color;
using DrawFont = System.Drawing.Font;
using DrawFontStyle = System.Drawing.FontStyle;
using WDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument;
using WPara = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WText = DocumentFormat.OpenXml.Wordprocessing.Text;
using WBody = DocumentFormat.OpenXml.Wordprocessing.Body;
using WTbl = DocumentFormat.OpenXml.Wordprocessing.Table;
using WTblRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using WTblCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using WTblProp = DocumentFormat.OpenXml.Wordprocessing.TableProperties;
using WTblW = DocumentFormat.OpenXml.Wordprocessing.TableWidth;
using WTblStyle = DocumentFormat.OpenXml.Wordprocessing.TableStyle;

namespace FileExplorer.Forms
{
    public partial class FileEditorForm
    {
        /// <summary>
        /// Guarda el archivo actual en disco. El formato de salida se infiere de la extensión
        /// elegida en el diálogo: PDF, DOCX, CSV, JSON, XML, TXT o TSV.
        /// </summary>
        void Save()
        {
            if (_ext == ".pdf")
            {
                string fileName = PickSaveFile(Path.GetFileName(_path), "PDF|*.pdf|Todos|*.*");
                if (fileName == null) return;
                try
                {
                    WritePdfFromText(fileName, rtbEdit.Text);
                    _isDirty = false; Flash("Guardado", C_GREEN); FileSaved?.Invoke(fileName);
                }
                catch (Exception ex) { AppLogger.Error("FileEditorForm.Save PDF", ex); MessageBox.Show("Error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                return;
            }

            if (_ext == ".docx" || _ext == ".doc")
            {
                string fileName = PickSaveFile(Path.GetFileName(_path), "Word|*.docx|Todos|*.*");
                if (fileName == null) return;
                SaveDocxTo(fileName); return;
            }

            string file = PickSaveFile(Path.GetFileName(_path),
                "CSV|*.csv|JSON|*.json|XML|*.xml|TXT|*.txt|TSV|*.tsv|Todos|*.*");
            if (file == null) return;
            try
            {
                string ext = Path.GetExtension(file).ToLowerInvariant().TrimStart('.');
                string content;
                if (ext == "csv") content = BuildCsv(',');
                else if (ext == "tsv") content = BuildCsv('\t');
                else if (ext == "json") content = _headers.Count > 0 ? BuildJson() : rtbEdit.Text;
                else if (ext == "xml") content = _headers.Count > 0 ? BuildXml() : rtbEdit.Text;
                else if (ext == "txt") content = _headers.Count > 0 ? BuildTxt() : rtbEdit.Text;
                else content = rtbEdit.Text;
                File.WriteAllText(file, content, Encoding.UTF8);
                _isDirty = false; Flash("Guardado", C_GREEN); FileSaved?.Invoke(file);
            }
            catch (Exception ex) { AppLogger.Error("FileEditorForm.Save", ex); MessageBox.Show("Error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        /// <summary>
        /// Guarda el contenido del editor como DOCX reemplazando todos los párrafos.
        /// </summary>
        void SaveDocxTo(string target)
        {
            try
            {
                if (!File.Exists(target) && (_ext == ".docx" || _ext == ".doc"))
                    File.Copy(_path, target, true);
                else if (!File.Exists(target))
                { File.WriteAllText(target, rtbEdit.Text, Encoding.UTF8); return; }

                using var doc = WDoc.Open(target, true);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) return;
                body.RemoveAllChildren();
                foreach (var line in rtbEdit.Text.Split('\n'))
                {
                    var para = new WPara(); var run = new WRun();
                    var wt = new WText(line.TrimEnd('\r'))
                    { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve };
                    run.AppendChild(wt); para.AppendChild(run); body.AppendChild(para);
                }
                doc.MainDocumentPart.Document.Save();
                _isDirty = false; Flash("Guardado", C_GREEN); FileSaved?.Invoke(target);
            }
            catch (Exception ex) { AppLogger.Error("FileEditorForm.SaveDocxTo", ex); MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        /// <summary>
        /// Muestra el menú de exportación con todas las opciones disponibles según el tipo de archivo.
        /// Para archivos de datos (CSV, JSON, XML, TXT, etc.) agrega las nuevas opciones:
        /// PDF, Word (.docx) y Excel (.xlsx).
        /// </summary>
        void ExportMenu()
        {
            if (IsExcel) return;
            var menu = new ContextMenuStrip { Font = new DrawFont("Segoe UI", 9.5f) };
            void Add(string t, Action a) { var item = new ToolStripMenuItem(t); item.Click += (s, ev) => a(); menu.Items.Add(item); }

            if (_ext == ".pdf")
            {
                Add("Exportar PDF a Word (.docx)", ExportPdfToWord);
            }
            else if (_ext == ".docx" || _ext == ".doc")
            {
                Add("Exportar Word a PDF (.pdf)", ExportWordToPdf);
            }
            else if (IsDataFile)
            {
                // Formatos de datos planos
                Add("Exportar como CSV", () => ExportAs("csv"));
                Add("Exportar como JSON", () => ExportAs("json"));
                Add("Exportar como XML", () => ExportAs("xml"));
                Add("Exportar como TXT (tabla)", () => ExportAs("txt"));
                Add("Exportar como TSV", () => ExportAs("tsv"));
                menu.Items.Add(new ToolStripSeparator());
                // Conversión a formatos de documento
                Add("Convertir a PDF (.pdf)", ExportDataToPdf);
                Add("Convertir a Word (.docx)", ExportDataToWord);
                Add("Convertir a Excel (.xlsx)", ExportDataToExcel);
            }
            else if (_ext == ".pptx" || _ext == ".ppt")
            {
                Add("Exportar texto como TXT", () => ExportTextAs("txt"));
                Add("Exportar texto como CSV", () => ExportTextAs("csv"));
            }

            if (menu.Items.Count > 0)
                menu.Show(btnExport, new System.Drawing.Point(0, btnExport.Height));
        }

        /// <summary>
        /// Exporta los datos actuales al formato indicado (csv, json, xml, txt, tsv) de forma asíncrona.
        /// </summary>
        void ExportAs(string fmt)
        {
            string fileName = PickSaveFile(
                Path.GetFileNameWithoutExtension(_path) + "_export." + fmt,
                fmt.ToUpper() + "|*." + fmt + "|Todos|*.*");
            if (fileName == null) return;
            Task.Run(() =>
            {
                try
                {
                    string content;
                    if (fmt == "csv") content = BuildCsv(',');
                    else if (fmt == "tsv") content = BuildCsv('\t');
                    else if (fmt == "json") content = BuildJson();
                    else if (fmt == "xml") content = BuildXml();
                    else if (fmt == "txt") content = BuildTxt();
                    else content = BuildCsv(',');
                    File.WriteAllText(fileName, content, Encoding.UTF8);
                    Invoke(new Action(() => Flash("Exportado como " + fmt.ToUpper(), C_GREEN)));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("FileEditorForm.ExportAs", ex);
                    Invoke(new Action(() => MessageBox.Show("Error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        }

        /// <summary>Exporta el texto plano del editor (sin tabla) al formato indicado.</summary>
        void ExportTextAs(string fmt)
        {
            string content = rtbEdit.Text;
            if (string.IsNullOrEmpty(content)) { MessageBox.Show("No hay texto para exportar.", "Info"); return; }
            string fileName = PickSaveFile(
                Path.GetFileNameWithoutExtension(_path) + "_texto." + fmt,
                fmt.ToUpper() + "|*." + fmt + "|Todos|*.*");
            if (fileName == null) return;
            Task.Run(() =>
            {
                try
                {
                    File.WriteAllText(fileName, content, Encoding.UTF8);
                    Invoke(new Action(() => Flash("Exportado como " + fmt.ToUpper(), C_GREEN)));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("FileEditorForm.ExportTextAs", ex);
                    Invoke(new Action(() => MessageBox.Show("Error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        }

        /// <summary>
        /// Convierte la tabla actual (CSV/JSON/XML/TXT/etc.) a PDF.
        /// Escribe cada fila como una línea de texto separada por tabuladores.
        /// </summary>
        void ExportDataToPdf()
        {
            if (_headers.Count == 0) { MessageBox.Show("No hay datos para exportar.", "Sin datos"); return; }
            string fileName = PickSaveFile(
                Path.GetFileNameWithoutExtension(_path) + ".pdf", "PDF|*.pdf|Todos|*.*");
            if (fileName == null) return;

            Task.Run(() =>
            {
                try
                {
                    var sb = new StringBuilder();
                    // Encabezado
                    sb.AppendLine(string.Join("\t", _headers));
                    sb.AppendLine(new string('-', Math.Min(120, _headers.Sum(h => h.Length + 2))));
                    // Filas
                    foreach (var row in _rows)
                        sb.AppendLine(string.Join("\t", _headers.Select((_, i) => i < row.Count ? row[i] : "")));

                    WritePdfFromText(fileName, sb.ToString());
                    Invoke(new Action(() => Flash("Exportado a PDF", C_GREEN)));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("FileEditorForm.ExportDataToPdf", ex);
                    Invoke(new Action(() => MessageBox.Show("Error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        }

        /// <summary>
        /// Convierte la tabla actual a Word (.docx) generando una tabla real con encabezados
        /// en negrita y una fila por cada registro.
        /// </summary>
        void ExportDataToWord()
        {
            if (_headers.Count == 0) { MessageBox.Show("No hay datos para exportar.", "Sin datos"); return; }
            string fileName = PickSaveFile(
                Path.GetFileNameWithoutExtension(_path) + ".docx", "Word|*.docx|Todos|*.*");
            if (fileName == null) return;

            Task.Run(() =>
            {
                try
                {
                    // Crear un DOCX mínimo válido en memoria y guardarlo
                    using var ms = new MemoryStream();
                    using (var tmp = WDoc.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
                    {
                        var mp = tmp.AddMainDocumentPart();
                        mp.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new WBody());
                        mp.Document.Save();
                    }
                    File.WriteAllBytes(fileName, ms.ToArray());

                    using var doc = WDoc.Open(fileName, true);
                    var mainPart = doc.MainDocumentPart!;
                    var body = mainPart.Document.Body!;
                    body.RemoveAllChildren();

                    // Título
                    var titlePara = new WPara();
                    var titleRun = new WRun();
                    titleRun.AppendChild(new WText(Path.GetFileNameWithoutExtension(_path))
                    { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
                    var rpr = new DocumentFormat.OpenXml.Wordprocessing.RunProperties();
                    rpr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                    rpr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "32" });
                    titleRun.PrependChild(rpr);
                    titlePara.AppendChild(titleRun);
                    body.AppendChild(titlePara);

                    // Tabla
                    var tbl = new WTbl();

                    // Fila de encabezados
                    var hdrRow = new WTblRow();
                    foreach (var h in _headers)
                    {
                        var cell = new WTblCell();
                        var para = new WPara();
                        var run = new WRun();
                        var rp2 = new DocumentFormat.OpenXml.Wordprocessing.RunProperties();
                        rp2.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                        run.AppendChild(rp2);
                        run.AppendChild(new WText(h)
                        { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
                        para.AppendChild(run); cell.AppendChild(para);
                        hdrRow.AppendChild(cell);
                    }
                    tbl.AppendChild(hdrRow);

                    // Filas de datos
                    foreach (var row in _rows)
                    {
                        var dataRow = new WTblRow();
                        for (int i = 0; i < _headers.Count; i++)
                        {
                            var cell = new WTblCell();
                            var para = new WPara();
                            var run = new WRun();
                            run.AppendChild(new WText(i < row.Count ? row[i] : "")
                            { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
                            para.AppendChild(run); cell.AppendChild(para);
                            dataRow.AppendChild(cell);
                        }
                        tbl.AppendChild(dataRow);
                    }

                    body.AppendChild(tbl);
                    mainPart.Document.Save();
                    Invoke(new Action(() => Flash($"Exportado a Word ({_rows.Count} filas)", C_GREEN)));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("FileEditorForm.ExportDataToWord", ex);
                    Invoke(new Action(() => MessageBox.Show("Error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        }

        /// <summary>
        /// Convierte la tabla actual a Excel (.xlsx) usando ClosedXML.
        /// La primera fila contiene los encabezados en negrita con fondo azul claro.
        /// </summary>
        void ExportDataToExcel()
        {
            if (_headers.Count == 0) { MessageBox.Show("No hay datos para exportar.", "Sin datos"); return; }
            string fileName = PickSaveFile(
                Path.GetFileNameWithoutExtension(_path) + ".xlsx", "Excel|*.xlsx|Todos|*.*");
            if (fileName == null) return;

            Task.Run(() =>
            {
                try
                {
                    using var wb = new XLWorkbook();
                    var ws = wb.Worksheets.Add("Datos");

                    // Encabezados
                    for (int c = 0; c < _headers.Count; c++)
                    {
                        var cell = ws.Cell(1, c + 1);
                        cell.Value = _headers[c];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 232, 255);
                    }

                    // Datos
                    for (int r = 0; r < _rows.Count; r++)
                        for (int c = 0; c < _headers.Count; c++)
                            ws.Cell(r + 2, c + 1).Value =
                                c < _rows[r].Count ? _rows[r][c] : "";

                    // Ajustar ancho de columnas
                    ws.Columns().AdjustToContents();
                    wb.SaveAs(fileName);
                    Invoke(new Action(() => Flash($"Exportado a Excel ({_rows.Count} filas)", C_GREEN)));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("FileEditorForm.ExportDataToExcel", ex);
                    Invoke(new Action(() => MessageBox.Show("Error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        }

        /// <summary>Convierte un PDF a DOCX extrayendo texto por líneas con PdfPig.</summary>
        void ExportPdfToWord()
        {
            string fileName = PickSaveFile(Path.GetFileNameWithoutExtension(_path) + ".docx", "Word|*.docx|Todos|*.*");
            if (fileName == null) return;
            Task.Run(() =>
            {
                try
                {
                    var sb = new StringBuilder();
                    using (var pdf = PdfDocument.Open(_path))
                    {
                        foreach (var page in pdf.GetPages())
                        {
                            var words = page.GetWords().ToList();
                            var lineMap = new SortedDictionary<int, List<string>>(
                                Comparer<int>.Create((a, b) => b.CompareTo(a)));
                            foreach (var w in words)
                            {
                                int key = (int)Math.Round(w.BoundingBox.Bottom / 4.0) * 4;
                                if (!lineMap.ContainsKey(key)) lineMap[key] = new List<string>();
                                lineMap[key].Add(w.Text);
                            }
                            foreach (var kv in lineMap)
                                sb.AppendLine(string.Join(" ", kv.Value).Trim());
                            sb.AppendLine();
                        }
                    }
                    using var ms2 = new MemoryStream();
                    using (var tmp2 = WDoc.Create(ms2, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
                    {
                        var mp2 = tmp2.AddMainDocumentPart();
                        mp2.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new WBody());
                        mp2.Document.Save();
                    }
                    File.WriteAllBytes(fileName, ms2.ToArray());

                    using var docx = WDoc.Open(fileName, true);
                    var mainPart = docx.MainDocumentPart!;
                    var body = mainPart.Document.Body!;
                    body.RemoveAllChildren();
                    foreach (var line in sb.ToString().Split('\n'))
                    {
                        var para = new WPara(); var run = new WRun();
                        var wt = new WText(line.TrimEnd('\r'))
                        { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve };
                        run.AppendChild(wt); para.AppendChild(run); body.AppendChild(para);
                    }
                    mainPart.Document.Save();
                    Invoke(new Action(() => Flash("Exportado a Word", C_GREEN)));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("FileEditorForm.ExportPdfToWord", ex);
                    Invoke(new Action(() => MessageBox.Show("Error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        }

        /// <summary>Convierte un DOCX a PDF extrayendo texto de todos los párrafos.</summary>
        void ExportWordToPdf()
        {
            string fileName = PickSaveFile(Path.GetFileNameWithoutExtension(_path) + ".pdf", "PDF|*.pdf|Todos|*.*");
            if (fileName == null) return;
            Task.Run(() =>
            {
                try
                {
                    var sb = new StringBuilder();
                    using (var doc = WDoc.Open(_path, false))
                    {
                        var body = doc.MainDocumentPart?.Document?.Body;
                        if (body != null)
                            foreach (var p in body.Descendants<WPara>())
                                sb.AppendLine(p.InnerText);
                    }
                    WritePdfFromText(fileName, sb.ToString());
                    Invoke(new Action(() => Flash("Exportado a PDF", C_GREEN)));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("FileEditorForm.ExportWordToPdf", ex);
                    Invoke(new Action(() => MessageBox.Show("Error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        }

        /// <summary>
        /// Genera un PDF mínimo (PDF 1.4) a partir de texto plano con fuente Helvetica.
        /// No requiere librerías externas.
        /// </summary>
        static void WritePdfFromText(string path, string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var streamSb = new StringBuilder();
            streamSb.AppendLine("BT");
            streamSb.AppendLine("/F1 11 Tf");
            streamSb.AppendLine("50 750 Td");
            streamSb.AppendLine("14 TL");
            foreach (var line in lines)
            {
                string safe = line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
                if (safe.Length > 120) safe = safe[..120] + "...";
                streamSb.AppendLine("(" + safe + ") Tj T*");
            }
            streamSb.AppendLine("ET");
            string streamContent = streamSb.ToString();
            byte[] streamBytes = Encoding.Latin1.GetBytes(streamContent);
            var pdf = new StringBuilder(); pdf.AppendLine("%PDF-1.4");
            var offs = new List<long>();
            offs.Add(pdf.Length); pdf.AppendLine("1 0 obj"); pdf.AppendLine("<< /Type /Catalog /Pages 2 0 R >>"); pdf.AppendLine("endobj");
            offs.Add(pdf.Length); pdf.AppendLine("2 0 obj"); pdf.AppendLine("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"); pdf.AppendLine("endobj");
            offs.Add(pdf.Length); pdf.AppendLine("3 0 obj"); pdf.AppendLine("<< /Type /Page /Parent 2 0 R"); pdf.AppendLine("   /MediaBox [0 0 595 842]"); pdf.AppendLine("   /Contents 4 0 R"); pdf.AppendLine("   /Resources << /Font << /F1 5 0 R >> >> >>"); pdf.AppendLine("endobj");
            offs.Add(pdf.Length); pdf.AppendLine("4 0 obj"); pdf.AppendLine("<< /Length " + streamBytes.Length + " >>"); pdf.AppendLine("stream"); pdf.Append(streamContent); pdf.AppendLine("endstream"); pdf.AppendLine("endobj");
            offs.Add(pdf.Length); pdf.AppendLine("5 0 obj"); pdf.AppendLine("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"); pdf.AppendLine("endobj");
            long xrefOffset = pdf.Length;
            pdf.AppendLine("xref"); pdf.AppendLine("0 6"); pdf.AppendLine("0000000000 65535 f ");
            foreach (var off in offs) pdf.AppendLine(off.ToString("D10") + " 00000 n ");
            pdf.AppendLine("trailer"); pdf.AppendLine("<< /Size 6 /Root 1 0 R >>"); pdf.AppendLine("startxref"); pdf.AppendLine(xrefOffset.ToString()); pdf.AppendLine("%%EOF");
            File.WriteAllText(path, pdf.ToString(), Encoding.Latin1);
        }

        /// <summary>
        /// Abre el formulario de migración a base de datos con los datos actuales.
        /// Al confirmar cambios, actualiza la grilla y marca el archivo como modificado.
        /// </summary>
        void OpenMigration()
        {
            if (_headers.Count == 0)
            { MessageBox.Show("Primero carga un archivo con datos.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var frm = new MigrationForm(_headers, _rows);
            frm.OnRowsChanged += () =>
            {
                dgv.RowCount = _rows.Count; dgv.Invalidate();
                _cellColors.Clear(); _isDirty = true; UpdateInfo();
                Flash("Tabla actualizada desde BD", DrawColor.FromArgb(52, 199, 89));
            };
            frm.ShowDialog(this);
        }
    }
}
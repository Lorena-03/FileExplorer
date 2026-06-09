using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;
using WDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument;
using WPara = DocumentFormat.OpenXml.Wordprocessing.Paragraph;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Exporta datos tabulares a cualquier formato soportado.
    /// Todos los métodos son estáticos y no dependen de la UI.
    /// </summary>
    public static class DataExporter
    {
        // ════════════════════════════════════════════════════════════════
        //  MÉTODO CENTRAL — devuelve el contenido como string o byte[]
        // ════════════════════════════════════════════════════════════════
        public static string BuildContent(
            string format,
            List<string> headers,
            List<List<string>> rows)
        {
            return format.ToLowerInvariant() switch
            {
                "csv" => BuildCsv(headers, rows, ','),
                "tsv" => BuildCsv(headers, rows, '\t'),
                "json" => BuildJson(headers, rows),
                "xml" => BuildXml(headers, rows),
                "txt" => BuildTxt(headers, rows),
                "html" => BuildHtml(headers, rows),
                "md" => BuildMarkdown(headers, rows),
                _ => BuildCsv(headers, rows, ','),
            };
        }

        // ── Guardar en disco con diálogo STA ─────────────────────────────
        public static string PickSaveFile(string defaultName, string format)
        {
            string fileName = null;
            var thread = new Thread(() =>
            {
                using var dlg = new SaveFileDialog
                {
                    Filter = format.ToUpper() + $"|*.{format}|Todos|*.*",
                    FileName = defaultName,
                };
                if (dlg.ShowDialog() == DialogResult.OK) fileName = dlg.FileName;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start(); thread.Join();
            return fileName;
        }

        // ════════════════════════════════════════════════════════════════
        //  EXPORTAR A EXCEL (.xlsx) — binario, devuelve byte[]
        // ════════════════════════════════════════════════════════════════
        public static byte[] BuildExcel(List<string> headers, List<List<string>> rows)
        {
            var wb = new XSSFWorkbook();
            var sheet = wb.CreateSheet("Datos");

            // Estilo cabecera
            var hdrStyle = wb.CreateCellStyle();
            var hdrFont = wb.CreateFont();
            hdrFont.IsBold = true;
            hdrFont.FontName = "Segoe UI";
            hdrStyle.SetFont(hdrFont);
            hdrStyle.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.LightBlue.Index;
            hdrStyle.FillPattern = FillPattern.SolidForeground;

            // Fila de cabeceras
            var hdrRow = sheet.CreateRow(0);
            for (int c = 0; c < headers.Count; c++)
            {
                var cell = hdrRow.CreateCell(c);
                cell.SetCellValue(headers[c]);
                cell.CellStyle = hdrStyle;
            }

            // Filas de datos
            for (int r = 0; r < rows.Count; r++)
            {
                var row = sheet.CreateRow(r + 1);
                for (int c = 0; c < headers.Count; c++)
                {
                    string val = c < rows[r].Count ? rows[r][c] : "";
                    row.CreateCell(c).SetCellValue(val);
                }
            }

            // Autoajustar ancho de columnas (máx 100 chars)
            for (int c = 0; c < headers.Count; c++)
                sheet.AutoSizeColumn(c);

            using var ms = new MemoryStream();
            wb.Write(ms);
            return ms.ToArray();
        }

        // ════════════════════════════════════════════════════════════════
        //  EXTRAER TEXTO DE WORD / PDF (para exportar como datos)
        // ════════════════════════════════════════════════════════════════
        public static string ExtractTextFromWord(string path)
        {
            try
            {
                using var doc = WDoc.Open(path, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) return "";
                var sb = new StringBuilder();
                foreach (var p in body.Descendants<WPara>())
                    sb.AppendLine(p.InnerText);
                return sb.ToString();
            }
            catch { return ""; }
        }

        public static string ExtractTextFromPdf(string path)
        {
            try
            {
                using var pdf = PdfDocument.Open(path);
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    var lineMap = new SortedDictionary<int, List<string>>(
                        Comparer<int>.Create((a, b) => b.CompareTo(a)));
                    foreach (var w in page.GetWords())
                    {
                        int key = (int)Math.Round(w.BoundingBox.Bottom / 4.0) * 4;
                        if (!lineMap.ContainsKey(key)) lineMap[key] = new();
                        lineMap[key].Add(w.Text);
                    }
                    foreach (var kv in lineMap)
                        sb.AppendLine(string.Join(" ", kv.Value).Trim());
                }
                return sb.ToString();
            }
            catch { return ""; }
        }

        // Convierte texto extraído de Word/PDF en tabla de una sola columna
        public static (List<string> headers, List<List<string>> rows)
            TextToTable(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();
            var headers = new List<string> { "Línea", "Contenido" };
            var rows = lines.Select((l, i) =>
                new List<string> { (i + 1).ToString(), l }).ToList();
            return (headers, rows);
        }

        // ════════════════════════════════════════════════════════════════
        //  FORMATOS DE TEXTO
        // ════════════════════════════════════════════════════════════════
        public static string BuildCsv(
            List<string> headers, List<List<string>> rows, char sep)
        {
            var sb = new StringBuilder(rows.Count * 80);
            sb.AppendLine(string.Join(sep, headers.Select(h => Quote(h, sep))));
            foreach (var r in rows)
                sb.AppendLine(string.Join(sep,
                    headers.Select((_, i) => Quote(i < r.Count ? r[i] : "", sep))));
            return sb.ToString();
        }

        public static string BuildJson(List<string> headers, List<List<string>> rows)
        {
            var arr = new JArray(rows.Select(row =>
            {
                var o = new JObject();
                for (int i = 0; i < headers.Count && i < row.Count; i++)
                    o[headers[i]] = row[i];
                return o;
            }));
            return arr.ToString(Formatting.Indented);
        }

        public static string BuildXml(List<string> headers, List<List<string>> rows)
        {
            var root = new XElement("data",
                rows.Select(row =>
                    new XElement("row",
                        headers.Select((h, i) =>
                            new XElement(
                                Regex.Replace(h, @"[^\w]", "_"),
                                i < row.Count ? row[i] : "")))));
            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"), root).ToString();
        }

        public static string BuildTxt(List<string> headers, List<List<string>> rows)
        {
            var widths = headers.Select((h, i) =>
                Math.Max(h.Length,
                    rows.Select(r => i < r.Count ? r[i].Length : 0)
                        .DefaultIfEmpty(0).Max())).ToList();

            string Line(IEnumerable<string> cells) =>
                "│ " + string.Join(" │ ",
                    cells.Select((c, i) => c.PadRight(widths[i]))) + " │";
            string Div() =>
                "├─" + string.Join("─┼─",
                    widths.Select(w => new string('─', w))) + "─┤";

            var sb = new StringBuilder();
            sb.AppendLine("┌─" + string.Join("─┬─",
                widths.Select(w => new string('─', w))) + "─┐");
            sb.AppendLine(Line(headers));
            sb.AppendLine(Div());
            foreach (var r in rows)
                sb.AppendLine(Line(headers.Select((_, i) =>
                    i < r.Count ? r[i] : "")));
            sb.AppendLine("└─" + string.Join("─┴─",
                widths.Select(w => new string('─', w))) + "─┘");
            return sb.ToString();
        }

        public static string BuildHtml(List<string> headers, List<List<string>> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.AppendLine("<style>table{border-collapse:collapse;font-family:Segoe UI,sans-serif;}");
            sb.AppendLine("th{background:#0a84ff;color:#fff;padding:8px 12px;}");
            sb.AppendLine("td{border:1px solid #ddd;padding:6px 12px;}");
            sb.AppendLine("tr:nth-child(even){background:#f5f5fa;}</style></head><body>");
            sb.AppendLine("<table><thead><tr>");
            foreach (var h in headers)
                sb.AppendLine($"<th>{Escape(h)}</th>");
            sb.AppendLine("</tr></thead><tbody>");
            foreach (var row in rows)
            {
                sb.AppendLine("<tr>");
                foreach (var h in headers.Select((_, i) => i))
                    sb.AppendLine($"<td>{Escape(h < row.Count ? row[h] : "")}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table></body></html>");
            return sb.ToString();
        }

        public static string BuildMarkdown(List<string> headers, List<List<string>> rows)
        {
            var widths = headers.Select((h, i) =>
                Math.Max(h.Length,
                    rows.Select(r => i < r.Count ? r[i].Length : 0)
                        .DefaultIfEmpty(0).Max())).ToList();

            string Row(IEnumerable<string> cells) =>
                "| " + string.Join(" | ",
                    cells.Select((c, i) => c.PadRight(widths[i]))) + " |";

            var sb = new StringBuilder();
            sb.AppendLine(Row(headers));
            sb.AppendLine("| " + string.Join(" | ",
                widths.Select(w => new string('-', w))) + " |");
            foreach (var r in rows)
                sb.AppendLine(Row(headers.Select((_, i) =>
                    i < r.Count ? r[i] : "")));
            return sb.ToString();
        }

        // ── Helpers ───────────────────────────────────────────────────────
        static string Quote(string v, char sep) =>
            v.Contains(sep) || v.Contains('"') || v.Contains('\n')
                ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;

        static string Escape(string v) =>
            v.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        // ── Filtro disponible para el SaveFileDialog ──────────────────────
        public static string AllExportFilter =>
            "CSV|*.csv|JSON|*.json|XML|*.xml|TXT (tabla)|*.txt|" +
            "TSV (tabuladores)|*.tsv|Excel|*.xlsx|" +
            "HTML|*.html|Markdown|*.md|Todos|*.*";
    }
}
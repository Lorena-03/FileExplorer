using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Parsea archivos de datos — lee valores CRUDOS, sin modificar nada.
    /// </summary>
    public static class DataParser
    {
        // ════════════════════════════════════════════════════════════
        //  DETECTAR SEPARADOR
        // ════════════════════════════════════════════════════════════
        public static char DetectSeparator(string text)
        {
            // Analizar las primeras 10 líneas para mayor precisión
            var lines = text.Split('\n').Take(10).ToArray();
            int tabs = 0, commas = 0, semis = 0, pipes = 0;
            foreach (var line in lines)
            {
                bool inQ = false;
                foreach (char c in line)
                {
                    if (c == '"') inQ = !inQ;
                    if (inQ) continue;
                    if (c == '\t') tabs++;
                    else if (c == ',') commas++;
                    else if (c == ';') semis++;
                    else if (c == '|') pipes++;
                }
            }
            int max = Math.Max(Math.Max(tabs, commas), Math.Max(semis, pipes));
            if (max == 0) return ',';
            if (tabs == max) return '\t';
            if (semis == max) return ';';
            if (pipes == max) return '|';
            return ',';
        }

        // ════════════════════════════════════════════════════════════
        //  CSV — lee valores RAW
        // ════════════════════════════════════════════════════════════
        public static Tuple<List<string>, List<List<string>>>
            ParseCsv(string text, char sep)
        {
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            var lines = new List<string>();
            foreach (var l in text.Split('\n'))
            {
                string lt = l.TrimEnd();
                if (!string.IsNullOrEmpty(lt)) lines.Add(lt);
            }

            if (lines.Count == 0)
                return Tuple.Create(new List<string>(), new List<List<string>>());

            // CSV estándar: primera fila = cabeceras SIEMPRE
            var headers = SplitLine(lines[0], sep);
            int colCount = headers.Count;
            var rows = new List<List<string>>(lines.Count - 1);

            for (int i = 1; i < lines.Count; i++)
            {
                var cells = SplitLine(lines[i], sep);
                while (cells.Count < colCount) cells.Add("");
                if (cells.Count > colCount) cells = cells.Take(colCount).ToList();
                rows.Add(cells);
            }
            return Tuple.Create(headers, rows);
        }

        // ════════════════════════════════════════════════════════════
        //  JSON — lee valores RAW
        // ════════════════════════════════════════════════════════════
        public static Tuple<List<string>, List<List<string>>>
            ParseJson(string raw)
        {
            try
            {
                var tok = JToken.Parse(raw);
                if (tok is JArray arr && arr.Count > 0 && arr[0] is JObject first)
                {
                    // JSON estándar: las llaves del primer objeto SON las cabeceras
                    var hdrs = first.Properties().Select(p => p.Name).ToList();
                    var rws = arr.OfType<JObject>()
                        .Select(o => hdrs
                            .Select(h => o[h] != null ? o[h].ToString() : "")
                            .ToList())
                        .ToList();
                    return Tuple.Create(hdrs, rws);
                }
                if (tok is JObject obj)
                {
                    var hdrs = new List<string> { "Clave", "Valor" };
                    var rws = obj.Properties()
                        .Select(p => new List<string>
                            { p.Name, p.Value != null ? p.Value.ToString() : "" })
                        .ToList();
                    return Tuple.Create(hdrs, rws);
                }
            }
            catch { }
            return Tuple.Create(new List<string>(), new List<List<string>>());
        }

        // ════════════════════════════════════════════════════════════
        //  XML — lee valores RAW
        // ════════════════════════════════════════════════════════════
        public static Tuple<List<string>, List<List<string>>>
            ParseXml(string raw)
        {
            try
            {
                var doc = XDocument.Parse(raw);
                var recs = doc.Root?.Elements().ToList();
                if (recs != null && recs.Count > 0)
                {
                    // XML estándar: nombres de elemento SON las cabeceras
                    var hdrs = recs[0].Elements()
                        .Select(e => e.Name.LocalName).ToList();
                    var rws = recs
                        .Select(r => hdrs
                            .Select(h => r.Element(h) != null ? r.Element(h).Value : "")
                            .ToList())
                        .ToList();
                    return Tuple.Create(hdrs, rws);
                }
            }
            catch { }
            return Tuple.Create(new List<string>(), new List<List<string>>());
        }

        // ════════════════════════════════════════════════════════════
        //  EXCEL — lee valores RAW como string
        // ════════════════════════════════════════════════════════════
        public static Tuple<List<string>, List<List<string>>>
            ParseExcel(string path, string ext)
        {
            using (var fs = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite))
            {
                NPOI.SS.UserModel.IWorkbook wb = ext == ".xlsx"
                    ? (NPOI.SS.UserModel.IWorkbook)new XSSFWorkbook(fs)
                    : new HSSFWorkbook(fs);

                var sheet = wb.GetSheetAt(0);
                if (sheet == null)
                    return Tuple.Create(new List<string>(), new List<List<string>>());

                var hdrs = new List<string>();
                var rws = new List<List<string>>();

                for (int r = 0; r <= sheet.LastRowNum; r++)
                {
                    var row = sheet.GetRow(r);
                    if (row == null) continue;

                    var cells = new List<string>();
                    for (int c = 0; c < row.LastCellNum; c++)
                    {
                        var cell = row.GetCell(c);
                        // Leer SIEMPRE como string crudo
                        cells.Add(cell != null ? cell.ToString().Trim() : "");
                    }

                    if (r == 0) hdrs = cells;
                    else rws.Add(cells);
                }
                return Tuple.Create(hdrs, rws);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  TXT — detectar tabla, leer cabeceras o inferirlas
        // ════════════════════════════════════════════════════════════
        public static bool TryParseTxt(string[] rawLines,
            out List<string> headers, out List<List<string>> rows)
        {
            headers = new List<string>();
            rows = new List<List<string>>();

            // Limpiar \r y líneas vacías o separadores de tabla ASCII
            var lines = rawLines
                .Select(l => l.TrimEnd('\r', ' '))
                .Where(l => !string.IsNullOrWhiteSpace(l)
                         && !l.TrimStart().StartsWith("─")
                         && !l.TrimStart().StartsWith("=")
                         && !Regex.IsMatch(l.Trim(), @"^[\-\+\|=─]+$"))
                .ToArray();

            if (lines.Length < 2) return false;

            // Detectar separador en el archivo completo
            char sep = DetectSeparatorInLines(lines);
            if (sep == '\0') return false;

            var firstCells = SplitLine(lines[0], sep);
            if (firstCells.Count < 2) return false;

            int colCount = firstCells.Count;

            // Verificar que la mayoría de líneas tienen el mismo número de columnas
            int consistent = lines.Count(l => SplitLine(l, sep).Count == colCount);
            if ((double)consistent / lines.Length < 0.5) return false;

            // ── Determinar si la primera fila es cabecera o datos ────
            bool isDataRow = IsDataRow(firstCells);

            if (!isDataRow)
            {
                // Primera línea = cabeceras reales del archivo
                headers = firstCells;
                rows = lines.Skip(1)
                    .Select(l =>
                    {
                        var cells = SplitLine(l, sep);
                        while (cells.Count < colCount) cells.Add("");
                        if (cells.Count > colCount)
                            cells = cells.Take(colCount).ToList();
                        return cells;
                    }).ToList();
            }
            else
            {
                // Sin cabeceras — inferir nombres por contenido
                var allRows = lines.Select(l =>
                {
                    var cells = SplitLine(l, sep);
                    while (cells.Count < colCount) cells.Add("");
                    if (cells.Count > colCount)
                        cells = cells.Take(colCount).ToList();
                    return cells;
                }).ToList();
                rows = allRows;
                headers = InferColumnNames(allRows, colCount);
            }

            return true;
        }

        // ── Detectar si una fila contiene DATOS reales (solo para TXT sin cabecera)
        static bool IsDataRow(List<string> cells)
        {
            foreach (var v in cells)
            {
                string vt = v.Trim();
                if (string.IsNullOrEmpty(vt)) continue;
                // ID de registro (ID001, EMP123)
                if (Regex.IsMatch(vt, @"^[A-Za-z]{1,6}\d{3,}$")) return true;
                // Fecha con separadores
                if (Regex.IsMatch(vt, @"^\d{1,4}[-/\\.]\d{1,4}[-/\\.]\d{2,4}$")) return true;
                // Teléfono (10+ dígitos)
                string d = Regex.Replace(vt, @"\D", "");
                if (d.Length >= 10 && d.Length <= 20) return true;
                // Email con @
                if (vt.Contains('@')) return true;
                // Email sin @ con dominio
                string vl = vt.ToLower();
                if (vl.Contains('.') && (vl.EndsWith(".com") || vl.EndsWith(".mx") ||
                    vl.EndsWith(".net") || vl.Contains("gmail") ||
                    vl.Contains("hotmail") || vl.Contains("yahoo") ||
                    vl.Contains("outlook"))) return true;
            }
            return false;
        }

        // ── Detectar separador analizando múltiples líneas ────────────
        static char DetectSeparatorInLines(string[] lines)
        {
            var sample = lines.Take(Math.Min(lines.Length, 15));
            int tabs = 0, commas = 0, semis = 0, pipes = 0;
            foreach (var line in sample)
            {
                bool inQ = false;
                foreach (char c in line)
                {
                    if (c == '"') inQ = !inQ;
                    if (inQ) continue;
                    if (c == '\t') tabs++;
                    else if (c == ',') commas++;
                    else if (c == ';') semis++;
                    else if (c == '|') pipes++;
                }
            }
            int max = Math.Max(Math.Max(tabs, commas), Math.Max(semis, pipes));
            if (max == 0) return '\0';
            if (tabs == max) return '\t';
            if (semis == max) return ';';
            if (pipes == max) return '|';
            if (commas > 0) return ',';
            return '\0';
        }

        // ── Detectar si una celda parece nombre de columna ────────────
        static bool IsFieldName(string v)
        {
            v = v.Trim();
            if (string.IsNullOrEmpty(v) || v.Length > 50) return false;

            // No empieza con dígito
            if (char.IsDigit(v[0])) return false;

            // No contiene @ (sería un email)
            if (v.Contains('@')) return false;

            // No es una fecha con separadores
            if (Regex.IsMatch(v, @"^\d{1,4}[-/\.]\d{1,2}[-/\.]\d{2,4}$")) return false;

            // No es un ID tipo "ID001", "EMP123"
            if (Regex.IsMatch(v, @"^[A-Za-z]{1,5}\d{2,}$")) return false;

            // No es solo dígitos
            if (Regex.IsMatch(v, @"^\d+$")) return false;

            // No empieza con + (sería teléfono)
            if (v.StartsWith("+")) return false;

            // Rechazar emails sin @ — detectar por patrón "algo.dominio.ext"
            // Ej: juan.gmail.com, mariahotmail.com, carlos.outlook.com
            string[] commonTlds = { ".com", ".mx", ".net", ".org", ".edu",
                                    ".gov", ".io", ".co", ".es", ".info" };
            string vLow = v.ToLower();
            if (commonTlds.Any(tld => vLow.EndsWith(tld))) return false;

            // Rechazar si contiene punto en medio seguido de texto (dominio)
            // Ej: "juan.gmail", "carlos.outlook"
            if (Regex.IsMatch(v, @"^[a-zA-Z0-9_\-]+\.[a-zA-Z]{2,}")) return false;

            // Acepta letras (incluyendo acentos), dígitos, espacios, guiones, _
            // NO acepta puntos (los puntos en nombres de campo son raros)
            return Regex.IsMatch(v, @"^[\w\s\-áéíóúÁÉÍÓÚñÑüÜ]+$");
        }

        // ════════════════════════════════════════════════════════════
        //  INFERIR NOMBRES DE COLUMNA — solo cuando NO hay cabecera
        // ════════════════════════════════════════════════════════════
        public static List<string> InferColumnNames(
            List<List<string>> rows, int colCount)
        {
            var names = new List<string>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int c = 0; c < colCount; c++)
            {
                var sample = rows
                    .Select(r => c < r.Count ? r[c].Trim() : "")
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Take(20).ToList();

                string name = GuessColumnName(sample, c);

                // Evitar duplicados
                if (usedNames.Contains(name))
                {
                    int n = 2;
                    string cand = name + n;
                    while (usedNames.Contains(cand)) cand = name + (++n);
                    name = cand;
                }
                usedNames.Add(name);
                names.Add(name);
            }
            return names;
        }

        static string GuessColumnName(List<string> sample, int idx)
        {
            if (sample.Count == 0) return "Columna " + (idx + 1);
            int n = sample.Count;

            // ── 1. Correo — con @ válido ──────────────────────────────
            if (sample.Count(v => v.Contains('@') &&
                Regex.IsMatch(v, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) >= n * 0.3)
                return "Correo";

            // ── 2. Correo — dañado (sin @, pero con dominio reconocible) ─
            if (sample.Count(v =>
            {
                string vl = v.ToLower();
                return vl.Contains("gmail") || vl.Contains("hotmail") ||
                       vl.Contains("yahoo") || vl.Contains("outlook") ||
                       vl.Contains("icloud") || vl.Contains("proton") ||
                       (vl.Contains('.') && (vl.EndsWith(".com") || vl.EndsWith(".mx") ||
                        vl.EndsWith(".net") || vl.EndsWith(".org") || vl.EndsWith(".edu")));
            }) >= n * 0.3)
                return "Correo";

            // ── 3. Fecha — cualquier formato con 3 grupos numéricos ───
            if (sample.Count(v =>
            {
                string vt = v.Trim();
                // Con separadores -, /, ., \
                if (Regex.IsMatch(vt, @"^\d{1,4}[-/\\.]\d{1,4}[-/\\.]\d{2,4}$")) return true;
                // Compacto 8 dígitos (yyyymmdd o ddmmyyyy)
                if (Regex.IsMatch(vt, @"^\d{8}$")) return true;
                return false;
            }) >= n * 0.3)
                return "FechaNacimiento";

            // ── 4. Teléfono — 10 a 20 dígitos (incluye números con errores) ─
            // Detecta: 10 dígitos exactos, con código país (12-13), o con errores (14-20)
            if (sample.Count(v =>
            {
                string d = Regex.Replace(v, @"\D", "");
                return d.Length >= 10 && d.Length <= 20;
            }) >= n * 0.3)
                return "Telefono";

            // ── 5. Código postal — exactamente 5 dígitos ─────────────
            if (sample.Count(v => Regex.IsMatch(v.Trim(), @"^\d{5}$")) >= n * 0.4)
                return "CodigoPostal";

            // ── 6. ID de registro — letras + dígitos (ID001, EMP123) ──
            if (sample.Count(v => Regex.IsMatch(v.Trim(), @"^[A-Za-z]{1,6}\d{2,}$")) >= n * 0.4)
                return "ID";

            // ── 7. Nombre propio — 2+ palabras, mayúscula, sin dígitos ─
            if (sample.Count(v => v.Contains(' ') && v.Length >= 4 &&
                char.IsUpper(v[0]) && !Regex.IsMatch(v, @"\d") &&
                !v.Contains('.') && !v.Contains('@')) >= n * 0.3)
                return "Nombre";

            // ── 8. Ciudad — palabra sola con mayúscula, sin dígitos ───
            if (sample.Count(v => !v.Contains(' ') && char.IsUpper(v[0]) &&
                !Regex.IsMatch(v, @"\d") && v.Length >= 3 && !v.Contains('.')) >= n * 0.4)
            {
                var cities = new[]
                {
                    "cdmx","df","monterrey","guadalajara","puebla","tijuana",
                    "juarez","cancun","leon","zapopan","merida","hermosillo",
                    "chihuahua","culiacan","saltillo","mexicali","aguascalientes",
                    "morelia","toluca","queretaro","acapulco","veracruz","monclova"
                };
                return sample.Any(v => cities.Any(ct => v.ToLower().Contains(ct)))
                    ? "Ciudad" : "Categoria";
            }

            return "Columna " + (idx + 1);
        }

        // ════════════════════════════════════════════════════════════
        //  SPLITTER CSV — respeta comillas, limpia \r
        // ════════════════════════════════════════════════════════════
        public static List<string> SplitLine(string line, char sep)
        {
            var result = new List<string>();
            bool inQuote = false;
            var current = new StringBuilder(64);

            // Quitar \r al final si lo hay
            if (line.Length > 0 && line[line.Length - 1] == '\r')
                line = line.Substring(0, line.Length - 1);

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // Comilla doble escapada dentro de campo ("" → ")
                    if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // saltar la siguiente comilla
                    }
                    else
                    {
                        inQuote = !inQuote;
                    }
                }
                else if (c == sep && !inQuote)
                {
                    result.Add(current.ToString().Trim('"').Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString().Trim('"').Trim());
            return result;
        }

        // ════════════════════════════════════════════════════════════
        //  DETECTAR ENCODING
        // ════════════════════════════════════════════════════════════
        public static Encoding DetectEncoding(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            // BOM UTF-8
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
                return new UTF8Encoding(true);
            // BOM UTF-16 LE
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
                return Encoding.Unicode;
            // BOM UTF-16 BE
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            // Default: UTF-8 sin BOM
            return Encoding.UTF8;
        }
    }
}
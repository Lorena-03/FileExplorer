using FileExplorer.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace FileExplorer.Helpers
{
    public static class DataValidator
    {
        static readonly Regex RxEmail = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
        static readonly Regex RxPostal = new(@"^\d{5}$");
        static readonly Regex RxDateIso = new(@"^\d{4}-\d{2}-\d{2}$");

        static readonly string[] DateCols = { "fecha", "date", "nacimiento", "birth", "dob", "cumple", "birthday", "fecnac", "fnac", "alta", "registro" };
        static readonly string[] EmailCols = { "email", "correo", "mail" };
        static readonly string[] PostalCols = { "cp", "postal", "codigopostal", "zip", "codigo_postal" };
        static readonly string[] PhoneCols = { "telefono", "phone", "tel", "celular", "movil", "mobile", "fono" };

        static bool IsDate(string h) => DateCols.Any(k => h.Contains(k));
        static bool IsEmail(string h) => EmailCols.Any(k => h.Contains(k));
        static bool IsPostal(string h) => PostalCols.Any(k => h.Contains(k));
        static bool IsPhone(string h) => PhoneCols.Any(k => h.Contains(k));

        // ════════════════════════════════════════════════════════════
        //  DETECCIÓN PARALELA
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Recorre todas las filas y columnas en paralelo para detectar errores de validación.
        /// Verifica espacios extra, campos vacíos, teléfonos, emails, códigos postales y fechas.
        /// Devuelve la lista ordenada por fila y columna.
        /// </summary>
        public static List<ValidationError> Validate(
            List<string> headers, List<List<string>> rows)
        {
            var norm = headers
                .Select(h => h.ToLowerInvariant().Trim()
                              .Replace(" ", "").Replace("_", "").Replace("-", ""))
                .ToList();

            var bag = new ConcurrentBag<ValidationError>();

            Parallel.For(0, rows.Count, rowIdx =>
            {
                var row = rows[rowIdx];
                for (int c = 0; c < headers.Count; c++)
                {
                    string val = c < row.Count ? (row[c] ?? "").Trim() : "";
                    string nh = c < norm.Count ? norm[c] : "";

                    // 1. Espacios extra
                    string rawVal = c < row.Count ? (row[c] ?? "") : "";
                    if (rawVal != rawVal.Trim() || rawVal.Contains("  "))
                    {
                        string fix = Regex.Replace(rawVal.Trim(), @"\s{2,}", " ");
                        bag.Add(Mk(rowIdx, c, headers[c], val,
                            ValidationErrorType.EmptyField,
                            "Espacios en blanco extra", fix));
                    }

                    // 2. Campo vacío
                    if (string.IsNullOrWhiteSpace(val))
                    {
                        bag.Add(Mk(rowIdx, c, headers[c], val,
                            ValidationErrorType.EmptyField,
                            $"Campo '{headers[c]}' vacío en fila {rowIdx + 2}", ""));
                        continue;
                    }

                    // 3. Teléfono
                    if (IsPhone(nh))
                    {
                        string d = Regex.Replace(val, @"[\s\-\(\)\+\.\D]", "");

                        if (d.Length == 10)
                        {
                            // Válido — solo reportar si tenía caracteres extra
                            if (val != d)
                                bag.Add(Mk(rowIdx, c, headers[c], val,
                                    ValidationErrorType.InvalidPhone,
                                    "Teléfono con caracteres extra (se limpiarán)", d));
                        }
                        else if (d.Length == 12 && d.StartsWith("52"))
                        {
                            // Válido con código de país México
                        }
                        else if (d.Length == 13 && d.StartsWith("521"))
                        {
                            bag.Add(Mk(rowIdx, c, headers[c], val,
                                ValidationErrorType.InvalidPhone,
                                "Teléfono: dígito extra en código de país (521 → 52)",
                                "52" + d.Substring(3)));
                        }
                        else if (d.Length > 12)
                        {
                            bag.Add(Mk(rowIdx, c, headers[c], val,
                                ValidationErrorType.InvalidPhone,
                                $"Teléfono: demasiados dígitos ({d.Length}), se recorta a 10",
                                d.Substring(d.Length - 10)));
                        }
                        else
                        {
                            bag.Add(Mk(rowIdx, c, headers[c], val,
                                ValidationErrorType.InvalidPhone,
                                $"Teléfono: debe tener 10 dígitos (tiene {d.Length})", ""));
                        }
                    }

                    // 4. Email
                    if (IsEmail(nh))
                    {
                        if (!val.Contains('@'))
                        {
                            string fix = TryFixEmail(val);
                            bag.Add(Mk(rowIdx, c, headers[c], val,
                                ValidationErrorType.InvalidEmail,
                                fix != null ? "Email sin '@' (auto-corrección disponible)" : "Email sin '@'",
                                fix ?? ""));
                        }
                        else if (!RxEmail.IsMatch(val))
                        {
                            bag.Add(Mk(rowIdx, c, headers[c], val,
                                ValidationErrorType.InvalidEmail,
                                $"Email '{val}' con formato inválido", FixEmail(val)));
                        }
                    }

                    // 5. Código postal
                    if (IsPostal(nh) && !RxPostal.IsMatch(val))
                    {
                        var d = Regex.Replace(val, @"\D", "");
                        if (d.Length > 5) d = d[..5];
                        while (d.Length < 5) d = "0" + d;
                        bag.Add(Mk(rowIdx, c, headers[c], val,
                            ValidationErrorType.InvalidPostalCode,
                            $"C.P. '{val}' debe tener 5 dígitos", d));
                    }

                    // 6. Fecha
                    if (IsDate(nh))
                    {
                        string normalized = NormalizeDate(val);
                        if (!RxDateIso.IsMatch(normalized))
                        {
                            string fix = TryConvertDate(val);
                            bag.Add(Mk(rowIdx, c, headers[c], val,
                                ValidationErrorType.InvalidDate,
                                fix != null ? "Fecha: formato incorrecto (auto-corrección disponible)"
                                            : "Fecha debe ser AAAA-MM-DD",
                                fix ?? ""));
                        }
                        else if (normalized != val)
                        {
                            bag.Add(Mk(rowIdx, c, headers[c], val,
                                ValidationErrorType.InvalidDate,
                                "Fecha: separador incorrecto (usar -)", normalized));
                        }
                    }
                }
            });

            return bag.OrderBy(e => e.Row).ThenBy(e => e.Column).ToList();
        }

        // ════════════════════════════════════════════════════════════
        //  AUTO-FIX
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Aplica correcciones automáticas a las filas del CSV basándose en los errores
        /// que tienen sugerencia disponible. Devuelve nueva matriz con valores corregidos.
        /// </summary>
        public static List<List<string>> AutoFix(
            List<string> headers,
            List<List<string>> rows,
            List<ValidationError> errors)
        {
            var result = rows.Select(r => r.ToList()).ToList();
            foreach (var e in errors.Where(e => e.CanAutoFix))
            {
                if (e.Row >= result.Count) continue;
                while (result[e.Row].Count <= e.Column) result[e.Row].Add("");
                result[e.Row][e.Column] = e.Suggestion;
            }
            return result;
        }

        // ════════════════════════════════════════════════════════════
        //  FECHAS
        // ════════════════════════════════════════════════════════════

        /// <summary>Normaliza separadores de fecha a guión (-, / y \ a -).</summary>
        public static string NormalizeDate(string v) =>
            Regex.Replace(v.Trim(), @"[\/\\\.]", "-");

        /// <summary>
        /// Intenta convertir una fecha en distintos formatos comunes al formato ISO 8601 (YYYY-MM-DD).
        /// Soporta múltiples culturas (es-MX, es-ES, invariante) y formatos con abreviatura de mes.
        /// </summary>
        public static string TryConvertDate(string v)
        {
            v = NormalizeDate(v);
            if (string.IsNullOrEmpty(v)) return null;
            if (Regex.IsMatch(v, @"^\d{4}-\d{2}-\d{2}$")) return v;

            string[] fmts = {
                "yyyy-MM-dd","yyyy-M-d","yyyyMMdd",
                "d-M-yyyy","dd-MM-yyyy","d-MM-yyyy","dd-M-yyyy",
                "M-d-yyyy","MM-dd-yyyy",
                "dd-MM-yy","d-M-yy","MM-dd-yy",
                "d-MMM-yyyy","dd-MMM-yyyy","MMM-d-yyyy","MMM-dd-yyyy"
            };
            var cultures = new[] {
                System.Globalization.CultureInfo.InvariantCulture,
                new System.Globalization.CultureInfo("es-MX"),
                new System.Globalization.CultureInfo("es-ES"),
            };

            foreach (var fmt in fmts)
                foreach (var cult in cultures)
                    if (DateTime.TryParseExact(v, fmt, cult,
                        System.Globalization.DateTimeStyles.None, out DateTime dt))
                        if (dt.Year >= 1900 && dt.Year <= DateTime.Now.Year)
                            return dt.ToString("yyyy-MM-dd");

            if (DateTime.TryParse(v,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime d2))
                if (d2.Year >= 1900 && d2.Year <= DateTime.Now.Year)
                    return d2.ToString("yyyy-MM-dd");

            return null;
        }

        // ════════════════════════════════════════════════════════════
        //  EMAILS
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Intenta corregir un email sin '@' buscando dominios conocidos dentro del texto.
        /// Soporta dominios de México, Gmail, Outlook, Yahoo, iCloud y dominios educativos/gubernamentales.
        /// </summary>
        public static string TryFixEmail(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            val = val.Trim().ToLower();

            string[] domains = {
                "outlook.com","hotmail.com","hotmail.es","hotmail.mx",
                "gmail.com","yahoo.com","yahoo.es","yahoo.com.mx",
                "live.com","live.com.mx","protonmail.com",
                "icloud.com","me.com","msn.com",
                "unam.mx","ipn.mx","tec.mx","itesm.mx",
                "gob.mx","edu.mx","com.mx"
            };

            foreach (var domain in domains)
            {
                int idx = val.IndexOf(domain, StringComparison.OrdinalIgnoreCase);
                if (idx > 0)
                {
                    string local = val.Substring(0, idx);
                    string dPart = val.Substring(idx);
                    if (local.Length >= 2 && !local.Contains('@'))
                        return local + "@" + dPart;
                }
            }
            return null;
        }

        /// <summary>
        /// Aplica heurísticas simples para corregir un email malformado:
        /// sustituye el primer punto por '@' si no hay '@', o agrega ".com" si falta dominio.
        /// </summary>
        static string FixEmail(string v)
        {
            string fix = TryFixEmail(v);
            if (fix != null) return fix;
            if (!v.Contains('@') && v.Contains('.'))
            { int i = v.IndexOf('.'); return v[..i] + "@" + v[(i + 1)..]; }
            if (v.Contains('@') && !v.Contains('.')) return v + ".com";
            return v;
        }

        // ════════════════════════════════════════════════════════════
        //  CSV
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Parsea una línea CSV respetando comillas dobles como delimitador de escape.
        /// Maneja celdas con comas internas y comillas escapadas ("").
        /// </summary>
        public static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQ = false;
            var cell = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQ && i + 1 < line.Length && line[i + 1] == '"')
                    { cell.Append('"'); i++; }
                    else inQ = !inQ;
                }
                else if (c == ',' && !inQ)
                { result.Add(cell.ToString()); cell.Clear(); }
                else cell.Append(c);
            }
            result.Add(cell.ToString());
            return result;
        }

        /// <summary>
        /// Serializa valores a una línea CSV válida, entrecomillando campos con comas,
        /// comillas dobles o saltos de línea, y escapando comillas internas con "".
        /// </summary>
        public static string ToCsvLine(IEnumerable<string> fields) =>
            string.Join(",", fields.Select(f =>
                f.Contains(',') || f.Contains('"') || f.Contains('\n')
                    ? "\"" + f.Replace("\"", "\"\"") + "\""
                    : f));

        // ── Helper interno ────────────────────────────────────────────

        /// <summary>Crea un ValidationError con todos sus campos.</summary>
        static ValidationError Mk(int row, int col, string colName, string val,
            ValidationErrorType type, string desc, string suggestion) =>
            new ValidationError
            {
                Row = row,
                Column = col,
                FieldName = colName,
                Value = val,
                ErrorType = type,
                Description = desc,
                Suggestion = suggestion,
            };
    }
}
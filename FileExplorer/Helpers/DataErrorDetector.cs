using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FileExplorer.Forms
{
    // ════════════════════════════════════════════════════════════════
    //  MODELO DE ERROR — una sola definición aquí
    // ════════════════════════════════════════════════════════════════
    public class DataError
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public string ColumnName { get; set; } = "";
        public string Value { get; set; } = "";
        public string Description { get; set; } = "";
        public string AutoFix { get; set; } = "";
        public bool Fixed { get; set; } = false;
    }

    // ════════════════════════════════════════════════════════════════
    //  DETECTOR DE ERRORES
    // ════════════════════════════════════════════════════════════════
    public static class ErrorDetector
    {
        static readonly string[] PhoneKeys = { "tel", "phone", "celular", "movil", "mobile", "fono", "telefono" };
        static readonly string[] EmailKeys = { "email", "correo", "mail" };
        static readonly string[] PostalKeys = { "cp", "postal", "codigopostal", "zip", "codigo_postal" };
        static readonly string[] DateKeys = { "fecha", "date", "nacimiento", "birth", "dob", "cumple", "birthday", "fecnac", "fnac" };


        static bool IsPhone(string h) => PhoneKeys.Any(k => h.Contains(k));
        static bool IsEmail(string h) => EmailKeys.Any(k => h.Contains(k));
        static bool IsPostal(string h) => PostalKeys.Any(k => h.Contains(k));
        static bool IsDate(string h) => DateKeys.Any(k => h.Contains(k));

        // ════════════════════════════════════════════════════════════
        //  DETECCIÓN PARALELA
        // ════════════════════════════════════════════════════════════
        public static List<DataError> DetectAll(
            List<string> headers,
            List<List<string>> rows)
        {
            var norm = headers
                .Select(h => h.ToLowerInvariant().Trim()
                              .Replace(" ", "").Replace("_", "").Replace("-", ""))
                .ToList();

            var bag = new ConcurrentBag<DataError>();

            Parallel.For(0, rows.Count, rowIdx =>
            {
                var row = rows[rowIdx];

                for (int c = 0; c < headers.Count; c++)
                {
                    string val = c < row.Count ? (row[c] ?? "").Trim() : "";
                    string nh = c < norm.Count ? norm[c] : "";

                    // 1. Espacios extra
                    if (val != val.Trim() || val.Contains("  "))
                        bag.Add(Mk(rowIdx, c, headers[c], "Espacios en blanco extra", val,
                            Regex.Replace(val.Trim(), @"\s{2,}", " ")));

                    // 2. Campo vacío
                    if (string.IsNullOrWhiteSpace(val))
                    {
                        bag.Add(Mk(rowIdx, c, headers[c], "Campo vacío", val, ""));
                        continue;
                    }

                    // 3. Teléfono
                    //    VÁLIDO:   10 dígitos exactos  (número local México)
                    //    VÁLIDO:   12 dígitos que empiecen con 52 (código país + 10)
                    //    INVÁLIDO: cualquier otro → auto-corregir a 10 dígitos
                    if (IsPhone(nh))
                    {
                        // Extraer solo dígitos (quitar +, espacios, guiones, paréntesis)
                        string d = Regex.Replace(val, @"[\s\-\(\)\+\.]", "");
                        d = Regex.Replace(d, @"\D", "");

                        // ── Caso 1: ya tiene 10 dígitos exactos → válido ─────
                        if (d.Length == 10)
                        {
                            // Solo reportar error si el formato tenía caracteres extraños
                            if (val != d && Regex.IsMatch(val, @"[^\d]"))
                                bag.Add(Mk(rowIdx, c, headers[c],
                                    "Teléfono: contiene caracteres extra (se limpiarán)",
                                    val, d));
                            // Si ya es "XXXXXXXXXX" no hay error
                        }
                        // ── Caso 2: 12 dígitos empezando con 52 → válido con código país ─
                        else if (d.Length == 12 && d.StartsWith("52"))
                        {
                            // Válido — no reportar error
                            // (52 + 10 dígitos = número con código de país México)
                        }
                        // ── Caso 3: 13 dígitos con 521 al inicio → quitar el 1 extra ────
                        else if (d.Length == 13 && d.StartsWith("521"))
                        {
                            // 521XXXXXXXXXX → 52XXXXXXXXXX (12 dígitos válidos)
                            string fix = "52" + d.Substring(3);
                            bag.Add(Mk(rowIdx, c, headers[c],
                                "Teléfono: dígito extra en código de país (521 → 52)",
                                val, fix));
                        }
                        // ── Caso 4: más de 12 dígitos → recortar a 10 ────────────────
                        else if (d.Length > 12)
                        {
                            // Intentar extraer los últimos 10 dígitos significativos
                            string fix = d.Substring(d.Length - 10);
                            bag.Add(Mk(rowIdx, c, headers[c],
                                string.Format("Teléfono: demasiados dígitos ({0}), se recorta a 10", d.Length),
                                val, fix));
                        }
                        // ── Caso 5: menos de 10 dígitos → error manual ────────────────
                        else
                        {
                            bag.Add(Mk(rowIdx, c, headers[c],
                                string.Format("Teléfono: debe tener 10 dígitos (tiene {0})", d.Length),
                                val, ""));
                        }
                    }

                    // 4. Email
                    if (IsEmail(nh))
                    {
                        if (!val.Contains('@'))
                        {
                            string fix = TryFixEmail(val);
                            bag.Add(Mk(rowIdx, c, headers[c],
                                fix != null
                                    ? "Email sin '@' (auto-corrección disponible)"
                                    : "Email sin '@'",
                                val, fix ?? ""));
                        }
                        else if (!Regex.IsMatch(val, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                        {
                            bag.Add(Mk(rowIdx, c, headers[c],
                                "Email con formato inválido", val, ""));
                        }
                    }

                    // 5. Código postal
                    if (IsPostal(nh))
                    {
                        var d = Regex.Replace(val, @"\D", "");
                        if (d.Length != 5)
                            bag.Add(Mk(rowIdx, c, headers[c],
                                $"C.P.: debe tener 5 dígitos (tiene {d.Length})",
                                val, d.Length > 5 ? d.Substring(0, 5) : d.PadLeft(5, '0')));
                    }

                    // 6. Fecha
                    if (IsDate(nh))
                    {
                        string normalized = NormalizeDate(val);
                        if (!Regex.IsMatch(normalized, @"^\d{4}-\d{2}-\d{2}$"))
                        {
                            string fix = TryConvertDate(val);
                            bag.Add(Mk(rowIdx, c, headers[c],
                                fix != null
                                    ? "Fecha: formato incorrecto (auto-corrección disponible)"
                                    : "Fecha debe ser AAAA-MM-DD",
                                val, fix ?? ""));
                        }
                        else if (normalized != val)
                        {
                            bag.Add(Mk(rowIdx, c, headers[c],
                                "Fecha: separador incorrecto (\\, / o .)",
                                val, normalized));
                        }
                    }
                }
            });

            return bag.OrderBy(e => e.Row).ThenBy(e => e.Col).ToList();
        }

        // ════════════════════════════════════════════════════════════
        //  AUTO-CORRECCIÓN EMAIL
        // ════════════════════════════════════════════════════════════
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

        // ════════════════════════════════════════════════════════════
        //  AUTO-CORRECCIÓN FECHAS
        // ════════════════════════════════════════════════════════════
        public static string NormalizeDate(string v) =>
            Regex.Replace(v.Trim(), @"[\/\\\.]", "-");

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
                new System.Globalization.CultureInfo("es-ES")
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

        // ── Helper interno ────────────────────────────────────────────
        static DataError Mk(int row, int col, string colName,
            string desc, string val, string fix) =>
            new DataError
            {
                Row = row,
                Col = col,
                ColumnName = colName,
                Description = desc,
                Value = val,
                AutoFix = fix
            };
    }
}
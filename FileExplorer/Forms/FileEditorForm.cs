using FileExplorer.Helpers;
using FileExplorer.Models;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using DrawColor = System.Drawing.Color;
using DrawFont = System.Drawing.Font;
using DrawFontStyle = System.Drawing.FontStyle;
using WDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument;
using WPara = DocumentFormat.OpenXml.Wordprocessing.Paragraph;

namespace FileExplorer.Forms
{
    public partial class FileEditorForm : Form
    {
        /// <summary>Se dispara cuando el archivo se guarda con la ruta resultante.</summary>
        public event Action<string> FileSaved;

        // ── Colores del tema ──────────────────────────────────────────
        internal static readonly DrawColor C_BG = DrawColor.FromArgb(252, 252, 254);
        internal static readonly DrawColor C_TOOL = DrawColor.FromArgb(245, 245, 248);
        internal static readonly DrawColor C_BORDER = DrawColor.FromArgb(209, 209, 214);
        internal static readonly DrawColor C_ACCENT = DrawColor.FromArgb(0, 122, 255);
        internal static readonly DrawColor C_GREEN = DrawColor.FromArgb(52, 199, 89);
        internal static readonly DrawColor C_RED = DrawColor.FromArgb(255, 59, 48);
        internal static readonly DrawColor C_ORANGE = DrawColor.FromArgb(255, 149, 0);
        internal static readonly DrawColor C_TXT = DrawColor.FromArgb(28, 28, 30);
        internal static readonly DrawColor C_SEC = DrawColor.FromArgb(142, 142, 147);
        internal static readonly DrawColor C_HDR = DrawColor.FromArgb(248, 248, 250);
        internal static readonly DrawColor C_ROWALT = DrawColor.FromArgb(250, 250, 253);
        internal static readonly DrawColor C_ERR_BG = DrawColor.FromArgb(255, 220, 220);
        internal static readonly DrawColor C_WARN_BG = DrawColor.FromArgb(255, 243, 205);
        internal static readonly DrawColor C_FIX_BG = DrawColor.FromArgb(220, 255, 230);
        internal static readonly DrawColor C_IDX_BG = DrawColor.FromArgb(235, 240, 255);
        internal static readonly DrawColor C_SEARCH = DrawColor.FromArgb(255, 255, 150);
        internal static readonly DrawColor C_HDR_BG = DrawColor.FromArgb(220, 232, 255);

        // ── Estado del archivo ────────────────────────────────────────
        internal readonly string _path;
        internal readonly string _ext;
        internal bool _isDirty = false;
        internal bool _tableMode = false;
        internal bool _isLoading = false;

        // ── Datos del archivo ─────────────────────────────────────────
        internal List<string> _headers = new();
        internal List<List<string>> _rows = new();
        internal List<ValidationError> _errors = new();
        internal Dictionary<(int, int), DrawColor> _cellColors = new();
        internal readonly HashSet<(int, int)> _searchHighlight = new();

        // ── Controles principales ─────────────────────────────────────
        internal Panel pnlToolbar;
        internal TabControl tabs;
        internal TabPage pgView, pgTable, pgEdit;
        internal Panel pnlDocScroll;
        internal FlowLayoutPanel flowDoc;
        internal DataGridView dgv;
        internal Label lblErrCount;
        internal ListBox lstErrors;
        internal RichTextBox rtbEdit;
        internal Button btnSave, btnExport, btnDetect, btnFixAll, btnToggleView, btnMigrate;
        internal Label lblInfo;
        internal TextBox txtSearch;
        internal ComboBox cboSearchCol;
        internal Label lblSearchResult;

        // ── Controles de gráficas ─────────────────────────────────────
        internal TabPage pgChart;
        internal Panel pnlChart;
        internal ComboBox cboChartX, cboChartY, cboChartType;

        // ── Clasificadores de tipo de archivo ─────────────────────────
        /// <summary>Verdadero si el archivo es un documento Office o PDF.</summary>
        internal bool IsDocFile => _ext is ".docx" or ".doc" or ".pdf" or ".pptx" or ".ppt";

        /// <summary>Verdadero si el archivo es una hoja de cálculo Excel.</summary>
        internal bool IsExcel => _ext is ".xlsx" or ".xls";

        /// <summary>Verdadero si el archivo contiene datos estructurados (CSV, JSON, XML, etc.).</summary>
        internal bool IsDataFile => _ext is ".csv" or ".tsv" or ".json" or ".xml" or ".txt" or ".md" or ".log";

        /// <summary>Verdadero si el archivo es una imagen.</summary>
        internal bool IsImage => _ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp"
                                         or ".webp" or ".tiff" or ".tif" or ".ico" or ".svg"
                                         or ".heic" or ".heif" or ".jfif";

        /// <summary>
        /// Inicializa el editor con la ruta del archivo, infiere la extensión
        /// y dispara la carga del contenido.
        /// </summary>
        public FileEditorForm(string path)
        {
            _path = path;
            _ext = Path.GetExtension(path).ToLowerInvariant();

            // PDF, Word y Excel → cargar normalmente y luego agregar pestaña de editor enriquecido

            InitializeComponent();
            LoadFile();

            // Para PDF, DOCX y XLSX: agregar pestaña de editor enriquecido
            if (_ext == ".pdf" || _ext == ".docx" || _ext == ".xlsx")
                AddRichEditorTab();
        }

        /// <summary>
        /// Agrega una pestaña "Editor" con el editor enriquecido (WordPdfEditorForm o ExcelEditorForm)
        /// embebido como panel dentro del FileEditorForm.
        /// </summary>
        void AddRichEditorTab()
        {
            var pgRich = new TabPage("  Editor  ");

            // Panel contenedor que ocupa toda la pestaña
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 245, 248) };

            // Botón para abrir en ventana independiente
            var btnOpen = new Button
            {
                Text = "Abrir en ventana completa",
                Dock = DockStyle.Top,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 255),
                ForeColor = Color.White,
                Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            btnOpen.FlatAppearance.BorderSize = 0;
            btnOpen.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 99, 220);
            btnOpen.Click += (_, __) =>
            {
                if (_ext == ".xlsx")
                    new ExcelEditorForm(_path).Show(this);
                else
                    new WordPdfEditorForm(_path).Show(this);
            };

            // Preview del contenido en un RichTextBox de solo lectura
            var rtbPreview = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 10.5f),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Padding = new Padding(16),
            };

            // Cargar contenido preview
            try
            {
                if (_ext == ".docx")
                {
                    using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(_path, false);
                    var body = doc.MainDocumentPart?.Document?.Body;
                    var sb = new System.Text.StringBuilder();
                    if (body != null)
                        foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                            sb.AppendLine(p.InnerText);
                    rtbPreview.Text = sb.ToString().TrimEnd();
                }
                else if (_ext == ".pdf")
                {
                    using var pdf = UglyToad.PdfPig.PdfDocument.Open(_path);
                    var sb = new System.Text.StringBuilder();
                    foreach (var pg2 in pdf.GetPages())
                    {
                        foreach (var w in pg2.GetWords()) sb.Append(w.Text + " ");
                        sb.AppendLine();
                    }
                    rtbPreview.Text = sb.ToString().Trim();
                }
                else if (_ext == ".xlsx")
                {
                    using var wb = new ClosedXML.Excel.XLWorkbook(_path);
                    var ws = wb.Worksheets.First();
                    var sb = new System.Text.StringBuilder();
                    int maxR = ws.LastRowUsed()?.RowNumber() ?? 0;
                    int maxC = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
                    for (int r = 1; r <= maxR; r++)
                    {
                        var parts = new System.Collections.Generic.List<string>();
                        for (int c = 1; c <= maxC; c++)
                            parts.Add((ws.Cell(r, c).IsEmpty() ? "" : ws.Cell(r, c).GetString()).PadRight(12));
                        sb.AppendLine(string.Join(" | ", parts));
                    }
                    rtbPreview.Font = new System.Drawing.Font("Courier New", 9.5f);
                    rtbPreview.Text = sb.ToString();
                }
            }
            catch { rtbPreview.Text = "(No se pudo previsualizar el contenido)"; }

            host.Controls.Add(rtbPreview);
            host.Controls.Add(btnOpen);
            pgRich.Controls.Add(host);
            tabs.TabPages.Add(pgRich);
        }

        /// <summary>
        /// Agrega un bloque de texto enriquecido al panel de vista de documento,
        /// con tamaño de fuente, color y estilo configurables.
        /// El alto se ajusta automáticamente al contenido.
        /// </summary>
        internal void AddDocText(string text, float size,
            DrawColor? color = null, DrawFontStyle style = DrawFontStyle.Regular)
        {
            var rtb = new RichTextBox
            {
                Text = text,
                ReadOnly = true,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
                BackColor = DrawColor.White,
                ForeColor = color ?? C_TXT,
                Font = new DrawFont("Segoe UI", size, style),
                ScrollBars = RichTextBoxScrollBars.None,
                WordWrap = true,
                DetectUrls = false,
                Width = Math.Max(500, flowDoc.Width - 100),
                Margin = new Padding(0, 2, 0, 2)
            };
            rtb.ContentsResized += (s, e) => rtb.Height = e.NewRectangle.Height + 4;
            AddDocBlock(rtb);
        }

        /// <summary>Agrega un espacio vertical al panel de vista de documento.</summary>
        internal void AddDocSpacer(int h) =>
            AddDocBlock(new Panel { Width = 10, Height = h, BackColor = DrawColor.Transparent });

        /// <summary>
        /// Agrega cualquier control al FlowLayoutPanel del documento,
        /// asignando margen mínimo si no tiene uno definido.
        /// </summary>
        internal void AddDocBlock(Control ctrl)
        {
            if (ctrl.Margin == Padding.Empty) ctrl.Margin = new Padding(0, 2, 0, 2);
            flowDoc.Controls.Add(ctrl);
        }

        /// <summary>
        /// Detecta si un archivo está pendiente de sincronización desde la nube
        /// (OneDrive offline o iCloud recall), verificando sus atributos de sistema.
        /// </summary>
        internal static bool IsCloudFile(string path)
        {
            try
            {
                var attrs = File.GetAttributes(path);
                const FileAttributes OFFLINE = (FileAttributes)0x1000;
                const FileAttributes RECALL = (FileAttributes)0x400000;
                return attrs.HasFlag(OFFLINE) || attrs.HasFlag(RECALL);
            }
            catch (Exception ex) { AppLogger.Warn("FileEditorForm.IsCloudFile: " + ex.Message); return false; }
        }

        /// <summary>
        /// Actualiza el label de información de la toolbar con el conteo de filas/columnas
        /// (modo tabla) o líneas/caracteres (modo editor). Prefija "*" si hay cambios sin guardar.
        /// </summary>
        internal void UpdateInfo()
        {
            if (tabs == null || lblInfo == null) return;
            string info = tabs.SelectedTab == pgTable
                ? _rows.Count.ToString("N0") + " filas  " + _headers.Count + " cols"
                : rtbEdit.Lines.Length.ToString("N0") + " lineas  " + rtbEdit.TextLength.ToString("N0") + " chars";
            if (_isDirty) info = "* " + info;
            lblInfo.Text = info;
        }

        /// <summary>
        /// Muestra un mensaje temporal en el label de información con el color indicado,
        /// y lo restaura al estado normal tras 2.5 segundos.
        /// </summary>
        internal void Flash(string msg, DrawColor color)
        {
            lblInfo.ForeColor = color; lblInfo.Text = msg;
            var t = new System.Windows.Forms.Timer { Interval = 2500 };
            t.Tick += (s, e) =>
            {
                try { if (!IsDisposed) { lblInfo.ForeColor = C_SEC; UpdateInfo(); } }
                catch (Exception ex) { AppLogger.Warn("FileEditorForm.Flash: " + ex.Message); }
                t.Dispose();
            };
            t.Start();
        }

        /// <summary>
        /// Abre un SaveFileDialog en hilo STA y devuelve la ruta elegida,
        /// o null si el usuario canceló.
        /// </summary>
        internal string PickSaveFile(string defaultName, string filter)
        {
            string fileName = null;
            var thread = new Thread(() =>
            {
                using var dlg = new SaveFileDialog
                { FileName = defaultName, Filter = filter, InitialDirectory = Path.GetDirectoryName(_path) };
                if (dlg.ShowDialog() == DialogResult.OK) fileName = dlg.FileName;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start(); thread.Join();
            return fileName;
        }

        /// <summary>
        /// Serializa los datos actuales (_headers + _rows) a formato CSV
        /// usando el separador indicado. Entrecomilla campos que lo requieran.
        /// </summary>
        internal string BuildCsv(char sep)
        {
            var sb = new StringBuilder(_rows.Count * 80);
            sb.AppendLine(string.Join(sep.ToString(), _headers.Select(h => Quote(h, sep))));
            foreach (var r in _rows)
                sb.AppendLine(string.Join(sep.ToString(),
                    _headers.Select((h, i) => Quote(i < r.Count ? r[i] : "", sep))));
            return sb.ToString();
        }

        /// <summary>
        /// Serializa los datos actuales a un array JSON con un objeto por fila,
        /// usando los headers como claves. Devuelve el JSON indentado.
        /// </summary>
        internal string BuildJson()
        {
            var arr = new JArray(_rows.Select(row => {
                var o = new JObject();
                for (int i = 0; i < _headers.Count && i < row.Count; i++) o[_headers[i]] = row[i];
                return o;
            }));
            return arr.ToString(Formatting.Indented);
        }

        /// <summary>
        /// Serializa los datos actuales a XML con estructura &lt;data&gt;&lt;row&gt;...&lt;/row&gt;&lt;/data&gt;.
        /// Los nombres de columna se limpian de caracteres no válidos para XML.
        /// </summary>
        internal string BuildXml()
        {
            var root = new XElement("data",
                _rows.Select(row => new XElement("row",
                    _headers.Select((h, i) => new XElement(
                        Regex.Replace(h, @"[^\w]", "_"), i < row.Count ? row[i] : "")))));
            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).ToString();
        }

        /// <summary>
        /// Serializa los datos actuales a texto plano con formato de tabla ASCII,
        /// con columnas alineadas y separadores de línea.
        /// </summary>
        internal string BuildTxt()
        {
            var widths = _headers.Select((h, i) =>
                Math.Max(h.Length, _rows.Select(r => i < r.Count ? r[i].Length : 0).DefaultIfEmpty(0).Max())).ToList();
            string Line(List<string> cells)
            {
                var parts = new List<string>();
                for (int i = 0; i < _headers.Count; i++)
                    parts.Add((i < cells.Count ? cells[i] : "").PadRight(widths[i]));
                return "| " + string.Join(" | ", parts) + " |";
            }
            string Div() => "+-" + string.Join("-+-", widths.Select(w => new string('-', w))) + "-+";
            var sb = new StringBuilder();
            sb.AppendLine(Div()); sb.AppendLine(Line(_headers)); sb.AppendLine(Div());
            foreach (var r in _rows) sb.AppendLine(Line(r));
            sb.AppendLine(Div());
            return sb.ToString();
        }

        /// <summary>
        /// Entrecomilla un valor de celda CSV si contiene el separador, comillas dobles o saltos de línea.
        /// Las comillas internas se escapan duplicándolas.
        /// </summary>
        static string Quote(string v, char sep)
        {
            if (v.Contains(sep) || v.Contains('"') || v.Contains('\n'))
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }
    }
}
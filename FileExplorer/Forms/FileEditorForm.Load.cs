using FileExplorer.Helpers;
using System.Text;
using UglyToad.PdfPig;
using DrawColor = System.Drawing.Color;
using DrawFont = System.Drawing.Font;
using DrawFontStyle = System.Drawing.FontStyle;
using DrawImage = System.Drawing.Image;
using DxText = DocumentFormat.OpenXml.Drawing.Text;
using PrsDoc = DocumentFormat.OpenXml.Packaging.PresentationDocument;
using SlidePart = DocumentFormat.OpenXml.Packaging.SlidePart;
using WDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument;
using WDrawing = DocumentFormat.OpenXml.Wordprocessing.Drawing;
using WPara = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WTbl = DocumentFormat.OpenXml.Wordprocessing.Table;
using WTblCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using WTblRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

namespace FileExplorer.Forms
{
    public partial class FileEditorForm
    {
        /// <summary>
        /// Punto de entrada de carga: detecta si el archivo está en la nube y,
        /// según la extensión, delega al método de carga correspondiente.
        /// </summary>
        void LoadFile()
        {
            if (!IsImage && IsCloudFile(_path))
            {
                if (flowDoc != null)
                {
                    if (tabs.TabPages.Contains(pgView)) tabs.SelectedTab = pgView;
                    AddDocText("Archivo en la nube (OneDrive)", 13f, C_ACCENT, DrawFontStyle.Bold);
                    AddDocText("Este archivo aun no se ha descargado localmente.\n\n" +
                        "1. Clic derecho en el Explorador\n" +
                        "2. Selecciona 'Descargar siempre este dispositivo'\n" +
                        "3. Espera a que sincronice y vuelve a abrirlo", 10.5f, C_SEC);
                }
                else
                    MessageBox.Show("Este archivo esta en la nube y no esta disponible localmente.",
                        "Archivo en la nube", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateInfo(); return;
            }

            try
            {
                if (IsImage) LoadImage();
                else if (_ext is ".docx" or ".doc") LoadWord();
                else if (_ext == ".pdf") LoadPdf();
                else if (_ext is ".pptx" or ".ppt") LoadPptx();
                else if (_ext is ".xlsx" or ".xls") LoadExcelAsync();
                else if (_ext is ".csv" or ".tsv") LoadCsvAsync();
                else if (_ext == ".json") LoadJsonAsync();
                else if (_ext == ".xml") LoadXmlAsync();
                else if (_ext is ".txt" or ".md" or ".log"
                              or ".ini" or ".yaml" or ".yml") LoadTextAsync();
                else OpenExternal();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"FileEditorForm.LoadFile [{_path}] fallo", ex);
                if (flowDoc != null) AddDocText("Error:\n" + ex.Message, 10f, C_RED);
                if (tabs.TabPages.Contains(pgView)) tabs.SelectedTab = pgView;
                else if (tabs.TabPages.Contains(pgTable)) tabs.SelectedTab = pgTable;
            }
            UpdateInfo();
        }

        /// <summary>
        /// Ejecuta un loader asíncrono deshabilitando los botones de detección y migración
        /// mientras carga, y los restaura al terminar.
        /// </summary>
        internal async void RunLoad(Func<Task> loader)
        {
            _isLoading = true;
            if (btnDetect != null) btnDetect.Enabled = false;
            if (btnMigrate != null) btnMigrate.Enabled = false;
            try { await loader(); }
            catch (Exception ex)
            {
                AppLogger.Error("FileEditorForm.RunLoad fallo", ex);
                MessageBox.Show("Error al cargar:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isLoading = false;
                if (btnDetect != null) btnDetect.Enabled = true;
                if (btnMigrate != null) btnMigrate.Enabled = true;
                UpdateInfo();
            }
        }

        /// <summary>Carga un CSV o TSV: detecta encoding y separador, parsea y construye la grilla virtual.</summary>
        void LoadCsvAsync() => RunLoad(async () =>
        {
            var enc = DataParser.DetectEncoding(_path);
            string text = await Task.Run(() => File.ReadAllText(_path, enc));
            rtbEdit.Font = new DrawFont("Consolas", 10f);
            rtbEdit.Text = text;
            char sep = DataParser.DetectSeparator(text);
            var r = await Task.Run(() => DataParser.ParseCsv(text, sep));
            _headers = r.Item1; _rows = r.Item2;
            BuildVirtualGrid();
            if (tabs.TabPages.Contains(pgTable)) tabs.SelectedTab = pgTable;
        });

        /// <summary>Carga un JSON: lo indenta en el editor y parsea la estructura tabular para la grilla.</summary>
        void LoadJsonAsync() => RunLoad(async () =>
        {
            string raw = await Task.Run(() => File.ReadAllText(_path, Encoding.UTF8));
            rtbEdit.Font = new DrawFont("Consolas", 10f);
            try { rtbEdit.Text = JToken.Parse(raw).ToString(Formatting.Indented); }
            catch { rtbEdit.Text = raw; }
            var r = await Task.Run(() => DataParser.ParseJson(raw));
            _headers = r.Item1; _rows = r.Item2;
            BuildVirtualGrid();
            if (tabs.TabPages.Contains(pgTable)) tabs.SelectedTab = pgTable;
        });

        /// <summary>Carga un XML: lo formatea en el editor y extrae filas/columnas para la grilla.</summary>
        void LoadXmlAsync() => RunLoad(async () =>
        {
            string raw = await Task.Run(() => File.ReadAllText(_path, Encoding.UTF8));
            rtbEdit.Font = new DrawFont("Consolas", 10f);
            try { rtbEdit.Text = XDocument.Parse(raw).ToString(); }
            catch { rtbEdit.Text = raw; }
            var r = await Task.Run(() => DataParser.ParseXml(raw));
            _headers = r.Item1; _rows = r.Item2;
            BuildVirtualGrid();
            if (tabs.TabPages.Contains(pgTable)) tabs.SelectedTab = pgTable;
        });

        /// <summary>
        /// Carga un archivo de texto plano. Si tiene estructura de tabla (TXT/MD con separadores),
        /// activa automáticamente la vista de grilla; si no, muestra el editor de texto.
        /// </summary>
        void LoadTextAsync() => RunLoad(async () =>
        {
            var enc = DataParser.DetectEncoding(_path);
            string text = await Task.Run(() => File.ReadAllText(_path, enc));
            rtbEdit.Font = new DrawFont("Segoe UI", 11f);
            rtbEdit.Text = text;

            string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd();

            bool parsed = await Task.Run(() =>
                DataParser.TryParseTxt(lines, out var hdrs, out var rws)
                    ? (_headers = hdrs) != null & (_rows = rws) != null
                    : false);

            if (parsed)
            {
                BuildVirtualGrid();
                if (tabs.TabPages.Contains(pgTable)) tabs.SelectedTab = pgTable;
                if (btnToggleView != null)
                { btnToggleView.Text = "Ver texto"; btnToggleView.BackColor = C_ACCENT; _tableMode = true; }
            }
            else
            {
                if (tabs.TabPages.Contains(pgEdit)) tabs.SelectedTab = pgEdit;
            }
        });

        /// <summary>Carga un archivo Excel (.xlsx/.xls) parseando todas sus hojas a la grilla virtual.</summary>
        void LoadExcelAsync() => RunLoad(async () =>
        {
            var r = await Task.Run(() => DataParser.ParseExcel(_path, _ext));
            _headers = r.Item1; _rows = r.Item2;
            BuildVirtualGrid();
            if (tabs.TabPages.Contains(pgTable)) tabs.SelectedTab = pgTable;
        });

        /// <summary>
        /// Carga una imagen en la vista de documento con su información de dimensiones y tamaño.
        /// Escala la imagen para caber en el panel y ofrece un botón para verla en tamaño real.
        /// Soporta SVG (muestra el código fuente) y archivos en la nube.
        /// </summary>
        void LoadImage()
        {
            flowDoc.Controls.Clear();
            if (tabs.TabPages.Contains(pgView)) tabs.SelectedTab = pgView;
            try
            {
                if (IsCloudFile(_path))
                {
                    AddDocText("Archivo en la nube (OneDrive)", 13f, C_ACCENT, DrawFontStyle.Bold);
                    AddDocText("Este archivo aun no se ha descargado localmente.", 10.5f, C_SEC);
                    return;
                }
                if (_ext == ".svg")
                {
                    AddDocText("Archivo SVG (vectorial)", 12f, C_ACCENT, DrawFontStyle.Bold);
                    string svg = File.ReadAllText(_path, Encoding.UTF8);
                    AddDocText(svg.Length > 3000 ? svg[..3000] + "\n..." : svg, 9f);
                    return;
                }

                DrawImage img;
                using (var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    img = DrawImage.FromStream(fs, true, false);

                long bytes = new FileInfo(_path).Length;
                string szTxt = bytes >= 1024 * 1024
                    ? $"{bytes / 1024.0 / 1024.0:F1} MB" : $"{bytes / 1024.0:F0} KB";

                AddDocBlock(new Label
                {
                    Text = $"  {img.Width} x {img.Height} px  -  {szTxt}  -  {_ext.ToUpper().TrimStart('.')}",
                    AutoSize = false,
                    Width = Math.Max(500, flowDoc.Width - 100),
                    Height = 28,
                    BackColor = DrawColor.FromArgb(40, 40, 44),
                    ForeColor = DrawColor.FromArgb(200, 200, 210),
                    Font = new DrawFont("Segoe UI", 9f),
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0),
                    Margin = new Padding(0, 0, 0, 8)
                });

                int maxW = Math.Max(500, flowDoc.Width - 100), maxH = 700;
                double scale = Math.Min((double)maxW / img.Width, (double)maxH / img.Height);
                if (scale > 1) scale = 1;
                int dw = (int)(img.Width * scale), dh = (int)(img.Height * scale);

                // Panel con fondo a cuadros para mostrar transparencia
                var pnlImg = new Panel { Width = dw + 2, Height = dh + 2, Margin = new Padding(0, 0, 0, 10) };
                var imgRef = img;
                pnlImg.Paint += (s, e) =>
                {
                    int ts = 12;
                    for (int ty = 0; ty < pnlImg.Height; ty += ts)
                        for (int tx = 0; tx < pnlImg.Width; tx += ts)
                        {
                            bool dark = ((tx / ts) + (ty / ts)) % 2 == 0;
                            e.Graphics.FillRectangle(new System.Drawing.SolidBrush(
                                dark ? DrawColor.FromArgb(195, 195, 195) : DrawColor.White), tx, ty, ts, ts);
                        }
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    e.Graphics.DrawImage(imgRef, 1, 1, dw, dh);
                };
                AddDocBlock(pnlImg);

                if (scale < 1)
                {
                    var btnReal = new Button
                    {
                        Text = $"Ver tamano real ({img.Width}x{img.Height})",
                        AutoSize = true,
                        Padding = new Padding(12, 5, 12, 5),
                        FlatStyle = FlatStyle.Flat,
                        BackColor = DrawColor.FromArgb(72, 72, 76),
                        ForeColor = DrawColor.White,
                        Font = new DrawFont("Segoe UI", 9f),
                        Cursor = Cursors.Hand,
                        Margin = new Padding(0, 4, 0, 0)
                    };
                    btnReal.FlatAppearance.BorderSize = 0;
                    btnReal.Click += (s, e) =>
                    {
                        try
                        {
                            var wa = Screen.PrimaryScreen?.WorkingArea;
                            int w = Math.Min(imgRef.Width + 40, wa?.Width ?? 1920);
                            int h = Math.Min(imgRef.Height + 60, wa?.Height ?? 1080);
                            var frm = new Form
                            {
                                Text = Path.GetFileName(_path),
                                Size = new System.Drawing.Size(w, h),
                                StartPosition = FormStartPosition.CenterScreen,
                                BackColor = DrawColor.FromArgb(30, 30, 30)
                            };
                            frm.Controls.Add(new PictureBox
                            { Image = imgRef, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill });
                            frm.Show();
                        }
                        catch (Exception ex) { AppLogger.Error("Error al mostrar imagen en tamano real", ex); }
                    };
                    AddDocBlock(btnReal);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"FileEditorForm.LoadImage [{_path}] fallo", ex);
                AddDocText("Error al cargar imagen:\n" + ex.Message, 10f, C_RED);
            }
        }

        /// <summary>Inicia la carga de Word difiriendo el renderizado al evento Shown para evitar bloquear la UI.</summary>
        void LoadWord()
        {
            flowDoc.Controls.Clear();
            if (tabs.TabPages.Contains(pgView)) tabs.SelectedTab = pgView;
            EventHandler h = null;
            h = (s, e) => { Shown -= h; RenderWordContent(); };
            Shown += h;
        }

        /// <summary>
        /// Renderiza el contenido DOCX en el panel de vista: párrafos con formato (negrita, color,
        /// tamaño), tablas como DataGridView e imágenes embebidas como PictureBox.
        /// También copia el texto plano al editor.
        /// </summary>
        void RenderWordContent()
        {
            flowDoc.Controls.Clear();
            try
            {
                using var doc = WDoc.Open(_path, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) { AddDocText("(Vacio)", 11f); return; }

                // Recopilar imágenes embebidas por RelationshipId
                var imgMap = new Dictionary<string, byte[]>();
                if (doc.MainDocumentPart != null)
                    foreach (var rel in doc.MainDocumentPart.Parts)
                        if (rel.OpenXmlPart is DocumentFormat.OpenXml.Packaging.ImagePart ip)
                        {
                            using var st = ip.GetStream(); using var ms = new MemoryStream();
                            st.CopyTo(ms); imgMap[rel.RelationshipId] = ms.ToArray();
                        }

                foreach (var el in body.ChildElements)
                {
                    if (el is WPara para)
                    {
                        var drw = para.Descendants<WDrawing>().FirstOrDefault();
                        if (drw != null)
                        {
                            var blip = drw.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
                            if (blip != null && imgMap.TryGetValue(blip.Embed?.Value ?? "", out byte[] imgBytes))
                                TryAddImage(imgBytes);
                        }
                        else RenderWordPara(para);
                    }
                    else if (el is WTbl tbl) RenderWordTable(tbl);
                }

                rtbEdit.Clear();
                foreach (var p in body.Descendants<WPara>())
                    rtbEdit.AppendText(p.InnerText + "\n");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"FileEditorForm.RenderWordContent [{_path}] fallo", ex);
                AddDocText("Error: " + ex.Message, 10f, C_RED);
            }
            if (tabs.TabPages.Contains(pgView)) tabs.SelectedTab = pgView;
        }

        /// <summary>
        /// Renderiza un párrafo DOCX como RichTextBox, aplicando el estilo de encabezado
        /// (H1/H2/H3) o texto normal según el ParagraphStyleId, y el formato de cada Run.
        /// </summary>
        void RenderWordPara(WPara para)
        {
            if (string.IsNullOrWhiteSpace(para.InnerText)) { AddDocSpacer(5); return; }
            string sid = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
            int h = sid.Contains("1") ? 1 : sid.Contains("2") ? 2 : sid.Contains("3") ? 3 : 0;
            var rtb = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
                BackColor = DrawColor.White,
                ScrollBars = RichTextBoxScrollBars.None,
                WordWrap = true,
                DetectUrls = false,
                Width = Math.Max(500, flowDoc.Width - 100),
                Margin = new Padding(0, h > 0 ? 5 : 1, 0, h > 0 ? 3 : 1)
            };
            rtb.ContentsResized += (s, e) => rtb.Height = e.NewRectangle.Height + 4;

            foreach (var run in para.Descendants<WRun>())
            {
                var rp = run.RunProperties;
                bool bold = rp?.Bold != null, ital = rp?.Italic != null, und = rp?.Underline != null;
                float sz = h == 1 ? 20f : h == 2 ? 15f : h == 3 ? 13f : 11f;
                string sv = rp?.FontSize?.Val?.Value;
                if (sv != null && float.TryParse(sv, out float hp)) sz = hp / 2f;
                var fs = DrawFontStyle.Regular;
                if (bold || h > 0) fs |= DrawFontStyle.Bold;
                if (ital) fs |= DrawFontStyle.Italic;
                if (und) fs |= DrawFontStyle.Underline;
                var col = h == 1 ? DrawColor.FromArgb(15, 15, 20)
                        : h == 2 ? DrawColor.FromArgb(40, 40, 50)
                        : h == 3 ? DrawColor.FromArgb(60, 60, 70) : C_TXT;
                string hex = rp?.Color?.Val?.Value ?? "";
                if (hex.Length == 6) try { col = ColorTranslator.FromHtml("#" + hex); } catch { }
                string txt = run.InnerText;
                if (string.IsNullOrEmpty(txt)) continue;
                rtb.SelectionStart = rtb.TextLength; rtb.SelectionLength = 0;
                rtb.SelectionFont = new DrawFont("Segoe UI", sz, fs);
                rtb.SelectionColor = col;
                rtb.AppendText(txt);
            }
            AddDocBlock(rtb);
        }

        /// <summary>
        /// Renderiza una tabla DOCX como DataGridView con la primera fila en negrita.
        /// El ancho de columna se calcula para distribuir el espacio disponible.
        /// </summary>
        void RenderWordTable(WTbl tbl)
        {
            var rows = tbl.Elements<WTblRow>().ToList();
            if (rows.Count == 0) return;
            int cols = rows.Max(r => r.Elements<WTblCell>().Count());
            int cw = Math.Max(80, Math.Max(400, flowDoc.Width - 120) / Math.Max(cols, 1));
            var dg = new DataGridView
            {
                Width = cw * cols + 2,
                Height = Math.Min(rows.Count * 28 + 32, 300),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                BackgroundColor = DrawColor.White,
                GridColor = DrawColor.FromArgb(200, 200, 210),
                Font = new DrawFont("Segoe UI", 9f),
                ColumnHeadersVisible = false,
                RowHeadersVisible = false,
                ScrollBars = ScrollBars.Both,
                DefaultCellStyle = new DataGridViewCellStyle
                { BackColor = DrawColor.White, ForeColor = C_TXT, Padding = new Padding(4, 2, 4, 2) },
                Margin = new Padding(0, 6, 0, 6)
            };
            for (int c = 0; c < cols; c++)
                dg.Columns.Add(new DataGridViewTextBoxColumn { Width = cw });
            bool first = true;
            foreach (var row in rows)
            {
                var cells = row.Elements<WTblCell>().Select(c => c.InnerText.Trim()).ToArray();
                var vals = new object[cols];
                for (int i = 0; i < cols; i++) vals[i] = i < cells.Length ? cells[i] : "";
                int ri = dg.Rows.Add(vals);
                if (first)
                {
                    dg.Rows[ri].DefaultCellStyle.BackColor = DrawColor.FromArgb(240, 240, 248);
                    dg.Rows[ri].DefaultCellStyle.Font = new DrawFont("Segoe UI", 9f, DrawFontStyle.Bold);
                    first = false;
                }
            }
            AddDocBlock(dg);
        }

        /// <summary>
        /// Carga un PDF página por página con PdfPig: extrae imágenes y reconstruye
        /// el texto agrupando palabras por posición vertical (bounding box).
        /// Cada página se muestra con un banner de número y su contenido como RichTextBox.
        /// </summary>
        void LoadPdf()
        {
            flowDoc.Controls.Clear(); rtbEdit.Clear();
            try
            {
                using var pdf = PdfDocument.Open(_path);
                int total = pdf.NumberOfPages;
                foreach (var page in pdf.GetPages())
                {
                    AddDocBlock(new Label
                    {
                        Text = "  Pagina " + page.Number + " / " + total,
                        AutoSize = false,
                        Width = Math.Max(500, flowDoc.Width - 100),
                        Height = 26,
                        BackColor = C_ACCENT,
                        ForeColor = DrawColor.White,
                        Font = new DrawFont("Segoe UI", 9f, DrawFontStyle.Bold),
                        TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                        Padding = new Padding(8, 0, 0, 0),
                        Margin = new Padding(0, 4, 0, 4)
                    });

                    try
                    {
                        foreach (var img in page.GetImages())
                        {
                            try
                            {
                                var rawBytes = img.RawBytes.ToArray();
                                if (rawBytes.Length == 0) continue;
                                using var ms = new MemoryStream(rawBytes);
                                DrawImage bmp = null;
                                try { bmp = DrawImage.FromStream(ms); } catch { }
                                if (bmp == null) continue;
                                int maxW = Math.Max(400, flowDoc.Width - 120);
                                int iw = Math.Min(bmp.Width, maxW);
                                int ih = (int)((double)bmp.Height * iw / bmp.Width);
                                AddDocBlock(new PictureBox
                                {
                                    Image = bmp,
                                    SizeMode = PictureBoxSizeMode.Zoom,
                                    Width = iw,
                                    Height = Math.Min(ih, 500),
                                    BackColor = DrawColor.Transparent,
                                    Margin = new Padding(0, 6, 0, 6)
                                });
                            }
                            catch (Exception ex) { AppLogger.Warn("FileEditorForm.LoadPdf imagen: " + ex.Message); }
                        }
                    }
                    catch (Exception ex) { AppLogger.Warn("FileEditorForm.LoadPdf GetImages: " + ex.Message); }

                    // Reagrupar palabras por línea usando la coordenada Y del bounding box
                    var words = page.GetWords().ToList();
                    var lineMap = new SortedDictionary<int, List<string>>(
                        Comparer<int>.Create((a, b) => b.CompareTo(a)));
                    foreach (var w in words)
                    {
                        int key = (int)Math.Round(w.BoundingBox.Bottom / 4.0) * 4;
                        if (!lineMap.ContainsKey(key)) lineMap[key] = new List<string>();
                        lineMap[key].Add(w.Text);
                    }
                    var sb = new StringBuilder();
                    foreach (var kv in lineMap) sb.AppendLine(string.Join(" ", kv.Value).Trim());

                    var rtbPage = new RichTextBox
                    {
                        ReadOnly = true,
                        BorderStyle = System.Windows.Forms.BorderStyle.None,
                        BackColor = DrawColor.White,
                        ForeColor = C_TXT,
                        Font = new DrawFont("Segoe UI", 10.5f),
                        ScrollBars = RichTextBoxScrollBars.None,
                        WordWrap = true,
                        DetectUrls = false,
                        Width = Math.Max(460, flowDoc.Width - 180),
                        Margin = Padding.Empty
                    };
                    rtbPage.ContentsResized += (s, ev) => rtbPage.Height = ev.NewRectangle.Height + 4;
                    rtbPage.Text = sb.ToString().Trim();
                    rtbEdit.AppendText("-- Pagina " + page.Number + " --\n" + rtbPage.Text + "\n\n");
                    AddDocBlock(rtbPage);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"FileEditorForm.LoadPdf [{_path}] fallo", ex);
                AddDocText("Error: " + ex.Message, 10f, C_RED);
            }
            if (tabs.TabPages.Contains(pgView)) tabs.SelectedTab = pgView;
        }

        /// <summary>
        /// Carga una presentación PPTX mostrando cada diapositiva con un banner numerado
        /// y el texto de todos sus elementos de texto (DxText).
        /// </summary>
        void LoadPptx()
        {
            flowDoc.Controls.Clear();
            try
            {
                using var prs = PrsDoc.Open(_path, false);
                int n = 1;
                var slideParts = prs.PresentationPart != null
                    ? prs.PresentationPart.SlideParts.ToList()
                    : new List<SlidePart>();
                foreach (var slide in slideParts)
                {
                    AddDocBlock(new Label
                    {
                        Text = "  Diapositiva " + n++,
                        AutoSize = false,
                        Width = Math.Max(500, flowDoc.Width - 100),
                        Height = 26,
                        BackColor = C_ACCENT,
                        ForeColor = DrawColor.White,
                        Font = new DrawFont("Segoe UI", 9f, DrawFontStyle.Bold),
                        TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                        Padding = new Padding(8, 0, 0, 0),
                        Margin = new Padding(0, 4, 0, 2)
                    });
                    foreach (var t in slide.Slide.Descendants<DxText>())
                        if (!string.IsNullOrWhiteSpace(t.Text)) AddDocText(t.Text, 11f);
                    AddDocSpacer(8);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"FileEditorForm.LoadPptx [{_path}] fallo", ex);
                AddDocText("Error: " + ex.Message, 10f, C_RED);
            }
            if (tabs.TabPages.Contains(pgView)) tabs.SelectedTab = pgView;
        }

        /// <summary>
        /// Muestra un mensaje para archivos sin visor integrado y un botón
        /// para abrirlos con el programa predeterminado del sistema.
        /// </summary>
        void OpenExternal()
        {
            AddDocText("Archivo: " + Path.GetFileName(_path) +
                "\nTipo: " + _ext + "\n\nNo tiene visor integrado.", 11f);
            var btn = new Button
            {
                Text = "Abrir con programa predeterminado",
                AutoSize = true,
                Padding = new Padding(14, 6, 14, 6),
                FlatStyle = FlatStyle.Flat,
                BackColor = C_ACCENT,
                ForeColor = DrawColor.White,
                Font = new DrawFont("Segoe UI", 9.5f),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 10, 0, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_path) { UseShellExecute = true }); }
                catch (Exception ex) { AppLogger.Error("FileEditorForm.OpenExternal fallo", ex); MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            AddDocBlock(btn);
            if (tabs.TabPages.Contains(pgView)) tabs.SelectedTab = pgView;
        }

        /// <summary>
        /// Intenta decodificar y mostrar una imagen desde un array de bytes embebido
        /// (p.ej. imágenes dentro de un DOCX). Escala al ancho máximo disponible.
        /// </summary>
        internal void TryAddImage(byte[] bytes)
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                var img = DrawImage.FromStream(ms);
                int maxW = Math.Max(400, flowDoc.Width - 140);
                int w = Math.Min(img.Width, maxW);
                int h = (int)((double)img.Height * w / img.Width);
                AddDocBlock(new PictureBox
                {
                    Image = img,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Width = w,
                    Height = Math.Min(h, 500),
                    BackColor = DrawColor.Transparent,
                    Margin = new Padding(0, 6, 0, 6)
                });
            }
            catch (Exception ex) { AppLogger.Warn("FileEditorForm.TryAddImage: " + ex.Message); }
        }
    }
}
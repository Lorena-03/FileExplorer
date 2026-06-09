using System.Drawing.Imaging;
using System.Text;
using WDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument;
using WBody = DocumentFormat.OpenXml.Wordprocessing.Body;
using WPara = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Editor de documentos Word/PDF con soporte para texto enriquecido,
    /// imágenes con ajuste de tamaño, listas, alineación, tablas
    /// y guardado como DOCX o PDF.
    /// </summary>
    public partial class WordPdfEditorForm : Form
    {
        static readonly Color C_BG = Color.FromArgb(245, 245, 248);
        static readonly Color C_TOOLBAR = Color.FromArgb(250, 250, 252);
        static readonly Color C_BORDER = Color.FromArgb(210, 210, 218);
        static readonly Color C_ACCENT = Color.FromArgb(0, 122, 255);
        static readonly Color C_TEXT = Color.FromArgb(28, 28, 30);
        static readonly Color C_SUB = Color.FromArgb(142, 142, 147);

        readonly string _filePath;
        readonly string _ext;
        bool _isDirty = false;

        Color _currentColor = Color.Black;
        Color _currentBgColor = Color.Transparent;

        // Referencias a botones de color del ToolStrip (definidos en Designer)
        public WordPdfEditorForm(string filePath)
        {
            _filePath = filePath;
            _ext = Path.GetExtension(filePath).ToLowerInvariant();
            InitUI();
            LoadFile();
        }

        // ════════════════════════════════════════════════════════════
        //  CARGA
        // ════════════════════════════════════════════════════════════

        void LoadFile()
        {
            if (!File.Exists(_filePath)) return;
            try
            {
                if (_ext == ".docx")
                {
                    using var doc = WDoc.Open(_filePath, false);
                    var body = doc.MainDocumentPart?.Document?.Body;
                    var sb = new StringBuilder();
                    if (body != null)
                        foreach (var p in body.Descendants<WPara>())
                            sb.AppendLine(p.InnerText);
                    _rtb.Text = sb.ToString().TrimEnd();
                }
                else if (_ext == ".pdf")
                {
                    using var pdf = UglyToad.PdfPig.PdfDocument.Open(_filePath);
                    var sb = new StringBuilder();
                    foreach (var pg in pdf.GetPages())
                    {
                        foreach (var w in pg.GetWords()) sb.Append(w.Text + " ");
                        sb.AppendLine();
                    }
                    _rtb.Text = sb.ToString().Trim();
                }
                _isDirty = false;
                UpdateInfo();
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════
        //  GUARDAR
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Abre un SaveFileDialog para que el usuario elija dónde guardar el archivo,
        /// luego guarda en DOCX o PDF y muestra confirmación.
        /// </summary>
        void SaveFile()
        {
            // Determinar filtro y extensión por defecto
            string filter = _ext == ".docx"
                ? "Word (*.docx)|*.docx|Todos los archivos|*.*"
                : "PDF (*.pdf)|*.pdf|Todos los archivos|*.*";

            string destPath = null;
            var thread = new Thread(() =>
            {
                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar archivo",
                    Filter = filter,
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
                if (_ext == ".docx" || destPath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                    SaveDocxTo(destPath);
                else
                    SavePdfTo(destPath);

                _isDirty = false;
                _lblInfo.Text = "Guardado  ✓";
                _lblInfo.ForeColor = Color.FromArgb(52, 199, 89);

                MessageBox.Show(
                    "Archivo guardado correctamente en:" + destPath,
                    "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);

                var t = new System.Windows.Forms.Timer { Interval = 3000 };
                t.Tick += (_, __) =>
                {
                    if (!IsDisposed) { _lblInfo.Text = "Listo"; _lblInfo.ForeColor = C_SUB; }
                    t.Dispose();
                };
                t.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar:" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Crea un DOCX válido en MemoryStream usando OpenXML
        /// y lo escribe al disco.
        /// </summary>
        void SaveDocxTo(string destPath)
        {
            using var ms = new MemoryStream();

            using (var doc = WDoc.Create(ms,
                DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
            {
                var mainPart = doc.AddMainDocumentPart();
                var wdoc = new DocumentFormat.OpenXml.Wordprocessing.Document();
                var body = new WBody();
                mainPart.Document = wdoc;
                wdoc.AppendChild(body);

                foreach (var line in _rtb.Lines)
                {
                    var para = new WPara();
                    var run = new WRun();
                    run.AppendChild(new WText(line)
                    {
                        Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve
                    });
                    para.AppendChild(run);
                    body.AppendChild(para);
                }

                body.AppendChild(new WPara());
                mainPart.Document.Save();
            }

            File.WriteAllBytes(destPath, ms.ToArray());
        }

        /// <summary>
        /// Crea un PDF 1.4 mínimo en MemoryStream usando bytes binarios
        /// y offsets calculados con precisión, luego lo escribe al disco.
        /// </summary>
        void SavePdfTo(string destPath)
        {
            // Paso 1 — construir stream de contenido
            var sbContent = new StringBuilder();
            sbContent.Append("BT\n");
            sbContent.Append("/F1 11 Tf\n");
            sbContent.Append("50 780 Td\n");
            sbContent.Append("14 TL\n");

            foreach (var line in _rtb.Lines)
            {
                string safe = line
                    .Replace("\\", "\\\\")
                    .Replace("(", "\\(")
                    .Replace(")", "\\)");
                if (safe.Length > 110) safe = safe[..110] + "...";
                sbContent.Append("(" + safe + ") Tj T*\n");
            }
            sbContent.Append("ET\n");

            byte[] contentBytes = Encoding.Latin1.GetBytes(sbContent.ToString());

            // Paso 2 — construir PDF con offsets reales
            using var ms = new MemoryStream();

            void W(string s)
            {
                var b = Encoding.Latin1.GetBytes(s);
                ms.Write(b, 0, b.Length);
            }

            var off = new long[6];

            W("%PDF-1.4\n");

            off[1] = ms.Position;
            W("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

            off[2] = ms.Position;
            W("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

            off[3] = ms.Position;
            W("3 0 obj\n");
            W("<< /Type /Page /Parent 2 0 R\n");
            W("   /MediaBox [0 0 595 842]\n");
            W("   /Contents 4 0 R\n");
            W("   /Resources << /Font << /F1 5 0 R >> >> >>\n");
            W("endobj\n");

            off[4] = ms.Position;
            W("4 0 obj\n");
            W("<< /Length " + contentBytes.Length + " >>\n");
            W("stream\n");
            ms.Write(contentBytes, 0, contentBytes.Length);
            W("\nendstream\nendobj\n");

            off[5] = ms.Position;
            W("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

            long xrefPos = ms.Position;
            W("xref\n0 6\n");
            W("0000000000 65535 f \n");
            for (int i = 1; i <= 5; i++)
                W(off[i].ToString("D10") + " 00000 n \n");

            W("trailer\n<< /Size 6 /Root 1 0 R >>\n");
            W("startxref\n" + xrefPos + "\n%%EOF\n");

            File.WriteAllBytes(_filePath, ms.ToArray());
        }

        // ════════════════════════════════════════════════════════════
        //  FORMATO DE TEXTO
        // ════════════════════════════════════════════════════════════

        void ApplyFont()
        {
            var f = _rtb.SelectionFont ?? _rtb.Font;
            _rtb.SelectionFont = new Font(
                _cboFont.SelectedItem?.ToString() ?? "Segoe UI", f.Size, f.Style);
        }

        void ApplySize()
        {
            if (!float.TryParse(_cboSize.SelectedItem?.ToString(), out float sz)) return;
            var f = _rtb.SelectionFont ?? _rtb.Font;
            _rtb.SelectionFont = new Font(f.FontFamily, sz, f.Style);
        }

        void ToggleStyle(FontStyle style)
        {
            var f = _rtb.SelectionFont ?? _rtb.Font;
            bool on = f.Style.HasFlag(style);
            _rtb.SelectionFont = new Font(f.FontFamily, f.Size,
                on ? f.Style & ~style : f.Style | style);
            UpdateToolbarState();
        }

        void PickColor(bool background)
        {
            using var dlg = new ColorDialog { FullOpen = true, AnyColor = true };
            dlg.Color = background ? (_currentBgColor == Color.Transparent ? Color.Yellow : _currentBgColor)
                                   : _currentColor;
            if (dlg.ShowDialog() != DialogResult.OK) return;
            if (background)
            {
                _currentBgColor = dlg.Color;
                _rtb.SelectionBackColor = dlg.Color;
            }
            else
            {
                _currentColor = dlg.Color;
                _rtb.SelectionColor = dlg.Color;
            }
            // Repintar controles de la toolbar
            foreach (Control c in Controls)
                if (c is ToolStrip ts) { ts.Invalidate(); break; }
        }

        void SetAlign(HorizontalAlignment align) =>
            _rtb.SelectionAlignment = align;

        void SetAlignJustify() =>
            _rtb.SelectionAlignment = HorizontalAlignment.Left;

        // ── Listas ────────────────────────────────────────────────────

        void InsertBullet() => _rtb.SelectedText = "• ";

        void InsertNumbered()
        {
            int n = _rtb.Lines.Count(l => l.Length > 1 && char.IsDigit(l[0])) + 1;
            _rtb.SelectedText = n + ". ";
        }

        // ── Imagen ────────────────────────────────────────────────────

        /// <summary>
        /// Abre selector de imagen, muestra diálogo de ajuste de tamaño
        /// con vista previa y opción de mantener proporciones,
        /// luego inserta la imagen escalada en el RichTextBox.
        /// </summary>
        void InsertImage()
        {
            string path = null;
            var t = new Thread(() =>
            {
                using var dlg = new OpenFileDialog
                {
                    Title = "Insertar imagen",
                    Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff|Todos|*.*",
                };
                if (dlg.ShowDialog() == DialogResult.OK) path = dlg.FileName;
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (path == null) return;

            Image original;
            try { original = Image.FromFile(path); }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo cargar la imagen:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Diálogo de ajuste de tamaño
            using var sf = new Form
            {
                Text = "Ajustar tamaño",
                ClientSize = new Size(380, 260),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(245, 245, 248),
                AutoScaleMode = AutoScaleMode.None,
            };

            var preview = new PictureBox
            {
                Left = 220,
                Top = 16,
                Width = 130,
                Height = 120,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(230, 230, 235),
                Image = original,
            };
            sf.Controls.Add(preview);

            sf.Controls.Add(new Label
            {
                Text = "Original:  " + original.Width + " × " + original.Height + " px",
                Left = 16,
                Top = 16,
                Width = 200,
                Height = 18,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                BackColor = Color.Transparent,
            });

            sf.Controls.Add(new Label
            {
                Text = "Ancho (px):",
                Left = 16,
                Top = 48,
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.Transparent,
            });
            var nudW = new NumericUpDown
            {
                Left = 16,
                Top = 68,
                Width = 110,
                Minimum = 10,
                Maximum = 2000,
                Value = Math.Min(original.Width, 500),
                Font = new Font("Segoe UI", 9.5f),
            };
            sf.Controls.Add(nudW);

            sf.Controls.Add(new Label
            {
                Text = "Alto (px):",
                Left = 16,
                Top = 104,
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.Transparent,
            });
            var nudH = new NumericUpDown
            {
                Left = 16,
                Top = 124,
                Width = 110,
                Minimum = 10,
                Maximum = 2000,
                Value = (int)((double)original.Height * Math.Min(original.Width, 500) / Math.Max(original.Width, 1)),
                Font = new Font("Segoe UI", 9.5f),
            };
            sf.Controls.Add(nudH);

            var chk = new CheckBox
            {
                Text = "Mantener proporciones",
                Left = 16,
                Top = 158,
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.Transparent,
            };
            sf.Controls.Add(chk);

            bool _updating = false;
            nudW.ValueChanged += (_, __) =>
            {
                if (_updating || !chk.Checked || original.Width == 0) return;
                _updating = true;
                nudH.Value = Math.Max(10, (int)((double)original.Height * (double)nudW.Value / original.Width));
                _updating = false;
            };
            nudH.ValueChanged += (_, __) =>
            {
                if (_updating || !chk.Checked || original.Height == 0) return;
                _updating = true;
                nudW.Value = Math.Max(10, (int)((double)original.Width * (double)nudH.Value / original.Height));
                _updating = false;
            };

            sf.Controls.Add(new Label { Left = 0, Top = 186, Width = 380, Height = 1, BackColor = C_BORDER });

            var btnCan = new Button
            {
                Text = "Cancelar",
                Left = 168,
                Top = 198,
                Width = 90,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(225, 225, 230),
                ForeColor = C_TEXT,
                DialogResult = DialogResult.Cancel,
                Font = new Font("Segoe UI", 9f),
            };
            btnCan.FlatAppearance.BorderSize = 0;

            var btnOk = new Button
            {
                Text = "Insertar",
                Left = 266,
                Top = 198,
                Width = 90,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_ACCENT,
                ForeColor = Color.White,
                DialogResult = DialogResult.OK,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };
            btnOk.FlatAppearance.BorderSize = 0;

            sf.Controls.AddRange(new Control[] { btnCan, btnOk });
            sf.AcceptButton = btnOk;
            sf.CancelButton = btnCan;

            if (sf.ShowDialog(this) != DialogResult.OK) { original.Dispose(); return; }

            try
            {
                using var bmp = new Bitmap(original, (int)nudW.Value, (int)nudH.Value);
                original.Dispose();
                Clipboard.SetImage(bmp);
                _rtb.Paste();
                _isDirty = true;
                UpdateInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al insertar imagen:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Tabla ASCII ───────────────────────────────────────────────

        /// <summary>Abre un diálogo bien proporcionado para insertar una tabla ASCII.</summary>
        void InsertTable()
        {
            using var frm = new Form
            {
                Text = "Insertar tabla",
                ClientSize = new Size(320, 200),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(245, 245, 248),
                AutoScaleMode = AutoScaleMode.None,
            };

            frm.Controls.Add(new Label
            {
                Text = "Nueva tabla",
                Left = 16,
                Top = 16,
                Width = 288,
                Height = 22,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(28, 28, 30),
                BackColor = Color.Transparent,
            });

            frm.Controls.Add(new Label
            {
                Text = "Filas:",
                Left = 16,
                Top = 56,
                Width = 80,
                Height = 18,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(60, 60, 65),
            });
            var nudR = new NumericUpDown
            {
                Left = 110,
                Top = 52,
                Width = 80,
                Height = 26,
                Minimum = 1,
                Maximum = 30,
                Value = 3,
                Font = new Font("Segoe UI", 10f),
            };
            frm.Controls.Add(nudR);

            frm.Controls.Add(new Label
            {
                Text = "Columnas:",
                Left = 16,
                Top = 94,
                Width = 90,
                Height = 18,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(60, 60, 65),
            });
            var nudC = new NumericUpDown
            {
                Left = 110,
                Top = 90,
                Width = 80,
                Height = 26,
                Minimum = 1,
                Maximum = 20,
                Value = 3,
                Font = new Font("Segoe UI", 10f),
            };
            frm.Controls.Add(nudC);

            frm.Controls.Add(new Label
            {
                Left = 0,
                Top = 138,
                Width = 320,
                Height = 1,
                BackColor = Color.FromArgb(218, 218, 223),
            });

            var btnCan = new Button
            {
                Text = "Cancelar",
                Left = 112,
                Top = 152,
                Width = 90,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel,
                BackColor = Color.FromArgb(225, 225, 230),
                ForeColor = Color.FromArgb(28, 28, 30),
                Font = new Font("Segoe UI", 9f),
            };
            btnCan.FlatAppearance.BorderSize = 0;

            var btnOk = new Button
            {
                Text = "Insertar",
                Left = 210,
                Top = 152,
                Width = 94,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK,
                BackColor = C_ACCENT,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 99, 220);

            frm.Controls.AddRange(new Control[] { btnCan, btnOk });
            frm.AcceptButton = btnOk;
            frm.CancelButton = btnCan;

            if (frm.ShowDialog(this) != DialogResult.OK) return;

            int rows = (int)nudR.Value, cols = (int)nudC.Value;
            var sb = new StringBuilder("\n");
            string div = "+" + string.Join("+", Enumerable.Repeat("----------", cols)) + "+\n";
            string row = "|" + string.Join("|", Enumerable.Repeat("          ", cols)) + "|\n";
            sb.Append(div);
            for (int r = 0; r < rows; r++) sb.Append(row).Append(div);
            _rtb.SelectedText = sb.ToString();
        }
        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════

        void UpdateToolbarState()
        {
            // El estado visual real lo maneja UpdateToolbarStateTs() via ToolStrip
            // Este método se mantiene por compatibilidad
            try { UpdateToolbarStateTs(); } catch { }
        }

        void UpdateInfo()
        {
            if (_rtb == null || _lblInfo == null) return;
            int words = _rtb.Text.Split(new[] { ' ', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries).Length;
            _lblInfo.Text = _rtb.Lines.Length + " líneas  ·  " + words +
                                 " palabras  ·  " + _rtb.TextLength + " chars" +
                                 (_isDirty ? "  *" : "");
            _lblInfo.ForeColor = C_SUB;
        }

        void OnKeyDown(object s, KeyEventArgs e)
        {
            if (!e.Control) return;
            switch (e.KeyCode)
            {
                case Keys.S: SaveFile(); e.SuppressKeyPress = true; break;
                case Keys.B: ToggleStyle(FontStyle.Bold); e.SuppressKeyPress = true; break;
                case Keys.I: ToggleStyle(FontStyle.Italic); e.SuppressKeyPress = true; break;
                case Keys.U: ToggleStyle(FontStyle.Underline); e.SuppressKeyPress = true; break;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isDirty)
            {
                var r = MessageBox.Show(
                    "¿Guardar cambios antes de cerrar?", "Cambios sin guardar",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Yes) SaveFile();
                else if (r == DialogResult.Cancel) { e.Cancel = true; return; }
            }
            base.OnFormClosing(e);
        }
    }
}
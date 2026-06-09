using FileExplorer.Helpers;
using System.Drawing.Imaging;
using System.Net;

namespace FileExplorer.Forms
{
    public partial class ImageViewerForm
    {
        /// <summary>
        /// Abre un SaveFileDialog STA y guarda la imagen editada en PNG, JPEG o BMP
        /// según la extensión elegida por el usuario.
        /// </summary>
        void SaveImageAs()
        {
            if (_edited == null) return;
            string fn = null;
            var t = new Thread(() =>
            {
                using var d = new SaveFileDialog
                {
                    FileName = Path.GetFileNameWithoutExtension(_allImages[_currentIdx]) + "_editado",
                    Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp"
                };
                if (d.ShowDialog() == DialogResult.OK) fn = d.FileName;
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (fn == null) return;
            var ext = Path.GetExtension(fn).ToLowerInvariant();
            _edited.Save(fn, ext == ".jpg" ? ImageFormat.Jpeg : ext == ".bmp" ? ImageFormat.Bmp : ImageFormat.Png);
            MessageBox.Show("Imagen guardada.", "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Muestra el diálogo de conversión y redimensionado de imagen.
        /// Permite elegir formato de salida (PNG/JPEG/BMP/TIFF), dimensiones personalizadas
        /// o presets, calidad JPEG y proporciones bloqueadas. Guarda de forma asíncrona.
        /// </summary>
        void ShowImageConvertDialog()
        {
            if (_original == null) { MessageBox.Show("Carga una imagen primero."); return; }
            int ow = _original.Width, oh = _original.Height;
            string path = _allImages[_currentIdx];

            var frm = new Form
            {
                Text = "Convertir / Redimensionar",
                StartPosition = FormStartPosition.CenterParent,
                BackColor = C_BG,
                Font = new Font("Segoe UI", 9.5f),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                Width = 500,
                Height = 520,
            };

            var btnOk = new Button
            {
                Text = "CONVERTIR Y GUARDAR",
                Left = 16,
                Top = 16,
                Width = 452,
                Height = 60,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_ACCENT,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            btnOk.FlatAppearance.BorderSize = 0;
            frm.Controls.Add(btnOk);

            int y = 90;
            void Lbl(string t, Color? c = null)
            {
                frm.Controls.Add(new Label { Text = t, Left = 16, Top = y, AutoSize = true, ForeColor = c ?? C_TEXT, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9f) });
                y += 22;
            }

            Lbl($"Archivo: {Path.GetFileName(path)}", C_SUB);
            Lbl($"Original: {ow} x {oh} px", C_SUB);
            y += 4;
            Lbl("Formato de salida:");
            var cboFmt = new ComboBox { Left = 16, Top = y, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(52, 52, 58), ForeColor = C_TEXT };
            cboFmt.Items.AddRange(new object[] { "PNG (.png)", "JPEG (.jpg)", "BMP (.bmp)", "TIFF (.tiff)" });
            cboFmt.SelectedIndex = 1; frm.Controls.Add(cboFmt); y += 34;

            Lbl("Ancho (px):");
            var nudW = new NumericUpDown { Left = 16, Top = y, Width = 130, Minimum = 1, Maximum = 16000, Value = ow, BackColor = Color.FromArgb(52, 52, 58), ForeColor = C_TEXT };
            frm.Controls.Add(new Label { Text = "Alto (px):", Left = 160, Top = y - 22, AutoSize = true, ForeColor = C_TEXT, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9f) });
            var nudH = new NumericUpDown { Left = 160, Top = y, Width = 130, Minimum = 1, Maximum = 16000, Value = oh, BackColor = Color.FromArgb(52, 52, 58), ForeColor = C_TEXT };
            var chk = new CheckBox { Left = 300, Top = y + 4, AutoSize = true, Text = "Prop.", Checked = true, ForeColor = C_SUB, BackColor = Color.Transparent };
            frm.Controls.AddRange(new Control[] { nudW, nudH, chk });
            // Mantener proporciones al cambiar el ancho
            nudW.ValueChanged += (_, __) => { if (chk.Checked && ow > 0) nudH.Value = Math.Max(1, (int)(nudW.Value * oh / ow)); };
            y += 34;

            Lbl("Presets:");
            int bx = 16;
            foreach (var (lb, pw, ph) in new (string, int, int)[] { ("Original", ow, oh), ("HD 720p", 1280, 720), ("Web 800", 800, 600), ("256x256", 256, 256) })
            {
                var tv = pw; var th = ph;
                var pb = new Button { Text = lb, Left = bx, Top = y, Width = 96, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(52, 52, 58), ForeColor = C_TEXT, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 8.5f) };
                pb.FlatAppearance.BorderSize = 0;
                pb.Click += (_, __) => { nudW.Value = Math.Min(nudW.Maximum, tv); nudH.Value = Math.Min(nudH.Maximum, th); };
                frm.Controls.Add(pb); bx += 100;
            }
            y += 36;

            frm.Controls.Add(new Label { Text = "Calidad JPEG:", Left = 16, Top = y, AutoSize = true, ForeColor = C_TEXT, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9f) });
            var lblQ = new Label { Text = "90%", Left = 140, Top = y, AutoSize = true, ForeColor = C_ACCENT, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            frm.Controls.Add(lblQ); y += 22;
            var trkQ = new TrackBar { Left = 16, Top = y, Width = 452, Height = 32, Minimum = 10, Maximum = 100, Value = 90, TickStyle = TickStyle.None, BackColor = C_BG };
            trkQ.ValueChanged += (_, __) => lblQ.Text = trkQ.Value + "%";
            frm.Controls.Add(trkQ);

            // Guardar: escala con HighQualityBicubic, aplica parámetro de calidad para JPEG
            btnOk.Click += async (_, __) =>
            {
                string[] exts = { ".png", ".jpg", ".bmp", ".tiff" };
                ImageFormat[] fmts = { ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.Bmp, ImageFormat.Tiff };
                string ext2 = exts[cboFmt.SelectedIndex];
                var fmt2 = fmts[cboFmt.SelectedIndex];
                string dest = null;
                var thr = new Thread(() =>
                {
                    using var d = new SaveFileDialog
                    {
                        FileName = Path.GetFileNameWithoutExtension(path) + "_conv" + ext2,
                        Filter = $"{fmt2.ToString().ToUpper()}|*{ext2}|Todos|*.*",
                        InitialDirectory = Path.GetDirectoryName(path)
                    };
                    if (d.ShowDialog() == DialogResult.OK) dest = d.FileName;
                });
                thr.SetApartmentState(ApartmentState.STA); thr.Start(); thr.Join();
                if (dest == null) return;

                btnOk.Enabled = false; btnOk.Text = "Procesando...";
                int w2 = (int)nudW.Value, h2 = (int)nudH.Value, q = trkQ.Value;

                await Task.Run(() =>
                {
                    using var bmp2 = new Bitmap(w2, h2);
                    using var g2 = Graphics.FromImage(bmp2);
                    g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g2.DrawImage(_original, 0, 0, w2, h2);
                    if (fmt2 == ImageFormat.Jpeg)
                    {
                        var enc = ImageCodecInfo.GetImageEncoders().First(c2 => c2.FormatID == ImageFormat.Jpeg.Guid);
                        var ep = new EncoderParameters(1);
                        ep.Param[0] = new EncoderParameter(Encoder.Quality, (long)q);
                        bmp2.Save(dest, enc, ep);
                    }
                    else bmp2.Save(dest, fmt2);
                });

                MessageBox.Show($"Guardado:\n{dest}", "Listo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                frm.Close();
            };

            frm.ShowDialog(this);
        }

        // ════════════════════════════════════════════════════════════
        //  GPS
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Muestra el diálogo para agregar o reemplazar coordenadas GPS en la imagen actual.
        /// Permite buscar un lugar por nombre vía Nominatim (OpenStreetMap), pegar coordenadas
        /// de Google Maps manualmente, o abrir Google Maps para copiarlas.
        /// Al confirmar, escribe los datos EXIF y recarga la imagen.
        /// </summary>
        void ShowAddGpsDialog()
        {
            string path = _allImages[_currentIdx];
            var frm = new Form
            {
                Text = "Agregar coordenadas GPS",
                Size = new Size(460, 400),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(36, 36, 40),
                ForeColor = Color.FromArgb(242, 242, 247),
                Font = new Font("Segoe UI", 9.5f),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lblSearch = new Label { Text = "Buscar lugar o direccion:", Left = 20, Top = 18, Width = 400, Height = 18, ForeColor = Color.FromArgb(200, 200, 210), BackColor = Color.Transparent };
            var txtSearch = new TextBox { Left = 20, Top = 40, Width = 300, Height = 26, Font = new Font("Segoe UI", 10f), PlaceholderText = "Ej: Torre Latinoamericana, CDMX", BackColor = Color.FromArgb(52, 52, 58), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            var btnSearch = new Button { Text = "Buscar", Left = 326, Top = 40, Width = 100, Height = 26, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(72, 72, 76), ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9f) };
            btnSearch.FlatAppearance.BorderSize = 0;
            var btnGM = new Button { Text = "Abrir Google Maps", Left = 20, Top = 72, Width = 200, Height = 26, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(10, 132, 255), ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9f) };
            btnGM.FlatAppearance.BorderSize = 0;
            btnGM.Click += (_, __) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://maps.google.com") { UseShellExecute = true });

            var lblHint = new Label { Text = "(busca, clic derecho, Copiar coordenadas, pega abajo)", Left = 228, Top = 76, Width = 210, Height = 18, ForeColor = Color.FromArgb(100, 100, 110), BackColor = Color.Transparent, Font = new Font("Segoe UI", 7.5f) };
            var lblResult = new Label { Left = 20, Top = 102, Width = 420, Height = 18, ForeColor = Color.FromArgb(48, 209, 88), BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f) };
            var sep = new Label { Left = 20, Top = 126, Width = 420, Height = 1, BackColor = Color.FromArgb(68, 68, 76) };

            frm.Controls.Add(new Label { Text = "Latitud:", Left = 20, Top = 138, Width = 80, Height = 18, ForeColor = Color.FromArgb(142, 142, 148), BackColor = Color.Transparent });
            var txtNewLat = new TextBox { Left = 20, Top = 158, Width = 400, Height = 26, Font = new Font("Consolas", 10f), BackColor = Color.FromArgb(52, 52, 58), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            frm.Controls.Add(new Label { Text = "Longitud:", Left = 20, Top = 194, Width = 80, Height = 18, ForeColor = Color.FromArgb(142, 142, 148), BackColor = Color.Transparent });
            var txtNewLon = new TextBox { Left = 20, Top = 214, Width = 400, Height = 26, Font = new Font("Consolas", 10f), BackColor = Color.FromArgb(52, 52, 58), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            frm.Controls.Add(new Label { Text = "Pega coordenadas de Google Maps.", Left = 20, Top = 250, Width = 420, Height = 18, ForeColor = Color.FromArgb(100, 180, 100), BackColor = Color.Transparent, Font = new Font("Segoe UI", 8f) });

            var btnSave = new Button { Text = "Guardar GPS", Left = 20, Top = 278, Width = 160, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(10, 132, 255), ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9.5f) };
            btnSave.FlatAppearance.BorderSize = 0;
            var btnCan = new Button { Text = "Cancelar", Left = 190, Top = 278, Width = 100, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(72, 72, 76), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnCan.FlatAppearance.BorderSize = 0;
            btnCan.Click += (_, __) => frm.Close();

            // Búsqueda geocoding vía Nominatim
            btnSearch.Click += async (_, __) =>
            {
                string q = txtSearch.Text.Trim(); if (string.IsNullOrEmpty(q)) return;
                btnSearch.Enabled = false; btnSearch.Text = "...";
                lblResult.Text = "Buscando..."; lblResult.ForeColor = Color.FromArgb(142, 142, 148);
                try
                {
                    string url = "https://nominatim.openstreetmap.org/search?q=" + Uri.EscapeDataString(q) + "&format=json&limit=1&addressdetails=0";
                    using var client = new System.Net.Http.HttpClient();
                    client.DefaultRequestHeaders.Add("User-Agent", "FileExplorer/1.0");
                    var json = await client.GetStringAsync(url);
                    var mLat = System.Text.RegularExpressions.Regex.Match(json, "\"lat\":\"([^\"]+)\"");
                    var mLon = System.Text.RegularExpressions.Regex.Match(json, "\"lon\":\"([^\"]+)\"");
                    var mName = System.Text.RegularExpressions.Regex.Match(json, "\"display_name\":\"([^\"]+)\"");
                    if (mLat.Success && mLon.Success
                        && double.TryParse(mLat.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lat)
                        && double.TryParse(mLon.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lon))
                    {
                        string dn = mName.Success ? mName.Groups[1].Value.Replace("\\u002C", ",") : q;
                        if (dn.Length > 60) dn = dn[..60] + "...";
                        txtNewLat.Text = lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                        txtNewLon.Text = lon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                        lblResult.Text = "✓ " + dn;
                        lblResult.ForeColor = Color.FromArgb(48, 209, 88);
                    }
                    else { lblResult.Text = "No encontrado"; lblResult.ForeColor = Color.FromArgb(255, 59, 48); }
                }
                catch { lblResult.Text = "Sin conexion"; lblResult.ForeColor = Color.FromArgb(255, 59, 48); }
                finally { btnSearch.Enabled = true; btnSearch.Text = "Buscar"; }
            };
            txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; btnSearch.PerformClick(); } };

            // Validar y escribir EXIF GPS en background
            btnSave.Click += (_, __) =>
            {
                // Limpiar formatos varios: grados, comas como decimales, letras N/S/E/W
                string Clean(string raw) => raw.Replace("°", "").Replace(",", ".").Replace(" ", "")
                    .Replace("N", "").Replace("S", "").Replace("E", "").Replace("W", "").Replace("O", "").Trim();
                if (!double.TryParse(Clean(txtNewLat.Text), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lat) || lat < -90 || lat > 90)
                { MessageBox.Show("Latitud invalida.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (!double.TryParse(Clean(txtNewLon.Text), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lon) || lon < -180 || lon > 180)
                { MessageBox.Show("Longitud invalida.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                frm.Close();
                Task.Run(() =>
                {
                    try
                    {
                        Invoke(new Action(() => { pic.Image = null; _original?.Dispose(); _original = null; _edited?.Dispose(); _edited = null; }));
                        WriteGps(path, lat, lon);
                        Invoke(new Action(() =>
                        {
                            _original = new Bitmap(path); _edited = (Bitmap)_original.Clone();
                            pic.SizeMode = PictureBoxSizeMode.Zoom; pic.Image = _edited;
                            txtLat.Text = lat.ToString("F6") + "°";
                            txtLon.Text = lon.ToString("F6") + "°";
                            lblMapMsg.Text = "Haz clic en el mapa para abrir en el navegador";
                            mapPanel.LoadLocation(lat, lon); btnAddGps.Visible = false;
                            MessageBox.Show("GPS guardado.", "GPS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }));
                    }
                    catch (Exception ex) { Invoke(new Action(() => MessageBox.Show("Error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error))); }
                });
            };

            frm.Controls.AddRange(new Control[] { lblSearch, txtSearch, btnSearch, btnGM, lblHint, lblResult, sep, txtNewLat, txtNewLon, btnSave, btnCan });
            frm.ShowDialog(this);
        }

        /// <summary>
        /// Escribe las coordenadas GPS como etiquetas EXIF en el archivo JPEG indicado.
        /// Convierte grados decimales a grados/minutos/segundos (racional) y usa un archivo
        /// temporal para no corromper el original si el guardado falla.
        /// </summary>
        static void WriteGps(string path, double lat, double lon)
        {
            string tmp = path + ".gpstmp";
            try
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                using (var bmp = new Bitmap(ms))
                {
                    // Crea un PropertyItem racional (tipo 5) con grados/minutos/segundos
                    static PropertyItem MkRat(int id, double deg, double min, double sec)
                    {
                        var p = (PropertyItem)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PropertyItem));
                        p.Id = id; p.Type = 5; var b = new byte[24];
                        void W(int o, uint n, uint d) { BitConverter.GetBytes(n).CopyTo(b, o); BitConverter.GetBytes(d).CopyTo(b, o + 4); }
                        W(0, (uint)Math.Abs(deg), 1); W(8, (uint)(min * 100), 100); W(16, (uint)(sec * 1000), 1000);
                        p.Value = b; p.Len = b.Length; return p;
                    }
                    // Crea un PropertyItem de referencia ASCII (N/S/E/W)
                    static PropertyItem MkRef(int id, string r)
                    {
                        var p = (PropertyItem)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PropertyItem));
                        p.Id = id; p.Type = 2; p.Value = System.Text.Encoding.ASCII.GetBytes(r + "\0"); p.Len = p.Value.Length; return p;
                    }

                    // Descomponer grados decimales a DMS
                    double al = Math.Abs(lat), ld = Math.Floor(al), lm = Math.Floor((al - ld) * 60), ls = ((al - ld) * 60 - lm) * 60;
                    double ao = Math.Abs(lon), od = Math.Floor(ao), om = Math.Floor((ao - od) * 60), os = ((ao - od) * 60 - om) * 60;

                    bmp.SetPropertyItem(MkRef(0x0001, lat >= 0 ? "N" : "S")); bmp.SetPropertyItem(MkRat(0x0002, ld, lm, ls));
                    bmp.SetPropertyItem(MkRef(0x0003, lon >= 0 ? "E" : "W")); bmp.SetPropertyItem(MkRat(0x0004, od, om, os));
                    bmp.Save(tmp, ImageFormat.Jpeg);
                }
                File.Delete(path); File.Move(tmp, path);
            }
            catch { if (File.Exists(tmp)) File.Delete(tmp); throw; }
        }

        /// <summary>
        /// Abre la imagen actual en Google Maps o OpenStreetMap según el parámetro.
        /// Si la imagen no tiene coordenadas GPS muestra un aviso.
        /// </summary>
        void OpenInMaps(bool osm)
        {
            var gps = GpsHelper.TryRead(_allImages[_currentIdx]);
            if (gps == null || !gps.HasData) { MessageBox.Show("Sin coordenadas GPS."); return; }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                osm ? gps.OpenStreetMapUrl : gps.GoogleMapsUrl)
            { UseShellExecute = true });
        }
    }

    // ── OsmMapPanel ──────────────────────────────────────────────────

    /// <summary>
    /// Panel que descarga y muestra un tile de mapa (CartoCDN o OpenStreetMap)
    /// con un marcador en la ubicación indicada. La descarga se hace en background.
    /// </summary>
    public class OsmMapPanel : Panel
    {
        double _lat, _lon;
        Bitmap _tile = null;
        string _status = "Sin coordenadas GPS";

        static readonly Color BG = Color.FromArgb(28, 28, 32);
        static readonly Color SUB = Color.FromArgb(130, 130, 140);
        static readonly Color BLUE = Color.FromArgb(180, 220, 255);

        public OsmMapPanel() { BackColor = BG; DoubleBuffered = true; Cursor = Cursors.Hand; }

        /// <summary>
        /// Descarga el tile de mapa en zoom 15 para las coordenadas dadas y dibuja el marcador.
        /// Intenta primero CartoCDN (voyager) y hace fallback a OSM si falla.
        /// </summary>
        public void LoadLocation(double lat, double lon)
        {
            _lat = lat; _lon = lon; _tile = null; _status = "⏳ Cargando mapa..."; Invalidate();
            var t = new Thread(() =>
            {
                try
                {
                    int zoom = 15;
                    int tx = (int)Math.Floor((lon + 180.0) / 360.0 * Math.Pow(2, zoom));
                    double lr = lat * Math.PI / 180.0;
                    int ty = (int)Math.Floor((1.0 - Math.Log(Math.Tan(lr) + 1.0 / Math.Cos(lr)) / Math.PI) / 2.0 * Math.Pow(2, zoom));

                    byte[] bytes = null;
                    string[] cs = { "a", "b", "c", "d" };
                    try
                    {
                        string url = $"https://{cs[new Random().Next(cs.Length)]}.basemaps.cartocdn.com/rastertiles/voyager/{zoom}/{tx}/{ty}.png";
                        using var wc = new WebClient(); wc.Headers["User-Agent"] = "FileExplorer/1.0";
                        bytes = wc.DownloadData(url);
                    }
                    catch
                    {
                        // Fallback a servidores OSM
                        foreach (var srv in new[] { "a", "b", "c" })
                        {
                            try
                            {
                                string u2 = $"https://{srv}.tile.openstreetmap.org/{zoom}/{tx}/{ty}.png";
                                using var wc = new WebClient(); wc.Headers["User-Agent"] = "FileExplorer/1.0";
                                bytes = wc.DownloadData(u2); break;
                            }
                            catch { }
                        }
                    }

                    if (bytes == null || bytes.Length == 0) { SetStatus($"📍 {lat:F5}°\n{lon:F5}°\n\nSin conexion"); return; }

                    Bitmap bmp;
                    using (var ms = new MemoryStream(bytes)) { var tmp = new Bitmap(ms); bmp = new Bitmap(tmp); tmp.Dispose(); }

                    // Calcular posición en píxeles del punto dentro del tile
                    double tc = Math.Pow(2, zoom); double lr2 = lat * Math.PI / 180.0;
                    int px = (int)(((lon + 180.0) / 360.0 * tc - tx) * 256);
                    int py = (int)(((1.0 - Math.Log(Math.Tan(lr2) + 1.0 / Math.Cos(lr2)) / Math.PI) / 2.0 * tc - ty) * 256);

                    // Dibujar marcador: sombra, palo, cabeza roja, punto blanco
                    using var g = Graphics.FromImage(bmp);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.FillEllipse(new SolidBrush(Color.FromArgb(50, 0, 0, 0)), px - 6, py + 8, 12, 5);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(200, 20, 20)), px - 2, py - 18, 4, 18);
                    g.FillEllipse(new SolidBrush(Color.FromArgb(220, 20, 20)), px - 9, py - 36, 18, 18);
                    g.FillEllipse(new SolidBrush(Color.White), px - 4, py - 31, 8, 8);
                    g.DrawEllipse(new Pen(Color.FromArgb(140, 10, 10), 1.5f), px - 9, py - 36, 18, 18);

                    if (IsDisposed) return;
                    Invoke(new Action(() => { _tile = bmp; _status = ""; Invalidate(); }));
                }
                catch (Exception ex) { SetStatus($"📍 {lat:F5}°\n{lon:F5}°\n\nError: {ex.Message[..Math.Min(40, ex.Message.Length)]}"); }
            });
            t.IsBackground = true; t.Start();
        }

        /// <summary>Limpia el tile y muestra el mensaje por defecto sin coordenadas.</summary>
        public void ClearMap() { _tile = null; _status = "Sin coordenadas GPS"; Invalidate(); }

        /// <summary>Actualiza el mensaje de estado desde cualquier hilo de forma segura.</summary>
        void SetStatus(string msg)
        {
            if (IsDisposed) return;
            try { Invoke(new Action(() => { _status = msg; _tile = null; Invalidate(); })); } catch { }
        }

        /// <summary>
        /// Dibuja el tile descargado escalado al tamaño del panel,
        /// o el mensaje de estado si aún no hay tile disponible.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillRectangle(new SolidBrush(BG), ClientRectangle);

            if (_tile != null)
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(_tile, 0, 0, Width, Height);
            }
            else
            {
                using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(_status, new Font("Segoe UI", 9f),
                    new SolidBrush(string.IsNullOrEmpty(_status) ? SUB : BLUE),
                    new RectangleF(0, 0, Width, Height), fmt);
            }

            using var pen = new Pen(Color.FromArgb(68, 68, 76), 1);
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
using FileExplorer.Helpers;
using FileExplorer.Models;
using System.Drawing.Imaging;
using System.Net;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Visor de imágenes con soporte para edición, filtros, zoom, recorte,
    /// dibujo a mano, texto superpuesto y coordenadas GPS EXIF.
    /// </summary>
    public partial class ImageViewerForm : Form
    {
        // ── Estado de la imagen ──────────────────────────────────────
        readonly List<string> _allImages;
        int _currentIdx;
        Bitmap _original, _edited;
        float _brightness = 0f, _contrast = 1f;
        bool _grayscale = false, _sepia = false;
        float _zoom = 1.0f;

        static readonly float[] IdentityRow = { 0f, 0f, 0f, 1f, 0f };

        // ── Estado del recorte ───────────────────────────────────────
        bool _cropMode = false;
        bool _cropConfirmed = false;
        Point _cropStart;
        Rectangle _cropRect;
        int _cropDragHandle = -1;
        Point _cropDragStart;
        Rectangle _cropRectAtDragStart;

        // ── Estado del dibujo ────────────────────────────────────────
        bool _drawMode = false;
        bool _isDrawing = false;
        Point _lastDrawPt;
        Color _drawColor = Color.FromArgb(255, 30, 30);
        int _brushSize = 4;

        // ── Estado del texto ─────────────────────────────────────────
        Color _textColor = Color.White;
        float _fontSize = 24f;
        string _fontFamily = "Segoe UI";
        bool _textBold = false;

        /// <summary>
        /// Inicializa el visor con la imagen inicial y la lista completa de imágenes de la carpeta.
        /// </summary>
        public ImageViewerForm(string startFile, List<string> allImages)
        {
            _allImages = allImages;
            _currentIdx = allImages.IndexOf(startFile);
            if (_currentIdx < 0) { _allImages.Insert(0, startFile); _currentIdx = 0; }

            InitializeComponent();
            Shown += (_, __) => LoadCurrentImage();
        }

        // ── Carga y miniaturas ───────────────────────────────────────

        /// <summary>
        /// Carga la imagen actual desde disco usando MemoryStream para no bloquear el archivo,
        /// permitiendo renombrar o mover la imagen mientras está abierta en el visor.
        /// Actualiza metadatos y reconstruye las miniaturas.
        /// </summary>
        void LoadCurrentImage()
        {
            var path = _allImages[_currentIdx];

            if (IsCloudFile(path))
            {
                if (MessageBox.Show("Archivo en la nube. ¿Descargar ahora?", "OneDrive",
                    MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                try { using var fs = File.OpenRead(path); fs.ReadByte(); } catch { }
                if (IsCloudFile(path)) { MessageBox.Show("Aún descargándose."); return; }
            }

            DisposeCurrentBitmaps();
            ResetEditState();

            try
            {
                // Cargar via MemoryStream para liberar el lock sobre el archivo
                // Esto permite renombrar/mover/eliminar la imagen mientras está abierta
                var bytes = File.ReadAllBytes(path);
                _original = new Bitmap(new MemoryStream(bytes));
                _edited = (Bitmap)_original.Clone();
            }
            catch (Exception ex) { MessageBox.Show("No se pudo abrir: " + ex.Message); return; }

            UpdateFileInfo(path);
            UpdateGpsPanel(path);

            pic.SizeMode = PictureBoxSizeMode.Zoom;
            pic.Image = _edited;
            pic.Invalidate();
            BuildThumbs();
        }

        /// <summary>Libera los bitmaps actualmente cargados en memoria.</summary>
        void DisposeCurrentBitmaps()
        {
            _original?.Dispose(); _edited?.Dispose();
            _original = null; _edited = null;
        }

        /// <summary>Restablece los controles de edición y los valores de estado al cargar una nueva imagen.</summary>
        void ResetEditState()
        {
            _brightness = 0f; _contrast = 1f;
            _grayscale = false; _sepia = false;
            _zoom = 1f;
            if (trkBright != null) trkBright.Value = 0;
            if (trkContr != null) trkContr.Value = 10;
        }

        /// <summary>Actualiza los labels de nombre, dimensiones y tamaño de archivo.</summary>
        void UpdateFileInfo(string path)
        {
            var fi = new FileInfo(path);
            lblTopName.Text = Path.GetFileName(path);
            lblInfoName.Text = Path.GetFileName(path);
            lblDim.Text = $"{_original.Width:N0} × {_original.Height:N0} px";
            lblSize.Text = FileHelper.FormatSize(fi.Length);
            Text = "Visor — " + Path.GetFileName(path);
        }

        /// <summary>Lee las coordenadas GPS de la imagen y actualiza el panel lateral y el mapa.</summary>
        void UpdateGpsPanel(string path)
        {
            var gps = GpsHelper.TryRead(_original);
            if (gps != null && gps.HasData)
            {
                txtLat.Text = gps.LatitudeText;
                txtLon.Text = gps.LongitudeText;
                lblMapMsg.Text = "Haz clic en el mapa para abrir en el navegador";
                mapPanel.LoadLocation(gps.Latitude, gps.Longitude);
                if (btnAddGps != null) btnAddGps.Visible = false;
            }
            else
            {
                txtLat.Text = "—"; txtLon.Text = "—";
                lblMapMsg.Text = "Sin coordenadas GPS";
                mapPanel.ClearMap();
                if (btnAddGps != null)
                {
                    btnAddGps.Visible = true;
                    btnAddGps.Enabled = true;
                    btnAddGps.Text = "📍  Agregar coordenadas GPS";
                    btnAddGps.BackColor = Color.FromArgb(52, 58, 52);
                    btnAddGps.ForeColor = Color.FromArgb(48, 209, 88);
                }
            }
        }

        /// <summary>Reconstruye la tira de miniaturas resaltando la imagen actual.</summary>
        void BuildThumbs()
        {
            foreach (Control c in strip.Controls)
                if (c is Panel p)
                    foreach (Control cc in p.Controls)
                        if (cc is PictureBox pb) { pb.Image?.Dispose(); pb.Image = null; }
            strip.Controls.Clear();

            int x = 8;
            for (int i = 0; i < _allImages.Count; i++)
            {
                int idx = i;
                bool sel = idx == _currentIdx;

                var card = new Panel
                {
                    Left = x,
                    Top = sel ? 6 : 10,
                    Width = sel ? 72 : 66,
                    Height = sel ? 76 : 68,
                    BackColor = sel ? C_ACCENT : C_CARD,
                    Cursor = Cursors.Hand
                };
                var pb2 = new PictureBox
                {
                    Left = sel ? 3 : 2,
                    Top = sel ? 3 : 2,
                    Width = sel ? 66 : 62,
                    Height = sel ? 70 : 64,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent
                };

                try
                {
                    // También usar MemoryStream en miniaturas para no bloquear archivos
                    var b = File.ReadAllBytes(_allImages[i]);
                    using var ms = new MemoryStream(b);
                    pb2.Image = new Bitmap(new Bitmap(ms));
                }
                catch { }

                EventHandler click = (_, __) => { _currentIdx = idx; LoadCurrentImage(); };
                pb2.Click += click;
                card.Click += click;
                card.Controls.Add(pb2);
                strip.Controls.Add(card);
                x += (sel ? 72 : 66) + 4;
            }
        }

        // ── Zoom / Navegación ────────────────────────────────────────

        /// <summary>Establece el nivel de zoom; con 0 ajusta automáticamente al tamaño del panel.</summary>
        void SetZoom(float z)
        {
            if (z <= 0f) { _zoom = 1f; pic.SizeMode = PictureBoxSizeMode.Zoom; pic.Image = _edited; }
            else { _zoom = Math.Max(0.1f, Math.Min(8f, z)); ApplyZoom(); }
        }

        /// <summary>Incrementa o decrementa el zoom actual en el delta indicado.</summary>
        void ChangeZoom(float d) => SetZoom(_zoom <= 1f && d > 0 ? 1f + d : _zoom + d);

        /// <summary>Aplica el zoom actual redibujando la imagen escalada en el PictureBox.</summary>
        void ApplyZoom()
        {
            if (_edited == null) return;
            if (_zoom <= 1f) { pic.SizeMode = PictureBoxSizeMode.Zoom; pic.Image = _edited; }
            else
            {
                int w = (int)(_edited.Width * _zoom), h = (int)(_edited.Height * _zoom);
                var bz = new Bitmap(w, h);
                using var g = Graphics.FromImage(bz);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(_edited, 0, 0, w, h);
                pic.SizeMode = PictureBoxSizeMode.CenterImage;
                pic.Image = bz;
            }
            pic.Invalidate();
        }

        /// <summary>Navega a la imagen anterior o siguiente de forma circular.</summary>
        void Navigate(int d)
        {
            _currentIdx = (_currentIdx + d + _allImages.Count) % _allImages.Count;
            LoadCurrentImage();
        }

        // ── Teclado ──────────────────────────────────────────────────

        /// <summary>Maneja atajos de teclado: flechas para navegar, +/- para zoom y Escape para cancelar.</summary>
        void Form_KeyDown(object s, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: Navigate(-1); break;
                case Keys.Right: Navigate(1); break;
                case Keys.Add: ChangeZoom(+0.25f); break;
                case Keys.Subtract: ChangeZoom(-0.25f); break;
                case Keys.NumPad0: SetZoom(0f); break;
                case Keys.Escape:
                    if (_cropMode) ToggleCrop();
                    else if (_drawMode) ToggleDraw();
                    break;
            }
        }

        // ── Utilidades ───────────────────────────────────────────────

        /// <summary>Detecta si un archivo está pendiente de descarga desde la nube.</summary>
        static bool IsCloudFile(string path)
        {
            try
            {
                var a = File.GetAttributes(path);
                return a.HasFlag((FileAttributes)0x1000) || a.HasFlag((FileAttributes)0x400000);
            }
            catch { return false; }
        }
    }
}
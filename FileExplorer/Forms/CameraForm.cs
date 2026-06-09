using AForge.Video;
using AForge.Video.DirectShow;
using SharpAvi;
using SharpAvi.Output;
using System.Drawing.Imaging;
using System.IO;
using static FileExplorer.Helpers.FileHelper;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Formulario de cámara con preview en tiempo real via AForge,
    /// captura de fotos y grabación de video AVI.
    /// </summary>
    public partial class CameraForm : Form
    {
        // ── Colores ───────────────────────────────────────────────────
        static readonly Color C_BG = Color.FromArgb(15, 15, 18);
        static readonly Color C_SURFACE = Color.FromArgb(28, 28, 32);
        static readonly Color C_CARD = Color.FromArgb(40, 40, 46);
        static readonly Color C_BORDER = Color.FromArgb(60, 60, 70);
        static readonly Color C_ACCENT = Color.FromArgb(10, 132, 255);
        static readonly Color C_TEXT = Color.FromArgb(242, 242, 247);
        static readonly Color C_SUB = Color.FromArgb(142, 142, 148);
        static readonly Color C_RED = Color.FromArgb(220, 38, 38);
        static readonly Color C_GREEN = Color.FromArgb(48, 209, 88);

        // ── AForge — cámara ───────────────────────────────────────────
        FilterInfoCollection _devices;
        VideoCaptureDevice _camera;
        Bitmap _lastFrame;
        readonly object _frameLock = new();

        // ── Video ─────────────────────────────────────────────────────
        AviWriter _aviWriter;
        IAviVideoStream _aviStream;
        bool _grabandoVideo = false;
        string _videoTempPath = "";
        int _videoFrames = 0;
        int _videoFps = 25;
        int _videoWidth = 640;
        int _videoHeight = 480;
        DateTime _videoStart;
        readonly System.Windows.Forms.Timer _timerVideo = new() { Interval = 1000 };

        // ── Foto ──────────────────────────────────────────────────────
        int _fotoCount = 0;

        public event Action<string> PhotoSaved;
        public event Action<string> VideoSaved;

        public CameraForm()
        {
            InitializeComponent();
            Shown += OnShown;
            FormClosing += OnFormClosing;
            _timerVideo.Tick += TimerVideo_Tick;
        }

        // ════════════════════════════════════════════════════════════
        //  CÁMARA
        // ════════════════════════════════════════════════════════════

        /// <summary>Enumera los dispositivos de video y los carga en el ComboBox.</summary>
        void OnShown(object s, EventArgs e)
        {
            Task.Run(() =>
            {
                _devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                SafeInvoke(() =>
                {
                    _cboCamara.Items.Clear();
                    if (_devices.Count == 0)
                    {
                        _cboCamara.Items.Add("(Sin cámara detectada)");
                        _cboCamara.SelectedIndex = 0;
                        SetEstado("No se detectó ninguna cámara", Color.FromArgb(255, 59, 48));
                        return;
                    }
                    foreach (FilterInfo d in _devices) _cboCamara.Items.Add(d.Name);
                    _cboCamara.SelectedIndex = 0;
                });
            });
        }

        /// <summary>Cambia a la cámara seleccionada en el ComboBox.</summary>
        void CambiarCamara()
        {
            DetenerCamara();
            if (_devices == null || _cboCamara.SelectedIndex < 0
                || _cboCamara.SelectedIndex >= _devices.Count) return;

            _camera = new VideoCaptureDevice(
                _devices[_cboCamara.SelectedIndex].MonikerString);
            _camera.NewFrame += Camera_NewFrame;
            _camera.Start();

            if (_camera.VideoCapabilities?.Length > 0)
                _videoFps = Math.Max(1, _camera.VideoCapabilities[0].AverageFrameRate);

            SetEstado("Cámara activa", C_ACCENT);
            _btnCapturar.Enabled = true;
            _btnGrabarVideo.Enabled = true;
        }

        /// <summary>Callback de AForge — se llama por cada frame de la cámara.</summary>
        void Camera_NewFrame(object sender, NewFrameEventArgs e)
        {
            Bitmap frameUI, frameGuardar;
            try
            {
                frameUI = (Bitmap)e.Frame.Clone();
                frameGuardar = (Bitmap)e.Frame.Clone();
            }
            catch { return; }

            if (_grabandoVideo && _aviStream != null)
            {
                try
                {
                    var bmp = new Bitmap(frameGuardar, _videoWidth, _videoHeight);
                    var data = bmp.LockBits(
                        new Rectangle(0, 0, bmp.Width, bmp.Height),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                    int bytes = Math.Abs(data.Stride) * bmp.Height;
                    var buffer = new byte[bytes];
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, bytes);
                    bmp.UnlockBits(data);
                    bmp.Dispose();
                    lock (_aviStream) { _aviStream.WriteFrame(true, buffer, 0, buffer.Length); _videoFrames++; }
                }
                catch { }
            }

            lock (_frameLock) { _lastFrame?.Dispose(); _lastFrame = frameGuardar; }

            SafeInvoke(() =>
            {
                var prev = _preview.Image;
                _preview.Image = frameUI;
                prev?.Dispose();
            });
        }

        /// <summary>Detiene y libera la cámara actual.</summary>
        void DetenerCamara()
        {
            if (_camera == null) return;
            _camera.NewFrame -= Camera_NewFrame;
            if (_camera.IsRunning) _camera.SignalToStop();
            _camera = null;
        }

        // ════════════════════════════════════════════════════════════
        //  FOTO
        // ════════════════════════════════════════════════════════════

        /// <summary>Captura el frame actual y lo muestra como miniatura.</summary>
        void BtnCapturar_Click(object s, EventArgs e)
        {
            Bitmap foto;
            lock (_frameLock)
            {
                if (_lastFrame == null) return;
                foto = (Bitmap)_lastFrame.Clone();
            }
            lock (_frameLock) { _lastFrame?.Dispose(); _lastFrame = foto; }

            var prevMini = _pbMiniatura.Image;
            _pbMiniatura.Image = (Bitmap)foto.Clone();
            _pbMiniatura.Visible = true;
            prevMini?.Dispose();

            _fotoCount++;
            _lblContador.Text = $"{_fotoCount} foto{(_fotoCount != 1 ? "s" : "")}";
            _btnGuardar.Enabled = true;
            FlashEffect();
            SetEstado("✓  Foto capturada — haz clic en Guardar foto", C_ACCENT);
        }

        /// <summary>Muestra un flash blanco breve para simular el disparo.</summary>
        async void FlashEffect()
        {
            _pnlFlash.Visible = true;
            _pnlFlash.BringToFront();
            await Task.Delay(80);
            SafeInvoke(() => _pnlFlash.Visible = false);
        }

        /// <summary>Guarda la foto capturada con diálogo STA.</summary>
        void BtnGuardar_Click(object s, EventArgs e)
        {
            Bitmap foto;
            lock (_frameLock)
            {
                if (_lastFrame == null) { MessageBox.Show("No hay foto."); return; }
                foto = (Bitmap)_lastFrame.Clone();
            }

            string? destino = null;
            var t = new Thread(() =>
            {
                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar foto",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    FileName = $"foto_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
                    Filter = "JPEG|*.jpg|PNG|*.png|Todos|*.*",
                    DefaultExt = "jpg",
                };
                if (dlg.ShowDialog() == DialogResult.OK) destino = dlg.FileName;
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (destino == null) { foto.Dispose(); return; }

            try
            {
                var fmt = destino.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    ? ImageFormat.Png : ImageFormat.Jpeg;
                foto.Save(destino, fmt); foto.Dispose();
                SetEstado($"✓  Guardada: {destino}", C_GREEN);
                PhotoSaved?.Invoke(destino);
                _btnGuardar.Enabled = false;
            }
            catch (Exception ex)
            {
                foto.Dispose();
                MessageBox.Show("Error al guardar:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetEstado("✗  Error al guardar", C_RED);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  VIDEO
        // ════════════════════════════════════════════════════════════

        void BtnGrabarVideo_Click(object s, EventArgs e)
        {
            if (!_grabandoVideo) IniciarGrabacionVideo();
            else DetenerGrabacionVideo();
        }

        /// <summary>Inicia la grabación de video AVI con los frames del preview.</summary>
        void IniciarGrabacionVideo()
        {
            lock (_frameLock)
            {
                if (_lastFrame != null)
                {
                    _videoWidth = (_lastFrame.Width / 4) * 4;
                    _videoHeight = (_lastFrame.Height / 4) * 4;
                }
            }
            _videoTempPath = Path.Combine(Path.GetTempPath(), $"video_{Guid.NewGuid():N}.avi");
            try
            {
                _aviWriter = new AviWriter(_videoTempPath) { FramesPerSecond = _videoFps, EmitIndex1 = true };
                _aviStream = _aviWriter.AddVideoStream(_videoWidth, _videoHeight, BitsPerPixel.Bpp32);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo iniciar la grabación:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _aviWriter = null; _aviStream = null; return;
            }
            _grabandoVideo = true;
            _videoFrames = 0;
            _videoStart = DateTime.Now;
            _btnGrabarVideo.Text = "⏹  Detener";
            _btnGrabarVideo.BackColor = Color.FromArgb(200, 60, 60);
            _btnGuardarVideo.Enabled = false;
            _pnlRecDot.Visible = true;
            _lblVideoTimer.Visible = true;
            _timerVideo.Start();
            SetEstado("⏺  Grabando video...", C_RED);
        }

        void DetenerGrabacionVideo()
        {
            _grabandoVideo = false; _timerVideo.Stop();
            try { _aviWriter?.Close(); } catch { }
            _aviWriter = null; _aviStream = null;

            _btnGrabarVideo.Text = "⏺  Grabar video";
            _btnGrabarVideo.BackColor = Color.FromArgb(180, 40, 40);
            _pnlRecDot.Visible = false;

            if (_videoFrames > 0 && File.Exists(_videoTempPath))
            { _btnGuardarVideo.Enabled = true; SetEstado($"⏹  Video detenido — {_videoFrames} frames grabados", C_GREEN); }
            else
            { SetEstado("⏹  Grabación detenida (sin frames)", C_SUB); _lblVideoTimer.Visible = false; }
        }

        void TimerVideo_Tick(object s, EventArgs e)
        {
            var elapsed = DateTime.Now - _videoStart;
            _lblVideoTimer.Text = $"⏺ {elapsed:mm\\:ss}";
            _pnlRecDot.Visible = DateTime.Now.Millisecond < 500;
        }

        /// <summary>Guarda el video temporal con diálogo STA.</summary>
        void BtnGuardarVideo_Click(object s, EventArgs e)
        {
            if (string.IsNullOrEmpty(_videoTempPath) || !File.Exists(_videoTempPath))
            { MessageBox.Show("No hay video disponible.", "Aviso"); return; }

            string? destino = null;
            var t = new Thread(() =>
            {
                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar video",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    FileName = $"video_{DateTime.Now:yyyyMMdd_HHmmss}.avi",
                    Filter = "AVI|*.avi|Todos|*.*",
                    DefaultExt = "avi",
                };
                if (dlg.ShowDialog() == DialogResult.OK) destino = dlg.FileName;
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (destino == null) return;

            try
            {
                File.Copy(_videoTempPath, destino, true);
                try { File.Delete(_videoTempPath); } catch { }
                _videoTempPath = "";
                SetEstado($"✓  Video guardado: {destino}", C_GREEN);
                VideoSaved?.Invoke(destino);
                _btnGuardarVideo.Enabled = false;
                _lblVideoTimer.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar el video:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  CIERRE
        // ════════════════════════════════════════════════════════════

        void OnFormClosing(object s, FormClosingEventArgs e)
        {
            if (_grabandoVideo)
            { _grabandoVideo = false; _timerVideo.Stop(); try { _aviWriter?.Close(); } catch { } }
            _aviWriter = null; _aviStream = null;

            if (!string.IsNullOrEmpty(_videoTempPath) && File.Exists(_videoTempPath))
                try { File.Delete(_videoTempPath); } catch { }

            DetenerCamara();
            lock (_frameLock) { _lastFrame?.Dispose(); _lastFrame = null; }
        }

        // ── Helpers ───────────────────────────────────────────────────

        void SetEstado(string msg, Color color) =>
            SafeInvoke(() => { _lblEstado.Text = msg; _lblEstado.ForeColor = color; });

        void SafeInvoke(Action a)
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); }
            catch { }
        }
    }
}
using FileExplorer.Helpers;
using NAudio.Wave;
using NAudio.Lame;
using System.Runtime.InteropServices;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Grabador de audio con soporte WAV y MP3, visualizador de forma de onda,
    /// medidor de nivel, pausa/reanudacion y guardado con dialogo STA.
    /// </summary>
    public partial class AudioRecorderForm : Form
    {
        internal static readonly Color C_BG      = Color.FromArgb(22, 22, 26);
        internal static readonly Color C_SURFACE = Color.FromArgb(32, 32, 38);
        internal static readonly Color C_CARD    = Color.FromArgb(44, 44, 52);
        internal static readonly Color C_BORDER  = Color.FromArgb(64, 64, 76);
        internal static readonly Color C_RED     = Color.FromArgb(255, 55, 55);
        internal static readonly Color C_RED2    = Color.FromArgb(200, 30, 30);
        internal static readonly Color C_GREEN   = Color.FromArgb(48, 209, 88);
        internal static readonly Color C_ACCENT  = Color.FromArgb(10, 132, 255);
        internal static readonly Color C_TEXT    = Color.FromArgb(242, 242, 247);
        internal static readonly Color C_SUB     = Color.FromArgb(142, 142, 148);

        // ── Estado ───────────────────────────────────────────────────
        WaveInEvent    _waveIn;
        WaveFileWriter _writer;
        string         _tempPath;
        bool           _recording = false;
        bool           _paused    = false;

        internal readonly List<float> _samples = new();
        internal readonly object      _lock    = new();
        internal float _peakLevel = 0f;

        readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 50 };
        DateTime _startTime;
        TimeSpan _elapsed = TimeSpan.Zero;

        // ── Controles ────────────────────────────────────────────────
        internal Label           lblTime, lblStatus, lblSavedPath;
        internal Panel           pnlWave, pnlLevel, pnlLevelFill;
        internal Button          btnRecord, btnPause, btnStop, btnSave;
        internal ComboBox        cboDevice, cboFormat;
        internal TextBox         txtFileName;

        /// <summary>Se dispara cuando la grabacion se guarda con la ruta del archivo resultante.</summary>
        public event Action<string> FileSaved;

        /// <summary>
        /// Inicializa el grabador, enumera microfonos disponibles en background
        /// y configura el timer de actualizacion de UI.
        /// </summary>
        public AudioRecorderForm()
        {
            InitializeComponent();
            Shown += (_, __) => Task.Run(() =>
            {
                try
                {
                    var devices = new List<string>();
                    for (int i = 0; i < WaveInEvent.DeviceCount; i++)
                        devices.Add(WaveInEvent.GetCapabilities(i).ProductName);
                    SafeInvoke(() =>
                    {
                        cboDevice.Items.Clear();
                        if (devices.Count > 0)
                        { cboDevice.Items.AddRange(devices.Cast<object>().ToArray()); cboDevice.SelectedIndex = 0; }
                        else
                        { cboDevice.Items.Add("(Sin microfono detectado)"); cboDevice.SelectedIndex = 0; btnRecord.Enabled = false; }
                    });
                }
                catch (Exception ex)
                {
                    AppLogger.Error("AudioRecorderForm: error al enumerar microfonos", ex);
                    SafeInvoke(() => { cboDevice.Items.Add("(Error al detectar microfonos)"); cboDevice.SelectedIndex = 0; btnRecord.Enabled = false; });
                }
            });
            _uiTimer.Tick += UiTimer_Tick;
        }

        // ════════════════════════════════════════════════════════════
        //  GRABACIÓN
        // ════════════════════════════════════════════════════════════

        /// <summary>Inicia o reanuda la grabacion.</summary>
        void BtnRecord_Click(object s, EventArgs e)
        {
            if (_recording && !_paused) return;
            if (_paused)
            {
                _paused = false; _startTime = DateTime.Now - _elapsed;
                _waveIn?.StartRecording();
                SetStatus("Grabando...", C_RED); _uiTimer.Start();
                btnRecord.Enabled = false; btnPause.Enabled = true; return;
            }
            if (cboDevice.Items.Count == 0 || cboDevice.SelectedIndex < 0) return;
            _elapsed = TimeSpan.Zero; lblTime.Text = "00:00:00";
            lock (_lock) _samples.Clear();
            lblSavedPath.Text = ""; btnSave.Enabled = false;
            _tempPath = Path.Combine(Path.GetTempPath(), $"rec_{Guid.NewGuid():N}.wav");
            var fmt = GetWaveFormat();
            try
            {
                _waveIn = new WaveInEvent { DeviceNumber = cboDevice.SelectedIndex, WaveFormat = fmt, BufferMilliseconds = 50 };
                _writer = new WaveFileWriter(_tempPath, fmt);
                _waveIn.DataAvailable    += WaveIn_DataAvailable;
                _waveIn.RecordingStopped += WaveIn_RecordingStopped;
                _waveIn.StartRecording();
            }
            catch (Exception ex)
            {
                AppLogger.Error("AudioRecorderForm.BtnRecord_Click", ex);
                MessageBox.Show("No se pudo iniciar la grabacion:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _recording = true; _paused = false; _startTime = DateTime.Now; _uiTimer.Start();
            SetStatus("Grabando...", C_RED);
            btnRecord.Enabled = false; btnPause.Enabled = true; btnStop.Enabled = true; btnSave.Enabled = false;
        }

        /// <summary>Pausa la grabacion.</summary>
        void BtnPause_Click(object s, EventArgs e)
        {
            if (!_recording || _paused) return;
            _waveIn?.StopRecording(); _elapsed = DateTime.Now - _startTime;
            _paused = true; _uiTimer.Stop();
            SetStatus("Pausado", C_SUB);
            btnRecord.Text = "Reanudar"; btnRecord.Enabled = true; btnPause.Enabled = false;
        }

        /// <summary>Detiene la grabacion y habilita el guardado.</summary>
        void BtnStop_Click(object s, EventArgs e)
        {
            if (!_recording) return;
            _waveIn?.StopRecording(); _uiTimer.Stop();
            _recording = false; _paused = false;
            SetStatus("Detenido — listo para guardar", C_GREEN);
            btnRecord.Text = "Grabar"; btnRecord.Enabled = true;
            btnPause.Enabled = false; btnStop.Enabled = false; btnSave.Enabled = true;
        }

        /// <summary>Acumula samples, escribe al WAV temporal y actualiza el nivel de pico.</summary>
        void WaveIn_DataAvailable(object s, WaveInEventArgs e)
        {
            try
            {
                _writer?.Write(e.Buffer, 0, e.BytesRecorded);
                var ns = new float[e.BytesRecorded / 2]; float peak = 0f;
                for (int i = 0; i < e.BytesRecorded - 1; i += 2)
                {
                    float norm = BitConverter.ToInt16(e.Buffer, i) / 32768f;
                    ns[i / 2] = norm; float abs = Math.Abs(norm); if (abs > peak) peak = abs;
                }
                lock (_lock) { _samples.AddRange(ns); if (_samples.Count > 88200) _samples.RemoveRange(0, _samples.Count - 88200); }
                _peakLevel = peak;
            }
            catch (Exception ex) { AppLogger.Error("AudioRecorderForm.WaveIn_DataAvailable", ex); }
        }

        /// <summary>Libera WaveIn y WaveFileWriter al detener.</summary>
        void WaveIn_RecordingStopped(object s, StoppedEventArgs e)
        {
            try { _writer?.Flush(); _writer?.Dispose(); _writer = null; _waveIn?.Dispose(); _waveIn = null; }
            catch (Exception ex) { AppLogger.Warn("AudioRecorderForm.WaveIn_RecordingStopped: " + ex.Message); }
        }

        /// <summary>Guarda la grabacion como WAV o MP3 con dialogo STA.</summary>
        void BtnSave_Click(object s, EventArgs e)
        {
            if (string.IsNullOrEmpty(_tempPath) || !File.Exists(_tempPath))
            { MessageBox.Show("No hay grabacion disponible.", "Aviso"); return; }

            bool mp3  = cboFormat.SelectedIndex == 3;
            string ext  = mp3 ? ".mp3" : ".wav";
            string name = string.IsNullOrWhiteSpace(txtFileName.Text)
                ? $"grabacion_{DateTime.Now:yyyyMMdd_HHmmss}" : txtFileName.Text.Trim();

            string destPath = null;
            var t = new Thread(() =>
            {
                using var dlg = new SaveFileDialog { Title = "Guardar grabacion", FileName = name + ext, Filter = mp3 ? "MP3|*.mp3|Todos|*.*" : "WAV|*.wav|Todos|*.*", DefaultExt = ext.TrimStart('.') };
                if (dlg.ShowDialog() == DialogResult.OK) destPath = dlg.FileName;
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (destPath == null) return;

            btnSave.Enabled = false; SetStatus("Guardando...", C_ACCENT);
            Task.Run(() =>
            {
                try
                {
                    if (mp3) ConvertToMp3(_tempPath, destPath); else File.Copy(_tempPath, destPath, true);
                    try { File.Delete(_tempPath); } catch { }
                    _tempPath = null;
                    SafeInvoke(() =>
                    {
                        lblSavedPath.Text = "Guardado: " + destPath;
                        SetStatus("Guardado correctamente", C_GREEN);
                        btnSave.Enabled = false; FileSaved?.Invoke(destPath);
                        txtFileName.Text = $"grabacion_{DateTime.Now:yyyyMMdd_HHmmss}";
                    });
                }
                catch (Exception ex)
                {
                    AppLogger.Error("AudioRecorderForm.BtnSave", ex);
                    SafeInvoke(() => { MessageBox.Show("Error al guardar:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); SetStatus("Error al guardar", C_RED); btnSave.Enabled = true; });
                }
            });
        }

        /// <summary>Convierte el WAV temporal a MP3 usando LameMP3FileWriter.</summary>
        static void ConvertToMp3(string wavPath, string mp3Path)
        {
            using var reader = new WaveFileReader(wavPath);
            using var writer = new LameMP3FileWriter(mp3Path, reader.WaveFormat, 128);
            reader.CopyTo(writer);
        }

        /// <summary>Actualiza cronometro, nivel y forma de onda cada 50ms.</summary>
        void UiTimer_Tick(object s, EventArgs e)
        {
            if (_recording && !_paused) { _elapsed = DateTime.Now - _startTime; lblTime.Text = _elapsed.ToString(@"hh\:mm\:ss"); }
            pnlLevelFill.Width = Math.Min((int)(_peakLevel * pnlLevel.Width), pnlLevel.Width);
            pnlLevelFill.BackColor = _peakLevel > 0.9f ? C_RED : _peakLevel > 0.6f ? Color.FromArgb(255, 180, 0) : C_GREEN;
            pnlWave.Invalidate();
            if (_recording && !_paused) lblStatus.ForeColor = DateTime.Now.Millisecond < 500 ? C_RED : Color.FromArgb(150, 40, 40);
        }

        /// <summary>Devuelve el WaveFormat segun el indice del ComboBox.</summary>
        WaveFormat GetWaveFormat() => cboFormat.SelectedIndex switch
        {
            0 => new WaveFormat(44100, 16, 2),
            1 => new WaveFormat(44100, 16, 1),
            2 => new WaveFormat(22050, 16, 1),
            3 => new WaveFormat(44100, 16, 2),
            _ => new WaveFormat(44100, 16, 2),
        };

        /// <summary>Actualiza el label de estado.</summary>
        void SetStatus(string text, Color color) { lblStatus.Text = text; lblStatus.ForeColor = color; }

        /// <summary>Detiene y libera todos los recursos al cerrar.</summary>
        void StopAndCleanup()
        {
            _uiTimer.Stop();
            try { _waveIn?.StopRecording(); } catch { }
            try { _waveIn?.Dispose(); }       catch { }
            try { _writer?.Dispose(); }       catch { }
            _waveIn = null; _writer = null;
            if (_tempPath != null && File.Exists(_tempPath)) try { File.Delete(_tempPath); } catch { }
        }

        /// <summary>Invoca una accion en el hilo UI de forma segura.</summary>
        void SafeInvoke(Action action)
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(action); }
            catch (Exception ex) { AppLogger.Warn("AudioRecorderForm.SafeInvoke: " + ex.Message); }
        }

        [DllImport("Gdi32.dll")]
        internal static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int cw, int ch);
    }
}

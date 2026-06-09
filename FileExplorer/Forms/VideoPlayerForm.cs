using LibVLCSharp.Shared;


namespace FileExplorer.Forms
{
    /// <summary>
    /// Reproductor de video con LibVLC, controles de progreso, volumen,
    /// velocidad y modo pantalla completa.
    /// </summary>
    public partial class VideoPlayerForm : Form
    {
        // ── Colores ───────────────────────────────────────────────────
        static readonly Color SpBg = Color.FromArgb(18, 18, 18);
        static readonly Color SpPanel = Color.FromArgb(28, 28, 28);
        static readonly Color SpCard = Color.FromArgb(40, 40, 40);
        static readonly Color SpGreen = Color.FromArgb(29, 185, 84);
        static readonly Color SpGreenH = Color.FromArgb(30, 215, 96);
        static readonly Color SpWhite = Color.White;
        static readonly Color SpGray = Color.FromArgb(179, 179, 179);
        static readonly Color SpDark = Color.FromArgb(83, 83, 83);
        static readonly Color SpBarBg = Color.FromArgb(83, 83, 83);
        static readonly Color SpBottom = Color.FromArgb(12, 12, 12);

        // ── LibVLC ────────────────────────────────────────────────────
        LibVLC _vlc;
        MediaPlayer _mp;
        Media _media;

        // ── Estado ────────────────────────────────────────────────────
        readonly string _path;
        bool _seekingProg = false;
        bool _seekingVol = false;
        bool _full = false;
        float _volume = 0.8f;

        readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 400 };
        readonly System.Windows.Forms.Timer _hideTimer = new() { Interval = 3000 };

        public VideoPlayerForm(string path)
        {
            _path = path;
            Core.Initialize();
            InitializeComponent();
            Load += (s, e) => { DoLayout(); LoadVideo(); };
            FormClosed += (s, e) => Cleanup();
        }

        // ════════════════════════════════════════════════════════════
        //  VIDEO
        // ════════════════════════════════════════════════════════════

        /// <summary>Carga y reproduce el video con LibVLC.</summary>
        void LoadVideo()
        {
            _vlc = new LibVLC();
            _mp = new MediaPlayer(_vlc);
            videoView.MediaPlayer = _mp;
            _mp.Volume = (int)(_volume * 100);

            _media = new Media(_vlc, _path, FromType.FromPath);
            _media.ParsedChanged += (s, e) =>
            {
                if (e.ParsedStatus == MediaParsedStatus.Done)
                    Invoke(() => lblTotal.Text = Fmt(_mp.Length / 1000));
            };
            _ = _media.Parse();
            _mp.Play(_media);
            SetPlayIcon(true);
            _uiTimer.Start();
        }

        void Toggle()
        {
            if (_mp == null) return;
            if (_mp.IsPlaying) { _mp.Pause(); SetPlayIcon(false); _uiTimer.Stop(); }
            else { _mp.Play(); SetPlayIcon(true); _uiTimer.Start(); }
        }

        void StopVideo()
        {
            _mp?.Stop();
            SetPlayIcon(false);
            pnlProgFg.Width = 0;
            pnlProgThumb.Left = 0;
            lblElapsed.Text = "0:00:00";
            _uiTimer.Stop();
        }

        void Seek(int secs)
        {
            if (_mp != null) _mp.Time = Math.Max(0, _mp.Time + secs * 1000L);
        }

        // ── Progreso ──────────────────────────────────────────────────
        void UpdateProgress()
        {
            if (_mp == null || _seekingProg) return;
            if (_mp.Length > 0)
            {
                double pct = (double)_mp.Time / _mp.Length;
                int fw = (int)(pnlProgBg.Width * pct);
                pnlProgFg.Width = fw;
                pnlProgThumb.Left = Math.Max(0, fw - 6);
                pnlProgThumb.Top = (pnlProgBg.Height - 12) / 2;
            }
            lblElapsed.Text = Fmt(_mp.Time / 1000);
        }

        void ProgDown(object s, MouseEventArgs e) { _seekingProg = true; SeekX(e.X); }
        void ProgMove(object s, MouseEventArgs e) { if (_seekingProg) SeekX(e.X); }
        void ProgUp(object s, MouseEventArgs e) { SeekX(e.X); _seekingProg = false; }

        void SeekX(int x)
        {
            if (_mp == null || pnlProgBg.Width == 0) return;
            double pct = Math.Clamp((double)x / pnlProgBg.Width, 0, 1);
            int fw = (int)(pnlProgBg.Width * pct);
            pnlProgFg.Width = fw;
            pnlProgThumb.Left = Math.Max(0, fw - 6);
            _mp.Time = (long)(pct * _mp.Length);
            lblElapsed.Text = Fmt(_mp.Time / 1000);
        }

        // ── Volumen ───────────────────────────────────────────────────
        void VolDown(object s, MouseEventArgs e) { _seekingVol = true; SetVolX(e.X); }
        void VolMove(object s, MouseEventArgs e) { if (_seekingVol) SetVolX(e.X); }
        void VolUp(object s, MouseEventArgs e) { _seekingVol = false; SetVolX(e.X); }

        void SetVolX(int x)
        {
            if (pnlVolBg.Width == 0) return;
            double pct = Math.Clamp((double)x / pnlVolBg.Width, 0, 1);
            _volume = (float)pct;
            pnlVolFg.Width = (int)(pnlVolBg.Width * pct);
            if (_mp != null) _mp.Volume = (int)(pct * 100);
            lblVol.Text = (int)(pct * 100) + "%";
        }

        // ── Pantalla completa ─────────────────────────────────────────

        /// <summary>Alterna entre ventana normal y pantalla completa.</summary>
        void ToggleFull()
        {
            _full = !_full;
            if (_full)
            {
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                lblTitle.Visible = false;
                _hideTimer.Stop(); _hideTimer.Start();
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = FormWindowState.Normal;
                lblTitle.Visible = true;
                _hideTimer.Stop();
                pnlBottom.Visible = true;
            }
            btnFull.ForeColor = _full ? SpGreen : SpGray;
        }

        void ShowBarFullscreen()
        {
            pnlBottom.Visible = true;
            _hideTimer.Stop(); _hideTimer.Start();
        }

        // ── Teclado ───────────────────────────────────────────────────
        void OnKey(object s, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Space: Toggle(); break;
                case Keys.Left: Seek(-10); break;
                case Keys.Right: Seek(10); break;
                case Keys.Up: SetVolX((int)(pnlVolBg.Width * Math.Min(1.0, _volume + 0.05))); break;
                case Keys.Down: SetVolX((int)(pnlVolBg.Width * Math.Max(0.0, _volume - 0.05))); break;
                case Keys.F: ToggleFull(); break;
                case Keys.Escape: if (_full) ToggleFull(); break;
            }
            if (_full) ShowBarFullscreen();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_full) ShowBarFullscreen();
        }

        void SetPlayIcon(bool playing) => btnPlay.Text = playing ? "⏸" : "▶";

        void Cleanup()
        {
            _uiTimer.Stop(); _hideTimer.Stop();
            _mp?.Stop(); _media?.Dispose(); _mp?.Dispose(); _vlc?.Dispose();
        }

        static string Fmt(long secs) =>
            TimeSpan.FromSeconds(secs).ToString(@"h\:mm\:ss");
    }
}
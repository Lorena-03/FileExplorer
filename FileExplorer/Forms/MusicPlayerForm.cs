using FileExplorer.Controls;
using FileExplorer.Helpers;
using FileExplorer.Models;
using NAudio.Wave;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FileExplorer.Forms
{
    public enum SpIconType { Shuffle, Previous, Play, Pause, Stop, Next, Repeat }

    public class SpIconButton : Button
    {
        public SpIconType IconType { get; set; }
        public bool Active { get; set; }
        public bool IsCircle { get; set; }

        static readonly Color CG = Color.FromArgb(29, 185, 84);
        static readonly Color CGH = Color.FromArgb(30, 215, 96);
        static readonly Color CGr = Color.FromArgb(179, 179, 179);
        static readonly Color CW = Color.White;

        public SpIconButton()
        {
            FlatStyle = FlatStyle.Flat;
            BackColor = Color.Transparent;
            ForeColor = CGr;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e)
        { base.OnMouseEnter(e); if (!Active) ForeColor = CW; Invalidate(); }

        protected override void OnMouseLeave(EventArgs e)
        { base.OnMouseLeave(e); if (!Active) ForeColor = CGr; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Color.Black);

            if (IsCircle)
            {
                var rc = ClientRectangle; rc.Inflate(-1, -1);
                using var path2 = new System.Drawing.Drawing2D.GraphicsPath();
                path2.AddEllipse(rc);
                Region = new Region(path2);
                g.FillEllipse(new SolidBrush(Active ? CGH : CG), rc);
                DrawIcon(g, Color.Black);
            }
            else
            {
                Region = null;
                DrawIcon(g, Active ? CG : ForeColor);
                if (Active)
                { int cx2 = Width / 2; g.FillEllipse(new SolidBrush(CG), cx2 - 2, Height - 5, 4, 4); }
            }
        }

        void DrawIcon(Graphics g, Color c)
        {
            int w = Width, h = Height;
            float cx = w / 2f, cy = h / 2f;
            using var br = new SolidBrush(c);
            using var pen = new Pen(c, 2f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
            };
            switch (IconType)
            {
                case SpIconType.Play:
                    {
                        float s = IsCircle ? 0.38f : 0.36f, pw = w * s, ph = h * s;
                        g.FillPolygon(br, new PointF[] { new(cx - pw * 0.3f, cy - ph * 0.5f), new(cx + pw * 0.7f, cy), new(cx - pw * 0.3f, cy + ph * 0.5f) }); break;
                    }
                case SpIconType.Pause:
                    {
                        float bw = w * 0.13f, bh = h * 0.38f, gap = w * 0.08f;
                        g.FillRectangle(br, cx - gap - bw, cy - bh, bw, bh * 2); g.FillRectangle(br, cx + gap, cy - bh, bw, bh * 2); break;
                    }
                case SpIconType.Stop:
                    { float s = w * 0.32f; g.FillRectangle(br, cx - s, cy - s, s * 2, s * 2); break; }
                case SpIconType.Previous:
                    {
                        float bw = w * 0.08f, th = h * 0.34f, tw = w * 0.28f;
                        g.FillRectangle(br, cx - tw - bw * 0.5f - 2, cy - th, bw, th * 2);
                        g.FillPolygon(br, new PointF[] { new(cx + tw * 0.4f - 2, cy - th), new(cx - tw * 0.6f - 2, cy), new(cx + tw * 0.4f - 2, cy + th) }); break;
                    }
                case SpIconType.Next:
                    {
                        float bw = w * 0.08f, th = h * 0.34f, tw = w * 0.28f;
                        g.FillPolygon(br, new PointF[] { new(cx - tw * 0.4f + 2, cy - th), new(cx + tw * 0.6f + 2, cy), new(cx - tw * 0.4f + 2, cy + th) });
                        g.FillRectangle(br, cx + tw * 0.6f + 2, cy - th, bw, th * 2); break;
                    }
                case SpIconType.Shuffle:
                    {
                        float m = 4f, y1 = cy + h * 0.18f, y2 = cy - h * 0.18f, x2 = w - m - 8;
                        g.DrawLine(pen, m, y1, x2, y2); g.DrawLine(pen, m, y2, x2, y1);
                        ArrowHead(g, br, w - m, y2); ArrowHead(g, br, w - m, y1); break;
                    }
                case SpIconType.Repeat:
                    {
                        float r = w * 0.30f;
                        g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, -30, 290);
                        double ang = Math.PI * (-30 + 290) / 180.0;
                        float ax = cx + r * (float)Math.Cos(ang), ay = cy + r * (float)Math.Sin(ang);
                        SmallArrow(g, br, ax, ay, (float)(ang + Math.PI * 0.5)); break;
                    }
            }
        }

        static void ArrowHead(Graphics g, SolidBrush br, float tx, float ty)
        { float s = 5f; g.FillPolygon(br, new PointF[] { new(tx, tx), new(tx - s, ty - s), new(tx - s, ty + s) }); }

        static void SmallArrow(Graphics g, SolidBrush br, float x, float y, float ang)
        {
            float s = 5f;
            var m = new System.Drawing.Drawing2D.Matrix();
            m.RotateAt(ang * 180f / (float)Math.PI, new PointF(x, y));
            var pts = new PointF[] { new(x, y), new(x - s, y - s), new(x + s, y - s) };
            m.TransformPoints(pts);
            g.FillPolygon(br, pts);
        }
    }

    public partial class MusicPlayerForm : Form
    {
        // ── Colores ───────────────────────────────────────────────────
        static readonly Color SpBg = Color.FromArgb(18, 18, 18);
        static readonly Color SpPanel = Color.FromArgb(28, 28, 28);
        static readonly Color SpSidebar = Color.FromArgb(18, 18, 18);
        static readonly Color SpCard = Color.FromArgb(40, 40, 40);
        static readonly Color SpGreen = Color.FromArgb(29, 185, 84);
        static readonly Color SpWhite = Color.White;
        static readonly Color SpGray = Color.FromArgb(179, 179, 179);
        static readonly Color SpDark = Color.FromArgb(83, 83, 83);
        static readonly Color SpBarBg = Color.FromArgb(83, 83, 83);
        static readonly Color SpBottom = Color.FromArgb(12, 12, 12);

        // ── Fuentes y pinceles ────────────────────────────────────────
        static readonly Font FntNormal = new("Segoe UI", 8.5f);
        static readonly Font FntBold = new("Segoe UI", 8.5f, FontStyle.Bold);
        static readonly Font FntSmall = new("Segoe UI", 7.5f);
        static readonly Font FntNote = new("Segoe UI", 7f);
        static readonly Font FntList = new("Segoe UI", 13f);
        static readonly Font FntLyricNormal = new("Segoe UI", 11.5f, FontStyle.Regular);
        static readonly Font FntLyricActive = new("Segoe UI", 13f, FontStyle.Bold);
        static readonly Brush BrWhite = new SolidBrush(Color.White);
        static readonly Brush BrGray = new SolidBrush(Color.FromArgb(179, 179, 179));
        static readonly Brush BrDark = new SolidBrush(Color.FromArgb(83, 83, 83));
        static readonly Brush BrGreen = new SolidBrush(Color.FromArgb(29, 185, 84));
        static readonly Brush BrCard = new SolidBrush(Color.FromArgb(40, 40, 40));

        // ── Spotify API ───────────────────────────────────────────────
        const string SP_CLIENT_ID = "TU_CLIENT_ID";
        const string SP_CLIENT_SECRET = "TU_CLIENT_SECRET";
        string _spToken = null;
        DateTime _spTokenExp = DateTime.MinValue;

        static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

        // ── Audio ─────────────────────────────────────────────────────
        WaveOutEvent _out;
        AudioFileReader _reader;
        EqualizerSampleProvider _eqProvider;

        // ── Letra ─────────────────────────────────────────────────────
        static readonly Regex LrcRx = new(
            @"\[(\d{1,2}):(\d{2})[\.\:](\d{1,3})\](.*)", RegexOptions.Compiled);
        List<(double Time, string Text)> _syncedLines = new();
        int _currentLine = -1;

        // ── Estado ────────────────────────────────────────────────────
        List<PlaylistItem> _queue = new();
        List<(string Name, List<PlaylistItem> Songs)> _lists = new();
        int _currentIdx = -1;
        bool _shuffle = false;
        bool _repeat = false;
        bool _seekingProg = false;
        bool _seekingVol = false;
        float _volume = 0.8f;
        CancellationTokenSource _playCts = new();

        static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PlaylistItem> _metaCache
            = new(StringComparer.OrdinalIgnoreCase);

        readonly System.Windows.Forms.Timer _ticker = new() { Interval = 500 };

        // ── Controles ─────────────────────────────────────────────────
        Panel pnlSidebar, pnlCenter, pnlRight, pnlBottom;
        PictureBox picArt;
        Label lblTitle, lblArtist, lblAlbum, lblElapsed, lblTotal;
        Panel pnlProgBg, pnlProgFg, pnlProgThumb, pnlVolBg, pnlVolFg;
        Label lblVol;
        SpIconButton btnShuffle, btnPrev, btnPlay, btnStop, btnNext, btnRepeat;
        TabControl tabSidebar;
        ListBox lstQueue, lstLists, lstListSongs;
        Label lblListName;
        Button btnNewList, btnDelList, btnPlayList, btnAddToList, btnRemFromList;
        Button btnEditMeta;   // ← botón editar metadatos
        RichTextBox rtbLyrics;
        Label lblLyricsStatus;
        EqualizerControl eqCtrl;

        public MusicPlayerForm(string startFile, List<string> allFiles)
        {
            InitializeComponent();
            LoadQueue(allFiles, startFile);
            _ticker.Tick += (s, e) => { UpdateProgress(); SyncLyrics(); };
            FormClosed += (s, e) => Cleanup();
        }

        // ── Helpers ───────────────────────────────────────────────────

        void SafeInvoke(Action action)
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(action); }
            catch (Exception ex) { AppLogger.Warn("MusicPlayerForm.SafeInvoke: " + ex.Message); }
        }

        static string Fmt(TimeSpan t) =>
            t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

        Label TLabel(string text) => new()
        {
            Text = text,
            Width = 60,
            Height = 18,
            ForeColor = SpGray,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8.5f),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        Button SBtn(string text, int left, int w = 100)
        {
            var b = new Button
            {
                Text = text,
                Left = left,
                Top = 10,
                Width = w,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = SpCard,
                ForeColor = SpGray,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(58, 58, 58);
            return b;
        }

        Label CenterLabel(float sz, FontStyle st, Color c, int h) => new()
        {
            AutoSize = false,
            Height = h,
            ForeColor = c,
            Font = new Font("Segoe UI", sz, st),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            AutoEllipsis = true,
            UseMnemonic = false,
        };

        void ResetUI()
        {
            lblTitle.Text = "—"; lblArtist.Text = "—"; lblAlbum.Text = "—";
            picArt.Image = null; picArt.Invalidate();
            rtbLyrics.Clear(); lblLyricsStatus.Text = "";
            pnlProgFg.Width = 0; lblElapsed.Text = "0:00"; lblTotal.Text = "0:00";
            SetPlayIcon(false);
        }

        void SetPlayIcon(bool playing)
        { btnPlay.IconType = playing ? SpIconType.Pause : SpIconType.Play; btnPlay.Invalidate(); }
    }
}
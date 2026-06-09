using FileExplorer.Helpers;
using FileExplorer.Models;
using NAudio.Wave;
using System.Net.Http.Headers;
using System.Text.Json;
using TagLib;

namespace FileExplorer.Forms
{
    public partial class MusicPlayerForm
    {
        // ════════════════════════════════════════════════════════════
        //  COLA
        // ════════════════════════════════════════════════════════════

        void LoadQueue(List<string> files, string start)
        {
            _queue.Clear();
            foreach (var f in files)
                _queue.Add(new PlaylistItem { FilePath = f, Title = Path.GetFileNameWithoutExtension(f) });
            RefreshQueueList();

            int startIdx = Math.Max(0, _queue.FindIndex(p =>
                p.FilePath.Equals(start, StringComparison.OrdinalIgnoreCase)));
            PlayIdx(startIdx);

            Task.Run(() =>
            {
                int n = 0;
                for (int i = 0; i < _queue.Count; i++)
                {
                    if (IsDisposed) return;
                    if (i == startIdx) continue;
                    _queue[i] = ReadMetaNoWait(_queue[i].FilePath);
                    if (++n % 5 == 0) SafeInvoke(() => lstQueue.Invalidate());
                    Thread.Sleep(80);
                }
                SafeInvoke(() => lstQueue.Invalidate());
            });
        }

        void RefreshQueueList()
        {
            lstQueue.Items.Clear();
            foreach (var p in _queue) lstQueue.Items.Add(p);
        }

        void AddFiles()
        {
            using var d = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Audio|*.mp3;*.wav;*.flac;*.aac;*.ogg;*.m4a;*.wma|Todos|*.*",
            };
            if (d.ShowDialog(this) != DialogResult.OK) return;
            foreach (var f in d.FileNames) _queue.Add(ReadMeta(f));
            RefreshQueueList();
        }

        void ClearQueue() { StopPlay(); _queue.Clear(); RefreshQueueList(); ResetUI(); }

        void RemoveSelectedFromQueue()
        {
            foreach (var i in lstQueue.SelectedIndices.Cast<int>().OrderByDescending(x => x))
            {
                if (i == _currentIdx) { StopPlay(); _currentIdx = -1; }
                _queue.RemoveAt(i);
            }
            RefreshQueueList();
        }

        // ════════════════════════════════════════════════════════════
        //  LISTAS
        // ════════════════════════════════════════════════════════════

        void CreateNewList()
        {
            string name = AskName($"Lista {_lists.Count + 1}");
            if (name == null) return;
            _lists.Add((name, new List<PlaylistItem>()));
            RefreshListsUI(); lstLists.SelectedIndex = _lists.Count - 1;
        }

        void DeleteList()
        {
            if (lstLists.SelectedIndex < 0) return;
            if (MessageBox.Show($"¿Eliminar \"{_lists[lstLists.SelectedIndex].Name}\"?",
                "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _lists.RemoveAt(lstLists.SelectedIndex); RefreshListsUI();
        }

        void PlayList()
        {
            if (lstLists.SelectedIndex < 0) { MessageBox.Show("Selecciona una lista."); return; }
            var songs = _lists[lstLists.SelectedIndex].Songs;
            if (songs.Count == 0) { MessageBox.Show("La lista esta vacia."); return; }
            _queue.Clear(); _queue.AddRange(songs);
            RefreshQueueList(); tabSidebar.SelectedIndex = 0; PlayIdx(0);
        }

        void AddSelectedToList()
        {
            var selIdx = lstQueue.SelectedIndices.Cast<int>().ToList();
            if (selIdx.Count == 0) { MessageBox.Show("Selecciona canciones primero."); return; }
            if (_lists.Count == 0)
            {
                if (MessageBox.Show("¿Crear una lista?", "Sin listas", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                CreateNewList(); if (_lists.Count == 0) return;
            }
            int dest = ChooseList(); if (dest < 0) return;
            foreach (var i in selIdx)
            {
                var s = _queue[i];
                if (!_lists[dest].Songs.Any(x => x.FilePath.Equals(s.FilePath, StringComparison.OrdinalIgnoreCase)))
                    _lists[dest].Songs.Add(s);
            }
            RefreshListsUI();
            MessageBox.Show($"{selIdx.Count} cancion(es) anadidas a \"{_lists[dest].Name}\".",
                "Listo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void RemoveFromList()
        {
            if (lstLists.SelectedIndex < 0 || lstListSongs.SelectedIndex < 0) return;
            foreach (var i in lstListSongs.SelectedIndices.Cast<int>().OrderByDescending(x => x))
                _lists[lstLists.SelectedIndex].Songs.RemoveAt(i);
            RefreshListSongs();
        }

        void RefreshListsUI()
        {
            lstLists.Items.Clear();
            foreach (var (n, s) in _lists) lstLists.Items.Add($"{n}  ({s.Count})");
            RefreshListSongs();
        }

        void RefreshListSongs()
        {
            lstListSongs.Items.Clear();
            if (lstLists.SelectedIndex < 0 || lstLists.SelectedIndex >= _lists.Count) return;
            foreach (var s in _lists[lstLists.SelectedIndex].Songs) lstListSongs.Items.Add(s);
        }

        int ChooseList()
        {
            if (_lists.Count == 1) return 0;
            using var dlg = new Form
            {
                Text = "Agregar a lista",
                Size = new Size(340, 200),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                BackColor = SpPanel,
                ForeColor = SpWhite,
                MaximizeBox = false,
                MinimizeBox = false,
            };
            var lbl = new Label { Text = "¿A cual lista?", Left = 10, Top = 10, Width = 300, Height = 20, ForeColor = SpGray, BackColor = Color.Transparent };
            var lst = new ListBox { Left = 10, Top = 34, Width = 300, Height = 100, BackColor = SpCard, ForeColor = SpWhite, BorderStyle = BorderStyle.None };
            foreach (var (n, _) in _lists) lst.Items.Add(n); lst.SelectedIndex = 0;
            var ok = new Button { Text = "Agregar", Left = 140, Top = 142, Width = 80, DialogResult = DialogResult.OK, BackColor = SpGreen, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat };
            var can = new Button { Text = "Cancelar", Left = 228, Top = 142, Width = 80, DialogResult = DialogResult.Cancel, BackColor = SpCard, ForeColor = SpWhite, FlatStyle = FlatStyle.Flat };
            ok.FlatAppearance.BorderSize = can.FlatAppearance.BorderSize = 0;
            dlg.Controls.AddRange(new Control[] { lbl, lst, ok, can });
            dlg.AcceptButton = ok; dlg.CancelButton = can;
            return dlg.ShowDialog(this) == DialogResult.OK ? lst.SelectedIndex : -1;
        }

        string AskName(string def)
        {
            using var dlg = new Form
            {
                Text = "Nombre",
                Size = new Size(400, 170),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                BackColor = SpPanel,
                ForeColor = SpWhite,
                MaximizeBox = false,
                MinimizeBox = false,
            };
            var txt = new System.Windows.Forms.TextBox { Left = 10, Top = 12, Width = 370, BackColor = SpCard, ForeColor = SpWhite, BorderStyle = BorderStyle.FixedSingle, Text = def };
            var ok = new Button { Text = "Aceptar", Left = 180, Top = 50, Width = 90, Height = 30, DialogResult = DialogResult.OK, BackColor = SpGreen, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat };
            var can = new Button { Text = "Cancelar", Left = 280, Top = 50, Width = 90, Height = 30, DialogResult = DialogResult.Cancel, BackColor = SpCard, ForeColor = SpWhite, FlatStyle = FlatStyle.Flat };
            ok.FlatAppearance.BorderSize = can.FlatAppearance.BorderSize = 0;
            dlg.Controls.AddRange(new Control[] { txt, ok, can });
            dlg.AcceptButton = ok; dlg.CancelButton = can;
            if (dlg.ShowDialog(this) != DialogResult.OK) return null;
            return string.IsNullOrWhiteSpace(txt.Text) ? def : txt.Text.Trim();
        }

        // ════════════════════════════════════════════════════════════
        //  REPRODUCCIÓN
        // ════════════════════════════════════════════════════════════

        void PlayIdx(int idx)
        {
            if (idx < 0 || idx >= _queue.Count) return;

            _playCts.Cancel(); _playCts.Dispose();
            _playCts = new CancellationTokenSource();
            var cts = _playCts;

            _ticker.Stop();
            var oldOut = _out;
            var oldReader = _reader;
            _out = null; _reader = null; _eqProvider = null;

            _currentIdx = idx; _currentLine = -1; _syncedLines.Clear();

            var item = _queue[idx];
            lblTitle.Text = string.IsNullOrEmpty(item.Title) ? Path.GetFileNameWithoutExtension(item.FilePath) : item.DisplayName;
            lblArtist.Text = item.DisplayArtist;
            lblAlbum.Text = item.DisplayAlbum;
            picArt.Image = item.AlbumArt; picArt.Invalidate();
            SetPlayIcon(false);
            pnlProgFg.Width = 0; lblElapsed.Text = "0:00"; lblTotal.Text = "—";
            rtbLyrics.Clear(); lblLyricsStatus.Text = "  Buscando...";
            lstQueue.SelectedIndex = idx; lstQueue.Invalidate();

            Task.Run(async () =>
            {
                try
                {
                    try { oldOut?.Stop(); } catch { }
                    try { oldOut?.Dispose(); } catch { }
                    try { oldReader?.Dispose(); } catch { }

                    if (cts.IsCancellationRequested) return;

                    // Leer metadatos (caché local → archivo → red)
                    if (string.IsNullOrEmpty(item.Artist) && item.Title == Path.GetFileNameWithoutExtension(item.FilePath))
                    {
                        item = ReadMetaNoWait(item.FilePath);
                        _queue[idx] = item;
                        if (cts.IsCancellationRequested) return;
                        SafeInvoke(() =>
                        {
                            lblTitle.Text = item.DisplayName;
                            lblArtist.Text = item.DisplayArtist;
                            lblAlbum.Text = item.DisplayAlbum;
                            if (item.AlbumArt != null) { picArt.Image = item.AlbumArt; picArt.Invalidate(); lstQueue.Invalidate(); }
                        });
                    }

                    if (cts.IsCancellationRequested) return;

                    // Construir cadena de audio con EQ
                    var reader = new AudioFileReader(item.FilePath);
                    var bands = EqualizerSampleProvider.DefaultBands();
                    if (eqCtrl != null)
                    {
                        var gains = eqCtrl.GetGains();
                        for (int i = 0; i < gains.Length && i < bands.Length; i++)
                            bands[i].Gain = gains[i];
                    }
                    var eq = new EqualizerSampleProvider(reader, bands);
                    var output = new WaveOutEvent { Volume = _volume };
                    output.Init(eq);

                    if (cts.IsCancellationRequested) { output.Dispose(); reader.Dispose(); return; }

                    _reader = reader; _out = output; _eqProvider = eq;
                    output.Play();

                    output.PlaybackStopped += (s, e) =>
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        try
                        {
                            BeginInvoke(new Action(() =>
                            {
                                if (_out != output) return;
                                SetPlayIcon(false); _ticker.Stop();
                                if (_repeat) PlayIdx(_currentIdx); else PlayNext();
                            }));
                        }
                        catch { }
                    };

                    SafeInvoke(() =>
                    {
                        if (cts.IsCancellationRequested) return;
                        if (reader.TotalTime.TotalSeconds > 0) lblTotal.Text = Fmt(reader.TotalTime);
                        SetPlayIcon(true); _ticker.Start();
                    });

                    if (!cts.IsCancellationRequested) await FetchAllAsync(item);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    AppLogger.Error("MusicPlayerForm.PlayIdx", ex);
                    SafeInvoke(() => MessageBox.Show("Error al reproducir:\n" + ex.Message,
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
                }
            }, cts.Token);
        }

        void Toggle()
        {
            if (_out == null) { if (_currentIdx >= 0) PlayIdx(_currentIdx); return; }
            try
            {
                if (_out.PlaybackState == PlaybackState.Playing)
                { _out.Pause(); SetPlayIcon(false); _ticker.Stop(); }
                else
                { _out.Play(); SetPlayIcon(true); _ticker.Start(); }
            }
            catch (Exception ex) { AppLogger.Error("MusicPlayerForm.Toggle", ex); }
        }

        void StopPlay()
        {
            try { _out?.Stop(); } catch { }
            if (_reader != null) try { _reader.Position = 0; } catch { }
            SetPlayIcon(false); pnlProgFg.Width = 0; pnlProgThumb.Left = 0;
            lblElapsed.Text = "0:00"; _currentLine = -1; _ticker.Stop();
        }

        void PlayNext()
        {
            if (_queue.Count == 0) return;
            PlayIdx(_shuffle ? new Random().Next(0, _queue.Count) : (_currentIdx + 1) % _queue.Count);
        }

        void PlayPrev()
        {
            if (_queue.Count == 0) return;
            if (_reader != null && _reader.CurrentTime.TotalSeconds > 3)
            { _reader.CurrentTime = TimeSpan.Zero; return; }
            PlayIdx(_currentIdx > 0 ? _currentIdx - 1 : _queue.Count - 1);
        }

        void Cleanup()
        {
            _playCts?.Cancel(); _ticker.Stop();
            try { _out?.Stop(); } catch { }
            try { _out?.Dispose(); } catch { }
            try { _reader?.Dispose(); } catch { }
            _out = null; _reader = null; _eqProvider = null;
        }

        void UpdateProgress()
        {
            if (_reader == null || _seekingProg) return;
            try
            {
                var pos = _reader.CurrentTime; var dur = _reader.TotalTime;
                if (dur.TotalSeconds > 0)
                {
                    double pct = pos.TotalSeconds / dur.TotalSeconds;
                    int fw = (int)(pnlProgBg.Width * pct);
                    pnlProgFg.Width = fw;
                    pnlProgThumb.Left = Math.Max(0, fw - 6);
                    pnlProgThumb.Top = (pnlProgBg.Height - 12) / 2;
                }
                lblElapsed.Text = Fmt(pos);
            }
            catch { }
        }

        void ProgDown(object s, MouseEventArgs e) { _seekingProg = true; SeekX(e.X); }
        void ProgMove(object s, MouseEventArgs e) { if (_seekingProg) SeekX(e.X); }
        void ProgUp(object s, MouseEventArgs e) { SeekX(e.X); _seekingProg = false; }

        void SeekX(int x)
        {
            if (_reader == null || pnlProgBg.Width == 0) return;
            try
            {
                double pct = Math.Clamp((double)x / pnlProgBg.Width, 0, 1);
                pnlProgFg.Width = (int)(pnlProgBg.Width * pct);
                pnlProgThumb.Left = Math.Max(0, pnlProgFg.Width - 6);
                _reader.CurrentTime = TimeSpan.FromSeconds(_reader.TotalTime.TotalSeconds * pct);
                lblElapsed.Text = Fmt(_reader.CurrentTime);
            }
            catch { }
        }

        void VolDown(object s, MouseEventArgs e) { _seekingVol = true; SetVol(e.X); }
        void VolMove(object s, MouseEventArgs e) { if (_seekingVol) SetVol(e.X); }
        void VolUp(object s, MouseEventArgs e) { _seekingVol = false; SetVol(e.X); }

        void SetVol(int x)
        {
            double pct = Math.Clamp((double)x / pnlVolBg.Width, 0, 1);
            _volume = (float)pct;
            pnlVolFg.Width = (int)(pnlVolBg.Width * pct);
            if (_out != null) _out.Volume = _volume;
            lblVol.Text = (int)(pct * 100) + "%";
        }

        // ════════════════════════════════════════════════════════════
        //  LETRAS
        // ════════════════════════════════════════════════════════════

        void ParseLrc(string lrc)
        {
            _syncedLines.Clear();
            foreach (var line in lrc.Split('\n'))
            {
                var m = LrcRx.Match(line.Trim()); if (!m.Success) continue;
                int min = int.Parse(m.Groups[1].Value), sec = int.Parse(m.Groups[2].Value);
                double t = min * 60.0 + sec + int.Parse(m.Groups[3].Value.PadRight(3, '0')[..3]) / 1000.0;
                _syncedLines.Add((t, m.Groups[4].Value.Trim()));
            }
            _syncedLines = _syncedLines.OrderBy(l => l.Time).ToList();
        }

        void SetLyricsPlain(string text)
        {
            _syncedLines.Clear();
            var lines = text.Split('\n').Select(l => l.TrimEnd()).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            double dur = _reader?.TotalTime.TotalSeconds ?? 180.0;
            double step = dur / Math.Max(lines.Length, 1);
            for (int i = 0; i < lines.Length; i++) _syncedLines.Add((i * step, lines[i]));
        }

        void RenderLyrics()
        {
            rtbLyrics.Clear(); _currentLine = -1;
            foreach (var (_, txt) in _syncedLines)
            {
                rtbLyrics.SelectionStart = rtbLyrics.TextLength;
                rtbLyrics.SelectionColor = SpDark;
                rtbLyrics.SelectionFont = FntLyricNormal;
                rtbLyrics.AppendText(txt + "\n\n");
            }
        }

        void SyncLyrics()
        {
            if (_reader == null || _syncedLines.Count == 0) return;
            try
            {
                double pos = _reader.CurrentTime.TotalSeconds;
                int active = 0;
                for (int i = 0; i < _syncedLines.Count; i++)
                { if (_syncedLines[i].Time <= pos) active = i; else break; }
                if (active == _currentLine) return;
                if (_currentLine >= 0) ColorLine(_currentLine, SpDark, false);
                ColorLine(active, SpWhite, true);
                _currentLine = active;
                try
                {
                    int cp = 0;
                    for (int i = 0; i < active; i++) cp += _syncedLines[i].Text.Length + 2;
                    rtbLyrics.SelectionStart = Math.Min(cp, rtbLyrics.TextLength);
                    rtbLyrics.ScrollToCaret();
                }
                catch { }
            }
            catch { }
        }

        void ColorLine(int idx, Color c, bool active)
        {
            if (idx < 0 || idx >= _syncedLines.Count) return;
            try
            {
                int cp = 0;
                for (int i = 0; i < idx; i++) cp += _syncedLines[i].Text.Length + 2;
                int len = _syncedLines[idx].Text.Length; if (len == 0) return;
                rtbLyrics.SelectionStart = Math.Min(cp, rtbLyrics.TextLength - 1);
                rtbLyrics.SelectionLength = Math.Min(len, rtbLyrics.TextLength - rtbLyrics.SelectionStart);
                rtbLyrics.SelectionColor = active ? SpWhite : SpDark;
                rtbLyrics.SelectionFont = active ? FntLyricActive : FntLyricNormal;
                rtbLyrics.SelectionLength = 0;
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════
        //  APIs + CACHÉ
        // ════════════════════════════════════════════════════════════

        async Task FetchAllAsync(PlaylistItem item)
        {
            if (IsDisposed || !IsHandleCreated) return;

            // 1 — Verificar caché local primero
            var cached = LoadCache(item.FilePath);
            if (cached != null)
            {
                bool hasLyrics = !string.IsNullOrEmpty(cached.Lyrics);
                bool hasArt = !string.IsNullOrEmpty(cached.AlbumArtBase64);

                if (hasLyrics || hasArt)
                {
                    ApplyCache(cached, item);
                    _queue[_currentIdx] = item;

                    SafeInvoke(() =>
                    {
                        if (IsDisposed) return;
                        lblTitle.Text = item.DisplayName;
                        lblArtist.Text = item.DisplayArtist;
                        lblAlbum.Text = item.DisplayAlbum;
                        if (item.AlbumArt != null) { picArt.Image = item.AlbumArt; picArt.Invalidate(); lstQueue.Invalidate(); }
                        if (!string.IsNullOrEmpty(item.Lyrics))
                        {
                            if (cached.LyricsType == "synced") ParseLrc(item.Lyrics);
                            else SetLyricsPlain(item.Lyrics);
                            RenderLyrics();
                            lblLyricsStatus.Text = "  Desde caché local";
                        }
                    });

                    // Si ya tenemos todo en caché, no consultar la red
                    if (hasLyrics && hasArt) return;
                }
            }

            // 2 — Consultar red para lo que falte
            SafeInvoke(() => lblLyricsStatus.Text = "  Buscando en línea...");
            await Task.WhenAll(FetchArtAsync(item), FetchLyricsAsync(item));

            // 3 — Guardar en caché local lo obtenido de la red
            SaveCache(item, _syncedLines.Any(l => l.Time > 0) ? "synced" : "plain");
        }

        async Task FetchArtAsync(PlaylistItem item)
        {
            if (item.AlbumArt != null)
            {
                SafeInvoke(() => { if (!IsDisposed) { picArt.Image = item.AlbumArt; picArt.Invalidate(); lstQueue.Invalidate(); } });
                return;
            }

            if (!string.IsNullOrEmpty(SP_CLIENT_ID) && SP_CLIENT_ID != "TU_CLIENT_ID")
            {
                try
                {
                    var token = await GetSpTokenAsync();
                    if (token != null)
                    {
                        string q = Uri.EscapeDataString($"track:{item.Title} artist:{item.Artist}");
                        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        var resp = await _http.GetStringAsync($"https://api.spotify.com/v1/search?q={q}&type=track&limit=1");
                        _http.DefaultRequestHeaders.Authorization = null;
                        var root = JsonDocument.Parse(resp).RootElement;
                        var items = root.GetProperty("tracks").GetProperty("items");
                        if (items.GetArrayLength() > 0)
                        {
                            var track = items[0];
                            var images = track.GetProperty("album").GetProperty("images");
                            string imgUrl = images.GetArrayLength() > 0 ? images[0].GetProperty("url").GetString() : null;
                            string spArtist = track.GetProperty("artists")[0].GetProperty("name").GetString() ?? item.Artist;
                            string spAlbum = track.GetProperty("album").GetProperty("name").GetString() ?? item.Album;
                            if (imgUrl != null)
                            {
                                using var ms = new MemoryStream(await _http.GetByteArrayAsync(imgUrl));
                                var img = Image.FromStream(ms); item.AlbumArt = img;
                                SafeInvoke(() =>
                                {
                                    if (IsDisposed) return;
                                    picArt.Image = img; picArt.Invalidate(); lstQueue.Invalidate();
                                    if (string.IsNullOrEmpty(item.Artist)) lblArtist.Text = spArtist;
                                    if (string.IsNullOrEmpty(item.Album)) lblAlbum.Text = spAlbum;
                                });
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex) { AppLogger.Warn("FetchArtAsync Spotify: " + ex.Message); _http.DefaultRequestHeaders.Authorization = null; }
            }

            try
            {
                string q = Uri.EscapeDataString($"{item.Artist} {item.Title}".Trim());
                var resp = await _http.GetStringAsync($"https://itunes.apple.com/search?term={q}&media=music&limit=1");
                var res = JsonDocument.Parse(resp).RootElement.GetProperty("results");
                if (res.GetArrayLength() > 0)
                {
                    var url = (res[0].GetProperty("artworkUrl100").GetString() ?? "").Replace("100x100bb", "600x600bb");
                    if (!string.IsNullOrEmpty(url))
                    {
                        using var ms = new MemoryStream(await _http.GetByteArrayAsync(url));
                        var img = Image.FromStream(ms); item.AlbumArt = img;
                        SafeInvoke(() => { if (!IsDisposed) { picArt.Image = img; picArt.Invalidate(); lstQueue.Invalidate(); } });
                    }
                }
            }
            catch (Exception ex) { AppLogger.Warn("FetchArtAsync iTunes: " + ex.Message); }
        }

        async Task FetchLyricsAsync(PlaylistItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.Lyrics))
            {
                SafeInvoke(() => { if (!IsDisposed) { SetLyricsPlain(item.Lyrics); RenderLyrics(); lblLyricsStatus.Text = "  Letra del archivo"; } });
                return;
            }
            if (string.IsNullOrWhiteSpace(item.Artist) && string.IsNullOrWhiteSpace(item.Title)) return;

            bool found = false;
            try
            {
                var a = Uri.EscapeDataString(item.Artist.Trim());
                var t = Uri.EscapeDataString(item.Title.Trim());
                var al = Uri.EscapeDataString(item.Album.Trim());
                int dur = (int)item.Duration.TotalSeconds;
                var resp = await _http.GetStringAsync($"https://lrclib.net/api/get?artist_name={a}&track_name={t}&album_name={al}&duration={dur}");
                var root = JsonDocument.Parse(resp).RootElement;

                if (root.TryGetProperty("syncedLyrics", out var sl) && sl.ValueKind != JsonValueKind.Null && !string.IsNullOrWhiteSpace(sl.GetString()))
                {
                    ParseLrc(sl.GetString()!);
                    if (_syncedLines.Count > 0)
                    {
                        item.Lyrics = sl.GetString()!;
                        SafeInvoke(() => { if (!IsDisposed) { RenderLyrics(); lblLyricsStatus.Text = "  Sincronizada · lrclib.net"; } });
                        found = true;
                    }
                }
                if (!found && root.TryGetProperty("plainLyrics", out var pl) && pl.ValueKind != JsonValueKind.Null && !string.IsNullOrWhiteSpace(pl.GetString()))
                {
                    item.Lyrics = pl.GetString()!;
                    SafeInvoke(() => { if (!IsDisposed) { SetLyricsPlain(item.Lyrics); RenderLyrics(); lblLyricsStatus.Text = "  lrclib.net"; } });
                    found = true;
                }
            }
            catch (Exception ex) { AppLogger.Warn("FetchLyricsAsync lrclib: " + ex.Message); }

            if (!found)
            {
                try
                {
                    var resp = await _http.GetStringAsync($"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(item.Artist.Trim())}/{Uri.EscapeDataString(item.Title.Trim())}");
                    var lp = JsonDocument.Parse(resp).RootElement;
                    if (lp.TryGetProperty("lyrics", out var lv) && !string.IsNullOrWhiteSpace(lv.GetString()))
                    {
                        item.Lyrics = lv.GetString()!.Trim();
                        SafeInvoke(() => { if (!IsDisposed) { SetLyricsPlain(item.Lyrics); RenderLyrics(); lblLyricsStatus.Text = "  lyrics.ovh"; } });
                        found = true;
                    }
                }
                catch (Exception ex) { AppLogger.Warn("FetchLyricsAsync lyrics.ovh: " + ex.Message); }
            }

            if (!found)
                SafeInvoke(() => { if (!IsDisposed) { rtbLyrics.SelectionColor = SpDark; rtbLyrics.AppendText("(Sin letra)"); lblLyricsStatus.Text = "Sin letra"; } });
        }

        // ════════════════════════════════════════════════════════════
        //  METADATOS
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Lee metadatos en este orden: caché local → etiquetas del archivo.
        /// Guarda en caché de memoria para evitar releer el archivo.
        /// </summary>
        static PlaylistItem ReadMetaNoWait(string path)
        {
            if (_metaCache.TryGetValue(path, out var mem)) return mem;

            // 1 — Caché local en disco
            var cached = LoadCache(path);
            if (cached != null && (!string.IsNullOrEmpty(cached.Title) || !string.IsNullOrEmpty(cached.Artist)))
            {
                var fromCache = new PlaylistItem { FilePath = path, Title = Path.GetFileNameWithoutExtension(path) };
                ApplyCache(cached, fromCache);
                _metaCache[path] = fromCache;
                return fromCache;
            }

            // 2 — Tags del archivo
            var item = new PlaylistItem { FilePath = path, Title = Path.GetFileNameWithoutExtension(path) };
            try
            {
                var tag = TagLib.File.Create(path);
                item.Title = string.IsNullOrEmpty(tag.Tag.Title) ? item.Title : tag.Tag.Title;
                item.Artist = tag.Tag.FirstPerformer ?? "";
                item.Album = tag.Tag.Album ?? "";
                item.Year = tag.Tag.Year;
                item.Lyrics = tag.Tag.Lyrics ?? "";
                item.Genere = tag.Tag.FirstGenre ?? "";
                item.Duration = tag.Properties.Duration;
                if (tag.Tag.Pictures.Length > 0)
                {
                    using var ms = new MemoryStream(tag.Tag.Pictures[0].Data.Data);
                    using var tmp = new System.Drawing.Bitmap(ms);
                    item.AlbumArt = new System.Drawing.Bitmap(tmp);
                }
            }
            catch (Exception ex) { AppLogger.Warn($"ReadMetaNoWait [{path}]: {ex.Message}"); }

            _metaCache[path] = item;
            return item;
        }

        static PlaylistItem ReadMeta(string path) => ReadMetaNoWait(path);

        async Task<string> GetSpTokenAsync()
        {
            if (_spToken != null && DateTime.UtcNow < _spTokenExp) return _spToken;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
                var cred = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{SP_CLIENT_ID}:{SP_CLIENT_SECRET}"));
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", cred);
                req.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") });
                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;
                var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                _spToken = json.RootElement.GetProperty("access_token").GetString();
                _spTokenExp = DateTime.UtcNow.AddSeconds(json.RootElement.GetProperty("expires_in").GetInt32() - 30);
                return _spToken;
            }
            catch (Exception ex) { AppLogger.Warn("GetSpTokenAsync: " + ex.Message); return null; }
        }
    }
}
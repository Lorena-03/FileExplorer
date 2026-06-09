using FileExplorer.Helpers;
using FileExplorer.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Gestión de caché local de metadatos y letras para el reproductor de música.
    /// Los datos se guardan en JSON en AppData/FileExplorer/MusicCache/ usando el
    /// hash MD5 del path como nombre de archivo, evitando rutas inválidas.
    /// </summary>
    public partial class MusicPlayerForm
    {
        // ── Ruta de la caché ──────────────────────────────────────────
        static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileExplorer", "MusicCache");

        // ── Modelo de caché ───────────────────────────────────────────
        class TrackCache
        {
            public string FilePath { get; set; } = "";
            public string Title { get; set; } = "";
            public string Artist { get; set; } = "";
            public string Album { get; set; } = "";
            public string Genre { get; set; } = "";
            public uint Year { get; set; }
            public double DurationSeconds { get; set; } // duración real del audio
            public string Lyrics { get; set; } = "";
            public string LyricsType { get; set; } = "";
            public string AlbumArtBase64 { get; set; } = "";
            public DateTime CachedAt { get; set; } = DateTime.UtcNow;
        }

        // ════════════════════════════════════════════════════════════
        //  LEER CACHÉ
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Intenta cargar los metadatos de una canción desde la caché local.
        /// Devuelve null si no existe o está corrupta.
        /// </summary>
        static TrackCache? LoadCache(string filePath)
        {
            try
            {
                string key = GetCacheKey(filePath);
                string file = Path.Combine(CacheDir, key + ".json");
                if (!File.Exists(file)) return null;
                string json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                return JsonSerializer.Deserialize<TrackCache>(json);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("MusicCache.LoadCache: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Aplica los datos de la caché a un PlaylistItem: título, artista, álbum,
        /// género, letra y carátula. Devuelve true si se aplicaron datos.
        /// </summary>
        static bool ApplyCache(TrackCache cache, PlaylistItem item)
        {
            if (cache == null) return false;
            if (!string.IsNullOrEmpty(cache.Title)) item.Title = cache.Title;
            if (!string.IsNullOrEmpty(cache.Artist)) item.Artist = cache.Artist;
            if (!string.IsNullOrEmpty(cache.Album)) item.Album = cache.Album;
            if (!string.IsNullOrEmpty(cache.Genre)) item.Genere = cache.Genre;
            if (cache.Year > 0) item.Year = cache.Year;
            if (!string.IsNullOrEmpty(cache.Lyrics)) item.Lyrics = cache.Lyrics;
            if (cache.DurationSeconds > 0)
                item.Duration = TimeSpan.FromSeconds(cache.DurationSeconds);

            if (!string.IsNullOrEmpty(cache.AlbumArtBase64))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(cache.AlbumArtBase64);
                    using var ms = new MemoryStream(bytes);
                    using var tmp = new System.Drawing.Bitmap(ms);
                    item.AlbumArt = new System.Drawing.Bitmap(tmp);
                }
                catch (Exception ex) { AppLogger.Warn("MusicCache.ApplyCache art: " + ex.Message); }
            }
            return true;
        }

        // ════════════════════════════════════════════════════════════
        //  GUARDAR CACHÉ
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Guarda los metadatos y letra de un PlaylistItem en la caché local.
        /// La carátula se serializa en Base64 (máx 300×300 px para ahorrar espacio).
        /// </summary>
        static void SaveCache(PlaylistItem item, string lyricsType = "plain")
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                string key = GetCacheKey(item.FilePath);

                string artBase64 = "";
                if (item.AlbumArt != null)
                {
                    try
                    {
                        int maxSz = 300;
                        int w = Math.Min(item.AlbumArt.Width, maxSz);
                        int h = Math.Min(item.AlbumArt.Height, maxSz);
                        using var bmp = new System.Drawing.Bitmap(item.AlbumArt, w, h);
                        using var ms = new MemoryStream();
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        artBase64 = Convert.ToBase64String(ms.ToArray());
                    }
                    catch (Exception ex) { AppLogger.Warn("MusicCache.SaveCache art: " + ex.Message); }
                }

                var cache = new TrackCache
                {
                    FilePath = item.FilePath,
                    Title = item.Title ?? "",
                    Artist = item.Artist ?? "",
                    Album = item.Album ?? "",
                    Genre = item.Genere ?? "",
                    Year = item.Year,
                    DurationSeconds = item.Duration.TotalSeconds,
                    Lyrics = item.Lyrics ?? "",
                    LyricsType = lyricsType,
                    AlbumArtBase64 = artBase64,
                    CachedAt = DateTime.UtcNow,
                };

                string json = JsonSerializer.Serialize(cache,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(CacheDir, key + ".json"),
                    json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("MusicCache.SaveCache: " + ex.Message);
            }
        }

        /// <summary>
        /// Actualiza solo los metadatos editables (título, artista, álbum, género)
        /// en la caché sin tocar la letra ni la carátula existentes.
        /// </summary>
        static void UpdateMetaCache(PlaylistItem item)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                string key = GetCacheKey(item.FilePath);
                string file = Path.Combine(CacheDir, key + ".json");

                TrackCache? existing = null;
                if (File.Exists(file))
                {
                    try { existing = JsonSerializer.Deserialize<TrackCache>(File.ReadAllText(file)); }
                    catch { }
                }

                existing ??= new TrackCache { FilePath = item.FilePath };
                existing.Title = item.Title ?? "";
                existing.Artist = item.Artist ?? "";
                existing.Album = item.Album ?? "";
                existing.Genre = item.Genere ?? "";
                existing.Year = item.Year;
                // DurationSeconds, Lyrics y AlbumArtBase64 NO se tocan
                if (item.Duration.TotalSeconds > 0)
                    existing.DurationSeconds = item.Duration.TotalSeconds;
                existing.CachedAt = DateTime.UtcNow;

                File.WriteAllText(file,
                    JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }),
                    System.Text.Encoding.UTF8);
            }
            catch (Exception ex) { AppLogger.Warn("MusicCache.UpdateMetaCache: " + ex.Message); }
        }

        // ════════════════════════════════════════════════════════════
        //  EDITAR METADATOS
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Abre el diálogo de edición de metadatos para la canción actual.
        /// Permite editar título, artista, álbum, género y año.
        /// Los cambios se guardan en la caché local y se reflejan en la UI.
        /// </summary>
        void EditCurrentMetadata()
        {
            if (_currentIdx < 0 || _currentIdx >= _queue.Count)
            { MessageBox.Show("No hay canción activa.", "Info"); return; }

            var item = _queue[_currentIdx];

            using var frm = new Form
            {
                Text = "Editar metadatos",
                ClientSize = new Size(420, 340),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = SpPanel,
                ForeColor = SpWhite,
                AutoScaleMode = AutoScaleMode.None,
            };

            // Helper para agregar campo
            int y = 16;
            System.Windows.Forms.TextBox AddField(string label, string value)
            {
                frm.Controls.Add(new Label
                {
                    Text = label,
                    Left = 16,
                    Top = y,
                    Width = 140,
                    Height = 18,
                    ForeColor = SpGray,
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 8.5f),
                });
                var tb = new System.Windows.Forms.TextBox
                {
                    Left = 16,
                    Top = y + 20,
                    Width = 386,
                    Height = 26,
                    Font = new Font("Segoe UI", 10f),
                    BackColor = SpCard,
                    ForeColor = SpWhite,
                    BorderStyle = BorderStyle.FixedSingle,
                    Text = value ?? "",
                };
                frm.Controls.Add(tb);
                y += 54;
                return tb;
            }

            var txtTitle = AddField("Título", item.Title);
            var txtArtist = AddField("Artista", item.Artist);
            var txtAlbum = AddField("Álbum", item.Album);
            var txtGenre = AddField("Género", item.Genere);

            // Año
            frm.Controls.Add(new Label
            {
                Text = "Año",
                Left = 16,
                Top = y,
                Width = 60,
                Height = 18,
                ForeColor = SpGray,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f),
            });
            var nudYear = new NumericUpDown
            {
                Left = 16,
                Top = y + 20,
                Width = 100,
                Height = 26,
                Minimum = 0,
                Maximum = 2099,
                Value = Math.Min(item.Year, 2099),
                Font = new Font("Segoe UI", 10f),
                BackColor = SpCard,
                ForeColor = SpWhite,
            };
            frm.Controls.Add(nudYear);
            y += 54;

            // Separador
            frm.Controls.Add(new Label
            {
                Left = 0,
                Top = y,
                Width = 420,
                Height = 1,
                BackColor = Color.FromArgb(60, 60, 60),
            });
            y += 10;

            // Botones
            var btnCan = new Button
            {
                Text = "Cancelar",
                Left = 210,
                Top = y,
                Width = 96,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = SpCard,
                ForeColor = SpGray,
                DialogResult = DialogResult.Cancel,
                Font = new Font("Segoe UI", 9f),
            };
            btnCan.FlatAppearance.BorderSize = 0;

            var btnOk = new Button
            {
                Text = "Guardar",
                Left = 314,
                Top = y,
                Width = 90,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = SpGreen,
                ForeColor = Color.Black,
                DialogResult = DialogResult.OK,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };
            btnOk.FlatAppearance.BorderSize = 0;

            frm.Controls.AddRange(new Control[] { btnCan, btnOk });
            frm.AcceptButton = btnOk;
            frm.CancelButton = btnCan;
            frm.ClientSize = new Size(420, y + 42);

            if (frm.ShowDialog(this) != DialogResult.OK) return;

            // Aplicar cambios al item
            item.Title = txtTitle.Text.Trim();
            item.Artist = txtArtist.Text.Trim();
            item.Album = txtAlbum.Text.Trim();
            item.Genere = txtGenre.Text.Trim();
            item.Year = (uint)nudYear.Value;

            // Actualizar UI
            lblTitle.Text = item.DisplayName;
            lblArtist.Text = item.DisplayArtist;
            lblAlbum.Text = item.DisplayAlbum;
            lstQueue.Invalidate();

            // Guardar en caché
            UpdateMetaCache(item);

            // También intentar escribir en las tags del archivo
            try
            {
                var tag = TagLib.File.Create(item.FilePath);
                tag.Tag.Title = item.Title;
                tag.Tag.Performers = new[] { item.Artist };
                tag.Tag.Album = item.Album;
                tag.Tag.Genres = string.IsNullOrEmpty(item.Genere)
                    ? Array.Empty<string>() : new[] { item.Genere };
                tag.Tag.Year = item.Year;
                // Duration es propiedad de audio, no se puede escribir con TagLib
                tag.Save();
                MessageBox.Show("Metadatos guardados en el archivo y en la caché local.",
                    "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                // Si no se puede escribir en el archivo (p.ej. está en uso), solo guardamos en caché
                MessageBox.Show("Metadatos guardados en la caché local.\n(No se pudo escribir en el archivo de audio.)",
                    "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            // Invalidar caché en memoria para que se recargue con los datos nuevos
            _metaCache.TryRemove(item.FilePath, out _);
        }

        // ════════════════════════════════════════════════════════════
        //  HELPER
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Genera una clave única para la caché usando MD5 del path del archivo,
        /// evitando problemas con rutas largas o caracteres especiales.
        /// </summary>
        static string GetCacheKey(string filePath)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(filePath.ToLowerInvariant()));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
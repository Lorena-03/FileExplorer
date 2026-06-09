namespace FileExplorer.Helpers
{
    public static class FileHelper
    {
        // ── HashSets para categorización rápida ──────────────────────

        /// <summary>Extensiones de archivos de audio soportadas.</summary>
        public static readonly HashSet<string> AudioExt = new(StringComparer.OrdinalIgnoreCase)
            { ".mp3",".wav",".flac",".aac",".ogg",".m4a",".wma",".opus",".ape" };

        /// <summary>Extensiones de archivos de video soportadas.</summary>
        public static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
            { ".mp4",".avi",".mkv",".mov",".wmv",".flv",".webm",".m4v",".3gp",".ts" };

        /// <summary>Extensiones de archivos de imagen soportadas.</summary>
        public static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg",".jpeg",".png",".bmp",".gif",".tiff",".tif",".webp",".ico" };

        /// <summary>Extensiones de archivos de datos estructurados (texto, config, logs).</summary>
        public static readonly HashSet<string> DataExt = new(StringComparer.OrdinalIgnoreCase)
            { ".csv",".json",".xml",".txt",".md",".log",".ini",".yaml",".yml" };

        /// <summary>Extensiones de documentos de Office y PDF.</summary>
        public static readonly HashSet<string> OfficeExt = new(StringComparer.OrdinalIgnoreCase)
            { ".docx",".doc",".xlsx",".xls",".pptx",".ppt",".pdf" };

        public enum FileCategory { Audio, Video, Image, Data, Office, Other }

        // ── Diccionario extensión → categoría (O(1)) ──────────────────
        static readonly Dictionary<string, FileCategory> _categoryMap = BuildCategoryMap();

        /// <summary>
        /// Construye el mapa de categorías consolidando todos los HashSets en un solo diccionario
        /// para lookups en O(1) en lugar de múltiples Contains().
        /// </summary>
        static Dictionary<string, FileCategory> BuildCategoryMap()
        {
            var d = new Dictionary<string, FileCategory>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in AudioExt) d[e] = FileCategory.Audio;
            foreach (var e in VideoExt) d[e] = FileCategory.Video;
            foreach (var e in ImageExt) d[e] = FileCategory.Image;
            foreach (var e in DataExt) d[e] = FileCategory.Data;
            foreach (var e in OfficeExt) d[e] = FileCategory.Office;
            return d;
        }

        /// <summary>Devuelve la categoría del archivo según su extensión en O(1).</summary>
        public static FileCategory Categorize(string path)
        {
            var ext = Path.GetExtension(path);
            return _categoryMap.TryGetValue(ext, out var cat) ? cat : FileCategory.Other;
        }

        // ── Diccionario extensión → descripción legible ───────────────
        static readonly Dictionary<string, string> _typeDescriptions
            = new(StringComparer.OrdinalIgnoreCase)
            {
                [".mp3"] = "Audio MP3",
                [".wav"] = "Audio WAV",
                [".flac"] = "Audio FLAC",
                [".aac"] = "Audio AAC",
                [".mp4"] = "Video MP4",
                [".avi"] = "Video AVI",
                [".mkv"] = "Video MKV",
                [".mov"] = "Video MOV",
                [".pdf"] = "PDF",
                [".docx"] = "Word",
                [".doc"] = "Word",
                [".xlsx"] = "Excel",
                [".xls"] = "Excel",
                [".pptx"] = "PowerPoint",
                [".ppt"] = "PowerPoint",
                [".csv"] = "CSV",
                [".json"] = "JSON",
                [".xml"] = "XML",
                [".txt"] = "Texto",
                [".jpg"] = "JPEG",
                [".jpeg"] = "JPEG",
                [".png"] = "PNG",
                [".gif"] = "GIF",
            };

        /// <summary>
        /// Devuelve una descripción legible del tipo de archivo (p.ej. "Audio MP3").
        /// Si la extensión no está en el mapa, devuelve la extensión en mayúsculas.
        /// </summary>
        public static string GetTypeDescription(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return _typeDescriptions.TryGetValue(ext, out var desc)
                ? desc
                : ext.TrimStart('.').ToUpperInvariant();
        }

        // ── Índices de íconos en el ImageList ─────────────────────────
        public const int ICO_FOLDER = 0, ICO_FILE = 1, ICO_AUDIO = 2,
                         ICO_VIDEO = 3, ICO_IMAGE = 4, ICO_PDF = 5,
                         ICO_WORD = 6, ICO_EXCEL = 7, ICO_PPT = 8,
                         ICO_DATA = 9, ICO_DRIVE = 10;

        static readonly Dictionary<string, int> _iconMap
            = new(StringComparer.OrdinalIgnoreCase)
            {
                [".mp3"] = ICO_AUDIO,
                [".wav"] = ICO_AUDIO,
                [".flac"] = ICO_AUDIO,
                [".aac"] = ICO_AUDIO,
                [".ogg"] = ICO_AUDIO,
                [".m4a"] = ICO_AUDIO,
                [".wma"] = ICO_AUDIO,
                [".opus"] = ICO_AUDIO,
                [".ape"] = ICO_AUDIO,
                [".mp4"] = ICO_VIDEO,
                [".avi"] = ICO_VIDEO,
                [".mkv"] = ICO_VIDEO,
                [".mov"] = ICO_VIDEO,
                [".wmv"] = ICO_VIDEO,
                [".flv"] = ICO_VIDEO,
                [".webm"] = ICO_VIDEO,
                [".m4v"] = ICO_VIDEO,
                [".ts"] = ICO_VIDEO,
                [".jpg"] = ICO_IMAGE,
                [".jpeg"] = ICO_IMAGE,
                [".png"] = ICO_IMAGE,
                [".bmp"] = ICO_IMAGE,
                [".gif"] = ICO_IMAGE,
                [".tiff"] = ICO_IMAGE,
                [".webp"] = ICO_IMAGE,
                [".ico"] = ICO_IMAGE,
                [".csv"] = ICO_DATA,
                [".json"] = ICO_DATA,
                [".xml"] = ICO_DATA,
                [".txt"] = ICO_DATA,
                [".md"] = ICO_DATA,
                [".log"] = ICO_DATA,
                [".ini"] = ICO_DATA,
                [".yaml"] = ICO_DATA,
                [".yml"] = ICO_DATA,
                [".pdf"] = ICO_PDF,
                [".docx"] = ICO_WORD,
                [".doc"] = ICO_WORD,
                [".xlsx"] = ICO_EXCEL,
                [".xls"] = ICO_EXCEL,
                [".pptx"] = ICO_PPT,
                [".ppt"] = ICO_PPT,
            };

        /// <summary>
        /// Devuelve el índice de ícono en el ImageList para la ruta indicada.
        /// Las carpetas siempre devuelven ICO_FOLDER; los archivos sin mapa devuelven ICO_FILE.
        /// </summary>
        public static int GetIconIndex(string path, bool isDir)
        {
            if (isDir) return ICO_FOLDER;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return _iconMap.TryGetValue(ext, out var idx) ? idx : ICO_FILE;
        }

        // ── Diccionario extensión → emoji (archivos recientes en Home) ─
        static readonly Dictionary<string, string> _emojiMap
            = new(StringComparer.OrdinalIgnoreCase)
            {
                [".pdf"] = "📄",
                [".docx"] = "📝",
                [".doc"] = "📝",
                [".xlsx"] = "📊",
                [".xls"] = "📊",
                [".jpg"] = "🖼️",
                [".jpeg"] = "🖼️",
                [".png"] = "🖼️",
                [".gif"] = "🖼️",
                [".mp3"] = "🎵",
                [".wav"] = "🎵",
                [".mp4"] = "🎬",
                [".avi"] = "🎬",
                [".mkv"] = "🎬",
            };

        /// <summary>Devuelve el emoji correspondiente a la extensión del archivo, o 📎 si no hay mapa.</summary>
        public static string GetEmoji(string path)
        {
            var ext = Path.GetExtension(path);
            return _emojiMap.TryGetValue(ext, out var emoji) ? emoji : "📎";
        }

        /// <summary>
        /// Formatea un tamaño en bytes a una cadena legible:
        /// B, KB (1 decimal), MB (1 decimal) o GB (2 decimales).
        /// </summary>
        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes / 1073741824.0:F2} GB";
        }

        /// <summary>
        /// Construye el ImageList de íconos para el ListView del explorador.
        /// Genera íconos de color sólido con una letra identificadora pintada con GDI+.
        /// El orden de inserción debe coincidir con las constantes ICO_*.
        /// </summary>
        public static ImageList BuildIconList(int size = 16)
        {
            var il = new ImageList
            {
                ImageSize = new Size(size, size),
                ColorDepth = ColorDepth.Depth32Bit,
            };
            // (color de fondo, letra) — orden: carpeta, genérico, audio, video, imagen,
            // PDF, Word, Excel, PPT, datos, unidad
            (Color bg, string ch)[] defs =
            {
                (Color.FromArgb(255, 204,   0), "F"),
                (Color.FromArgb(180, 180, 185), "D"),
                (Color.FromArgb(255,  45,  85), "A"),
                (Color.FromArgb( 52, 199,  89), "V"),
                (Color.FromArgb(  0, 199, 190), "I"),
                (Color.FromArgb(255,  59,  48), "P"),
                (Color.FromArgb(  0, 122, 255), "W"),
                (Color.FromArgb( 52, 199,  89), "X"),
                (Color.FromArgb(255, 149,   0), "S"),
                (Color.FromArgb(175,  82, 222), "J"),
                (Color.FromArgb( 99,  99, 102), "D"),
            };
            foreach (var (bg, ch) in defs)
            {
                var bmp = new Bitmap(size, size);
                using var g = Graphics.FromImage(bmp);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillRectangle(new SolidBrush(bg), 0, 0, size, size);
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                g.DrawString(ch,
                    new Font("Segoe UI", size <= 16 ? 7f : 11f, FontStyle.Bold),
                    Brushes.White, new RectangleF(0, 0, size, size), sf);
                il.Images.Add(bmp);
            }
            return il;
        }

        /// <summary>
        /// Copia recursivamente un directorio completo al destino indicado,
        /// creando las subcarpetas necesarias y sobreescribiendo archivos existentes.
        /// </summary>
        public static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src))
                CopyDirectory(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        /// <summary>
        /// Devuelve una ruta que no colisiona con archivos o carpetas existentes.
        /// Si la ruta ya existe, agrega un sufijo "(2)", "(3)", etc. hasta encontrar una libre.
        /// </summary>
        public static string GetUniquePath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return path;
            var dir = Path.GetDirectoryName(path) ?? "";
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            int i = 2; string c;
            do { c = Path.Combine(dir, $"{name} ({i++}){ext}"); }
            while (File.Exists(c) || Directory.Exists(c));
            return c;
        }
    }
}
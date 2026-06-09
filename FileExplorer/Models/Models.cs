namespace FileExplorer.Models
{
    public class PlaylistItem
    {
        public string FilePath { get; set; } = "";
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public uint Year { get; set; }
        public TimeSpan Duration { get; set; }
        public Image AlbumArt { get; set; }
        public string Lyrics { get; set; } = "";

        /// <summary>Devuelve el título si existe, o el nombre del archivo sin extensión.</summary>
        public string DisplayName =>
            string.IsNullOrWhiteSpace(Title) ? Path.GetFileNameWithoutExtension(FilePath) : Title;

        /// <summary>Devuelve el artista si existe, o "Artista desconocido".</summary>
        public string DisplayArtist =>
            string.IsNullOrWhiteSpace(Artist) ? "Artista desconocido" : Artist;

        /// <summary>Devuelve el álbum si existe, o "Álbum desconocido".</summary>
        public string DisplayAlbum =>
            string.IsNullOrWhiteSpace(Album) ? "Álbum desconocido" : Album;

        /// <summary>Formatea la duración como m:ss o h:mm:ss según corresponda.</summary>
        public string DurationText =>
            Duration.TotalHours >= 1
                ? Duration.ToString(@"h\:mm\:ss")
                : Duration.ToString(@"m\:ss");

        public string Genere { get; internal set; }
    }

    public enum ValidationErrorType
    {
        InvalidDate,
        InvalidPostalCode,
        InvalidEmail,
        EmptyField,
        InvalidPhone
    }

    public class ValidationError
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public string FieldName { get; set; } = "";
        public string Value { get; set; } = "";
        public ValidationErrorType ErrorType { get; set; }
        public string Description { get; set; } = "";
        public string Suggestion { get; set; } = "";
        public bool Fixed { get; set; } = false;

        // ── Aliases para compatibilidad con FileEditorForm.Errors.cs ──

        /// <summary>Alias de Column — índice de columna del error.</summary>
        public int Col => Column;

        /// <summary>Alias de FieldName — nombre de la columna.</summary>
        public string ColumnName => FieldName;

        /// <summary>Alias de Suggestion — valor sugerido para corrección automática.</summary>
        public string AutoFix
        {
            get => Suggestion;
            set => Suggestion = value;
        }

        /// <summary>Indica si el error tiene una sugerencia aplicable y no es un campo vacío.</summary>
        public bool CanAutoFix =>
            !string.IsNullOrEmpty(Suggestion) &&
            ErrorType != ValidationErrorType.EmptyField;
    }

    public class GpsLocation
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public bool HasData { get; set; }

        /// <summary>Devuelve la latitud formateada con indicador N/S.</summary>
        public string LatitudeText =>
            $"{Math.Abs(Latitude):F6}° {(Latitude >= 0 ? "N" : "S")}";

        /// <summary>Devuelve la longitud formateada con indicador E/W.</summary>
        public string LongitudeText =>
            $"{Math.Abs(Longitude):F6}° {(Longitude >= 0 ? "E" : "W")}";

        /// <summary>Devuelve la altitud formateada en metros.</summary>
        public string AltitudeText =>
            $"{Altitude:F1} m";

        /// <summary>Formatea un valor double usando punto como separador decimal (invariante).</summary>
        string I(double v) =>
            v.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>URL de Google Maps apuntando a las coordenadas actuales.</summary>
        public string GoogleMapsUrl =>
            $"https://www.google.com/maps?q={I(Latitude)},{I(Longitude)}";

        /// <summary>URL de OpenStreetMap apuntando a las coordenadas actuales con zoom 15.</summary>
        public string OpenStreetMapUrl =>
            $"https://www.openstreetmap.org/?mlat={I(Latitude)}&mlon={I(Longitude)}&zoom=15";
    }
}
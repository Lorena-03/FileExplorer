using FileExplorer.Models;
using System.Drawing.Imaging;

namespace FileExplorer.Helpers
{
    public static class GpsHelper
    {
        // IDs de las propiedades EXIF estándar para coordenadas GPS
        const int LAT_REF = 0x0001; // Hemisferio latitud: "N" o "S"
        const int LAT = 0x0002; // Valor latitud en grados/minutos/segundos
        const int LON_REF = 0x0003; // Hemisferio longitud: "E" o "W"
        const int LON = 0x0004; // Valor longitud en grados/minutos/segundos
        const int ALT_REF = 0x0005; // Referencia altitud: 0 = sobre el mar
        const int ALT = 0x0006; // Valor altitud en metros

        /// <summary>
        /// Carga la imagen desde disco y extrae sus coordenadas GPS.
        /// Devuelve <c>null</c> si el archivo no existe, no es imagen o no tiene GPS.
        /// </summary>
        public static GpsLocation TryRead(string path)
        {
            try
            {
                using var b = new Bitmap(path);
                return TryRead(b);
            }
            catch { return null; }
        }

        /// <summary>
        /// Extrae latitud, longitud y altitud de los metadatos EXIF de la imagen.
        /// Aplica el signo negativo según el hemisferio (S/W).
        /// Devuelve <c>null</c> si la imagen no contiene las propiedades GPS mínimas.
        /// </summary>
        public static GpsLocation TryRead(Image img)
        {
            try
            {
                var ids = img.PropertyIdList;
                if (!ids.Contains(LAT) || !ids.Contains(LON)) return null;

                double lat = ReadRat(img.GetPropertyItem(LAT));
                double lon = ReadRat(img.GetPropertyItem(LON));

                // Invertir signo si el hemisferio es Sur u Oeste
                if (Ascii(img.GetPropertyItem(LAT_REF)) == "S") lat = -lat;
                if (Ascii(img.GetPropertyItem(LON_REF)) == "W") lon = -lon;

                double alt = ids.Contains(ALT)
                    ? ReadSingleRat(img.GetPropertyItem(ALT))
                    : 0;

                return new GpsLocation
                {
                    Latitude = lat,
                    Longitude = lon,
                    Altitude = alt,
                    HasData = true,
                };
            }
            catch { return null; }
        }

        /// <summary>
        /// Convierte una propiedad EXIF de tipo racional con tres componentes
        /// (grados, minutos, segundos) a un valor decimal en grados.
        /// Requiere al menos 24 bytes (3 racionales × 8 bytes cada uno).
        /// </summary>
        static double ReadRat(PropertyItem item)
        {
            var b = item.Value ?? Array.Empty<byte>();
            if (b.Length < 24) return 0;
            return R(b, 0) + R(b, 8) / 60.0 + R(b, 16) / 3600.0;
        }

        /// <summary>
        /// Convierte una propiedad EXIF de tipo racional con un solo componente
        /// (p. ej. altitud) a valor decimal. Requiere al menos 8 bytes.
        /// </summary>
        static double ReadSingleRat(PropertyItem item)
        {
            var b = item.Value ?? Array.Empty<byte>();
            return b.Length < 8 ? 0 : R(b, 0);
        }

        /// <summary>
        /// Lee un número racional EXIF (numerador / denominador) desde el offset
        /// indicado en el array de bytes. Devuelve 0 si el denominador es cero.
        /// </summary>
        static double R(byte[] b, int o)
        {
            uint n = BitConverter.ToUInt32(b, o);
            uint d = BitConverter.ToUInt32(b, o + 4);
            return d == 0 ? 0 : (double)n / d;
        }

        /// <summary>
        /// Decodifica el valor de una propiedad EXIF como texto ASCII,
        /// eliminando nulos y espacios en los extremos.
        /// Devuelve cadena vacía si el valor es nulo.
        /// </summary>
        static string Ascii(PropertyItem item) =>
            item?.Value == null
                ? ""
                : System.Text.Encoding.ASCII
                    .GetString(item.Value).Trim('\0').Trim();

        /// <summary>
        /// Genera el HTML completo de un mapa Leaflet centrado en las coordenadas
        /// del <see cref="GpsLocation"/> indicado, con un marcador y popup en el punto exacto.
        /// El HTML es autónomo y puede cargarse directamente en un WebBrowser o archivo.
        /// </summary>
        public static string BuildMapHtml(GpsLocation loc)
        {
            var lat = loc.Latitude.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            var lon = loc.Longitude.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

            return $@"<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'/>
  <link rel='stylesheet'
        href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
  <style>
    html, body, #map {{ margin:0; padding:0; width:100%; height:100%; }}
  </style>
</head>
<body>
  <div id='map'></div>
  <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
  <script>
    var map = L.map('map').setView([{lat}, {lon}], 15);
    L.tileLayer(
      'https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png'
    ).addTo(map);
    L.marker([{lat}, {lon}])
      .addTo(map)
      .bindPopup('{loc.LatitudeText}, {loc.LongitudeText}')
      .openPopup();
  </script>
</body>
</html>";
        }
    }
}
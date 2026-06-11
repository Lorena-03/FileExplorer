#  File Explorer



File Explorer es una aplicación creada en C# Windows Forms que integra diferentes herramientas para la gestión de archivos, edición de documentos, validación de datos, administración de bases de datos y manejo de contenido multimedia. Su objetivo es centralizar estas funciones en una sola plataforma, facilitando la organización, el procesamiento y el almacenamiento eficiente de la información.
![image alt]( https://github.com/Lorena-03/FileExplorer/blob/47f7ae97182eb5a9604c6f7c85244980b0bee439/Captura%20de%20pantalla%202026-06-08%20193226.png)

<p align="center">
  Explorador de archivos avanzado desarrollado en C# Windows Forms
</p>

<p align="center">
  Administración de archivos • Multimedia • Bases de datos • Edición de documentos • Validación de datos
</p>

---

#  Capturas del sistema
 
---

##  Reproductor de música
![image alt](https://github.com/Lorena-03/FileExplorer/blob/70750ffc62368c63ded9b5d6878043a8c38ff680/Captura%20de%20pantalla%202026-06-08%20210044.png)


---

##  Reproductor de video

![image alt](https://github.com/Lorena-03/FileExplorer/blob/2bf8eb9f016fc02340ac86d8979e38d8888faadc/Captura%20de%20pantalla%202026-06-09%20090400.png)

---

## Editor de documentos
![image alt](https://github.com/Lorena-03/FileExplorer/blob/58cfe438ea6f4a5dd03a91f1b1c984fae087e21a/Captura%20de%20pantalla%202026-06-09%20085007.png)


---

## Editor de imágenes
![image alt](https://github.com/Lorena-03/FileExplorer/blob/d4980c97fe635a20571fdef090458dd1ff69d3cd/Captura%20de%20pantalla%202026-06-08%20205207.png)

---

##  Migración de bases de datos
![image alt](https://github.com/Lorena-03/FileExplorer/blob/80b58bd6b26bada2cb056212fb791e99b9384a6a/Captura%20de%20pantalla%202026-06-09%20071509.png)


---

# Descripción

File Explorer es una aplicación desarrollada en C# Windows Forms que integra herramientas para:

* Exploración y administración de archivos
* Reproducción multimedia
* Edición de documentos
* Validación de datos
* Conversión y exportación
* Migración hacia bases de datos
* Manejo de imágenes con GPS
* Edición de metadatos musicales

El proyecto fue desarrollado con una arquitectura modular utilizando formularios, helpers, controles personalizados y modelos.

---

#  Características principales

##  Explorador de archivos

* Navegación entre carpetas
* Exploración de unidades
* Copiar, mover y eliminar archivos
* Compresión ZIP
* Extracción ZIP
* Drag & Drop
* Filtros de búsqueda
* Papelera de reciclaje
* Iconos personalizados

##  Multimedia

* Reproductor musical
* Letras sincronizadas
* Ecualizador de 10 bandas
* Reproductor de video
* Grabador de audio
* Cámara web integrada
* Portadas automáticas
* Edición de metadatos MP3

##  Documentos

* Editor TXT
* Editor JSON
* Editor XML
* Editor CSV
* Editor Excel
* Editor Word
* Editor PDF
* Conversión de formatos

## Bases de datos

* Migración a SQL Server
* Migración a MariaDB
* Creación automática de tablas
* Validación previa de datos
* Consultas de registros
* Logs de migración

##  Procesamiento de datos

* Validación automática
* Corrección de errores
* Exportación de datos
* Conversión tabular
* Extracción desde Word/PDF

##  Imágenes y GPS

* Lectura EXIF
* Coordenadas GPS
* OpenStreetMap
* Google Maps
* Visualización geográfica

---

#  Arquitectura del proyecto

```text
FileExplorer/
│
├── Forms/
├── Helpers/
├── Controls/
├── Models/
├── Resources/
├── images/
├── README.md
└── FileExplorer.sln
```

---

# Formularios principales

##  Main Form

Ventana principal del sistema encargada de coordinar los módulos del explorador.

---

##  Music Player Form

Reproductor de música con:

* Letras sincronizadas
* Ecualizador
* Portadas automáticas
* Playlists
* Caché local
* Edición de metadatos

### Librerías utilizadas

* NAudio
* TagLib#
* Spotify API
* LRCLib API

---

##  Video Player Form

Reproductor multimedia basado en LibVLCSharp.

### Funciones

* Pantalla completa
* Control de velocidad
* Barra de progreso
* Atajos de teclado
* Control de volumen

### Librerías utilizadas

* LibVLCSharp
* LibVLCSharp.WinForms

---

## Word PDF Editor Form

Editor de documentos compatible con:

* DOCX
* PDF

### Funciones

* Negrita
* Cursiva
* Subrayado
* Imágenes
* Tablas
* Colores
* Exportación PDF

### Librerías utilizadas

* DocumentFormat.OpenXml
* UglyToad.PdfPig

---

## Migration Form

Sistema de migración de datos hacia:

* SQL Server
* MariaDB

### Funciones

* Verificación de conexión
* Creación automática de BD
* Creación de tablas
* Inserción de registros
* Barra de progreso
* Logs

### Librerías utilizadas

* Microsoft.Data.SqlClient
* MySqlConnector

---

#  Helpers principales

##  DatabaseMigrator

Gestiona:

* Conexiones
* Creación de BD
* Migraciones
* Inserciones masivas

---

##  DataExporter

Exportación a:

* CSV
* JSON
* XML
* TXT
* HTML
* Markdown
* Excel

---

##  DataParser

Procesamiento de:

* CSV
* JSON
* XML
* Excel
* TXT

---

## DataValidator

Valida:

* Emails
* Fechas
* Teléfonos
* Códigos postales
* Campos vacíos

---

##  EqualizerSampleProvider

Ecualizador de audio en tiempo real de 10 bandas.

---

##  FileHelper

Administración avanzada de archivos y directorios.

---

##  GpsHelper

Obtención de coordenadas GPS desde imágenes EXIF.

---

#  Controls personalizados

##  EqualizerControl

Ecualizador gráfico con presets:

* Rock
* Pop
* Jazz
* Clásica
* Electrónica
* Bass Boost

---

##  FileListControl

Control tipo Explorador de Windows.

### Funciones

* Abrir archivos
* Renombrar
* ZIP
* Papelera
* Drag & Drop

---

##  SidebarControl

Barra lateral de navegación rápida.

---

#  Modelos del sistema

##  PlaylistItem

Modelo de canciones y metadatos musicales.

##  ValidationError

Representa errores detectados durante validación.

##  GpsLocation

Modelo de coordenadas GPS y mapas.

##  MigrationOptions

Configuración de conexión para migraciones.

---

# Tecnologías utilizadas

## Lenguaje

* C#

## Framework

* .NET
* Windows Forms

## Librerías principales

* NAudio
* TagLib#
* LibVLCSharp
* Newtonsoft.Json
* OpenCvSharp
* SharpAvi
* ClosedXML
* NPOI
* PdfPig
* DocumentFormat.OpenXml
* MySqlConnector

---

#  APIs utilizadas

* Spotify API
* LRCLib API
* OpenStreetMap
* Leaflet API
* Google Maps
* SMTP

---

#  Requisitos

* Windows 10/11
* Visual Studio
* .NET Framework
* Cámara y micrófono opcionales

---

#  Instalación

## Clonar repositorio

```bash
git clone [https://github.com/usuario/file-explorer.git](https://github.com/Lorena-03/FileExplorer/tree/master)
```

## Abrir solución

```text
FileExplorer.sln
```

## Restaurar paquetes NuGet

```text
Restore NuGet Packages
```

## Ejecutar proyecto

```text
F5
```

---


---

#  Documentación

La documentación completa del sistema se encuentra incluida en el proyecto académico.

---

#  Autores
Lorena Cervantes, 
Sergio Flores

---

#  Licencia

Proyecto desarrollado con fines educativos y académicos.

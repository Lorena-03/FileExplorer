namespace FileExplorer.Helpers
{
    /// <summary>
    /// Logger simple que escribe a disco en:
    ///   %AppData%\FileExplorer\app.log
    ///
    /// Niveles: INFO | WARN | ERROR
    /// Rotación automática: si el archivo supera 2 MB se renombra a app.log.bak
    /// Thread-safe mediante lock.
    /// </summary>
    public static class AppLogger
    {
        static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileExplorer", "app.log");

        static readonly string BakPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileExplorer", "app.log.bak");

        static readonly object _lock = new();
        const long MaxBytes = 2 * 1024 * 1024; // 2 MB

        // ════════════════════════════════════════════════════════════
        //  API pública
        // ════════════════════════════════════════════════════════════
        public static void Info(string message) => Write("INFO ", message);
        public static void Warn(string message) => Write("WARN ", message);
        public static void Error(string message, Exception? ex = null)
        {
            Write("ERROR", message);
            if (ex != null)
                Write("ERROR", $"  → {ex.GetType().Name}: {ex.Message}");
        }

        // ════════════════════════════════════════════════════════════
        //  Escritura interna
        // ════════════════════════════════════════════════════════════
        static void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                    Rotate();
                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch
            {
                // El logger nunca debe romper la aplicación.
            }
        }

        static void Rotate()
        {
            if (!File.Exists(LogPath)) return;
            var info = new FileInfo(LogPath);
            if (info.Length < MaxBytes) return;

            try
            {
                if (File.Exists(BakPath)) File.Delete(BakPath);
                File.Move(LogPath, BakPath);
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════
        //  Abrir el log desde la UI (opcional)
        // ════════════════════════════════════════════════════════════
        public static void OpenLogFile()
        {
            try
            {
                if (!File.Exists(LogPath))
                {
                    MessageBox.Show("No hay archivo de log todavía.", "Log",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(LogPath)
                    { UseShellExecute = true });
            }
            catch { }
        }

        public static string LogFilePath => LogPath;
    }
}
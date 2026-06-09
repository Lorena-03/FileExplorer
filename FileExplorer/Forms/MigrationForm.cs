using FileExplorer.Helpers;
using FileExplorer.Models;
using DrawColor = System.Drawing.Color;
using DrawFont = System.Drawing.Font;
using DrawFontStyle = System.Drawing.FontStyle;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Formulario para configurar la conexion a base de datos y migrar datos.
    /// Las credenciales por defecto estan en el codigo y se cargan cada vez que
    /// se abre el formulario — nunca se guardan al cerrar.
    /// Funciona en red local sin necesidad de internet.
    /// </summary>
    public partial class MigrationForm : Form
    {
        internal static readonly DrawColor C_BG = DrawColor.FromArgb(252, 252, 254);
        internal static readonly DrawColor C_TOOL = DrawColor.FromArgb(245, 245, 248);
        internal static readonly DrawColor C_ACCENT = DrawColor.FromArgb(0, 122, 255);
        internal static readonly DrawColor C_GREEN = DrawColor.FromArgb(52, 199, 89);
        internal static readonly DrawColor C_RED = DrawColor.FromArgb(255, 59, 48);
        internal static readonly DrawColor C_ORANGE = DrawColor.FromArgb(255, 149, 0);
        internal static readonly DrawColor C_TXT = DrawColor.FromArgb(28, 28, 30);
        internal static readonly DrawColor C_SEC = DrawColor.FromArgb(142, 142, 147);

        // ════════════════════════════════════════════════════════════
        //  CREDENCIALES POR DEFECTO
        // ════════════════════════════════════════════════════════════
        const string SQL_DEFAULT_HOST = "";
        const string SQL_DEFAULT_PORT = "1433";
        const string SQL_DEFAULT_DATABASE = "FileArchive";
        const string SQL_DEFAULT_USER = "sa";
        const string SQL_DEFAULT_PASSWORD = "1234";

        const string MY_DEFAULT_HOST = "";
        const string MY_DEFAULT_PORT = "3306";
        const string MY_DEFAULT_DATABASE = "files";
        const string MY_DEFAULT_USER = "sergio";
        const string MY_DEFAULT_PASSWORD = "123456";

        internal readonly List<string> _headers;
        internal readonly List<List<string>> _rows;

        /// <summary>Se dispara cuando el usuario aplica cambios del visor BD al archivo original.</summary>
        public event Action OnRowsChanged;

        internal ComboBox cboEngine;
        internal TextBox txtHost, txtPort, txtDatabase, txtUser, txtPassword, txtTable;
        internal Button btnTest, btnMigrate, btnVerDatos, btnClose, _btnSobre;
        internal ProgressBar pgBar;
        internal Label lblStatus;
        internal RichTextBox rtbLog;
        internal CancellationTokenSource _cts;

        internal const int LBL_W = 140;
        internal const int FLD_X = 155;
        internal const int PAD_X = 16;

        /// <summary>Inicializa el formulario con los headers y filas a migrar.</summary>
        public MigrationForm(List<string> headers, List<List<string>> rows)
        {
            _headers = headers; _rows = rows;
            InitializeComponent();
            LoadDefaultCredentials();
        }

        /// <summary>
        /// Carga las credenciales por defecto segun el motor seleccionado.
        /// El Host siempre queda vacio para que el usuario lo llene.
        /// </summary>
        internal void LoadDefaultCredentials()
        {
            bool isSql = cboEngine.SelectedIndex == 0;
            txtHost.Text = "";
            txtPort.Text = isSql ? SQL_DEFAULT_PORT : MY_DEFAULT_PORT;
            txtDatabase.Text = isSql ? SQL_DEFAULT_DATABASE : MY_DEFAULT_DATABASE;
            txtUser.Text = isSql ? SQL_DEFAULT_USER : MY_DEFAULT_USER;
            txtPassword.Text = isSql ? SQL_DEFAULT_PASSWORD : MY_DEFAULT_PASSWORD;
        }

        /// <summary>Construye las opciones de migracion leyendo todos los campos de la UI.</summary>
        internal MigrationOptions BuildOptions() => new()
        {
            Engine = cboEngine.SelectedIndex == 0 ? DbEngine.SqlServer : DbEngine.MariaDB,
            Host = txtHost.Text.Trim(),
            Port = int.TryParse(txtPort.Text, out int p) ? p : (cboEngine.SelectedIndex == 0 ? 1433 : 3306),
            Database = txtDatabase.Text.Trim(),
            User = txtUser.Text.Trim(),
            Password = txtPassword.Text,
            TableName = string.IsNullOrWhiteSpace(txtTable?.Text) ? "archivos" : txtTable.Text.Trim(),
            DropIfExists = _btnSobre?.Tag is true,
            CreateTable = true,
        };

        /// <summary>Prueba la conexion con los datos ingresados y muestra el resultado en el log.</summary>
        async void TestConnection()
        {
            if (string.IsNullOrWhiteSpace(txtHost.Text))
            { lblStatus.Text = "  Ingresa la IP del servidor primero."; lblStatus.ForeColor = C_RED; txtHost.Focus(); return; }
            if (string.IsNullOrWhiteSpace(txtDatabase.Text))
            { lblStatus.Text = "  Completa el campo Base de datos."; lblStatus.ForeColor = C_RED; return; }

            btnTest.Enabled = false;
            lblStatus.Text = "  Probando conexion..."; lblStatus.ForeColor = C_ORANGE;
            try
            {
                bool ok = await DatabaseMigrator.TestConnectionAsync(BuildOptions());
                lblStatus.Text = ok ? "  Conexion exitosa" : "  No se pudo conectar — verifica IP, puerto y credenciales";
                lblStatus.ForeColor = ok ? C_GREEN : C_RED;
                Log(ok ? "Conexion exitosa" : "Conexion fallida — verifica los datos ingresados", ok ? C_GREEN : C_RED);
                if (ok) btnVerDatos.Enabled = true;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "  Error: " + ex.Message[..Math.Min(80, ex.Message.Length)];
                lblStatus.ForeColor = C_RED;
                Log("Error: " + ex.Message, C_RED);
            }
            finally { btnTest.Enabled = true; }
        }

        /// <summary>Inicia la migracion asincrona mostrando progreso en la barra y el log.</summary>
        async void StartMigration()
        {
            if (string.IsNullOrWhiteSpace(txtHost.Text))
            { lblStatus.Text = "  Ingresa la IP del servidor primero."; lblStatus.ForeColor = C_RED; txtHost.Focus(); return; }
            if (string.IsNullOrWhiteSpace(txtDatabase.Text) || string.IsNullOrWhiteSpace(txtUser.Text))
            { lblStatus.Text = "  Completa todos los campos de conexion."; lblStatus.ForeColor = C_RED; return; }

            btnMigrate.Enabled = false; btnTest.Enabled = false; btnVerDatos.Enabled = false;
            pgBar.Visible = true; pgBar.Style = ProgressBarStyle.Continuous;
            pgBar.Maximum = Math.Max(1, _rows.Count); pgBar.Value = 0;
            _cts = new CancellationTokenSource();
            var opts = BuildOptions();

            var progress = new Progress<(int done, int total, string msg)>(r =>
            {
                pgBar.Value = Math.Min(r.done, pgBar.Maximum);
                lblStatus.Text = "  " + r.msg;
                lblStatus.ForeColor = r.done >= r.total ? C_GREEN : C_ACCENT;
                Log(r.msg, r.done >= r.total ? C_GREEN : C_ACCENT);
            });
            try
            {
                Log($"Verificando base de datos '{opts.Database}' en {opts.Host}:{opts.Port}...", C_ORANGE);
                await DatabaseMigrator.EnsureDatabaseExistsAsync(opts, _cts.Token);
                Log("Base de datos lista", C_GREEN);
                Log($"Migrando {_rows.Count:N0} filas a tabla '{opts.TableName}'...", C_ACCENT);
                await DatabaseMigrator.MigrateAsync(opts, _headers, _rows, progress, _cts.Token);
                lblStatus.Text = $"  Completado — {_rows.Count:N0} registros migrados";
                lblStatus.ForeColor = C_GREEN;
                btnVerDatos.Enabled = true;
            }
            catch (OperationCanceledException)
            { Log("Migracion cancelada", C_ORANGE); lblStatus.Text = "  Migracion cancelada"; lblStatus.ForeColor = C_ORANGE; }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message, C_RED);
                lblStatus.Text = "  Error: " + ex.Message[..Math.Min(100, ex.Message.Length)];
                lblStatus.ForeColor = C_RED;
            }
            finally { btnMigrate.Enabled = true; btnTest.Enabled = true; pgBar.Visible = false; _cts?.Dispose(); _cts = null; }
        }

        /// <summary>
        /// Lee los datos de la tabla en la BD, detecta errores con DataValidator
        /// y los muestra en el visor editable.
        /// </summary>
        async void VerDatosDB()
        {
            btnVerDatos.Enabled = false;
            lblStatus.Text = "  Leyendo datos..."; lblStatus.ForeColor = C_ORANGE;
            Log($"Consultando tabla '{BuildOptions().TableName}'...", C_ORANGE);
            try
            {
                var opts = BuildOptions();
                var (headers, rows) = await DatabaseMigrator.ReadTableAsync(opts);
                if (rows.Count == 0)
                {
                    Log("La tabla esta vacia o no existe.", C_ORANGE);
                    lblStatus.Text = "  La tabla esta vacia"; lblStatus.ForeColor = C_ORANGE;
                    MessageBox.Show($"La tabla '{opts.TableName}' esta vacia o no existe.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Log($"{rows.Count} filas leidas", C_GREEN);
                lblStatus.Text = "  Analizando errores..."; lblStatus.ForeColor = C_ORANGE;

                // ← Cambiado: DataValidator.Validate en lugar de ErrorDetector.DetectAll
                var errores = await Task.Run(() => DataValidator.Validate(headers, rows));
                int autoFix = errores.Count(e => !string.IsNullOrEmpty(e.AutoFix));
                int manual = errores.Count - autoFix;

                if (errores.Count == 0)
                { Log("Sin errores — datos correctos", C_GREEN); lblStatus.Text = $"  {rows.Count} filas sin errores"; lblStatus.ForeColor = C_GREEN; }
                else
                { Log($"{errores.Count} error(es): {autoFix} auto-corregibles, {manual} manuales", C_ORANGE); lblStatus.Text = $"  {errores.Count} error(es)"; lblStatus.ForeColor = C_ORANGE; }

                var visor = new DbDataViewerForm(headers, rows, errores);
                visor.ChangesSaved += (editedHeaders, editedRows) =>
                {
                    _rows.Clear(); _rows.AddRange(editedRows);
                    OnRowsChanged?.Invoke();
                    Log($"{editedRows.Count} filas aplicadas al archivo original", C_GREEN);
                    lblStatus.Text = $"  {editedRows.Count} filas actualizadas"; lblStatus.ForeColor = C_GREEN;
                };
                visor.Show(this);
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message, C_RED);
                lblStatus.Text = "  Error: " + ex.Message[..Math.Min(80, ex.Message.Length)];
                lblStatus.ForeColor = C_RED;
                MessageBox.Show("Error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { btnVerDatos.Enabled = true; }
        }

        /// <summary>Agrega una linea al log con timestamp y color indicado.</summary>
        internal void Log(string msg, DrawColor color)
        {
            if (rtbLog.IsDisposed) return;
            rtbLog.SelectionStart = rtbLog.TextLength; rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = color;
            rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}]  {msg}\n");
            rtbLog.ScrollToCaret();
        }
    }
}
using FileExplorer.Models;
using DrawColor = System.Drawing.Color;
using DrawFont = System.Drawing.Font;
using DrawFontStyle = System.Drawing.FontStyle;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Formulario editable para visualizar y corregir datos leidos de la base de datos.
    /// Los cambios se pueden aplicar al archivo original mediante el evento ChangesSaved.
    /// </summary>
    public partial class DbDataViewerForm : Form
    {
        internal static readonly DrawColor C_BG = DrawColor.FromArgb(252, 252, 254);
        internal static readonly DrawColor C_ACCENT = DrawColor.FromArgb(0, 122, 255);
        internal static readonly DrawColor C_GREEN = DrawColor.FromArgb(52, 199, 89);
        internal static readonly DrawColor C_RED = DrawColor.FromArgb(255, 59, 48);
        internal static readonly DrawColor C_ORANGE = DrawColor.FromArgb(255, 149, 0);
        internal static readonly DrawColor C_TXT = DrawColor.FromArgb(28, 28, 30);
        internal static readonly DrawColor C_SEC = DrawColor.FromArgb(142, 142, 147);
        internal static readonly DrawColor C_HDR_BG = DrawColor.FromArgb(220, 232, 255);
        internal static readonly DrawColor C_IDX_BG = DrawColor.FromArgb(235, 240, 255);
        internal static readonly DrawColor C_WARN = DrawColor.FromArgb(255, 243, 205);
        internal static readonly DrawColor C_ERR = DrawColor.FromArgb(255, 220, 220);

        internal readonly List<string> _headers;
        internal readonly List<List<string>> _editedRows;
        internal readonly List<ValidationError> _errores;   // ← cambiado de List<DataError>
        internal readonly Dictionary<(int, int), DrawColor> _cellColors = new();
        internal bool _isDirty = false;

        /// <summary>Se dispara cuando el usuario confirma aplicar los cambios al archivo original.</summary>
        public event Action<List<string>, List<List<string>>> ChangesSaved;

        internal DataGridView dgv;
        internal Label lblBanner, lblEditHint;
        internal Button btnApply;

        /// <summary>Inicializa el visor con los datos de la BD y la lista de errores detectados.</summary>
        public DbDataViewerForm(List<string> headers, List<List<string>> rows, List<ValidationError> errores)
        {
            _headers = headers;
            _editedRows = rows.Select(r => new List<string>(r)).ToList();
            _errores = errores;
            InitializeComponent();
        }

        /// <summary>
        /// Confirma y dispara ChangesSaved con los datos editados
        /// para que FileEditorForm los aplique al archivo original.
        /// </summary>
        void BtnApply_Click(object s, EventArgs e)
        {
            if (MessageBox.Show(
                $"Aplicar {_editedRows.Count} filas editadas a la tabla original?\n\nEl editor de archivos se actualizara.",
                "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            ChangesSaved?.Invoke(_headers, _editedRows);
            _isDirty = false; btnApply.Enabled = false;
            lblEditHint.Text = "Cambios aplicados a la tabla original";
            lblEditHint.ForeColor = DrawColor.FromArgb(0, 140, 60);
            lblBanner.Text = $"{_editedRows.Count} filas sincronizadas con el editor de archivos.";
            lblBanner.BackColor = DrawColor.FromArgb(232, 255, 238);
            lblBanner.ForeColor = DrawColor.FromArgb(0, 120, 50);
        }

        /// <summary>Muestra un dialogo con los errores detectados en la fila seleccionada.</summary>
        void ShowRowErrors(int rowIdx)
        {
            var errs = _errores.Where(er => er.Row == rowIdx).ToList();
            if (errs.Count == 0)
            { MessageBox.Show($"Fila {rowIdx + 1}: Sin errores.", "Sin errores", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Fila {rowIdx + 1} — {errs.Count} error(es):\n");
            foreach (var err in errs)
            {
                sb.AppendLine($"Columna: {err.ColumnName}");
                sb.AppendLine($"Valor: {err.Value}");
                sb.AppendLine($"Problema: {err.Description}");
                sb.AppendLine(string.IsNullOrEmpty(err.AutoFix)
                    ? "Requiere revision manual."
                    : $"Correccion sugerida: {err.AutoFix}");
                sb.AppendLine();
            }
            MessageBox.Show(sb.ToString(), $"Errores fila {rowIdx + 1}", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
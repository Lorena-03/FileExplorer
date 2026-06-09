using DrawColor = System.Drawing.Color;
using DrawFont = System.Drawing.Font;
using DrawFontStyle = System.Drawing.FontStyle;

namespace FileExplorer.Forms
{
    public partial class MigrationForm
    {
        /// <summary>
        /// Construye toda la interfaz: campos de conexion, opciones de migracion,
        /// barra de progreso y log de operaciones.
        /// Las credenciales se cargan desde el codigo al abrir y al cambiar motor.
        /// </summary>
        void InitializeComponent()
        {
            Text = "Migrar a base de datos";
            Size = new System.Drawing.Size(700, 660);
            MinimumSize = new System.Drawing.Size(600, 560);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = C_BG;
            Font = new DrawFont("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.Sizable;

            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 340,
                BackColor = C_TOOL,
                Padding = new Padding(PAD_X, 14, PAD_X, 14)
            };

            // ── Banner informativo ────────────────────────────────────
            var lblBanner = new Label
            {
                Left = PAD_X,
                Top = 8,
                Width = 660,
                Height = 22,
                ForeColor = C_SEC,
                BackColor = DrawColor.Transparent,
                Font = new DrawFont("Segoe UI", 8f, DrawFontStyle.Italic),
            };
            pnlTop.Controls.Add(lblBanner);

            int y = 36;

            // Motor
            Row(pnlTop, "Motor:", y);
            cboEngine = Combo(pnlTop, FLD_X, y, 210); y += 34;

            // Host
            Row(pnlTop, "Host / IP:", y);
            txtHost = Fld(pnlTop, FLD_X, y, 220, "");
            var lblHostHint = new Label
            {
                Left = FLD_X + 226,
                Top = y + 6,
                AutoSize = true,
                Text = "Escribe la IP del servidor (ej: 192.168.1.100)",
                ForeColor = C_ORANGE,
                BackColor = DrawColor.Transparent,
                Font = new DrawFont("Segoe UI", 8f, DrawFontStyle.Bold),
            };
            pnlTop.Controls.Add(lblHostHint);
            y += 34;

            // Puerto
            Row(pnlTop, "Puerto:", y);
            txtPort = Fld(pnlTop, FLD_X, y, 80, "");
            Hint(pnlTop, FLD_X + 86, y, "SQL Server: 1433   |   MariaDB/MySQL: 3306"); y += 34;

            // Base de datos
            Row(pnlTop, "Base de datos:", y);
            txtDatabase = Fld(pnlTop, FLD_X, y, 220, ""); y += 34;

            // Usuario
            Row(pnlTop, "Usuario:", y);
            txtUser = Fld(pnlTop, FLD_X, y, 220, ""); y += 34;

            // Contrasena
            Row(pnlTop, "Contrasena:", y);
            txtPassword = new TextBox
            {
                Left = FLD_X,
                Top = y,
                Width = 220,
                Height = 26,
                Font = new DrawFont("Segoe UI", 9.5f),
                PasswordChar = '●',
            };
            pnlTop.Controls.Add(txtPassword);

            // Boton mostrar/ocultar contrasena
            var btnShowPass = new Button
            {
                Left = FLD_X + 226,
                Top = y,
                Width = 26,
                Height = 26,
                Text = "👁",
                FlatStyle = FlatStyle.Flat,
                BackColor = C_TOOL,
                ForeColor = C_TXT,
                Cursor = Cursors.Hand,
                Font = new DrawFont("Segoe UI", 10f),
            };
            btnShowPass.FlatAppearance.BorderSize = 0;
            btnShowPass.Click += (_, __) =>
                txtPassword.PasswordChar = txtPassword.PasswordChar == '\0' ? '●' : '\0';
            pnlTop.Controls.Add(btnShowPass);
            y += 34;

            // Tabla destino
            Row(pnlTop, "Tabla destino:", y);
            txtTable = Fld(pnlTop, FLD_X, y, 220, "archivos");
            Hint(pnlTop, FLD_X + 226, y, "Nombre de la tabla en la BD"); y += 34;

            // Opciones Sobreescribir / Agregar
            var btnSobre = new Button
            {
                Left = FLD_X,
                Top = y,
                Width = 150,
                Height = 28,
                Text = "Sobreescribir",
                FlatStyle = FlatStyle.Flat,
                BackColor = DrawColor.FromArgb(220, 38, 38),
                ForeColor = DrawColor.White,
                Font = new DrawFont("Segoe UI", 9f, DrawFontStyle.Bold),
                Cursor = Cursors.Hand,
                Tag = true,
            };
            btnSobre.FlatAppearance.BorderSize = 0;

            var btnAgregar = new Button
            {
                Left = FLD_X + 158,
                Top = y,
                Width = 120,
                Height = 28,
                Text = "Agregar filas",
                FlatStyle = FlatStyle.Flat,
                BackColor = DrawColor.FromArgb(220, 220, 225),
                ForeColor = DrawColor.FromArgb(60, 60, 70),
                Font = new DrawFont("Segoe UI", 9f),
                Cursor = Cursors.Hand,
                Tag = false,
            };
            btnAgregar.FlatAppearance.BorderSize = 0;

            btnSobre.Click += (_, __) =>
            {
                btnSobre.BackColor = DrawColor.FromArgb(220, 38, 38); btnSobre.ForeColor = DrawColor.White;
                btnSobre.Font = new DrawFont("Segoe UI", 9f, DrawFontStyle.Bold);
                btnAgregar.BackColor = DrawColor.FromArgb(220, 220, 225); btnAgregar.ForeColor = DrawColor.FromArgb(60, 60, 70);
                btnAgregar.Font = new DrawFont("Segoe UI", 9f);
                btnSobre.Tag = true;
            };
            btnAgregar.Click += (_, __) =>
            {
                btnAgregar.BackColor = DrawColor.FromArgb(52, 199, 89); btnAgregar.ForeColor = DrawColor.White;
                btnAgregar.Font = new DrawFont("Segoe UI", 9f, DrawFontStyle.Bold);
                btnSobre.BackColor = DrawColor.FromArgb(220, 220, 225); btnSobre.ForeColor = DrawColor.FromArgb(60, 60, 70);
                btnSobre.Font = new DrawFont("Segoe UI", 9f);
                btnSobre.Tag = false;
            };
            _btnSobre = btnSobre;

            Row(pnlTop, "Al migrar:", y);
            pnlTop.Controls.AddRange(new Control[] { btnSobre, btnAgregar });

            // Motor: items + evento que recarga credenciales al cambiar
            cboEngine.Items.AddRange(new object[] { "SQL Server", "MariaDB / MySQL" });
            cboEngine.SelectedIndex = 0;
            cboEngine.SelectedIndexChanged += (_, __) =>
            {
                txtPort.Text = cboEngine.SelectedIndex == 0 ? "1433" : "3306";
                LoadDefaultCredentials();   // recargar credenciales del motor seleccionado
            };

            // ── Botones de accion ─────────────────────────────────────
            var pnlBtns = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = C_TOOL };
            btnTest = Btn("Probar conexion", C_ORANGE, 16, TestConnection);
            btnMigrate = Btn("Migrar datos", C_ACCENT, 193, StartMigration);
            btnVerDatos = Btn("Ver datos BD", DrawColor.FromArgb(60, 130, 60), 370, VerDatosDB);
            btnClose = Btn("Cerrar", DrawColor.FromArgb(80, 80, 85), 547, () => { _cts?.Cancel(); Close(); });
            btnVerDatos.Enabled = false;
            pnlBtns.Controls.AddRange(new Control[] { btnTest, btnMigrate, btnVerDatos, btnClose });

            pgBar = new ProgressBar { Dock = DockStyle.Top, Height = 6, Visible = false };
            lblStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                ForeColor = C_SEC,
                BackColor = DrawColor.Transparent,
                Font = new DrawFont("Segoe UI", 8.5f),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Text = $"  {_rows.Count:N0} filas  {_headers.Count} columnas listas para migrar",
            };
            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new DrawFont("Consolas", 9.5f),
                BackColor = DrawColor.FromArgb(18, 18, 20),
                ForeColor = DrawColor.FromArgb(200, 200, 210),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                DetectUrls = false,
            };

            Controls.Add(rtbLog);
            Controls.Add(lblStatus);
            Controls.Add(pgBar);
            Controls.Add(pnlTop);
            Controls.Add(pnlBtns);
        }

        /// <summary>Agrega un label de campo al panel.</summary>
        void Row(Panel p, string text, int y) =>
            p.Controls.Add(new Label
            {
                Left = 0,
                Top = y + 4,
                Width = LBL_W,
                Height = 22,
                Text = text,
                ForeColor = C_TXT,
                BackColor = DrawColor.Transparent,
                Font = new DrawFont("Segoe UI", 9f, DrawFontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            });

        /// <summary>Agrega un hint de ayuda junto al campo.</summary>
        void Hint(Panel p, int x, int y, string text) =>
            p.Controls.Add(new Label
            {
                Left = x,
                Top = y + 6,
                AutoSize = true,
                Text = text,
                ForeColor = C_SEC,
                BackColor = DrawColor.Transparent,
                Font = new DrawFont("Segoe UI", 8f),
            });

        /// <summary>Crea y agrega un TextBox al panel.</summary>
        TextBox Fld(Panel p, int x, int y, int w, string def)
        {
            var tb = new TextBox
            {
                Left = x,
                Top = y,
                Width = w,
                Height = 26,
                Font = new DrawFont("Segoe UI", 9.5f),
                Text = def,
            };
            p.Controls.Add(tb); return tb;
        }

        /// <summary>Crea y agrega un ComboBox al panel.</summary>
        ComboBox Combo(Panel p, int x, int y, int w)
        {
            var cb = new ComboBox
            {
                Left = x,
                Top = y,
                Width = w,
                Height = 26,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new DrawFont("Segoe UI", 9.5f),
            };
            p.Controls.Add(cb); return cb;
        }

        /// <summary>Crea un boton de accion con color y accion especificados.</summary>
        Button Btn(string text, DrawColor bg, int left, Action act)
        {
            var b = new Button
            {
                Text = text,
                Left = left,
                Top = 11,
                Width = 165,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = DrawColor.White,
                Font = new DrawFont("Segoe UI", 9f),
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (_, __) => act();
            return b;
        }
    }
}
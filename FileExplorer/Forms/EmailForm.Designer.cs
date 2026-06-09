using System.Text;

namespace FileExplorer.Forms
{
    public partial class EmailForm
    {
        // ── Controles ────────────────────────────────────────────────
        TextBox          txtTo, txtCc, txtSubject, txtFrom, txtPass, txtSmtp, txtPort;
        RichTextBox      rtbBody;
        FlowLayoutPanel  flowAttach;
        Button           btnSend;
        Label            lblStatus;
        Panel            pnlSmtp;
        bool             _smtpVisible = false;

        /// <summary>
        /// Construye toda la interfaz: toolbar con botones de envio,
        /// campos de remitente/destinatario, panel SMTP colapsable,
        /// adjuntos, cuerpo del mensaje y barra de estado.
        /// </summary>
        void InitializeComponent()
        {
            Text            = "Enviar por correo";
            Size            = new Size(660, 720);
            MinimumSize     = new Size(580, 600);
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = C_BG;
            Font            = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = false;

            // ── Toolbar ───────────────────────────────────────────────
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = C_TOOLBAR };
            toolbar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(C_BORDER), 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);

            var lblTitulo = new Label
            {
                Text = "Enviar correo", Left = 16, Top = 12, Width = 220, Height = 34,
                ForeColor = C_TEXT, Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft,
            };

            btnSend      = ToolBtn("Enviar",   C_GREEN,                        Color.FromArgb(30, 165, 65));
            var btnGmail   = ToolBtn("Gmail",    Color.FromArgb(220, 50, 40),   Color.FromArgb(185, 30, 20));
            var btnOutlook = ToolBtn("Outlook",  Color.FromArgb(0, 120, 212),   Color.FromArgb(0, 90, 170));

            btnSend.Anchor = btnGmail.Anchor = btnOutlook.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSend.Click    += BtnSend_Click;
            btnGmail.Click   += BtnGmail_Click;
            btnOutlook.Click += BtnOutlook_Click;
            new ToolTip().SetToolTip(btnGmail,   "Abrir Gmail en el navegador");
            new ToolTip().SetToolTip(btnOutlook, "Abrir en Outlook Web");
            new ToolTip().SetToolTip(btnSend,    "Enviar directamente por SMTP");

            toolbar.Controls.AddRange(new Control[] { lblTitulo, btnSend, btnGmail, btnOutlook });
            toolbar.Resize += (_, __) =>
            {
                int rx = toolbar.Width - 14;
                btnSend.Left    = rx - btnSend.Width;    rx -= btnSend.Width    + 8;
                btnGmail.Left   = rx - btnGmail.Width;   rx -= btnGmail.Width   + 8;
                btnOutlook.Left = rx - btnOutlook.Width;
            };

            // ── Área de campos con scroll ─────────────────────────────
            var scroll = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, AutoScroll = true };
            var inner  = new Panel { Left = 0, Top = 0, BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            scroll.Controls.Add(inner);
            scroll.Resize += (_, __) => inner.Width = scroll.Width;

            int y = 0;

            // Remitente
            y += AddSection(inner, "REMITENTE", y);
            AddRow(inner, ref y, "De:", out txtFrom, "tu@ejemplo.com");
            txtFrom.TextChanged += (_, __) => AutoDetectSmtp();

            AddLbl(inner, "Contrasena:", y);
            var passRow = new Panel { Left = 0, Top = y + 22, Height = 36, BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            txtPass = new TextBox { Left = 0, Top = 4, Height = 30, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10f), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = C_TEXT, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var eyeBtn = new Button { Text = "o", Left = 0, Top = 4, Width = 36, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 220, 228), ForeColor = C_TEXT, Font = new Font("Segoe UI", 9f), Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            eyeBtn.FlatAppearance.BorderSize = 0;
            eyeBtn.Click += (_, __) => txtPass.UseSystemPasswordChar = !txtPass.UseSystemPasswordChar;
            passRow.Controls.AddRange(new Control[] { txtPass, eyeBtn });
            passRow.Resize += (_, __) => { eyeBtn.Left = passRow.Width - 40; txtPass.Width = passRow.Width - 46; };
            inner.Controls.Add(passRow);
            y += 62;

            // SMTP colapsable
            var btnSmtpToggle = new Button { Left = 16, Top = y, Height = 24, AutoSize = true, Text = ">  Configuracion SMTP avanzada", FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = C_SUB, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand };
            btnSmtpToggle.FlatAppearance.BorderSize = 0;
            btnSmtpToggle.Click += (_, __) =>
            {
                _smtpVisible = !_smtpVisible; pnlSmtp.Visible = _smtpVisible;
                btnSmtpToggle.Text = (_smtpVisible ? "v" : ">") + "  Configuracion SMTP avanzada";
                scroll.PerformLayout();
            };
            inner.Controls.Add(btnSmtpToggle);
            y += 30;

            pnlSmtp = new Panel { Left = 0, Top = y, Height = 108, Visible = false, BackColor = Color.FromArgb(240, 242, 250), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            pnlSmtp.Paint += (s, e) => { using var p2 = new Pen(C_BORDER); e.Graphics.DrawRectangle(p2, 0, 0, pnlSmtp.Width - 1, pnlSmtp.Height - 1); };
            pnlSmtp.Controls.Add(new Label { Text = "Servidor SMTP:", Left = 12, Top = 10, AutoSize = true, ForeColor = C_SUB, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), BackColor = Color.Transparent });
            txtSmtp = new TextBox { Left = 12, Top = 28, Height = 28, Width = 400, Font = new Font("Segoe UI", 9.5f), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            pnlSmtp.Controls.Add(new Label { Text = "Puerto:", Left = 12, Top = 64, AutoSize = true, ForeColor = C_SUB, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), BackColor = Color.Transparent });
            txtPort = new TextBox { Left = 12, Top = 82, Width = 80, Height = 28, Font = new Font("Segoe UI", 9.5f), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, Text = "587" };
            pnlSmtp.Controls.Add(new Label { Left = 100, Top = 84, AutoSize = true, Text = "Gmail: 587  -  Outlook: 587  -  SSL activado", ForeColor = Color.FromArgb(160, 160, 170), Font = new Font("Segoe UI", 8f), BackColor = Color.Transparent });
            pnlSmtp.Controls.AddRange(new Control[] { txtSmtp, txtPort });
            pnlSmtp.Resize += (_, __) => txtSmtp.Width = Math.Max(80, pnlSmtp.Width - 24);
            inner.Controls.Add(pnlSmtp);
            y += 114;

            inner.Controls.Add(new Label { Left = 0, Top = y, Height = 1, BackColor = C_BORDER, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right }); y += 2;

            // Destinatario
            y += AddSection(inner, "DESTINATARIO", y);
            AddRow(inner, ref y, "Para:", out txtTo, "destinatario@ejemplo.com");
            AddRow(inner, ref y, "CC:",   out txtCc, "(opcional)");
            AddRow(inner, ref y, "Asunto:", out txtSubject, "");

            inner.Controls.Add(new Label { Left = 0, Top = y, Height = 1, BackColor = C_BORDER, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right }); y += 2;

            // Adjuntos
            y += AddSection(inner, "ADJUNTOS", y);
            flowAttach = new FlowLayoutPanel { Left = 0, Top = y, Height = 44, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var btnAddFile = new Button { Text = "+  Adjuntar archivo", AutoSize = true, Padding = new Padding(10, 4, 10, 4), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(228, 228, 236), ForeColor = C_TEXT, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 4) };
            btnAddFile.FlatAppearance.BorderSize = 0;
            btnAddFile.Click += (_, __) =>
            {
                using var dlg = new OpenFileDialog { Multiselect = true, Filter = "Todos|*.*" };
                if (dlg.ShowDialog() == DialogResult.OK)
                    foreach (var f in dlg.FileNames)
                        if (!_attachments.Contains(f)) { _attachments.Add(f); flowAttach.Controls.Add(MkTag(f)); }
            };
            flowAttach.Controls.Add(btnAddFile);
            foreach (var f in _attachments) flowAttach.Controls.Add(MkTag(f));
            inner.Controls.Add(flowAttach);
            y += 52;

            inner.Controls.Add(new Label { Left = 0, Top = y, Height = 1, BackColor = C_BORDER, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right }); y += 2;

            // Mensaje
            y += AddSection(inner, "MENSAJE", y);
            rtbBody = new RichTextBox { Left = 0, Top = y, Height = 160, Font = new Font("Segoe UI", 10.5f), BorderStyle = BorderStyle.None, BackColor = C_SURFACE, ForeColor = C_TEXT, ScrollBars = RichTextBoxScrollBars.Vertical, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Padding = new Padding(4) };
            rtbBody.Text = DefaultBody();
            inner.Controls.Add(rtbBody);
            y += 168; inner.Height = y + 16;

            scroll.Resize += (_, __) =>
            {
                int w = scroll.Width - (scroll.VerticalScroll.Visible ? 18 : 2);
                inner.Width = w; passRow.Width = w - 32; pnlSmtp.Width = w - 32;
                flowAttach.Width = w - 32; rtbBody.Width = w - 32;
                txtFrom.Width = w - 32; txtTo.Width = w - 32; txtCc.Width = w - 32; txtSubject.Width = w - 32;
                foreach (Control c in inner.Controls)
                    if (c is Label l && l.Height == 1) l.Width = w;
            };

            // Status bar
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = C_TOOLBAR };
            statusBar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, 0, statusBar.Width, 0);
            lblStatus = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = C_SUB, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f), Padding = new Padding(14, 0, 0, 0), Text = "Ingresa tus datos y presiona Enviar, o usa Gmail / Outlook." };
            statusBar.Controls.Add(lblStatus);

            Controls.Add(scroll); Controls.Add(toolbar); Controls.Add(statusBar);
            AutoDetectSmtp();
        }

        // ── Helpers de layout ─────────────────────────────────────────

        /// <summary>Agrega un encabezado de sección al panel y devuelve la altura consumida.</summary>
        int AddSection(Panel parent, string title, int y)
        {
            parent.Controls.Add(new Label { Text = title, Left = 16, Top = y + 10, AutoSize = true, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = Color.FromArgb(120, 120, 130), BackColor = Color.Transparent });
            return 30;
        }

        /// <summary>Agrega una fila con label y TextBox al panel.</summary>
        void AddRow(Panel parent, ref int y, string label, out TextBox box, string placeholder)
        {
            AddLbl(parent, label, y);
            box = new TextBox { Left = 16, Top = y + 22, Height = 30, Width = 400, PlaceholderText = placeholder, Font = new Font("Segoe UI", 10f), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = C_TEXT, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            parent.Controls.Add(box); y += 64;
        }

        /// <summary>Agrega solo una etiqueta de campo al panel.</summary>
        void AddLbl(Panel parent, string text, int y) =>
            parent.Controls.Add(new Label { Text = text, Left = 16, Top = y + 4, AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = C_SUB, BackColor = Color.Transparent });

        /// <summary>Crea un botón de toolbar con esquinas redondeadas.</summary>
        Button ToolBtn(string text, Color bg, Color hover)
        {
            var b = new Button { Text = text, Top = 14, Height = 34, Width = TextRenderer.MeasureText(text, new Font("Segoe UI", 10f, FontStyle.Bold)).Width + 28, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = hover;
            b.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, b.Width, b.Height, 8, 8));
            return b;
        }

        /// <summary>
        /// Infiere el servidor SMTP y puerto a partir del dominio del remitente.
        /// Soporta Gmail, Outlook/Hotmail/Live y Yahoo.
        /// </summary>
        void AutoDetectSmtp()
        {
            if (txtSmtp == null || txtPort == null) return;
            string domain = (txtFrom?.Text ?? "").Contains('@') ? txtFrom.Text.Split('@')[1].ToLower() : "";
            if (domain.Contains("gmail"))
            { txtSmtp.Text = "smtp.gmail.com"; txtPort.Text = "587"; }
            else if (domain.Contains("outlook") || domain.Contains("hotmail") || domain.Contains("live"))
            { txtSmtp.Text = "smtp-mail.outlook.com"; txtPort.Text = "587"; }
            else if (domain.Contains("yahoo"))
            { txtSmtp.Text = "smtp.mail.yahoo.com"; txtPort.Text = "587"; }
            else if (!string.IsNullOrEmpty(domain) && string.IsNullOrEmpty(txtSmtp.Text))
            { txtSmtp.Text = "smtp." + domain; txtPort.Text = "587"; }
        }

        /// <summary>
        /// Crea un chip visual para un archivo adjunto con nombre, tamaño y botón de eliminar.
        /// </summary>
        Panel MkTag(string path)
        {
            long sz = File.Exists(path) ? new FileInfo(path).Length : 0;
            string szTx = sz >= 1048576 ? $"{sz / 1048576.0:F1} MB" : $"{sz / 1024.0:F0} KB";
            string name = Path.GetFileName(path);
            using var fnt = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            int tagW = Math.Max(180, Math.Min(500, TextRenderer.MeasureText(name, fnt).Width + 60));

            var tag = new Panel { Width = tagW, Height = 40, BackColor = Color.FromArgb(230, 235, 250), Margin = new Padding(0, 0, 8, 4) };
            tag.Paint += (s, e) => { e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; using var p = new Pen(Color.FromArgb(180, 190, 224)); e.Graphics.DrawRectangle(p, 0, 0, tag.Width - 1, tag.Height - 1); };
            tag.Controls.Add(new Label { Text = name, Left = 10, Top = 6, Width = tagW - 28, Height = 17, Font = fnt, ForeColor = C_TEXT, BackColor = Color.Transparent, AutoEllipsis = false });
            tag.Controls.Add(new Label { Text = szTx, Left = 10, Top = 22, Width = tagW - 28, Height = 14, Font = new Font("Segoe UI", 7.5f), ForeColor = C_SUB, BackColor = Color.Transparent });
            var xBtn = new Label { Text = "X", Left = tagW - 18, Top = 3, Width = 16, Height = 16, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = C_SUB, BackColor = Color.Transparent, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter };
            xBtn.Click += (_, __) => { _attachments.Remove(path); flowAttach.Controls.Remove(tag); };
            tag.Controls.Add(xBtn);
            new ToolTip().SetToolTip(tag, name);
            return tag;
        }
    }
}

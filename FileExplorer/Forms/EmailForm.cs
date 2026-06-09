using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FileExplorer.Forms
{
    /// <summary>
    /// Formulario para enviar archivos por correo via SMTP directo,
    /// Gmail o Outlook Web. Guarda credenciales cifradas con DPAPI.
    /// </summary>
    public partial class EmailForm : Form
    {
        static readonly Color C_BG = Color.FromArgb(245, 245, 248);
        static readonly Color C_SURFACE = Color.FromArgb(255, 255, 255);
        static readonly Color C_TOOLBAR = Color.FromArgb(250, 250, 252);
        static readonly Color C_BORDER = Color.FromArgb(210, 210, 218);
        static readonly Color C_ACCENT = Color.FromArgb(10, 132, 255);
        static readonly Color C_TEXT = Color.FromArgb(28, 28, 30);
        static readonly Color C_SUB = Color.FromArgb(142, 142, 147);
        static readonly Color C_GREEN = Color.FromArgb(52, 199, 89);
        static readonly Color C_RED = Color.FromArgb(255, 59, 48);

        static readonly string CredPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileExplorer", "email_creds.dat");

        readonly List<string> _attachments;

        /// <summary>Inicializa el formulario con un único archivo adjunto.</summary>
        public EmailForm(string filePath) : this(new List<string> { filePath }) { }

        /// <summary>Inicializa el formulario con una lista de archivos adjuntos.</summary>
        public EmailForm(List<string> filePaths)
        {
            _attachments = filePaths ?? new List<string>();
            InitializeComponent();
            LoadCredentials();
            SuggestSubject();
        }

        // ════════════════════════════════════════════════════════════
        //  ENVIAR POR SMTP
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Valida los campos, guarda credenciales y envía el correo
        /// de forma asíncrona via SmtpClient con SSL.
        /// Detecta automáticamente la configuración correcta para Gmail y Outlook.
        /// </summary>
        async void BtnSend_Click(object sender, EventArgs e)
        {
            if (!txtFrom.Text.Contains('@'))
            { ShowStatus("Ingresa tu correo en el campo 'De'.", C_RED); txtFrom.Focus(); return; }
            if (string.IsNullOrWhiteSpace(txtPass.Text))
            { ShowStatus("Ingresa tu contrasena.", C_RED); txtPass.Focus(); return; }
            if (!txtTo.Text.Contains('@'))
            { ShowStatus("Ingresa al menos un destinatario en 'Para'.", C_RED); txtTo.Focus(); return; }
            if (string.IsNullOrWhiteSpace(txtSmtp.Text))
            { ShowStatus("Falta el servidor SMTP. Abre la configuracion avanzada.", C_RED); return; }
            if (!int.TryParse(txtPort.Text, out int port))
            { ShowStatus("Puerto SMTP invalido.", C_RED); return; }

            btnSend.Enabled = false; btnSend.Text = "Enviando...";
            ShowStatus("Conectando al servidor de correo...", C_SUB);
            SaveCredentials();

            string fromAddr = txtFrom.Text.Trim();
            string passVal = txtPass.Text;
            string toVal = txtTo.Text;
            string ccVal = txtCc.Text;
            string subjectVal = txtSubject.Text.Trim();
            string bodyVal = rtbBody.Text;
            string smtpHost = txtSmtp.Text.Trim();
            var files = _attachments.Where(File.Exists).ToList();

            // Calcular timeout según tamaño total de adjuntos (mínimo 60 seg, +30 seg por cada 5 MB)
            long totalBytes = files.Sum(f => new FileInfo(f).Length);
            int timeoutMs = Math.Max(60_000, 60_000 + (int)(totalBytes / (5 * 1024 * 1024)) * 30_000);

            try
            {
                await Task.Run(() =>
                {
                    using var mail = new MailMessage();
                    mail.From = new MailAddress(fromAddr);
                    foreach (var a in ParseAddr(toVal)) mail.To.Add(a);
                    foreach (var a in ParseAddr(ccVal)) mail.CC.Add(a);
                    mail.Subject = subjectVal;
                    mail.Body = bodyVal;
                    mail.IsBodyHtml = false;
                    foreach (var path in files) mail.Attachments.Add(new Attachment(path));

                    using var smtp = new SmtpClient(smtpHost, port)
                    {
                        EnableSsl = true,
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        UseDefaultCredentials = false,
                        Credentials = new NetworkCredential(fromAddr, passVal),
                        Timeout = timeoutMs,
                    };
                    smtp.Send(mail);
                });

                ShowStatus("Correo enviado correctamente.", C_GREEN);
                btnSend.Text = "Enviado"; btnSend.BackColor = Color.FromArgb(30, 165, 65);
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException?.Message ?? ex.Message;
                btnSend.Enabled = true; btnSend.Text = "Enviar"; btnSend.BackColor = C_GREEN;

                if (msg.Contains("535") || msg.Contains("Authentication") || msg.Contains("5.7"))
                {
                    bool isGmail = smtpHost.Contains("gmail");
                    bool isOutlook = smtpHost.Contains("office365") || smtpHost.Contains("outlook");

                    if (isGmail)
                        ShowStatus("Gmail: usa una Contraseña de aplicación en myaccount.google.com/apppasswords", C_RED);
                    else if (isOutlook)
                        ShowStatus("Outlook: verifica que SMTP AUTH esté habilitado en tu cuenta Microsoft.", C_RED);
                    else
                        ShowStatus("Error de autenticación. Verifica usuario y contraseña.", C_RED);
                }
                else if (msg.Contains("timed out") || msg.Contains("timeout") || msg.Contains("connect"))
                {
                    bool isGmail = smtpHost.Contains("gmail");
                    bool isOutlook = smtpHost.Contains("office365") || smtpHost.Contains("outlook");

                    if (isGmail)
                        ShowStatus("Gmail: timeout. Verifica que el puerto sea 587 y SSL/TLS esté activo.", C_RED);
                    else if (isOutlook)
                        ShowStatus("Outlook: timeout. Usa smtp.office365.com puerto 587.", C_RED);
                    else
                        ShowStatus("Timeout: no se pudo conectar. Verifica servidor SMTP y puerto.", C_RED);
                }
                else
                {
                    ShowStatus(msg, C_RED);
                }
            }
        }

        // ════════════════════════════════════════════════════════════
        //  ABRIR EN NAVEGADOR
        // ════════════════════════════════════════════════════════════

        /// <summary>Construye la URL de Gmail con los campos pre-rellenos y la abre en el navegador.</summary>
        void BtnGmail_Click(object sender, EventArgs e)
        {
            var url = new StringBuilder("https://mail.google.com/mail/?view=cm&fs=1");
            if (!string.IsNullOrWhiteSpace(txtTo.Text)) url.Append("&to=").Append(Uri.EscapeDataString(txtTo.Text.Trim()));
            if (!string.IsNullOrWhiteSpace(txtCc.Text)) url.Append("&cc=").Append(Uri.EscapeDataString(txtCc.Text.Trim()));
            url.Append("&su=").Append(Uri.EscapeDataString(txtSubject.Text.Trim()));
            url.Append("&body=").Append(Uri.EscapeDataString(rtbBody.Text));
            Open(url.ToString());
            ShowStatus("Gmail abierto en el navegador. Adjunta los archivos manualmente.", C_GREEN);
        }

        /// <summary>Construye la URL de Outlook Web con los campos pre-rellenos y la abre en el navegador.</summary>
        void BtnOutlook_Click(object sender, EventArgs e)
        {
            var url = new StringBuilder("https://outlook.live.com/mail/0/deeplink/compose?");
            url.Append("to=").Append(Uri.EscapeDataString(txtTo.Text.Trim()));
            if (!string.IsNullOrWhiteSpace(txtCc.Text)) url.Append("&cc=").Append(Uri.EscapeDataString(txtCc.Text.Trim()));
            url.Append("&subject=").Append(Uri.EscapeDataString(txtSubject.Text.Trim()));
            url.Append("&body=").Append(Uri.EscapeDataString(rtbBody.Text));
            Open(url.ToString());
            ShowStatus("Outlook abierto en el navegador. Adjunta los archivos manualmente.", C_GREEN);
        }

        /// <summary>Abre una URL en el navegador predeterminado del sistema.</summary>
        static void Open(string url) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════

        /// <summary>Actualiza el texto y color de la barra de estado desde cualquier hilo.</summary>
        void ShowStatus(string msg, Color color)
        {
            if (InvokeRequired) { Invoke(() => ShowStatus(msg, color)); return; }
            lblStatus.ForeColor = color; lblStatus.Text = msg;
        }

        /// <summary>Divide una cadena de direcciones separadas por coma o punto y coma.</summary>
        static string[] ParseAddr(string raw) =>
            (raw ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim()).Where(s => s.Contains('@')).ToArray();

        /// <summary>Pre-rellena el asunto según la cantidad de archivos adjuntos.</summary>
        void SuggestSubject() =>
            txtSubject.Text = _attachments.Count == 1
                ? "Archivo: " + Path.GetFileName(_attachments[0])
                : _attachments.Count > 1 ? $"{_attachments.Count} archivos adjuntos" : "";

        /// <summary>Devuelve el cuerpo de correo predeterminado listando los archivos adjuntos.</summary>
        string DefaultBody() =>
            "Hola,\n\nTe envio el siguiente archivo:\n\n" +
            string.Join("\n", _attachments.Select(f => "  -  " + Path.GetFileName(f))) +
            "\n\nSaludos.";

        // ════════════════════════════════════════════════════════════
        //  CREDENCIALES (DPAPI)
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Guarda remitente, servidor SMTP y contrasena cifrada con DPAPI
        /// en un archivo JSON en AppData.
        /// </summary>
        void SaveCredentials()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CredPath)!);
                var data = new
                {
                    From = txtFrom.Text.Trim(),
                    Smtp = txtSmtp.Text.Trim(),
                    Port = txtPort.Text.Trim(),
                    PassEnc = Convert.ToBase64String(
                        ProtectedData.Protect(Encoding.UTF8.GetBytes(txtPass.Text), null, DataProtectionScope.CurrentUser))
                };
                File.WriteAllText(CredPath, JsonSerializer.Serialize(data), Encoding.UTF8);
            }
            catch { }
        }

        /// <summary>
        /// Lee el archivo de credenciales, descifra la contrasena con DPAPI
        /// y pre-rellena los campos del formulario.
        /// </summary>
        void LoadCredentials()
        {
            try
            {
                if (!File.Exists(CredPath)) return;
                var json = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(CredPath, Encoding.UTF8));
                if (json.TryGetProperty("From", out var f) && !string.IsNullOrEmpty(f.GetString())) txtFrom.Text = f.GetString()!;
                if (json.TryGetProperty("Smtp", out var s) && !string.IsNullOrEmpty(s.GetString())) txtSmtp.Text = s.GetString()!;
                if (json.TryGetProperty("Port", out var p) && !string.IsNullOrEmpty(p.GetString())) txtPort.Text = p.GetString()!;
                if (json.TryGetProperty("PassEnc", out var pe) && !string.IsNullOrEmpty(pe.GetString()))
                {
                    var dec = ProtectedData.Unprotect(Convert.FromBase64String(pe.GetString()!), null, DataProtectionScope.CurrentUser);
                    txtPass.Text = Encoding.UTF8.GetString(dec);
                }
                if (!string.IsNullOrEmpty(txtSmtp.Text)) AutoDetectSmtp();
            }
            catch { }
        }

        /// <summary>Importacion nativa de GDI32 para botones con esquinas redondeadas.</summary>
        [DllImport("Gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int cw, int ch);
    }
}
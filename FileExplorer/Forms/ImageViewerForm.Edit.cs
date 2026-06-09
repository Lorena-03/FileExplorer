using System.Drawing.Imaging;

namespace FileExplorer.Forms
{
    public partial class ImageViewerForm
    {
        // ════════════════════════════════════════════════════════════
        //  TRANSFORMACIONES / FILTROS
        // ════════════════════════════════════════════════════════════

        /// <summary>Rota la imagen 90° en sentido horario.</summary>
        void RotateCW() { _edited?.RotateFlip(RotateFlipType.Rotate90FlipNone); ShowEdited(); }

        /// <summary>Rota la imagen 90° en sentido antihorario.</summary>
        void RotateCCW() { _edited?.RotateFlip(RotateFlipType.Rotate270FlipNone); ShowEdited(); }

        /// <summary>Voltea la imagen horizontalmente.</summary>
        void FlipH() { _edited?.RotateFlip(RotateFlipType.RotateNoneFlipX); ShowEdited(); }

        /// <summary>Voltea la imagen verticalmente.</summary>
        void FlipV() { _edited?.RotateFlip(RotateFlipType.RotateNoneFlipY); ShowEdited(); }

        /// <summary>Alterna el filtro de escala de grises, desactivando sepia si estaba activo.</summary>
        void ApplyGrayscale() { _grayscale = !_grayscale; _sepia = false; ApplyFilters(); }

        /// <summary>Alterna el filtro sepia, desactivando escala de grises si estaba activa.</summary>
        void ApplySepia() { _sepia = !_sepia; _grayscale = false; ApplyFilters(); }

        /// <summary>Muestra la imagen editada en el PictureBox con modo Zoom.</summary>
        void ShowEdited()
        {
            if (_edited == null) return;
            pic.SizeMode = PictureBoxSizeMode.Zoom;
            pic.Image = _edited;
            pic.Invalidate();
        }

        /// <summary>
        /// Aplica brillo, contraste y el filtro activo (escala de grises o sepia)
        /// generando una nueva imagen editada desde el original.
        /// </summary>
        void ApplyFilters()
        {
            if (_original == null) return;
            _edited = new Bitmap(_original.Width, _original.Height);
            using var g = Graphics.FromImage(_edited);
            var ia = new ImageAttributes();
            ia.SetColorMatrix(BuildColorMatrix());
            g.DrawImage(_original,
                new Rectangle(0, 0, _original.Width, _original.Height),
                0, 0, _original.Width, _original.Height,
                GraphicsUnit.Pixel, ia);
            ShowEdited();
        }

        /// <summary>
        /// Construye la ColorMatrix de brillo/contraste con el filtro activo.
        /// </summary>
        ColorMatrix BuildColorMatrix()
        {
            float b = _brightness, c = _contrast;

            if (_grayscale)
            {
                float r = 0.2126f * c, g2 = 0.7152f * c, lb = 0.0722f * c;
                return new ColorMatrix(new float[][]
                {
                    new[] { r, r, r, 0f, 0f },
                    new[] { g2, g2, g2, 0f, 0f },
                    new[] { lb, lb, lb, 0f, 0f },
                    IdentityRow,
                    new[] { b, b, b, 0f, 1f }
                });
            }

            if (_sepia)
                return new ColorMatrix(new float[][]
                {
                    new[] { 0.393f*c, 0.349f*c, 0.272f*c, 0f, 0f },
                    new[] { 0.769f*c, 0.686f*c, 0.534f*c, 0f, 0f },
                    new[] { 0.189f*c, 0.168f*c, 0.131f*c, 0f, 0f },
                    IdentityRow,
                    new[] { b, b, b, 0f, 1f }
                });

            return new ColorMatrix(new float[][]
            {
                new[] { c, 0f, 0f, 0f, 0f },
                new[] { 0f, c, 0f, 0f, 0f },
                new[] { 0f, 0f, c, 0f, 0f },
                IdentityRow,
                new[] { b, b, b, 0f, 1f }
            });
        }

        /// <summary>
        /// Restablece todos los filtros y ajustes devolviendo la imagen al estado original.
        /// </summary>
        void ResetEdits()
        {
            _brightness = 0f; _contrast = 1f;
            _grayscale = false; _sepia = false;
            trkBright.Value = 0; trkContr.Value = 10;
            if (_original != null) { _edited = (Bitmap)_original.Clone(); ShowEdited(); }
        }

        // ════════════════════════════════════════════════════════════
        //  HERRAMIENTAS — RECORTE
        // ════════════════════════════════════════════════════════════

        /// <summary>Resetea el color de fondo de los botones de herramienta al estado inactivo.</summary>
        void ResetToolBtns()
        {
            if (btnCrop != null) btnCrop.BackColor = C_CARD;
            if (btnDraw != null) btnDraw.BackColor = C_CARD;
            if (btnText != null) btnText.BackColor = C_CARD;
        }

        /// <summary>Activa o desactiva el modo de recorte.</summary>
        void ToggleCrop()
        {
            if (_cropMode) { CancelCrop(); return; }
            _cropMode = true; _drawMode = false; ResetToolBtns();
            btnCrop.BackColor = C_ACCENT;
            pic.Cursor = Cursors.Cross;
        }

        /// <summary>Activa o desactiva el modo de dibujo a mano alzada.</summary>
        void ToggleDraw()
        {
            _drawMode = !_drawMode; _cropMode = false; ResetToolBtns();
            btnDraw.BackColor = _drawMode ? Color.FromArgb(255, 159, 10) : C_CARD;
            pic.Cursor = _drawMode ? Cursors.Cross : Cursors.Default;
        }

        /// <summary>Desactiva los modos activos y abre el diálogo de texto.</summary>
        void ToggleTextMode()
        {
            if (_edited == null) { MessageBox.Show("Carga una imagen primero.", "Sin imagen"); return; }
            _cropMode = false; _drawMode = false; ResetToolBtns();
            pic.Cursor = Cursors.Default;
            ShowTextInputDialog();
        }

        /// <summary>Abre un ColorDialog para cambiar el color del pincel de dibujo.</summary>
        void PickDrawColor()
        {
            using var dlg = new ColorDialog { Color = _drawColor, FullOpen = true, AnyColor = true };
            if (dlg.ShowDialog() == DialogResult.OK)
            { _drawColor = dlg.Color; btnDrawColor.BackColor = _drawColor; }
        }

        /// <summary>Cicla entre tamaños de pincel: XL → 2px → S → 4px → M → 8px → L → 16px.</summary>
        void CycleBrushSize()
        {
            (_brushSize, string lbl) = _brushSize switch
            {
                2 => (4, "S"),
                4 => (8, "M"),
                8 => (16, "L"),
                _ => (2, "XL")
            };
            btnDrawSize.Text = lbl;
        }

        /// <summary>
        /// Aplica el recorte seleccionado convirtiéndolo a coordenadas de imagen,
        /// reemplazando la imagen editada con el área recortada.
        /// </summary>
        void ApplyCrop()
        {
            if (_edited == null || _cropRect.Width < 4 || _cropRect.Height < 4) { CancelCrop(); return; }

            float imgAspect = (float)_edited.Width / _edited.Height;
            float boxAspect = (float)pic.Width / pic.Height;
            float scale; int offX = 0, offY = 0;

            if (imgAspect > boxAspect)
            { scale = (float)pic.Width / _edited.Width; offY = (int)((pic.Height - _edited.Height * scale) / 2); }
            else
            { scale = (float)pic.Height / _edited.Height; offX = (int)((pic.Width - _edited.Width * scale) / 2); }

            int bx = (int)((_cropRect.X - offX) / scale);
            int by = (int)((_cropRect.Y - offY) / scale);
            int bw = (int)(_cropRect.Width / scale);
            int bh = (int)(_cropRect.Height / scale);

            var bc = new Rectangle(bx, by, bw, bh);
            bc.Intersect(new Rectangle(0, 0, _edited.Width, _edited.Height));
            if (bc.Width < 2 || bc.Height < 2) { CancelCrop(); return; }

            _edited = _edited.Clone(bc, _edited.PixelFormat);
            CancelCrop(); ShowEdited();
        }

        /// <summary>Cancela el modo de recorte y limpia el rectángulo de selección.</summary>
        void CancelCrop()
        {
            _cropMode = false; _cropConfirmed = false;
            _cropRect = Rectangle.Empty; _cropDragHandle = -1;
            btnCrop.BackColor = C_CARD;
            pic.Cursor = Cursors.Default;
            pic.Invalidate();
        }

        // ════════════════════════════════════════════════════════════
        //  MOUSE EVENTS
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Maneja MouseDown en el PictureBox: inicia selección de recorte, arrastre de handle
        /// o inicio de trazo de dibujo según el modo activo.
        /// </summary>
        void Pic_MouseDown(object s, MouseEventArgs e)
        {
            if (_cropMode && e.Button == MouseButtons.Left)
            {
                if (_cropConfirmed)
                {
                    if (GetCropApplyRect().Contains(e.Location)) { ApplyCrop(); return; }
                    if (GetCropCancelRect().Contains(e.Location)) { CancelCrop(); return; }

                    int h = HitHandle(e.Location);
                    if (h >= 0)
                    {
                        _cropDragHandle = h;
                        _cropDragStart = e.Location;
                        _cropRectAtDragStart = _cropRect;
                        return;
                    }
                    if (_cropRect.Contains(e.Location))
                    {
                        _cropDragHandle = 8;
                        _cropDragStart = e.Location;
                        _cropRectAtDragStart = _cropRect;
                        return;
                    }
                    _cropConfirmed = false; _cropRect = Rectangle.Empty;
                    pic.Cursor = Cursors.Cross;
                }
                _cropStart = e.Location; _cropRect = Rectangle.Empty;
                return;
            }

            if (_drawMode && e.Button == MouseButtons.Left)
            { _isDrawing = true; _lastDrawPt = PicToImg(e.Location); }
        }

        /// <summary>
        /// Maneja MouseMove: actualiza el rectángulo de recorte, desplaza handles
        /// o dibuja trazos sobre la imagen según el modo activo.
        /// </summary>
        void Pic_MouseMove(object s, MouseEventArgs e)
        {
            if (_cropMode)
            {
                if (_cropConfirmed)
                {
                    int h2 = HitHandle(e.Location);
                    if (h2 >= 0) pic.Cursor = HandleCursor(h2);
                    else if (_cropRect.Contains(e.Location)) pic.Cursor = Cursors.SizeAll;
                    else pic.Cursor = Cursors.Cross;

                    if (e.Button == MouseButtons.Left && _cropDragHandle >= 0)
                    {
                        int dx = e.X - _cropDragStart.X, dy = e.Y - _cropDragStart.Y;
                        var r = _cropRectAtDragStart;

                        if (_cropDragHandle == 8)
                        {
                            _cropRect = new Rectangle(
                                Math.Max(0, Math.Min(pic.Width - r.Width, r.X + dx)),
                                Math.Max(0, Math.Min(pic.Height - r.Height, r.Y + dy)),
                                r.Width, r.Height);
                        }
                        else
                        {
                            int x1 = r.Left, y1 = r.Top, x2 = r.Right, y2 = r.Bottom;
                            if (_cropDragHandle == 0 || _cropDragHandle == 3 || _cropDragHandle == 5) x1 = Math.Min(r.Right - 8, x1 + dx);
                            if (_cropDragHandle == 2 || _cropDragHandle == 4 || _cropDragHandle == 7) x2 = Math.Max(r.Left + 8, x2 + dx);
                            if (_cropDragHandle == 0 || _cropDragHandle == 1 || _cropDragHandle == 2) y1 = Math.Min(r.Bottom - 8, y1 + dy);
                            if (_cropDragHandle == 5 || _cropDragHandle == 6 || _cropDragHandle == 7) y2 = Math.Max(r.Top + 8, y2 + dy);
                            x1 = Math.Max(0, x1); y1 = Math.Max(0, y1);
                            x2 = Math.Min(pic.Width, x2); y2 = Math.Min(pic.Height, y2);
                            _cropRect = new Rectangle(x1, y1, x2 - x1, y2 - y1);
                        }
                        pic.Invalidate();
                    }
                    return;
                }

                if (e.Button == MouseButtons.Left)
                {
                    _cropRect = new Rectangle(
                        Math.Min(_cropStart.X, e.X), Math.Min(_cropStart.Y, e.Y),
                        Math.Abs(e.X - _cropStart.X), Math.Abs(e.Y - _cropStart.Y));
                    pic.Invalidate();
                }
                return;
            }

            if (_drawMode && _isDrawing && e.Button == MouseButtons.Left && _edited != null)
            {
                var cur = PicToImg(e.Location);
                using var g = Graphics.FromImage(_edited);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var pen = new Pen(_drawColor, _brushSize)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                g.DrawLine(pen, _lastDrawPt, cur);
                _lastDrawPt = cur;
                pic.Image = _edited;
                pic.Invalidate();
            }
        }

        /// <summary>
        /// Maneja MouseUp: confirma la selección de recorte o termina el trazo de dibujo.
        /// </summary>
        void Pic_MouseUp(object s, MouseEventArgs e)
        {
            if (_cropMode)
            {
                _cropDragHandle = -1;
                if (!_cropConfirmed && _cropRect.Width > 8 && _cropRect.Height > 8)
                { _cropConfirmed = true; pic.Cursor = Cursors.SizeAll; pic.Invalidate(); }
                return;
            }
            if (_drawMode) _isDrawing = false;
        }

        // ════════════════════════════════════════════════════════════
        //  PAINT DEL PICTUREBOX (overlay de recorte)
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Dibuja el overlay de recorte: zona oscurecida, rejilla de tercios,
        /// handles de redimensionado y botones de Aplicar/Cancelar.
        /// </summary>
        void Pic_Paint(object s, PaintEventArgs e)
        {
            if (!_cropMode || (_cropRect.Width <= 0 && _cropRect.Height <= 0)) return;

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var dim = new SolidBrush(Color.FromArgb(130, 0, 0, 0));
            if (_cropRect.Width > 0 && _cropRect.Height > 0)
            {
                g.FillRectangle(dim, 0, 0, pic.Width, _cropRect.Top);
                g.FillRectangle(dim, 0, _cropRect.Bottom, pic.Width, pic.Height - _cropRect.Bottom);
                g.FillRectangle(dim, 0, _cropRect.Top, _cropRect.Left, _cropRect.Height);
                g.FillRectangle(dim, _cropRect.Right, _cropRect.Top, pic.Width - _cropRect.Right, _cropRect.Height);
            }

            using var penSel = new Pen(Color.White, 1.5f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            g.DrawRectangle(penSel, _cropRect);

            using var penGrid = new Pen(Color.FromArgb(70, 255, 255, 255), 1f);
            int tx = _cropRect.Width / 3, ty = _cropRect.Height / 3;
            g.DrawLine(penGrid, _cropRect.X + tx, _cropRect.Y, _cropRect.X + tx, _cropRect.Bottom);
            g.DrawLine(penGrid, _cropRect.X + tx * 2, _cropRect.Y, _cropRect.X + tx * 2, _cropRect.Bottom);
            g.DrawLine(penGrid, _cropRect.X, _cropRect.Y + ty, _cropRect.Right, _cropRect.Y + ty);
            g.DrawLine(penGrid, _cropRect.X, _cropRect.Y + ty * 2, _cropRect.Right, _cropRect.Y + ty * 2);

            if (_cropConfirmed)
            {
                foreach (var hr in GetHandleRects())
                {
                    g.FillRectangle(Brushes.White, hr);
                    g.DrawRectangle(new Pen(Color.FromArgb(80, 0, 0, 0)), hr);
                }

                string dim2 = $"{_cropRect.Width}\u00d7{_cropRect.Height}";
                var dFont = new Font("Segoe UI", 8f, FontStyle.Bold);
                var dSz = g.MeasureString(dim2, dFont);
                int dx2 = _cropRect.X + (_cropRect.Width - (int)dSz.Width) / 2, dy2 = _cropRect.Y + 6;
                g.FillRectangle(new SolidBrush(Color.FromArgb(150, 0, 0, 0)), dx2 - 4, dy2 - 2, dSz.Width + 8, dSz.Height + 4);
                g.DrawString(dim2, dFont, Brushes.White, dx2, dy2);

                DrawCropButton(g, GetCropApplyRect(), "\u2713  Aplicar recorte", Color.FromArgb(35, 160, 55));
                DrawCropButton(g, GetCropCancelRect(), "\u2715  Cancelar", Color.FromArgb(180, 50, 50));
            }
            else if (_cropRect.Width > 0)
            {
                string hint = $"{_cropRect.Width}\u00d7{_cropRect.Height}";
                var hFont = new Font("Segoe UI", 8f);
                var hSz = g.MeasureString(hint, hFont);
                g.FillRectangle(new SolidBrush(Color.FromArgb(160, 0, 0, 0)),
                    _cropRect.X, _cropRect.Bottom + 4, hSz.Width + 10, hSz.Height + 6);
                g.DrawString(hint, hFont, Brushes.White, _cropRect.X + 5, _cropRect.Bottom + 7);
            }
        }

        // ── Helpers de recorte ───────────────────────────────────────

        /// <summary>Devuelve los 8 rectángulos de los handles de redimensionado del recorte.</summary>
        Rectangle[] GetHandleRects()
        {
            int hs = 9;
            int cx = _cropRect.X + _cropRect.Width / 2 - hs / 2;
            int cy = _cropRect.Y + _cropRect.Height / 2 - hs / 2;
            return new[]
            {
                new Rectangle(_cropRect.X  - hs/2, _cropRect.Y      - hs/2, hs, hs),
                new Rectangle(cx,                   _cropRect.Y      - hs/2, hs, hs),
                new Rectangle(_cropRect.Right-hs/2, _cropRect.Y      - hs/2, hs, hs),
                new Rectangle(_cropRect.X  - hs/2, cy,                       hs, hs),
                new Rectangle(_cropRect.Right-hs/2, cy,                       hs, hs),
                new Rectangle(_cropRect.X  - hs/2, _cropRect.Bottom - hs/2, hs, hs),
                new Rectangle(cx,                   _cropRect.Bottom - hs/2, hs, hs),
                new Rectangle(_cropRect.Right-hs/2, _cropRect.Bottom - hs/2, hs, hs),
            };
        }

        /// <summary>Retorna el índice del handle que contiene el punto, o -1 si ninguno.</summary>
        int HitHandle(Point p)
        {
            var hs = GetHandleRects();
            for (int i = 0; i < hs.Length; i++)
            { var h = hs[i]; h.Inflate(8, 8); if (h.Contains(p)) return i; }
            return -1;
        }

        /// <summary>Devuelve el cursor adecuado para el handle indicado.</summary>
        Cursor HandleCursor(int h) => h switch
        {
            0 or 7 => Cursors.SizeNWSE,
            2 or 5 => Cursors.SizeNESW,
            1 or 6 => Cursors.SizeNS,
            _ => Cursors.SizeWE
        };

        /// <summary>Rectángulo del botón "Aplicar recorte" sobre el canvas.</summary>
        Rectangle GetCropApplyRect()
        {
            int bw = 160, bh = 30;
            int bx = _cropRect.X + _cropRect.Width / 2 - bw - 4;
            int by = Math.Min(pic.Height - bh - 8, _cropRect.Bottom + 8);
            return new Rectangle(Math.Max(0, bx), by, bw, bh);
        }

        /// <summary>Rectángulo del botón "Cancelar" sobre el canvas.</summary>
        Rectangle GetCropCancelRect()
        {
            int bw = 100, bh = 30;
            int bx = _cropRect.X + _cropRect.Width / 2 + 4;
            int by = Math.Min(pic.Height - bh - 8, _cropRect.Bottom + 8);
            return new Rectangle(Math.Min(pic.Width - bw, bx), by, bw, bh);
        }

        /// <summary>Dibuja un botón con esquinas redondeadas sobre el canvas de recorte.</summary>
        void DrawCropButton(Graphics g, Rectangle r, string text, Color bg)
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            int rad = 6;
            path.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            path.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            path.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            path.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            path.CloseFigure();
            g.FillPath(new SolidBrush(bg), path);

            var sf = new StringFormat
            { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Brushes.White, new RectangleF(r.X, r.Y, r.Width, r.Height), sf);
        }

        /// <summary>
        /// Convierte un punto en coordenadas del PictureBox a coordenadas de la imagen editada.
        /// </summary>
        Point PicToImg(Point p)
        {
            if (_edited == null || pic.Width <= 0 || pic.Height <= 0) return p;

            float ia = (float)_edited.Width / _edited.Height;
            float ba = (float)pic.Width / pic.Height;
            float sc; int ox = 0, oy = 0;

            if (ia > ba) { sc = (float)pic.Width / _edited.Width; oy = (int)((pic.Height - _edited.Height * sc) / 2); }
            else { sc = (float)pic.Height / _edited.Height; ox = (int)((pic.Width - _edited.Width * sc) / 2); }

            return new Point(
                Math.Max(0, Math.Min(_edited.Width - 1, (int)((p.X - ox) / sc))),
                Math.Max(0, Math.Min(_edited.Height - 1, (int)((p.Y - oy) / sc))));
        }

        // ════════════════════════════════════════════════════════════
        //  DIÁLOGO DE TEXTO
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Muestra un formulario modal para agregar texto sobre la imagen,
        /// con opciones de fuente, tamaño, negrita, color y posición.
        /// </summary>
        void ShowTextInputDialog()
        {
            Action refreshPreview = null;
            const int LX = 20, W = 520;

            var frm = new Form
            {
                Text = "\u270F  Agregar texto a la imagen",
                Size = new Size(564, 430),
                MinimumSize = new Size(564, 430),
                MaximumSize = new Size(564, 430),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(36, 36, 40),
                Font = new Font("Segoe UI", 9.5f),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
            };
            int fy = 14;

            void AL(string t, int x, int y) =>
                frm.Controls.Add(new Label
                {
                    Text = t,
                    Left = x,
                    Top = y,
                    AutoSize = true,
                    ForeColor = C_SUB,
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 8.5f)
                });

            AL("Texto:", LX, fy); fy += 18;
            var txtInput = new TextBox
            {
                Left = LX,
                Top = fy,
                Width = W,
                Height = 28,
                Font = new Font("Segoe UI", 11f),
                BackColor = Color.FromArgb(52, 52, 58),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            frm.Controls.Add(txtInput); fy += 38;

            AL("Fuente:", LX, fy); fy += 18;
            var cboFont = new ComboBox
            {
                Left = LX,
                Top = fy,
                Width = W,
                Height = 26,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(52, 52, 58),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f)
            };
            foreach (var f in new[] { "Segoe UI", "Arial", "Times New Roman", "Courier New", "Verdana", "Tahoma", "Impact", "Comic Sans MS" })
                cboFont.Items.Add(f);
            cboFont.SelectedItem = _fontFamily;
            if (cboFont.SelectedIndex < 0) cboFont.SelectedIndex = 0;
            frm.Controls.Add(cboFont); fy += 36;

            AL("Tamaño (px):", LX, fy); AL("Estilo:", LX + 180, fy); fy += 18;
            var nudSize = new NumericUpDown
            {
                Left = LX,
                Top = fy,
                Width = 150,
                Height = 28,
                Minimum = 8,
                Maximum = 300,
                Value = (decimal)_fontSize,
                BackColor = Color.FromArgb(52, 52, 58),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f)
            };
            frm.Controls.Add(nudSize);
            var chkBold = new CheckBox
            {
                Left = LX + 180,
                Top = fy + 2,
                Width = 120,
                Height = 24,
                Text = "Negrita",
                Checked = _textBold,
                ForeColor = C_TEXT,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9.5f)
            };
            frm.Controls.Add(chkBold); fy += 36;

            AL("Color del texto:", LX, fy); fy += 18;
            var btnColor = new Button
            {
                Left = LX,
                Top = fy,
                Width = 64,
                Height = 28,
                BackColor = _textColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnColor.FlatAppearance.BorderSize = 1;
            btnColor.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 130);
            btnColor.Click += (_, __) =>
            {
                using var dlg = new ColorDialog { Color = btnColor.BackColor, FullOpen = true, AnyColor = true };
                if (dlg.ShowDialog() == DialogResult.OK) { btnColor.BackColor = dlg.Color; refreshPreview?.Invoke(); }
            };
            frm.Controls.Add(btnColor); fy += 36;

            AL("Posición del texto:", LX, fy); fy += 18;
            var cboPosicion = new ComboBox
            {
                Left = LX,
                Top = fy,
                Width = 280,
                Height = 26,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(52, 52, 58),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f)
            };
            cboPosicion.Items.AddRange(new object[]
            {
                "Superior izquierda","Superior centro","Superior derecha",
                "Centro izquierda","Centro","Centro derecha",
                "Inferior izquierda","Inferior centro","Inferior derecha"
            });
            cboPosicion.SelectedIndex = 0;
            frm.Controls.Add(cboPosicion); fy += 36;

            var lblPrev = new Label
            {
                Left = LX,
                Top = fy,
                Width = W,
                Height = 44,
                Text = "Vista previa",
                ForeColor = _textColor,
                BackColor = Color.FromArgb(22, 22, 26),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                Font = new Font(_fontFamily, 15f, _textBold ? FontStyle.Bold : FontStyle.Regular)
            };
            frm.Controls.Add(lblPrev); fy += 52;

            refreshPreview = () =>
            {
                if (lblPrev == null) return;
                lblPrev.Text = string.IsNullOrEmpty(txtInput.Text) ? "Vista previa" : txtInput.Text;
                lblPrev.ForeColor = btnColor.BackColor;
                try
                {
                    lblPrev.Font = new Font(
                        cboFont.SelectedItem?.ToString() ?? "Segoe UI",
                        Math.Max(8f, Math.Min(18f, (float)nudSize.Value)),
                        chkBold.Checked ? FontStyle.Bold : FontStyle.Regular);
                }
                catch { }
            };

            txtInput.TextChanged += (_, __) => refreshPreview?.Invoke();
            cboFont.SelectedIndexChanged += (_, __) => refreshPreview?.Invoke();
            nudSize.ValueChanged += (_, __) => refreshPreview?.Invoke();
            chkBold.CheckedChanged += (_, __) => refreshPreview?.Invoke();
            refreshPreview.Invoke();

            var btnOk = new Button
            {
                Text = "\u270F  Agregar texto",
                Left = LX,
                Top = fy,
                Width = 180,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_ACCENT,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            var btnCan = new Button
            {
                Text = "Cancelar",
                Left = LX + 190,
                Top = fy,
                Width = 100,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 68),
                ForeColor = C_TEXT,
                Cursor = Cursors.Hand
            };
            btnCan.FlatAppearance.BorderSize = 0;
            btnCan.Click += (_, __) => frm.Close();
            frm.Controls.AddRange(new Control[] { btnOk, btnCan });

            btnOk.Click += (_, __) =>
            {
                string txt = txtInput.Text;
                if (string.IsNullOrEmpty(txt)) { frm.Close(); return; }

                _textColor = btnColor.BackColor;
                _fontFamily = cboFont.SelectedItem?.ToString() ?? "Segoe UI";
                _fontSize = (float)nudSize.Value;
                _textBold = chkBold.Checked;

                using var drawFont = new Font(_fontFamily, _fontSize, _textBold ? FontStyle.Bold : FontStyle.Regular);
                using var gm = Graphics.FromImage(_edited);
                SizeF sz = gm.MeasureString(txt, drawFont);

                const int margin = 20;
                int col = cboPosicion.SelectedIndex % 3, row = cboPosicion.SelectedIndex / 3;
                int px = col == 0 ? margin : col == 1 ? (int)((_edited.Width - sz.Width) / 2) : (int)(_edited.Width - sz.Width - margin);
                int py = row == 0 ? margin : row == 1 ? (int)((_edited.Height - sz.Height) / 2) : (int)(_edited.Height - sz.Height - margin);
                px = Math.Max(0, px); py = Math.Max(0, py);

                gm.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                gm.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                using var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
                gm.DrawString(txt, drawFont, shadow, px + 2, py + 2);
                using var brush = new SolidBrush(_textColor);
                gm.DrawString(txt, drawFont, brush, px, py);

                pic.Image = _edited; pic.Invalidate(); frm.Close();
            };

            frm.AcceptButton = btnOk;
            txtInput.Focus();
            frm.ShowDialog(this);
        }

        // ════════════════════════════════════════════════════════════
        //  ELIMINAR IMAGEN
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Pide confirmación y elimina la imagen actual del disco.
        /// Navega a la siguiente imagen o cierra el visor si no quedan más.
        /// </summary>
        void DeleteCurrentImage()
        {
            var path = _allImages[_currentIdx];
            var name = Path.GetFileName(path);

            var result = MessageBox.Show(
                $"¿Eliminar \"{name}\" permanentemente?\n\nEsta acción no se puede deshacer.",
                "Eliminar imagen",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes) return;

            DisposeCurrentBitmaps();
            pic.Image = null;

            try { File.Delete(path); }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo eliminar:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                LoadCurrentImage();
                return;
            }

            _allImages.RemoveAt(_currentIdx);

            if (_allImages.Count == 0) { Close(); return; }

            if (_currentIdx >= _allImages.Count)
                _currentIdx = _allImages.Count - 1;

            LoadCurrentImage();
        }
    }
}
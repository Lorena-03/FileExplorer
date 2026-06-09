using FileExplorer.Helpers;
using System.IO.Compression;

namespace FileExplorer.Controls
{
    public class FileListControl : UserControl
    {
        static readonly Color BgColor = Color.White;
        static readonly Color HdrBg = Color.FromArgb(246, 246, 246);
        static readonly Color HdrFg = Color.FromArgb(120, 120, 128);
        static readonly Color RowFg = Color.FromArgb(28, 28, 30);
        static readonly Color BorderC = Color.FromArgb(209, 209, 214);
        static readonly Color DropHigh = Color.FromArgb(210, 230, 255);

        public event Action<string> FileOpened;
        public event Action<string> FolderNavigated;
        public event Action<string> SelectionChanged;

        ListView _lv;
        ImageList _icons;
        int _sortCol = 0;
        bool _sortAsc = true;

        // ── Drag & Drop ──────────────────────────────────────────────
        ListViewItem _dragOverItem = null;
        Color _dragOverOldBg = Color.White;
        Point _dragStartPoint = Point.Empty;
        bool _dragging = false;

        public List<string> ClipboardPaths { get; set; } = new();
        public bool ClipboardIsCut { get; set; }
        public int TotalItems => _lv.Items.Count;

        public FileListControl()
        {
            _icons = FileHelper.BuildIconList(16);
            BackColor = BgColor;

            _lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                SmallImageList = _icons,
                MultiSelect = true,
                HideSelection = false,
                BackColor = BgColor,
                ForeColor = RowFg,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5f),
                OwnerDraw = true,
                // AllowDrop se asigna después de agregar columnas para evitar
                // "Error al registrar DragDrop" en .NET 8 con PerMonitorV2 DPI.
            };
            _lv.Columns.Add("Nombre", 360);
            _lv.Columns.Add("Modificado", 155);
            _lv.Columns.Add("Tipo", 110);
            _lv.Columns.Add("Tamaño", 90);

            // ← Fix: asignar AllowDrop aquí, fuera del inicializador de objeto
            _lv.AllowDrop = true;

            _lv.DrawColumnHeader += (s, e) =>
            {
                e.Graphics.FillRectangle(new SolidBrush(HdrBg), e.Bounds);
                e.Graphics.DrawLine(new Pen(BorderC),
                    e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right, e.Bounds.Bottom - 1);
                var sf = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                e.Graphics.DrawString(e.Header.Text,
                    new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    new SolidBrush(HdrFg),
                    new RectangleF(e.Bounds.X + 6, e.Bounds.Y,
                        e.Bounds.Width - 10, e.Bounds.Height), sf);
            };
            _lv.DrawItem += (s, e) => e.DrawDefault = true;
            _lv.DrawSubItem += (s, e) => e.DrawDefault = true;
            _lv.DoubleClick += (s, e) => Open();
            _lv.KeyDown += OnKeyDown;
            _lv.SelectedIndexChanged += (s, e) =>
                SelectionChanged?.Invoke(GetPath() ?? "");
            _lv.ColumnClick += (s, e) =>
            {
                if (_sortCol == e.Column) _sortAsc = !_sortAsc;
                else { _sortCol = e.Column; _sortAsc = true; }
                _lv.ListViewItemSorter = new LvComparer(e.Column, _sortAsc);
            };

            // ── Drag & Drop eventos ──────────────────────────────────
            _lv.MouseDown += Lv_MouseDown;
            _lv.MouseMove += Lv_MouseMove;
            _lv.MouseUp += Lv_MouseUp;
            _lv.DragEnter += Lv_DragEnter;
            _lv.DragOver += Lv_DragOver;
            _lv.DragLeave += Lv_DragLeave;
            _lv.DragDrop += Lv_DragDrop;

            Controls.Add(_lv);
            BuildCtxMenu();
        }

        // ════════════════════════════════════════════════════════════
        //  DRAG & DROP — INICIAR ARRASTRE
        // ════════════════════════════════════════════════════════════

        void Lv_MouseDown(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                _dragStartPoint = e.Location;
        }

        void Lv_MouseMove(object s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _dragging) return;
            if (_dragStartPoint == Point.Empty) return;

            // Iniciar arrastre solo si se movió suficiente
            if (Math.Abs(e.X - _dragStartPoint.X) < SystemInformation.DragSize.Width &&
                Math.Abs(e.Y - _dragStartPoint.Y) < SystemInformation.DragSize.Height)
                return;

            var paths = GetPaths();
            if (paths.Count == 0) return;

            _dragging = true;
            var data = new DataObject(DataFormats.FileDrop, paths.ToArray());
            _lv.DoDragDrop(data, DragDropEffects.Move | DragDropEffects.Copy);
            _dragging = false;
            _dragStartPoint = Point.Empty;
        }

        void Lv_MouseUp(object s, MouseEventArgs e)
        {
            _dragStartPoint = Point.Empty;
            _dragging = false;
        }

        // ════════════════════════════════════════════════════════════
        //  DRAG & DROP — RECIBIR ARCHIVOS
        // ════════════════════════════════════════════════════════════

        void Lv_DragEnter(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = e.KeyState == 8  // Ctrl presionado = copiar
                    ? DragDropEffects.Copy
                    : DragDropEffects.Move;
            else
                e.Effect = DragDropEffects.None;
        }

        void Lv_DragOver(object s, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            { e.Effect = DragDropEffects.None; return; }

            e.Effect = (e.KeyState & 8) != 0
                ? DragDropEffects.Copy
                : DragDropEffects.Move;

            // Resaltar carpeta bajo el cursor
            var pt = _lv.PointToClient(new Point(e.X, e.Y));
            var item = _lv.GetItemAt(pt.X, pt.Y);

            if (item != _dragOverItem)
            {
                // Restaurar item anterior
                if (_dragOverItem != null)
                { _dragOverItem.BackColor = _dragOverOldBg; _dragOverItem = null; }

                // Resaltar nueva carpeta
                if (item != null && Directory.Exists(item.Tag as string))
                {
                    _dragOverOldBg = item.BackColor;
                    item.BackColor = DropHigh;
                    _dragOverItem = item;
                }
            }
        }

        void Lv_DragLeave(object s, EventArgs e)
        {
            if (_dragOverItem != null)
            { _dragOverItem.BackColor = _dragOverOldBg; _dragOverItem = null; }
        }

        void Lv_DragDrop(object s, DragEventArgs e)
        {
            // Limpiar resaltado
            if (_dragOverItem != null)
            { _dragOverItem.BackColor = _dragOverOldBg; _dragOverItem = null; }

            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var sources = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (sources == null || sources.Length == 0) return;

            // Determinar carpeta destino
            var pt = _lv.PointToClient(new Point(e.X, e.Y));
            var dropItem = _lv.GetItemAt(pt.X, pt.Y);
            string destDir;

            if (dropItem != null && Directory.Exists(dropItem.Tag as string))
                destDir = dropItem.Tag as string;   // soltar sobre carpeta
            else
                destDir = Tag as string ?? "";       // soltar en carpeta actual

            if (string.IsNullOrEmpty(destDir)) return;

            bool copy = (e.Effect == DragDropEffects.Copy);
            int ok = 0, skip = 0;

            foreach (var src in sources)
            {
                try
                {
                    string name = Path.GetFileName(src);
                    string dst = FileHelper.GetUniquePath(Path.Combine(destDir, name));

                    // No mover sobre sí mismo
                    if (string.Equals(src, dst, StringComparison.OrdinalIgnoreCase))
                    { skip++; continue; }

                    // No mover carpeta dentro de sí misma
                    if (Directory.Exists(src) &&
                        dst.StartsWith(src + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(
                            $"No se puede mover '{name}' dentro de sí misma.",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }

                    if (copy)
                    {
                        if (Directory.Exists(src)) FileHelper.CopyDirectory(src, dst);
                        else File.Copy(src, dst, false);
                    }
                    else
                    {
                        if (Directory.Exists(src)) Directory.Move(src, dst);
                        else File.Move(src, dst);
                    }
                    ok++;
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"FileListControl.DragDrop [{src}] falló", ex);
                    MessageBox.Show(
                        $"Error al {(copy ? "copiar" : "mover")} '{Path.GetFileName(src)}':\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // Recargar lista
            LoadDirectory(Tag as string ?? "");

            if (ok > 0)
                AppLogger.Info($"FileListControl: {(copy ? "copiados" : "movidos")} {ok} elemento(s) a {destDir}");
        }

        // ════════════════════════════════════════════════════════════
        //  CARGA DE DIRECTORIO
        // ════════════════════════════════════════════════════════════
        public void LoadDirectory(string path)
        {
            _lv.BeginUpdate();
            _lv.Items.Clear();
            try
            {
                foreach (var d in Directory.GetDirectories(path).OrderBy(x => x))
                {
                    try
                    {
                        var di = new DirectoryInfo(d);
                        var item = new ListViewItem(di.Name, FileHelper.ICO_FOLDER) { Tag = d };
                        item.SubItems.Add(di.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
                        item.SubItems.Add("Carpeta");
                        item.SubItems.Add("—");
                        _lv.Items.Add(item);
                    }
                    catch (Exception ex)
                    { AppLogger.Warn($"FileListControl: no se pudo leer carpeta [{d}]: {ex.Message}"); }
                }
                foreach (var f in Directory.GetFiles(path).OrderBy(x => x))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        var item = new ListViewItem(fi.Name, FileHelper.GetIconIndex(f, false)) { Tag = f };
                        item.SubItems.Add(fi.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
                        item.SubItems.Add(FileHelper.GetTypeDescription(f));
                        item.SubItems.Add(FileHelper.FormatSize(fi.Length));
                        _lv.Items.Add(item);
                    }
                    catch (Exception ex)
                    { AppLogger.Warn($"FileListControl: no se pudo leer archivo [{f}]: {ex.Message}"); }
                }
            }
            catch (UnauthorizedAccessException)
            { MessageBox.Show("Acceso denegado.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            catch (Exception ex)
            {
                AppLogger.Error($"FileListControl.LoadDirectory [{path}] falló", ex);
                MessageBox.Show("Error al cargar el directorio:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            _lv.EndUpdate();
        }

        public void FilterItems(string q)
        {
            foreach (ListViewItem it in _lv.Items)
                it.ForeColor = it.Text.ToLowerInvariant().Contains(q.ToLowerInvariant())
                    ? RowFg : Color.FromArgb(200, 200, 205);
        }

        public void ClearFilter()
        {
            foreach (ListViewItem it in _lv.Items) it.ForeColor = RowFg;
        }

        public string GetPath() =>
            _lv.SelectedItems.Count > 0 ? _lv.SelectedItems[0].Tag as string : null;

        public List<string> GetPaths() =>
            _lv.SelectedItems.Cast<ListViewItem>()
               .Select(i => i.Tag as string ?? "").ToList();

        void OnKeyDown(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) Open();
            else if (e.KeyCode == Keys.Delete) MoverAPapelera();
            else if (e.KeyCode == Keys.F2) Rename();
            else if (e.Control && e.KeyCode == Keys.C) Copy(false);
            else if (e.Control && e.KeyCode == Keys.X) Copy(true);
            else if (e.Control && e.KeyCode == Keys.V) Paste();
            else if (e.Control && e.KeyCode == Keys.A)
                foreach (ListViewItem it in _lv.Items) it.Selected = true;
        }

        void Open()
        {
            var p = GetPath();
            if (p == null) return;
            if (Directory.Exists(p)) FolderNavigated?.Invoke(p);
            else FileOpened?.Invoke(p);
        }

        public void Copy(bool cut)
        {
            ClipboardPaths = GetPaths();
            ClipboardIsCut = cut;
        }

        public void Paste()
        {
            if (ClipboardPaths.Count == 0) return;
            var cur = Tag as string ?? "";
            foreach (var src in ClipboardPaths)
            {
                try
                {
                    var dst = FileHelper.GetUniquePath(Path.Combine(cur, Path.GetFileName(src)));
                    if (Directory.Exists(src))
                    { FileHelper.CopyDirectory(src, dst); if (ClipboardIsCut) Directory.Delete(src, true); }
                    else
                    { File.Copy(src, dst); if (ClipboardIsCut) File.Delete(src); }
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"FileListControl.Paste [{src}] falló", ex);
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            if (ClipboardIsCut) ClipboardPaths.Clear();
            LoadDirectory(cur);
        }

        public void Delete()
        {
            var paths = GetPaths();
            if (paths.Count == 0) return;
            if (MessageBox.Show(
                $"¿Eliminar PERMANENTEMENTE {paths.Count} elemento(s)?\n(No irá a la Papelera)",
                "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                != DialogResult.Yes) return;
            foreach (var p in paths)
            {
                try
                {
                    if (Directory.Exists(p)) Directory.Delete(p, true);
                    else File.Delete(p);
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"FileListControl.Delete [{p}] falló", ex);
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            LoadDirectory(Tag as string ?? "");
        }

        public void Rename()
        {
            if (_lv.SelectedItems.Count == 0) return;
            _lv.LabelEdit = true;
            _lv.SelectedItems[0].BeginEdit();
            void handler(object s2, LabelEditEventArgs e)
            {
                _lv.LabelEdit = false;
                _lv.AfterLabelEdit -= handler;
                if (e.Label == null || e.Item < 0) return;
                var it = _lv.Items[e.Item];
                var old = it.Tag as string ?? "";
                var newp = Path.Combine(Path.GetDirectoryName(old) ?? "", e.Label);
                try
                {
                    if (Directory.Exists(old)) Directory.Move(old, newp);
                    else File.Move(old, newp);
                    it.Tag = newp;
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"FileListControl.Rename [{old}] falló", ex);
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    e.CancelEdit = true;
                }
            }
            _lv.AfterLabelEdit += handler;
        }

        public void NewFolder()
        {
            var cur = Tag as string ?? "";
            using var dlg = new Form
            {
                Text = "Nueva carpeta",
                Size = new Size(420, 170),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
            };
            var txt = new TextBox { Left = 10, Top = 12, Width = 390, Text = "Nueva carpeta" };
            var ok = new Button { Text = "Crear", Left = 210, Top = 50, Width = 90, Height = 30, DialogResult = DialogResult.OK };
            var ca = new Button { Text = "Cancelar", Left = 308, Top = 50, Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
            dlg.Controls.AddRange(new Control[] { txt, ok, ca });
            dlg.AcceptButton = ok; dlg.CancelButton = ca;
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var np = FileHelper.GetUniquePath(Path.Combine(cur, txt.Text.Trim()));
            try { Directory.CreateDirectory(np); LoadDirectory(cur); }
            catch (Exception ex)
            {
                AppLogger.Error("FileListControl.NewFolder falló", ex);
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  COMPRIMIR / DESCOMPRIMIR
        // ════════════════════════════════════════════════════════════
        public async Task ComprimirSeleccion()
        {
            var paths = GetPaths();
            if (paths.Count == 0) return;
            string cur = Tag as string ?? "";
            string defName = paths.Count == 1
                ? Path.GetFileNameWithoutExtension(paths[0]) + ".zip" : "archivos.zip";
            string destZip = null;
            var t = new Thread(() =>
            {
                using var dlg = new SaveFileDialog
                { Title = "Guardar archivo ZIP", FileName = defName, Filter = "ZIP|*.zip", InitialDirectory = cur };
                if (dlg.ShowDialog() == DialogResult.OK) destZip = dlg.FileName;
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (destZip == null) return;
            try
            {
                await Task.Run(() =>
                {
                    if (File.Exists(destZip)) File.Delete(destZip);
                    using var zip = ZipFile.Open(destZip, ZipArchiveMode.Create);
                    foreach (var p in paths)
                    {
                        if (File.Exists(p))
                            zip.CreateEntryFromFile(p, Path.GetFileName(p), CompressionLevel.Optimal);
                        else if (Directory.Exists(p))
                            AddFolderToZip(zip, p, Path.GetFileName(p));
                    }
                });
                LoadDirectory(cur);
                MessageBox.Show($"Comprimido: {Path.GetFileName(destZip)}",
                    "Listo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error("FileListControl.ComprimirSeleccion falló", ex);
                MessageBox.Show("Error al comprimir:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static void AddFolderToZip(ZipArchive zip, string folder, string entryBase)
        {
            foreach (var f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string rel = Path.Combine(entryBase, Path.GetRelativePath(folder, f));
                zip.CreateEntryFromFile(f, rel, CompressionLevel.Optimal);
            }
        }

        public async Task DescomprimirSeleccion()
        {
            var paths = GetPaths()
                .Where(p => p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList();
            if (paths.Count == 0) { MessageBox.Show("Selecciona al menos un archivo ZIP.", "Aviso"); return; }
            string cur = Tag as string ?? "";
            string dest = null;
            var t = new Thread(() =>
            {
                using var dlg = new FolderBrowserDialog
                { Description = "Extraer en:", SelectedPath = cur, UseDescriptionForTitle = true };
                if (dlg.ShowDialog() == DialogResult.OK) dest = dlg.SelectedPath;
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (dest == null) return;
            int ok2 = 0, fail = 0;
            await Task.Run(() =>
            {
                foreach (var zip in paths)
                {
                    try
                    {
                        string outDir = Path.Combine(dest, Path.GetFileNameWithoutExtension(zip));
                        Directory.CreateDirectory(outDir);
                        ZipFile.ExtractToDirectory(zip, outDir, overwriteFiles: true);
                        ok2++;
                    }
                    catch (Exception ex) { AppLogger.Error($"FileListControl.Descomprimir [{zip}] falló", ex); fail++; }
                }
            });
            LoadDirectory(cur);
            string msg = ok2 > 0 ? $"{ok2} ZIP extraído(s)." : "";
            if (fail > 0) msg += $"\n{fail} no se pudo(n) extraer.";
            MessageBox.Show(msg.Trim(), "Listo", MessageBoxButtons.OK,
                fail > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        // ════════════════════════════════════════════════════════════
        //  PAPELERA
        // ════════════════════════════════════════════════════════════
        public void MoverAPapelera()
        {
            var paths = GetPaths();
            if (paths.Count == 0) return;
            if (MessageBox.Show($"¿Mover {paths.Count} elemento(s) a la Papelera?",
                "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes) return;
            foreach (var p in paths)
            {
                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(p,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"FileListControl.MoverAPapelera [{p}] falló", ex);
                    MessageBox.Show($"No se pudo mover '{Path.GetFileName(p)}' a la Papelera:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            LoadDirectory(Tag as string ?? "");
        }

        public void AbrirPapelera()
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo("shell:RecycleBinFolder")
                    { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Error("FileListControl.AbrirPapelera falló", ex);
                MessageBox.Show("No se pudo abrir la Papelera:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void BuildCtxMenu()
        {
            var ctx = new ContextMenuStrip { Font = new Font("Segoe UI", 9.5f) };
            void A(string txt, Action act, string shortcut = "")
            {
                var item = new ToolStripMenuItem(txt);
                if (!string.IsNullOrEmpty(shortcut)) item.ShortcutKeyDisplayString = shortcut;
                item.Click += (_, __) => act();
                ctx.Items.Add(item);
            }
            A("Abrir", Open);
            ctx.Items.Add(new ToolStripSeparator());
            A("Copiar", () => Copy(false), "Ctrl+C");
            A("Cortar", () => Copy(true), "Ctrl+X");
            A("Pegar", Paste, "Ctrl+V");
            ctx.Items.Add(new ToolStripSeparator());
            A("Comprimir en ZIP", () => _ = ComprimirSeleccion());
            A("Extraer aquí (ZIP)", () => _ = DescomprimirSeleccion());
            ctx.Items.Add(new ToolStripSeparator());
            A("Cambiar nombre", Rename, "F2");
            A("Nueva carpeta", NewFolder);
            ctx.Items.Add(new ToolStripSeparator());
            A("Mover a Papelera", MoverAPapelera, "Supr");
            A("Eliminar (sin papelera)", Delete);
            ctx.Items.Add(new ToolStripSeparator());
            A("Abrir Papelera de reciclaje", AbrirPapelera);
            _lv.ContextMenuStrip = ctx;
        }
    }

    class LvComparer : System.Collections.IComparer
    {
        int _col; bool _asc;
        public LvComparer(int col, bool asc) { _col = col; _asc = asc; }
        public int Compare(object x, object y)
        {
            var a = ((ListViewItem)x).SubItems[_col].Text;
            var b = ((ListViewItem)y).SubItems[_col].Text;
            return _asc
                ? string.Compare(a, b, StringComparison.OrdinalIgnoreCase)
                : string.Compare(b, a, StringComparison.OrdinalIgnoreCase);
        }
    }
}

namespace FileExplorer.Forms
{
    internal class DoubleBufferedDataGridView : DataGridView
    {
        public DockStyle Dock { get; set; }
        public Color BackgroundColor { get; set; }
        public BorderStyle BorderStyle { get; set; }
        public Font Font { get; set; }
        public Color GridColor { get; set; }
        public bool VirtualMode { get; set; }
        public bool AllowUserToAddRows { get; set; }
        public bool AllowUserToDeleteRows { get; set; }
        public int RowHeadersWidth { get; set; }
        public DataGridViewSelectionMode SelectionMode { get; set; }
        public DataGridViewAutoSizeColumnsMode AutoSizeColumnsMode { get; set; }
        public int ColumnHeadersHeight { get; set; }
        public object RowTemplate { get; set; }
        public bool DoubleBuffered { get; set; }
        public DataGridViewCellStyle DefaultCellStyle { get; set; }
        public DataGridViewCellStyle ColumnHeadersDefaultCellStyle { get; set; }
        public DataGridViewCellStyle AlternatingRowsDefaultCellStyle { get; set; }
    }
}
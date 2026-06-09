
namespace FileExplorer.Forms
{
    internal class DrawSize
    {
        private int v1;
        private int v2;

        public DrawSize(int v1, int v2)
        {
            this.v1 = v1;
            this.v2 = v2;
        }

        public static implicit operator Size(DrawSize v)
        {
            throw new NotImplementedException();
        }
    }
}
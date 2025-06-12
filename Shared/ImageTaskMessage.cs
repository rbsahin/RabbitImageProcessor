namespace Shared
{
    public class ImageTaskMessage
    {
        public string FileName { get; set; } = string.Empty;
        public string Operation { get; set; } = "resize"; // varsayılan olarak resize
        public int Width { get; set; }
        public int Height { get; set; }
    }
}

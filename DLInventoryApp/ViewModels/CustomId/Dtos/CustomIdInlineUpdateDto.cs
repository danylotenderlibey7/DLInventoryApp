using DLInventoryApp.Models;

namespace DLInventoryApp.ViewModels.CustomId.Dtos
{
    public class CustomIdInlineUpdateDto
    {
        public CustomIdElementType Type { get; set; }
        public string? Text { get; set; }
        public string? Format { get; set; }
        public byte[]? Version { get; set; }
    }
}
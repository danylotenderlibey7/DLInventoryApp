using DLInventoryApp.Models;

namespace DLInventoryApp.ViewModels.CustomId
{
    public class CustomIdElementRowVm
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public CustomIdElementType Type { get; set; }
        public string? Text { get; set; }
        public string? Format { get; set; }
    }
}

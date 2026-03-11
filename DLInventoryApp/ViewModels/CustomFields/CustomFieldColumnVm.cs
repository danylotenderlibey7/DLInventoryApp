using DLInventoryApp.Models;

namespace DLInventoryApp.ViewModels.CustomFields
{
    public class CustomFieldColumnVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public CustomFieldType Type { get; set; }
        public int Order { get; set; }
        public bool ShowInTable { get; set; }
    }
}

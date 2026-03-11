using DLInventoryApp.Models;

namespace DLInventoryApp.ViewModels.CustomFields
{
    public class CreateCustomFieldVm
    {
        public Guid InventoryId { get; set; }
        public string InventoryTitle { get; set; } = string.Empty;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public CustomFieldType Type { get; set; }
        public bool ShowInTable { get; set; }
    }
}

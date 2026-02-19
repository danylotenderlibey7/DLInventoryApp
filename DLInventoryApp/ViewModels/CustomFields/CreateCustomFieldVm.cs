using DLInventoryApp.Models;

namespace DLInventoryApp.ViewModels.CustomFields
{
    public class CreateCustomFieldVm
    {
        public Guid InventoryId { get; set; }
        public string InventoryTitle { get; set; } = string.Empty;
        public string Name { get; set; } = null!;
        public CustomFieldType Type { get; set; }
        public bool IsRequired { get; set; }
        public bool IsUnique { get; set; }
    }
}

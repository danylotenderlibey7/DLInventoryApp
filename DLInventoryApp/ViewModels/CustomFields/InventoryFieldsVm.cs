using DLInventoryApp.ViewModels.Items;

namespace DLInventoryApp.ViewModels.CustomFields
{
    public class InventoryFieldsVm
    {
        public Guid InventoryId { get; set; }
        public string InventoryTitle { get; set; } = null!;
        public List<CustomFieldColumnVm> Fields { get; set; } = new();
    }
}

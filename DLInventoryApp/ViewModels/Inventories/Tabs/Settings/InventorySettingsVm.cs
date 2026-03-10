using DLInventoryApp.ViewModels.Categories;

namespace DLInventoryApp.ViewModels.Inventories.Tabs.Settings
{
    public class InventorySettingsVm
    {
        public Guid InventoryId { get; set; }
        public bool CanManage { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsPublic { get; set; }
        public string OwnerName { get; set; } = "";
        public string OwnerId { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<string> Tags { get; set; } = new();
        public int? CategoryId { get; set; }
        public List<CategoryOptionVm> Categories { get; set; } = new();
        public byte[]? Version { get; set; }
    }
}
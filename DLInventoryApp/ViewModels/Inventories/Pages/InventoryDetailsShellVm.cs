using DLInventoryApp.ViewModels.CustomFields;
using DLInventoryApp.ViewModels.CustomId;
using DLInventoryApp.ViewModels.Discussions;
using DLInventoryApp.ViewModels.Inventories.Tabs.Access;
using DLInventoryApp.ViewModels.Inventories.Tabs.Odoo;
using DLInventoryApp.ViewModels.Inventories.Tabs.Settings;
using DLInventoryApp.ViewModels.Items.Tabs;

namespace DLInventoryApp.ViewModels.Inventories.Pages
{
    public class InventoryDetailsShellVm
    {
        public Guid InventoryId { get; set; }
        public string Title { get; set; } = null!;
        public string ActiveTab { get; set; } = "items";
        public bool CanEditItems { get; set; }
        public bool CanManageInventory { get; set; }
        public InventoryItemsVm? Items { get; set; }
        public DiscussionIndexVm? Discussion { get; set; }
        public CustomIdIndexVm? CustomId { get; set; }
        public InventorySettingsVm? Settings { get; set; }
        public InventoryFieldsVm? Fields { get; set; }
        public InventoryAccessVm? Accesses { get; set; }
        public OdooTabVm? Odoo { get; set; }
    }
}

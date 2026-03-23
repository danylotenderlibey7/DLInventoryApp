namespace DLInventoryApp.ViewModels.Inventories.Tabs.Odoo
{
    public class OdooTabVm
    {
        public Guid InventoryId { get; set; }
        public string InventoryTitle { get; set; } = null!;
        public string? ApiToken { get; set; }
        public bool CanManage { get; set; }
    }
}
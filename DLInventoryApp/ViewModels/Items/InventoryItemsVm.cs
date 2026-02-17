namespace DLInventoryApp.ViewModels.Items
{
    public class InventoryItemsVm
    {
        public Guid InventoryId { get; set; }
        public string InventoryTitle { get; set; } = null!;
        public List<InventoryItemRowVm> Items { get; set; } = new();
    }
}

namespace DLInventoryApp.Models
{
    public class InventoryWriteAccess
    {
        public Guid InventoryId { get; set; }
        public Inventory Inventory { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
    }
}

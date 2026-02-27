namespace DLInventoryApp.Models
{
    public class InventorySequence
    {
        public Guid InventoryId { get; set; }
        public Inventory Inventory { get; set; } = null!;
        public int NextValue { get; set; } = 1;
    }
}

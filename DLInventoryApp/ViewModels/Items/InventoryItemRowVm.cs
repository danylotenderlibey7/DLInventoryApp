namespace DLInventoryApp.ViewModels.Items
{
    public class InventoryItemRowVm
    {
        public Guid Id { get; set; }
        public string CustomId { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

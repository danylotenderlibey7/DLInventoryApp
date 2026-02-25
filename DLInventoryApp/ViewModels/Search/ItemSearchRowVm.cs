namespace DLInventoryApp.ViewModels.Search
{
    public class ItemSearchRowVm
    {
        public Guid Id { get; set; }
        public Guid InventoryId { get; set; }
        public string InventoryTitle { get; set; } = "";
        public string CustomId { get; set; } = null!;
        public string? Snippet { get; set; }
    }
}

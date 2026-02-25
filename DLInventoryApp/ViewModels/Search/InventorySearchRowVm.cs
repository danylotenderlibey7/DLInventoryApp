namespace DLInventoryApp.ViewModels.Search
{
    public class InventorySearchRowVm
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Snippet { get; set; }
    }
}

namespace DLInventoryApp.ViewModels.Search
{
    public class SearchResultVm
    {
        public string? Query { get; set; }
        public List<InventorySearchRowVm> Inventories { get; set; } = new();
        public List<ItemSearchRowVm> Items { get; set; } = new();
    }
}

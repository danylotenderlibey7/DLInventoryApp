namespace DLInventoryApp.ViewModels.Inventories.Inline
{
    public class InventoryInlineTagsDto
    {
        public List<string> Tags { get; set; } = new();
        public string? Version { get; set; }
    }
}

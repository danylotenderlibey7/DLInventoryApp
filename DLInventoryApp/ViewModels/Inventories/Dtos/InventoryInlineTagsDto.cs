namespace DLInventoryApp.ViewModels.Inventories.Inline
{
    public class InventoryInlineTagsDto
    {
        public List<string> Tags { get; set; } = new();
        public uint Version { get; set; }
    }
}

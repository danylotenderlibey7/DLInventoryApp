namespace DLInventoryApp.ViewModels.Inventories
{
    public class InventoryInlineUpdateDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? CategoryId { get; set; }
        public bool IsPublic { get; set; }
        public uint Version { get; set; }
    }
}

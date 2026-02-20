namespace DLInventoryApp.Models
{
    public class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public List<InventoryTag> InventoryTags { get; set; } = new();
    }
}

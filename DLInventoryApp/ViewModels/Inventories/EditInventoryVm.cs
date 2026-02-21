using DLInventoryApp.Models;

namespace DLInventoryApp.ViewModels.Inventories
{
    public class EditInventoryVm
    {
        public Guid InventoryId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; } 
        public bool IsPublic { get; set; } = false;
        public int? CategoryId { get; set; } = null!;
        public List<string> Tags { get; set; } = new(); 
        public byte[]? Version { get; set; }
    }
}

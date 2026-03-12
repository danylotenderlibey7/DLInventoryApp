using DLInventoryApp.Models;
using DLInventoryApp.ViewModels.Categories;

namespace DLInventoryApp.ViewModels.Inventories.Pages
{
    public class EditInventoryVm
    {
        public Guid InventoryId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; } 
        public bool IsPublic { get; set; } = false;
        public int? CategoryId { get; set; } = null!;
        public List<CategoryOptionVm> Categories { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public uint Version { get; set; }
    }
}

using DLInventoryApp.Models;

namespace DLInventoryApp.ViewModels.Inventories
{
    public class InventoryAccessVm
    {
        public Guid InventoryId { get; set; }
        public string InventoryTitle { get; set; } = null!;
        public List<AccessUserVm> Users { get; set; } = new();
        public string? NewUserEmail { get; set; }
    }
}

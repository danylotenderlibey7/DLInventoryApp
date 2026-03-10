namespace DLInventoryApp.Services.Interfaces
{
    public interface IAccessService
    {
        Task<bool> CanEditItems(Guid inventoryId, string userId);
        Task<bool> CanManageInventory(Guid inventoryId, string userId);
    }
}

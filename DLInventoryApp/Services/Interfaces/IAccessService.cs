namespace DLInventoryApp.Services.Interfaces
{
    public interface IAccessService
    {
        Task<bool> CanWriteInventory(Guid inventoryId, string userId);
    }
}

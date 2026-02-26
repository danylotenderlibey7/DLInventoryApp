using DLInventoryApp.Services.Models;

namespace DLInventoryApp.Services.Interfaces
{
    public interface ICustomIdGenerator
    {
        Task<CustomIdResult> GenerateAsync(Guid inventoryId);
    }
}

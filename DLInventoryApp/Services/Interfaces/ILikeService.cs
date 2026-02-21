using DLInventoryApp.Services.Models;

namespace DLInventoryApp.Services.Interfaces
{
    public interface ILikeService
    {
        Task<LikeToggleResult> ToggleAsync(Guid itemId, string userId);
    }
}
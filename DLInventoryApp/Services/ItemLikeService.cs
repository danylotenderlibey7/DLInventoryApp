using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services
{
    public class ItemLikeService : ILikeService
    {
        private readonly ApplicationDbContext _context;
        public ItemLikeService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<LikeToggleResult> ToggleAsync(Guid itemId, string userId)
        {
            bool isLikedNow;
            var like = await _context.ItemLikes
                .SingleOrDefaultAsync(l => l.ItemId == itemId && l.UserId == userId);
            if (like == null)
            {
                like = new ItemLike
                {
                    ItemId = itemId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ItemLikes.Add(like);
                isLikedNow = true;
            }
            else
            {
                _context.ItemLikes.Remove(like);
                isLikedNow = false;
            }
            await _context.SaveChangesAsync();
            var count = await _context.ItemLikes.CountAsync(x => x.ItemId == itemId);
            return new LikeToggleResult { IsLiked = isLikedNow, LikesCount = count };
        }
    }
}

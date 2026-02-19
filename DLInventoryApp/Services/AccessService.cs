using DLInventoryApp.Data;
using DLInventoryApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services
{
    public class AccessService : IAccessService
    {
        private readonly ApplicationDbContext _context;
        public AccessService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<bool> CanWriteInventory(Guid inventoryId, string userId)
        {
            var ownerId = await _context.Inventories
                .Where(inv => inv.Id == inventoryId)
                .Select(inv => inv.OwnerId)
                .SingleOrDefaultAsync();
            if (ownerId == userId) return true;
            var hasAccess = await _context.InventoryWriteAccesses
                .AnyAsync(ia => ia.InventoryId == inventoryId && ia.UserId == userId);
            return hasAccess;
        }
    }
}

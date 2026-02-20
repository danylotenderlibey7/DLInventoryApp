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
            var inventory = await _context.Inventories
                .Where(inv => inv.Id == inventoryId)
                .Select(inv => new
                {
                    inv.OwnerId,
                    inv.IsPublic
                }).SingleOrDefaultAsync(); 
            if (inventory == null) return false;
            if (inventory.IsPublic) return true; 
            if (inventory.OwnerId == userId) return true;
            return await _context.InventoryWriteAccesses
                .AnyAsync(ia => ia.InventoryId == inventoryId && ia.UserId == userId);
        }
    }
}

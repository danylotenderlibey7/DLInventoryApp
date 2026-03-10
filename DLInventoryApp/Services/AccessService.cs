using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services
{
    public class AccessService : IAccessService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public AccessService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        public async Task<bool> CanEditItems(Guid inventoryId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return false;
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && await _userManager.IsInRoleAsync(user, "Admin")) return true;
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
        public async Task<bool> CanManageInventory(Guid inventoryId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))  return false;
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && await _userManager.IsInRoleAsync(user, "Admin")) return true;
            var ownerId = await _context.Inventories
                .Where(i => i.Id == inventoryId)
                .Select(i => i.OwnerId)
                .SingleOrDefaultAsync(); 
            if (ownerId == null)  return false;
            return ownerId == userId;
        }
    }
}

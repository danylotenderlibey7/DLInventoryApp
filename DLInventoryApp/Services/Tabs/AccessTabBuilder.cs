using DLInventoryApp.Data;
using DLInventoryApp.ViewModels.Inventories.Tabs.Access;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services.Tabs
{
    public class AccessTabBuilder
    {
        private readonly ApplicationDbContext _context;
        public AccessTabBuilder(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<InventoryAccessVm> BuildAsync(Guid inventoryId, string inventoryTitle)
        {
            var users = await _context.InventoryWriteAccesses
                .Where(x => x.InventoryId == inventoryId)
                .Select(x => new AccessUserVm
                {
                    UserId = x.UserId,
                    Email = x.User.Email!,
                    UserName = x.User.UserName
                }).ToListAsync();
            var inventory = await _context.Inventories
               .Where(inv => inv.Id == inventoryId)
               .Select(inv => new
               {
                   inv.Id,
                   inv.IsPublic
               }).SingleOrDefaultAsync();
            if (inventory == null)  throw new InvalidOperationException("Inventory not found.");
            return new InventoryAccessVm
            {
                InventoryId = inventoryId,
                InventoryTitle = inventoryTitle,
                IsPublic = inventory.IsPublic,
                Users = users
            };
        }
    }
}
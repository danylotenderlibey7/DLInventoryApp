using DLInventoryApp.Data;
using DLInventoryApp.ViewModels.Categories;
using DLInventoryApp.ViewModels.Inventories.Tabs.Settings;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services.Tabs
{
    public class SettingsTabBuilder
    {
        private readonly ApplicationDbContext _context;
        public SettingsTabBuilder(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<InventorySettingsVm?> BuildAsync(Guid inventoryId, bool canManage)
        {
            var settings = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .Select(x => new InventorySettingsVm
                {
                    InventoryId = x.Id,
                    CanManage = canManage,
                    Title = x.Title,
                    Description = x.Description,
                    IsPublic = x.IsPublic,
                    OwnerId = x.OwnerId,
                    OwnerName = x.Owner != null ? (x.Owner.UserName ?? x.Owner.Email ?? "Owner") : "Owner",
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    Tags = x.InventoryTags.Select(t => t.Tag.Name).ToList(),
                    Version = x.Version,
                    CategoryId = x.CategoryId,
                    Categories = _context.Categories
                    .OrderBy(x => x.Name)
                    .Select(x => new CategoryOptionVm 
                    { 
                        Id = x.Id, 
                        Name = x.Name 
                    }).ToList()
                }).SingleOrDefaultAsync();
            return settings;
        }
    }
}
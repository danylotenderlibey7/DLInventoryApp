using DLInventoryApp.Data;
using DLInventoryApp.ViewModels.CustomFields;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services.Tabs
{
    public class FieldsTabBuilder
    {
        private readonly ApplicationDbContext _context;
        public FieldsTabBuilder(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<InventoryFieldsVm> BuildAsync(Guid inventoryId, string inventoryTitle, bool canManage)
        {
            var fields = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .OrderBy(f => f.Order)
                .Select(f => new CustomFieldColumnVm
                {
                    Id = f.Id,
                    Name = f.Name,
                    Description = f.Description,
                    Type = f.Type,
                    Order = f.Order,
                    ShowInTable = f.ShowInTable
                }).ToListAsync();
            return new InventoryFieldsVm
            {
                InventoryId = inventoryId,
                InventoryTitle = inventoryTitle,
                Fields = fields,
                CanManage = canManage
            };
        }
    }
}